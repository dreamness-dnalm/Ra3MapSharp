using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class PlayerStartTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-starts-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 64, 40, 4);
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private async Task Apply(object patch)
    {
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var plan = (PreparedEditInfo)result.Data!;
        result = await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
    }

    [Test]
    public async Task SlotAndPositionValidationDoNotChangeMap()
    {
        Assert.That((await Run("starts.place", new { playerSlot = 0, x = 8, y = 8 })).Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That((await Run("starts.place", new { playerSlot = 7, x = 8, y = 8 })).Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That((await Run("starts.place", new { playerSlot = 1, x = 0, y = 0, space = "mapGrid" })).Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.Zero);
        Assert.That((await Run("starts.place", new { playerSlot = 1, x = 8, y = 8 })).Status, Is.EqualTo("succeeded"));
        Assert.That((await Run("starts.place", new { playerSlot = 1, x = 10, y = 10 })).Error?.Code, Is.EqualTo("START_EXISTS"));
        Assert.That((await Run("starts.place", new { playerSlot = 2, x = 8, y = 8 })).Error?.Code, Is.EqualTo("START_POSITION_CONFLICT"));
        Assert.That(_session.Facade.GetWaypoints().Single().WaypointName, Is.EqualTo("Player_1_Start"));
    }

    [Test]
    public async Task MirroredPlayerEntityUpdatesAndRestoresWaypointBindings()
    {
        object Start(int x) => new { id = "start-a", kind = "playerStart", parameters = new { playerSlot = 1, x, y = 8 } };
        var mirror = new { id = "start-b", kind = "derived", parameters = new { sourceEntityId = "start-a", transform = "rotate180", playerSlot = 2 } };
        var patch = new { upsert = new[] { mirror, Start(8) } };
        await Apply(patch);
        var ids = _session.Handles.WaypointObjectIds.ToArray();
        var before = await _session.CaptureSnapshotAsync();
        var starts = _session.Facade.GetWaypoints();
        Assert.That(starts.Single(w => w.WaypointName == "Player_2_Start").Position.X, Is.EqualTo(555));
        await Apply(patch);
        Assert.That(_session.Handles.WaypointObjectIds, Is.EqualTo(ids));
        await Apply(new { upsert = new[] { Start(10) } });
        Assert.That(_session.Handles.WaypointObjectIds.Count, Is.EqualTo(2));
        Assert.That(_session.Handles.WaypointObjectIds.Intersect(ids), Is.Empty);
        await Run("history.undo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.Handles.WaypointObjectIds, Is.EqualTo(ids));
        await Run("map.save", new { });
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That(_session.DesignEntities["start-a"].WaypointFingerprints!.Keys.Single(), Is.EqualTo(ids[0]));
        await Apply(new { remove = new[] { "start-a", "start-b" } });
        Assert.That(_session.Facade.GetWaypoints(), Is.Empty);
    }

    [Test]
    public async Task UnownedAndManuallyMovedStartsCannotBeClaimedByDesign()
    {
        await Run("starts.place", new { playerSlot = 1, x = 8, y = 8 });
        var start = new { id = "start", kind = "playerStart", parameters = new { playerSlot = 1, x = 10, y = 10 } };
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { start } } });
        Assert.That(result.Error?.Code, Is.EqualTo("START_EXISTS"));
        await Run("waypoints.delete", new { objectId = _session.Handles.WaypointObjectIds.Single() });
        await Apply(new { upsert = new[] { start } });
        await Run("waypoints.move", new { objectId = _session.Handles.WaypointObjectIds.Single(), x = 11, y = 10 });
        result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { remove = new[] { "start" } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
    }
}
