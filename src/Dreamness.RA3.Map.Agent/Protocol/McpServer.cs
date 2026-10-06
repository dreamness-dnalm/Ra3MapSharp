using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;

namespace Dreamness.RA3.Map.Agent.Protocol;

/// <summary>Local stdio MCP adapter. Map commands execute serially; rendering stays asynchronous.</summary>
public sealed class McpServer
{
    private readonly AgentRuntime _runtime;
    private readonly Dictionary<string, JsonElement> _tools;
    private bool _initialized;
    private bool _ready;
    public McpServer(AgentRuntime runtime)
    {
        _runtime = runtime;
        _tools = CommandSchemas.Tools().Select(t => JsonSerializer.SerializeToElement(t, AgentJson.Options))
            .ToDictionary(t => t.GetProperty("name").GetString()!);
    }

    public async Task<object?> HandleAsync(string line, CancellationToken token = default)
    {
        JsonElement? id = null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Error(null, -32600, "Expected JSON-RPC object.");
            var hasId = root.TryGetProperty("id", out var rawId);
            if (hasId && rawId.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) return Error(null, -32600, "Invalid request id.");
            if (hasId) id = rawId.Clone();
            if (!root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0"
                || !root.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
                return Error(id, -32600, "Invalid JSON-RPC request.");
            var method = methodElement.GetString();
            if (!hasId)
            {
                if (method == "notifications/initialized" && _initialized) _ready = true;
                return null;
            }
            root.TryGetProperty("params", out var parameters);
            if (method == "initialize")
            {
                if (_initialized) return Error(id, -32600, "Already initialized.");
                if (parameters.ValueKind != JsonValueKind.Object
                    || !parameters.TryGetProperty("protocolVersion", out var protocol) || protocol.ValueKind != JsonValueKind.String
                    || !parameters.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Object
                    || !parameters.TryGetProperty("clientInfo", out var client) || client.ValueKind != JsonValueKind.Object
                    || !client.TryGetProperty("name", out var clientName) || clientName.ValueKind != JsonValueKind.String
                    || !client.TryGetProperty("version", out var clientVersion) || clientVersion.ValueKind != JsonValueKind.String)
                    return Error(id, -32602, "initialize requires protocolVersion, capabilities and clientInfo.");
                _initialized = true;
                return Result(id, new { protocolVersion = "2025-06-18", capabilities = new { tools = new { listChanged = false } },
                    serverInfo = new { name = "ra3-map-agent", version = "0.1.0" },
                    instructions = "Use map create/open first. Writes require explicit revision. Water/light stay default. Preview success is not gameplay validation." });
            }
            if (method == "ping") return Result(id, new { });
            if (!_ready) return Error(id, -32000, "Initialize and send notifications/initialized first.");
            if (method == "tools/list")
            {
                if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("cursor", out _))
                    return Error(id, -32602, "No pagination cursor is issued by this server.");
                return Result(id, new { tools = _tools.Values.ToArray() });
            }
            if (method != "tools/call") return Error(id, -32601, "Method not found.");
            if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String || !_tools.TryGetValue(nameElement.GetString()!, out var tool))
                return Error(id, -32602, "Unknown tool.");
            var input = parameters.TryGetProperty("arguments", out var supplied) ? supplied : JsonSerializer.SerializeToElement(new { });
            CommandSchemas.Validate(input, tool.GetProperty("inputSchema"));
            var request = input.Deserialize<CommandRequest>(AgentJson.Options)!;
            request.Command = CommandSchemas.Definitions.Keys.Single(name => "ra3_" + name.Replace('.', '_') == nameElement.GetString());
            if (request.Arguments.ValueKind == JsonValueKind.Undefined) request.Arguments = JsonSerializer.SerializeToElement(new { });
            var result = await _runtime.ExecuteAsync(request, token);
            var content = new List<object>();
            if (result.Data is Rendering.IAgentImage preview && result.Status == "succeeded")
            {
                try
                {
                    var bytes = await Rendering.PreviewInspection.ReadVerifiedAsync(preview.ImagePath, preview.ImageHash, token);
                    content.Add(new { type = "image", mimeType = "image/png", data = Convert.ToBase64String(bytes) });
                }
                catch (Automation.AutomationException ex) { result = CommandResult.Failed(request, request.SessionId, ex); }
            }
            content.Insert(0, new { type = "text", text = JsonSerializer.Serialize(result, AgentJson.Options) });
            return Result(id, new { content, isError = result.Status != "succeeded" });
        }
        catch (JsonException ex) { return Error(id, id == null ? -32700 : -32602, ex.Message); }
        catch (ArgumentException ex) { return Error(id, -32602, ex.Message); }
        catch (InvalidOperationException ex) { return Error(id, -32602, ex.Message); }
        catch (OperationCanceledException) { return Error(id, -32800, "Request cancelled."); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return Error(id, -32603, "Internal tool server error; inspect stderr.");
        }
    }

    private static object Result(JsonElement? id, object result) => new { jsonrpc = "2.0", id, result };
    private static object Error(JsonElement? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
}
