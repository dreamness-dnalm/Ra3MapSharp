using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class DesignDerivationTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-derivation-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 64, 40, 4);
        await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 64, height = 40 }, height = 260 });
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private static object Platform(float height = 300) => new { id = "base", kind = "platform",
        parameters = new { region = new { x = 4, y = 4, width = 16, height = 12 }, value = height, falloff = .25 } };
    private static object Tree(float x = 8) => new { id = "tree", kind = "objects", parameters = new
        { placements = new[] { new { typeName = "CC_Tree01", x, y = 6.25, z = 7, angleRadians = .3 } } } };
    private static object Mirror(string source, params string[] dependsOn) => new { id = source + "-mirror", kind = "derived", dependsOn,
        parameters = new { sourceEntityId = source, transform = "rotate180" } };
    private async Task<PreparedEditInfo> Apply(object patch)
    {
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var plan = (PreparedEditInfo)result.Data!;
        result = await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return plan;
    }
    private void AssertMirroredHeights(string source, string target)
    {
        var targetCells = _session.DesignEntities[target].Heights.ToDictionary(c => (c.X, c.Y), c => c.After);
        foreach (var cell in _session.DesignEntities[source].Heights)
            Assert.That(targetCells[(_session.Facade.MapWidth - 1 - cell.X, _session.Facade.MapHeight - 1 - cell.Y)], Is.EqualTo(cell.After));
    }

    [Test]
    public async Task RectangularMapRotationPreservesCellAndWorldCoordinateConventions()
    {
        var patch = new { upsert = new[] { Mirror("tree"), Mirror("base"), Tree(), Platform() } };
        await Apply(patch);
        AssertMirroredHeights("base", "base-mirror");
        var obj = _session.Handles.ResolveUnit(_session.Facade, _session.DesignEntities["tree-mirror"].ObjectFingerprints.Keys.Single());
        Assert.That(obj.Position.X, Is.EqualTo(555).Within(.001));
        Assert.That(obj.Position.Y, Is.EqualTo(332.5).Within(.001));
        Assert.That(obj.Position.Z, Is.EqualTo(7));
        Assert.That(obj.Angle, Is.EqualTo(.3 * 180 / Math.PI + 180).Within(.0001));
        var before = await _session.CaptureSnapshotAsync();
        await Apply(patch);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        var plan = await Apply(new { upsert = new[] { Tree(9) } });
        Assert.That(plan.Results[0].GetProperty("automaticallyRebuilt")[0].GetString(), Is.EqualTo("tree-mirror"));
        Assert.That(_session.Handles.UnitObjectIds.Count, Is.EqualTo(2));
        await Run("history.undo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test]
    public async Task MirroredResourcePreservesOwnershipAndSettingsWithIndependentIdentity()
    {
        var source = new { id = "ore", kind = "objects", parameters = new { placements = new[] {
            new { typeName = "OreNode", name = "OreA", x = 8, y = 6, angleRadians = MathF.PI / 2,
                ownerTeam = "Player_1/teamPlayer_1", settings = new { objectInitialHealth = 65, objectIndestructible = true } } } } };
        await Apply(new { upsert = new object[] { source, Mirror("ore") } });
        var a = _session.Handles.ResolveUnit(_session.Facade, _session.DesignEntities["ore"].ObjectFingerprints.Keys.Single());
        var bId = _session.DesignEntities["ore-mirror"].ObjectFingerprints.Keys.Single();
        var b = _session.Handles.ResolveUnit(_session.Facade, bId);
        Assert.That(b.BelongToTeam, Is.EqualTo(a.BelongToTeam));
        Assert.That(b.BelongToTeam, Is.EqualTo("Player_1/teamPlayer_1"));
        Assert.That(b.Properties.GetProperty<int>("objectInitialHealth"), Is.EqualTo(65));
        Assert.That(b.Properties.GetProperty<bool>("objectIndestructible"), Is.True);
        Assert.That(b.UniqueId, Is.Not.EqualTo(a.UniqueId));
        Assert.That(b.ObjName, Is.Not.EqualTo(a.ObjName));
        Assert.That(b.Angle, Is.EqualTo(270).Within(.0001));
        Assert.That((await Run("objects.configure", new { objectId = bId, settings = new { objectInitialHealth = 10 } })).Status, Is.EqualTo("succeeded"));
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Mirror("ore") } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
    }

    [Test]
    public async Task ExplicitUpsertUpgradesOldCompilerOnlyAfterCandidateApply()
    {
        var patch = new { upsert = new[] { Tree(), Mirror("tree") } };
        await Apply(patch);
        _session.DesignEntities["tree"].AlgorithmVersion = "entity-compiler-v4";
        var before = await _session.CaptureSnapshotAsync();
        var oldId = _session.DesignEntities["tree"].ObjectFingerprints.Keys.Single();
        var prepared = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Tree() } } });
        Assert.That(prepared.Status, Is.EqualTo("succeeded"), prepared.Error?.Message);
        var plan = (PreparedEditInfo)prepared.Data!;
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.DesignEntities["tree"].AlgorithmVersion, Is.EqualTo("entity-compiler-v4"));
        Assert.That(plan.Results[0].GetProperty("automaticallyRebuilt").EnumerateArray().Select(x => x.GetString()), Does.Contain("tree-mirror"));
        Assert.That((await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash })).Status, Is.EqualTo("succeeded"));
        Assert.That(_session.DesignEntities["tree"].AlgorithmVersion, Is.EqualTo(Dreamness.RA3.Map.Automation.Design.DesignEntityState.CurrentAlgorithmVersion));
        Assert.That(_session.DesignEntities["tree"].ObjectFingerprints.Keys.Single(), Is.Not.EqualTo(oldId));
    }

    [Test]
    public async Task ReferencedPlatformHeightsAndMirroredRampFollowSourceChanges()
    {
        var ramp = new { id = "ramp", kind = "ramp", parameters = new { polyline = new[] { new { x = 12, y = 10 }, new { x = 26, y = 10 } },
            startHeightFrom = "base", endHeight = 260, widthCells = 4, transitionCells = 2 } };
        await Apply(new { upsert = new[] { ramp, Platform(), Mirror("base"), Mirror("ramp", "base-mirror") } });
        AssertMirroredHeights("ramp", "ramp-mirror");
        var plan = await Apply(new { upsert = new[] { Platform(350) } });
        Assert.That(plan.Results[0].GetProperty("automaticallyRebuilt").EnumerateArray().Select(x => x.GetString()),
            Is.EquivalentTo(new[] { "base-mirror", "ramp", "ramp-mirror" }));
        var startCap = _session.DesignEntities["ramp"].Heights.Single(c => c.X == 14 && c.Y == 14);
        Assert.That(startCap.After, Is.EqualTo(350));
        AssertMirroredHeights("base", "base-mirror");
        AssertMirroredHeights("ramp", "ramp-mirror");
        await Run("map.save", new { });
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        await Apply(new { upsert = new[] { ramp } });
        Assert.That(_session.HasUnexportedChanges, Is.False);
    }

    [Test]
    public async Task SelfOverlapAndManualSourceConflictDoNotPublishCandidates()
    {
        var crossing = new { id = "base", kind = "platform", parameters = new
            { region = new { x = 28, y = 16, width = 8, height = 8 }, value = 300 } };
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { crossing, Mirror("base") } } });
        Assert.That(result.Error?.Code, Is.EqualTo("SYMMETRY_OVERLAP"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Apply(new { upsert = new[] { Tree() } });
        var id = _session.DesignEntities["tree"].ObjectFingerprints.Keys.Single();
        await Run("objects.move", new { objectId = id, x = 9, y = 6 });
        result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Mirror("tree") } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
        Assert.That(_session.Handles.UnitObjectIds.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ConflictingLiteralAndReferenceAreRejected()
    {
        var ramp = new { id = "ramp", kind = "ramp", parameters = new { polyline = new[] { new { x = 12, y = 10 }, new { x = 26, y = 10 } },
            startHeightFrom = "base", startHeight = 300, endHeight = 260 } };
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { ramp, Platform() } } });
        Assert.That(result.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.DesignEntities, Is.Empty);
    }
}
