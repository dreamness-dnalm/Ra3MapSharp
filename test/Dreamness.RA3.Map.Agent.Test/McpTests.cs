using System.Text.Json;
using Dreamness.RA3.Map.Agent.Protocol;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using NUnit.Framework;

namespace Dreamness.RA3.Map.Agent.Test;

public class McpTests
{
    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value, AgentJson.Options);

    [Test]
    public async Task SchemaCoverageMatchesActualRuntimeCommands()
    {
        await using var runtime = new AgentRuntime();
        var capabilities = Json((await runtime.ExecuteAsync(new CommandRequest { Command = "system.capabilities" })).Data);
        var commands = capabilities.GetProperty("commands").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Concat(capabilities.GetProperty("hostCommands").EnumerateArray().Select(c => c.GetString())).Append("batch.execute");
        Assert.That(CommandSchemas.Definitions.Keys, Is.EquivalentTo(commands));
        var tools = CommandSchemas.Tools().Select(Json).ToArray();
        Assert.That(tools.Select(t => t.GetProperty("name").GetString()).Distinct().Count(), Is.EqualTo(tools.Length));
    }

    [Test]
    public async Task McpCreatesEditsQueriesAndReportsToolErrorsThroughExistingRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-mcp-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var runtime = new AgentRuntime();
            var server = new McpServer(runtime);
            var sequence = 0;
            async Task<JsonElement> Rpc(string method, object? parameters = null) => Json(await server.HandleAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = ++sequence, method, @params = parameters })));
            var early = await Rpc("tools/list");
            Assert.That(early.TryGetProperty("error", out _), Is.True);
            var init = await Rpc("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "test", version = "1" } });
            Assert.That(init.GetProperty("result").GetProperty("protocolVersion").GetString(), Is.EqualTo("2025-06-18"));
            Assert.That(await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"), Is.Null);
            var tools = await Rpc("tools/list");
            Assert.That(tools.GetProperty("result").GetProperty("tools").GetArrayLength(), Is.EqualTo(CommandSchemas.Definitions.Count));
            var created = await Rpc("tools/call", new { name = "ra3_map_create", arguments = new { arguments = new { parentPath = root, mapName = "Test", playableWidth = 16, playableHeight = 16 } } });
            Assert.That(created.GetProperty("result").GetProperty("isError").GetBoolean(), Is.False);
            var edited = await Rpc("tools/call", new { name = "ra3_objects_place", arguments = new { sessionId = "$current", expectedRevision = 0,
                arguments = new { typeName = "CC_Tree01", x = 2, y = 2 } } });
            Assert.That(edited.GetProperty("result").GetProperty("isError").GetBoolean(), Is.False);
            var stale = await Rpc("tools/call", new { name = "ra3_objects_place", arguments = new { sessionId = "$current", expectedRevision = 0,
                arguments = new { typeName = "CC_Tree01", x = 3, y = 3 } } });
            Assert.That(stale.GetProperty("result").GetProperty("isError").GetBoolean(), Is.True);
            var missing = await Rpc("tools/call", new { name = "ra3_objects_place", arguments = new { sessionId = "$current", expectedRevision = 1,
                arguments = new { x = 2, y = 2 } } });
            Assert.That(missing.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
            var query = await Rpc("tools/call", new { name = "ra3_objects_query", arguments = new { sessionId = "$current" } });
            using var contents = JsonDocument.Parse(query.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
            Assert.That(contents.RootElement.GetProperty("data").GetProperty("total").GetInt32(), Is.EqualTo(1));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void CliffSchemasValidateStyleAndExposeMutationInPreparedEdits()
    {
        var args = Json(new { region = new { x = 0, y = 0, width = 32, height = 24 }, seed = 1,
            style = new { name = "rock", pieces = new[] { new { typeName = "TestCliff", lengthCells = 2, depthCells = 1 } } } });
        var schema = CommandSchemas.Definitions["objects.place_cliffs"].GetProperty("argumentsSchema");
        Assert.DoesNotThrow(() => CommandSchemas.Validate(args, schema));
        Assert.Throws<ArgumentException>(() => CommandSchemas.Validate(Json(new { region = new { x = 0, y = 0, width = 32, height = 24 },
            seed = 1, style = new { name = "rock", pieces = new[] { new { typeName = "TestCliff", lengthCells = 0 } } } }), schema));
        Assert.DoesNotThrow(() => CommandSchemas.Validate(Json(new { baseRevision = 0,
            commands = new[] { new { command = "objects.place_cliffs", arguments = args } } }),
            CommandSchemas.Definitions["edits.prepare"].GetProperty("argumentsSchema")));
        var tool = CommandSchemas.Tools().Select(Json).Single(t => t.GetProperty("name").GetString() == "ra3_objects_place_cliffs");
        Assert.That(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean(), Is.False);
        Assert.That(tool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(v => v.GetString()), Does.Contain("expectedRevision"));
    }

    [Test]
    public void SchemasRejectNestedInvalidShapesAndUnknownArguments()
    {
        var schema = CommandSchemas.Definitions["objects.scatter"].GetProperty("argumentsSchema");
        var valid = Json(new { region = new { kind = "circle", centerX = 10, centerY = 10, radius = 5 }, profile = new { waterLevel = 200 },
            seed = 1, count = 10, typeNames = new[] { "CC_Tree01" } });
        Assert.DoesNotThrow(() => CommandSchemas.Validate(valid, schema));
        Assert.Throws<ArgumentException>(() => CommandSchemas.Validate(Json(new { region = new { kind = "circle", x = 1, y = 1 }, profile = new { },
            seed = 1, count = 10, typeNames = new[] { "CC_Tree01" } }), schema));
        Assert.Throws<ArgumentException>(() => CommandSchemas.Validate(Json(new { unknown = true }), CommandSchemas.Definitions["map.info"].GetProperty("argumentsSchema")));
    }

    [Test]
    public async Task MalformedMessagesDoNotEndTheServerAndNotificationsStaySilent()
    {
        await using var runtime = new AgentRuntime();
        var server = new McpServer(runtime);
        var bad = Json(await server.HandleAsync("{"));
        Assert.That(bad.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32700));
        var invalid = Json(await server.HandleAsync("{\"jsonrpc\":2,\"id\":3,\"method\":\"ping\"}"));
        Assert.That(invalid.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32600));
        Assert.That(await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/cancelled\",\"params\":{\"requestId\":7}}"), Is.Null);
        var ping = Json(await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":\"still-alive\",\"method\":\"ping\"}"));
        Assert.That(ping.GetProperty("id").GetString(), Is.EqualTo("still-alive"));
        Assert.That(ping.TryGetProperty("result", out _), Is.True);
    }
}
