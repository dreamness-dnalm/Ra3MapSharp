using System.Text.Json;
using Dreamness.RA3.Map.Agent.Rendering;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamness.RA3.Map.Agent.Test;

public class RenderingTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private string _launcher = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "RA3-AgentTests-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 16, 12, 4);
        _launcher = Path.Combine(_root, "editor", "WbLauncher.exe");
        Directory.CreateDirectory(Path.Combine(_root, "editor", "data", "config"));
        File.WriteAllText(_launcher, "fake executable, replaced by injected process");
        File.WriteAllText(Path.Combine(_root, "editor", "data", "config", "map-task-launch.json"), "{\"protocol\":1,\"modConfig\":\"test\"}");
    }

    [TearDown]
    public async Task TearDown()
    {
        await _session.CloseAsync();
        Directory.Delete(_root, true);
    }

    private WorldBuilderRenderer Renderer(Func<string, CancellationToken, Task<int>> render) =>
        new(_launcher, Path.Combine(_root, "renders"), new FakeProcess(render));

    private static async Task<int> Png(string path, CancellationToken token)
    {
        using var image = new Image<Rgba32>(16, 12, new Rgba32(10, 100, 200));
        await image.SaveAsPngAsync(Path.ChangeExtension(path, ".overview.png"), token);
        return 0;
    }

    [Test]
    public async Task SnapshotIsDetachedAndOldRevisionCannotBeRequested()
    {
        var snapshot = await _session.CaptureSnapshotAsync(0);
        var oldBytes = snapshot.MapBytes.ToArray();
        var result = await CommandExecutor.Default.ExecuteAsync(new CommandRequest
        {
            SessionId = _session.SessionId, Command = "terrain.set_height", ExpectedRevision = 0,
            Arguments = JsonSerializer.SerializeToElement(new { height = 260, region = new { x = 0, y = 0, width = 2, height = 2 } })
        });
        Assert.That(result.Status, Is.EqualTo("succeeded"));
        Assert.That(snapshot.MapBytes, Is.EqualTo(oldBytes));
        Assert.That((await _session.CaptureSnapshotAsync(1)).ContentHash, Is.Not.EqualTo(snapshot.ContentHash));
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await _session.CaptureSnapshotAsync(0))!.Code,
            Is.EqualTo("REVISION_CONFLICT"));
        snapshot.MapBytes[0] ^= 1;
        Assert.That((await _session.CaptureSnapshotAsync()).MapBytes[0], Is.EqualTo(oldBytes[0]));
    }

    [Test]
    public async Task OverviewBindsExactSnapshotAndPlayableCoordinatesWithoutMutatingSession()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var sourceBytes = await File.ReadAllBytesAsync(_session.UserMapFilePath);
        var result = await Renderer(Png).RenderAsync(snapshot);
        Assert.That(result.MapContentHash, Is.EqualTo(snapshot.ContentHash));
        Assert.That(result.Revision, Is.EqualTo(0));
        Assert.That(result.PixelToPlayableGrid, Is.EqualTo(new double[] { 1, 0, 0, 0, -1, 12 }));
        Assert.That(result.RendererConfig.GetProperty("modConfig").GetString(), Is.EqualTo("test"));
        Assert.That(await File.ReadAllBytesAsync(_session.UserMapFilePath), Is.EqualTo(sourceBytes));
        Assert.That(_session.Revision, Is.EqualTo(0));
        Assert.That(_session.Dirty, Is.False);
    }

    [Test]
    public async Task NonzeroExitNeverAcceptsEvenAValidImage()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var renderer = Renderer(async (path, token) => { await Png(path, token); return 8; });
        var ex = Assert.ThrowsAsync<AutomationException>(async () => await renderer.RenderAsync(snapshot));
        Assert.That(ex!.Code, Is.EqualTo("RENDER_FAILED"));
        Assert.That(ex.Details!["exitCode"], Is.EqualTo("8"));
    }

    [Test]
    public async Task SuccessfulExitRequiresNewOutput()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var renderer = Renderer((_, _) => Task.FromResult(0));
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await renderer.RenderAsync(snapshot))!.Code,
            Is.EqualTo("RENDER_OUTPUT_MISSING"));
    }

    [Test]
    public async Task EditorCannotModifySnapshotUnnoticed()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var renderer = Renderer(async (path, token) =>
        {
            await Png(path, token);
            await File.AppendAllTextAsync(path, "changed", token);
            return 0;
        });
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await renderer.RenderAsync(snapshot))!.Code,
            Is.EqualTo("SNAPSHOT_CHANGED"));
    }

    [Test]
    public async Task RenderConfigChangeInvalidatesProvenance()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        WorldBuilderRenderer? renderer = null;
        renderer = Renderer(async (path, token) =>
        {
            await Png(path, token);
            await File.WriteAllTextAsync(renderer!.ConfigPath, "{\"protocol\":2}", token);
            return 0;
        });
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await renderer.RenderAsync(snapshot))!.Code,
            Is.EqualTo("RENDER_CONFIG_CHANGED"));
    }

    [Test]
    public async Task RenderJobDoesNotBlockEditingAndDeduplicatesAndCancels()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderer = Renderer(async (_, token) =>
        {
            entered.SetResult(true);
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });
        await using var runtime = new AgentRuntime(renderer);
        var request = new CommandRequest
        {
            RequestId = "preview-one", Command = "preview.start", SessionId = _session.SessionId,
            Arguments = JsonSerializer.SerializeToElement(new { requiredRevision = 0 })
        };
        var start = await runtime.ExecuteAsync(request);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var duplicate = await runtime.ExecuteAsync(request);
        var jobId = JsonSerializer.SerializeToElement(start.Data, AgentJson.Options).GetProperty("jobId").GetString();
        Assert.That(JsonSerializer.Serialize(duplicate.Data), Is.EqualTo(JsonSerializer.Serialize(start.Data)));
        var edit = await runtime.ExecuteAsync(new CommandRequest
        {
            Command = "terrain.set_height", SessionId = _session.SessionId, ExpectedRevision = 0,
            Arguments = JsonSerializer.SerializeToElement(new { height = 280, region = new { x = 2, y = 2, width = 1, height = 1 } })
        });
        Assert.That(edit.Status, Is.EqualTo("succeeded"));
        await runtime.ExecuteAsync(new CommandRequest { Command = "jobs.cancel", Arguments = JsonSerializer.SerializeToElement(new { jobId }) });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var status = await runtime.ExecuteAsync(new CommandRequest { Command = "jobs.status", Arguments = JsonSerializer.SerializeToElement(new { jobId }) });
            var data = JsonSerializer.SerializeToElement(status.Data, AgentJson.Options);
            Assert.That(data.GetProperty("revision").GetInt32(), Is.EqualTo(0));
            if (data.GetProperty("state").GetString() == "cancelled") break;
            await Task.Delay(10, timeout.Token);
        }
        Assert.That(_session.Revision, Is.EqualTo(1));
    }

    private sealed class FakeProcess : IOverviewProcess
    {
        private readonly Func<string, CancellationToken, Task<int>> _run;
        public FakeProcess(Func<string, CancellationToken, Task<int>> run) => _run = run;
        public Task<int> RunAsync(string launcherPath, string mapPath, CancellationToken cancellationToken) => _run(mapPath, cancellationToken);
    }

    [Test]
    public async Task CropAndResizePreserveCoordinatesAndRejectChangedSource()
    {
        var source = await Renderer(async (path, token) =>
        {
            using var pixels = new Image<Rgba32>(16, 12);
            for (var y = 0; y < 12; y++)
            for (var x = 0; x < 16; x++) pixels[x, y] = new Rgba32((byte)x, (byte)y, 0);
            await pixels.SaveAsPngAsync(Path.ChangeExtension(path, ".overview.png"), token);
            return 0;
        }).RenderAsync(await _session.CaptureSnapshotAsync());
        var crop = new PixelCrop { X = 4, Y = 2, Width = 8, Height = 6 };
        var inspection = await PreviewInspection.CreateAsync(source, crop, 8);
        Assert.That(Path.GetDirectoryName(inspection.ImagePath), Is.EqualTo(Path.GetDirectoryName(source.ImagePath)));
        using var image = Image.Load<Rgba32>(inspection.ImagePath);
        Assert.That(image[0, 0], Is.EqualTo(new Rgba32(4, 2, 0)));
        Assert.That(inspection.PixelToPlayableGrid, Is.EqualTo(new double[] { 1, 0, 4, 0, -1, 10 }));
        var resized = await PreviewInspection.CreateAsync(source, crop, 4);
        Assert.That((resized.Width, resized.Height), Is.EqualTo((4, 3)));
        Assert.That(resized.PixelToPlayableGrid, Is.EqualTo(new double[] { 2, 0, 4, 0, -2, 10 }));
        Assert.That(resized.MapContentHash, Is.EqualTo(source.MapContentHash));
        Assert.That(resized.Revision, Is.EqualTo(source.Revision));
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await PreviewInspection.CreateAsync(source,
            new PixelCrop { X = 4, Y = 2, Width = int.MaxValue, Height = 3 }))!.Code, Is.EqualTo("INVALID_ARGUMENT"));
        await File.AppendAllTextAsync(source.ImagePath, "modified");
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await PreviewInspection.CreateAsync(source, null))!.Code, Is.EqualTo("PREVIEW_CHANGED"));
    }

    [Test]
    public async Task McpInspectionReturnsInlinePngAndProvenance()
    {
        await using var runtime = new AgentRuntime(Renderer(Png));
        var start = await runtime.ExecuteAsync(new CommandRequest { Command = "preview.start", SessionId = _session.SessionId });
        var jobId = JsonSerializer.SerializeToElement(start.Data, AgentJson.Options).GetProperty("jobId").GetString();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var status = await runtime.ExecuteAsync(new CommandRequest { Command = "jobs.status", Arguments = JsonSerializer.SerializeToElement(new { jobId }) });
            if (JsonSerializer.SerializeToElement(status.Data, AgentJson.Options).GetProperty("state").GetString() == "succeeded") break;
            await Task.Delay(10, timeout.Token);
        }
        var server = new Protocol.McpServer(runtime);
        await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}");
        await server.HandleAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
        var response = await server.HandleAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = "ra3_preview_inspect", arguments = new { arguments = new { jobId, maxEdge = 8 } } } }));
        var result = JsonSerializer.SerializeToElement(response, AgentJson.Options).GetProperty("result");
        Assert.That(result.GetProperty("isError").GetBoolean(), Is.False);
        var content = result.GetProperty("content");
        Assert.That(content.GetArrayLength(), Is.EqualTo(2));
        Assert.That(content[1].GetProperty("type").GetString(), Is.EqualTo("image"));
        using var decoded = Image.Load(Convert.FromBase64String(content[1].GetProperty("data").GetString()!));
        Assert.That((decoded.Width, decoded.Height), Is.EqualTo((8, 6)));
        using var metadata = JsonDocument.Parse(content[0].GetProperty("text").GetString()!);
        Assert.That(metadata.RootElement.GetProperty("data").GetProperty("revision").GetInt32(), Is.EqualTo(0));
    }
}
