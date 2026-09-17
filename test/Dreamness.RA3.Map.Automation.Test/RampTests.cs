using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class RampTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-ramp-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
        await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 32, height = 24 }, height = 260 });
        await Run("terrain.set_height", new { region = new { x = 16, y = 0, width = 16, height = 24 }, height = 300 });
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string name, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = name, Arguments = JsonSerializer.SerializeToElement(args) });
    private TerrainTraversal Analyze() => new(_session.Facade, new TerrainMovementProfile { MaxSlopeDegrees = 35, ClearanceCells = 1, RespectPassability = false, WaterLevel = 200 });

    [Test]
    public async Task RampConnectsPlatformsAfterSerializationAndUndoRestoresCliff()
    {
        var before = Analyze();
        Assert.That(before.ComponentAt(3, 12), Is.Not.EqualTo(before.ComponentAt(28, 12)));
        var outside = _session.Facade.GetTerrainHeight(20, 6);
        var result = await Run("terrain.ramp", new { polyline = new[] { new { x = 6.5, y = 12.5 }, new { x = 25.5, y = 12.5 } },
            widthCells = 6, transitionCells = 2, startHeight = 260, endHeight = 300, maxSlopeDegrees = 35 });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var after = Analyze();
        Assert.That(after.ComponentAt(3, 12), Is.Positive.And.EqualTo(after.ComponentAt(28, 12)));
        Assert.That(_session.Facade.GetTerrainHeight(20, 6), Is.EqualTo(outside));
        Assert.That(_session.Facade.GetTerrainHeight(10, 16), Is.EqualTo(260));
        Assert.That(_session.Facade.GetTerrainHeight(29, 16), Is.EqualTo(300));
        await Run("history.undo", new { });
        var undone = Analyze();
        Assert.That(undone.ComponentAt(3, 12), Is.Not.EqualTo(undone.ComponentAt(28, 12)));
        await Run("history.redo", new { });
        var redone = Analyze();
        Assert.That(redone.ComponentAt(3, 12), Is.EqualTo(redone.ComponentAt(28, 12)));
    }

    [Test]
    public async Task TooShortRampFailsWithoutPartialHeightChanges()
    {
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("terrain.ramp", new { polyline = new[] { new { x = 14, y = 12 }, new { x = 18, y = 12 } },
            startHeight = 260, endHeight = 500, maxSlopeDegrees = 35 });
        Assert.That(result.Error?.Code, Is.EqualTo("SLOPE_LIMIT_EXCEEDED"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.Revision, Is.EqualTo(before.Revision));
    }

    [Test]
    public async Task PolylineBendAndZeroTransitionAreSupported()
    {
        var result = await Run("terrain.ramp", new { polyline = new[] { new { x = 8, y = 8 }, new { x = 24, y = 8 }, new { x = 24, y = 16 } },
            widthCells = 4, transitionCells = 0, startHeight = 260, endHeight = 280, maxSlopeDegrees = 35 });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
    }

    [Test]
    public async Task RebuildPassabilityPersistsAndUndoRestoresOriginalFlags()
    {
        var previous = _session.Facade.GetPassability(19, 16);
        var result = await Run("terrain.rebuild_passability", new { maxSlopeDegrees = 35, expandCardinalHalo = true });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        Assert.That(_session.Facade.GetPassability(19, 16), Is.EqualTo("Impassable"));
        await Run("history.undo", new { });
        Assert.That(_session.Facade.GetPassability(19, 16), Is.EqualTo(previous));
        await Run("history.redo", new { });
        Assert.That(_session.Facade.GetPassability(19, 16), Is.EqualTo("Impassable"));
    }
}
