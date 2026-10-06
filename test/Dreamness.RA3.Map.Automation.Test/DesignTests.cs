using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class DesignTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-design-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private static object Group(int x, string id = "trees", string label = "trees") => new
    { id, kind = "objects", label, parameters = new { placements = new[] { new { typeName = "CC_Tree01", x, y = 8 } } } };
    private static object Platform(int x, string id = "base", float value = 300) => new
    { id, kind = "platform", parameters = new { region = new { x, y = 2, width = 6, height = 6 }, value, falloff = .4 } };
    private async Task<PreparedEditInfo> Prepare(object patch)
    {
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return (PreparedEditInfo)result.Data!;
    }
    private async Task Apply(object patch)
    {
        var plan = await Prepare(patch);
        var result = await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        Assert.That((await _session.CaptureSnapshotAsync()).DesignHash, Is.EqualTo(plan.CandidateDesignHash));
    }

    [Test]
    public async Task RepeatedEntityIsNotDuplicatedAndLocalUpdatePreservesUnownedObjects()
    {
        await Run("objects.place", new { typeName = "CC_Tree01", x = 25, y = 15 });
        var unowned = _session.Handles.UnitObjectIds.Single();
        await Apply(new { upsert = new[] { Group(6), Group(16, "east") } });
        var first = _session.DesignEntities["trees"].ObjectFingerprints.Keys.Single();
        var east = _session.DesignEntities["east"].ObjectFingerprints.Keys.Single();
        var before = await _session.CaptureSnapshotAsync();
        await Apply(new { upsert = new[] { Group(6) } });
        Assert.That(_session.DesignEntities["trees"].ObjectFingerprints.Keys.Single(), Is.EqualTo(first));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Apply(new { upsert = new[] { Group(10) } });
        Assert.That(_session.Handles.UnitObjectIds.Count, Is.EqualTo(3));
        Assert.That(_session.Handles.UnitObjectIds, Does.Contain(unowned).And.Contain(east));
        Assert.That(_session.Handles.UnitObjectIds, Does.Not.Contain(first));
        await Run("history.undo", new { });
        Assert.That(_session.DesignEntities["trees"].ObjectFingerprints.Keys.Single(), Is.EqualTo(first));
        await Run("history.redo", new { });
        await Apply(new { remove = new[] { "trees" } });
        Assert.That(_session.Handles.UnitObjectIds.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task LabelOnlyEditIsDesignDirtyAndSurvivesReopenExportAndUndo()
    {
        await Apply(new { upsert = new[] { Group(6) } });
        await Run("map.save", new { });
        var before = await _session.CaptureSnapshotAsync();
        await Apply(new { upsert = new[] { Group(6, label: "revised intent") } });
        Assert.That(_session.Dirty, Is.False);
        Assert.That(_session.DesignDirty, Is.True);
        Assert.That(_session.HasUnexportedChanges, Is.True);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That(_session.DesignDirty, Is.True);
        Assert.That(_session.DesignEntities["trees"].Spec.Label, Is.EqualTo("revised intent"));
        var export = await Run("map.export_project", new { parentPath = _root, mapName = "Copy" });
        Assert.That(export.Status, Is.EqualTo("succeeded"));
        using (var copy = await MapSessionManager.OpenAsync(_root, "Copy"))
        {
            Assert.That(copy.DesignDirty, Is.False);
            Assert.That(copy.DesignEntities["trees"].Spec.Label, Is.EqualTo("revised intent"));
        }
        await Run("history.undo", new { });
        Assert.That(_session.DesignEntities["trees"].Spec.Label, Is.EqualTo("trees"));
        Assert.That(_session.DesignDirty, Is.False);
        await Run("history.redo", new { });
        await Run("map.save", new { });
        Assert.That(_session.DesignDirty, Is.False);
    }

    [Test]
    public async Task MovingAndDeletingPlatformRestoresOriginalHeightsWithoutDrift()
    {
        var original = _session.Facade.GetTerrainHeight(7, 7);
        await Apply(new { upsert = new[] { Platform(2) } });
        var before = await _session.CaptureSnapshotAsync();
        Assert.That(_session.DesignEntities["base"].Heights.Count, Is.GreaterThan(0));
        await Apply(new { upsert = new[] { Platform(2) } });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Apply(new { upsert = new[] { Platform(16) } });
        Assert.That(_session.Facade.GetTerrainHeight(7, 7), Is.EqualTo(original));
        await Apply(new { remove = new[] { "base" } });
        Assert.That(_session.Facade.GetTerrainHeight(23, 7), Is.EqualTo(original));
        Assert.That(_session.DesignEntities, Is.Empty);
    }

    [Test]
    public async Task ManualOwnedObjectOrHeightChangesRejectUpdateButDoNotBlockUnrelatedEntities()
    {
        await Apply(new { upsert = new[] { Group(6), Platform(2) } });
        var objectId = _session.DesignEntities["trees"].ObjectFingerprints.Keys.Single();
        await Run("objects.move", new { objectId, x = 7, y = 8 });
        var failed = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Group(10) } } });
        Assert.That(failed.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
        await Apply(new { upsert = new[] { Platform(16) } });
        await Run("terrain.set_height", new { region = new { x = 18, y = 4, width = 1, height = 1 }, height = 400 });
        failed = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { remove = new[] { "base" } } });
        Assert.That(failed.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
    }

    [Test]
    public async Task ScatterAndRampBindingsSurviveNormalizationAndDoNotRegenerateOnRepeat()
    {
        await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 32, height = 24 }, height = 260 });
        var ramp = new { id = "ramp", kind = "ramp", parameters = new { polyline = new[] { new { x = 5, y = 12 }, new { x = 26, y = 12 } },
            startHeight = 260, endHeight = 300, widthCells = 4, transitionCells = 2 } };
        var forest = new { id = "forest", kind = "scatter", parameters = new { region = new { x = 2, y = 2, width = 28, height = 20 },
            profile = new { waterLevel = 200, maxSlopeDegrees = 35 }, seed = 731, count = 12, typeNames = new[] { "CC_Tree01" } } };
        var patch = new { upsert = new object[] { ramp, forest } };
        await Apply(patch);
        var snapshot = await _session.CaptureSnapshotAsync();
        var ids = _session.Handles.UnitObjectIds.ToArray();
        Assert.That(ids.Length, Is.EqualTo(12));
        await Apply(patch);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
        Assert.That(_session.Handles.UnitObjectIds, Is.EqualTo(ids));
        await Apply(new { remove = new[] { "ramp", "forest" } });
        Assert.That(_session.Handles.UnitObjectIds, Is.Empty);
        Assert.That(_session.Facade.GetTerrainHeight(25, 16), Is.EqualTo(260));
    }

    [Test]
    public async Task IdenticalHeightRegionsStillHaveExclusiveOwnership()
    {
        await Apply(new { upsert = new[] { Platform(2) } });
        var result = await Run("design.prepare", new { baseRevision = _session.Revision,
            patch = new { upsert = new[] { Platform(2, "duplicate") } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_OVERLAP"));
        Assert.That(_session.DesignEntities.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task OverlapAndProtectionFailuresLeaveNoMetadataOrHandles()
    {
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new
        { upsert = new[] { Platform(2), Platform(3, "overlap", 400), Group(6) } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_OVERLAP"));
        Assert.That(_session.DesignEntities, Is.Empty);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Run("protections.add", new { id = "lock", region = new { x = 2, y = 2, width = 6, height = 6 }, layers = new[] { "terrain" } });
        result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Group(6), Platform(2) } } });
        Assert.That(result.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That(_session.DesignEntities, Is.Empty);
        Assert.That(_session.Handles.UnitObjectIds, Is.Empty);
    }
}
