using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class DesignDependencyTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-dependencies-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
        await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 32, height = 24 }, height = 260 });
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private static object Base(float height = 300) => new { id = "base", kind = "platform",
        parameters = new { region = new { x = 2, y = 2, width = 20, height = 20 }, value = height } };
    private static object Ramp() => new { id = "ramp", kind = "ramp", dependsOn = new[] { "base" },
        parameters = new { polyline = new[] { new { x = 6, y = 12 }, new { x = 24, y = 12 } },
            startHeight = 280, endHeight = 320, widthCells = 4, transitionCells = 2 } };
    private static object Forest() => new { id = "forest", kind = "scatter", dependsOn = new[] { "ramp" },
        parameters = new { region = new { x = 3, y = 3, width = 16, height = 16 }, profile = new { waterLevel = 200, maxSlopeDegrees = 80 },
            seed = 731, count = 6, typeNames = new[] { "CC_Tree01" }, minDistanceCells = 2 } };
    private async Task<PreparedEditInfo> Apply(object patch)
    {
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var plan = (PreparedEditInfo)result.Data!;
        result = await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return plan;
    }

    [Test]
    public async Task ParentChangeRebuildsDescendantsAndUndoRestoresLayerStack()
    {
        await Run("objects.place", new { typeName = "CC_Tree01", x = 30, y = 22 });
        var external = _session.Handles.UnitObjectIds.Single();
        await Apply(new { upsert = new[] { Forest(), Ramp(), Base() } });
        var oldIds = _session.DesignEntities["forest"].ObjectFingerprints.Keys.ToArray();
        var before = await _session.CaptureSnapshotAsync();
        var plan = await Apply(new { upsert = new[] { Base(350) } });
        Assert.That(plan.Results[0].GetProperty("automaticallyRebuilt").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "ramp", "forest" }));
        Assert.That(_session.Handles.UnitObjectIds.Count, Is.EqualTo(7));
        Assert.That(_session.Handles.UnitObjectIds, Does.Contain(external));
        Assert.That(_session.DesignEntities["forest"].ObjectFingerprints.Keys.Intersect(oldIds), Is.Empty);
        Assert.That(_session.Facade.GetTerrainHeight(9, 9), Is.EqualTo(350));
        await Run("history.undo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.DesignEntities["forest"].ObjectFingerprints.Keys, Is.EquivalentTo(oldIds));
        await Run("history.redo", new { });
        await Apply(new { remove = new[] { "forest", "ramp" } });
        Assert.That(_session.Facade.GetTerrainHeight(14, 16), Is.EqualTo(350));
        await Apply(new { remove = new[] { "base" } });
        Assert.That(_session.Facade.GetTerrainHeight(14, 16), Is.EqualTo(260));
        Assert.That(_session.Handles.UnitObjectIds, Is.EqualTo(new[] { external }));
    }

    [Test]
    public async Task RepeatedOverlappingLayoutAndSavedReopenAreStable()
    {
        var patch = new { upsert = new[] { Ramp(), Base(), Forest() } };
        await Apply(patch);
        var before = await _session.CaptureSnapshotAsync();
        await Run("map.save", new { });
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        await Apply(patch);
        var after = await _session.CaptureSnapshotAsync();
        Assert.That(after.ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(after.DesignHash, Is.EqualTo(before.DesignHash));
        Assert.That(_session.HasUnexportedChanges, Is.False);
    }

    [Test]
    public async Task MissingDependencyCycleAndParentRemovalAreRejectedBeforeEditing()
    {
        var before = await _session.CaptureSnapshotAsync();
        var missing = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Ramp() } } });
        Assert.That(missing.Error?.Code, Is.EqualTo("ENTITY_DEPENDENCY"));
        var cycle = new { id = "base", kind = "platform", dependsOn = new[] { "ramp" }, parameters = new
            { region = new { x = 2, y = 2, width = 20, height = 20 }, value = 300 } };
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Ramp(), cycle } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_DEPENDENCY_CYCLE"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Apply(new { upsert = new[] { Ramp(), Base() } });
        result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { remove = new[] { "base" } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_DEPENDENCY"));
        Assert.That(_session.DesignEntities.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task ManualOverlayChangeBlocksFoundationUpdate()
    {
        await Apply(new { upsert = new[] { Ramp(), Base() } });
        await Run("terrain.set_height", new { region = new { x = 10, y = 12, width = 1, height = 1 }, height = 500 });
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Base(350) } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test]
    public async Task DependencyCanBeRemovedInSamePatchAsItsFormerParent()
    {
        await Apply(new { upsert = new[] { Ramp(), Base() } });
        var standalone = new { id = "ramp", kind = "ramp", parameters = new
        { polyline = new[] { new { x = 6, y = 12 }, new { x = 24, y = 12 } }, startHeight = 280, endHeight = 320, widthCells = 4, transitionCells = 2 } };
        await Apply(new { upsert = new[] { standalone }, remove = new[] { "base" } });
        Assert.That(_session.DesignEntities.Keys, Is.EqualTo(new[] { "ramp" }));
        Assert.That(_session.Facade.GetTerrainHeight(9, 9), Is.EqualTo(260));
        await Apply(new { remove = new[] { "ramp" } });
        Assert.That(_session.Facade.GetTerrainHeight(14, 16), Is.EqualTo(260));
    }

    [Test]
    public async Task HistoryFourMigrationPreservesExistingDesignSaveHash()
    {
        await Apply(new { upsert = new[] { Base() } });
        await Run("map.save", new { });
        var before = await _session.CaptureSnapshotAsync();
        var indexPath = Path.Combine(_session.AutomationDirectoryPath, "History", "index.json");
        _session.Dispose();
        var index = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(indexPath))!;
        index["schemaVersion"] = 4;
        File.WriteAllText(indexPath, index.ToJsonString());
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That((await _session.CaptureSnapshotAsync()).DesignHash, Is.EqualTo(before.DesignHash));
        Assert.That(_session.HasUnexportedChanges, Is.False);
        await Apply(new { upsert = new[] { Base() } });
        Assert.That(_session.HasUnexportedChanges, Is.False);
    }

    [Test]
    public async Task SharedParentDoesNotPermitSiblingOverlays()
    {
        var sibling = new { id = "sibling", kind = "platform", dependsOn = new[] { "base" },
            parameters = new { region = new { x = 8, y = 10, width = 4, height = 4 }, value = 400 } };
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Ramp(), sibling, Base() } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_OVERLAP"));
        Assert.That(_session.DesignEntities, Is.Empty);
    }
}
