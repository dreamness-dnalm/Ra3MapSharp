using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class ObjectFootprintTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp] public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-footprints-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }
    [TearDown] public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string name, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, Command = name, ExpectedRevision = _session.Revision, Arguments = JsonSerializer.SerializeToElement(args) });
    private static JsonElement Data(CommandResult result)
    {
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return JsonSerializer.SerializeToElement(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    private static ObjectFootprint[] Profiles() => new[] {
        new ObjectFootprint { TypeName = "OreNode", Boxes = new[] {
            new FootprintBox { WidthCells = 8, DepthCells = 8 },
            new FootprintBox { Label = "access", WidthCells = 8, DepthCells = 4, OffsetXCells = 8 } } },
        new ObjectFootprint { TypeName = "CC_Tree01", Boxes = new[] { new FootprintBox { WidthCells = 2, DepthCells = 2 } } }
    };

    [Test]
    public void SeparatingAxesRespectRotationOffsetAndContact()
    {
        var thin = new FootprintBox { WidthCells = 10, DepthCells = 1 };
        var a = new OrientedFootprintBox(thin, 0, 0, Math.PI / 4);
        var b = new OrientedFootprintBox(thin, 0, 2, Math.PI / 4);
        Assert.That(a.MaxX > b.MinX && a.MaxY > b.MinY, Is.True, "Axis-aligned bounding boxes overlap.");
        Assert.That(a.Overlaps(b), Is.False, "Rotated thin rectangles are separated.");
        var square = new FootprintBox { WidthCells = 2, DepthCells = 2 };
        var center = new OrientedFootprintBox(square, 5, 5, 0);
        Assert.That(center.Overlaps(new OrientedFootprintBox(square, 7, 5, 0)), Is.False);
        Assert.That(center.Overlaps(new OrientedFootprintBox(square, 6.9, 5, 0)), Is.True);
        var offset = new OrientedFootprintBox(new FootprintBox { WidthCells = 2, DepthCells = 4, OffsetXCells = 3 }, 10, 10, Math.PI / 2);
        Assert.That(offset.MinX, Is.EqualTo(8).Within(1e-8));
        Assert.That(offset.MinY, Is.EqualTo(12).Within(1e-8));
        Assert.That(offset.MaxY, Is.EqualTo(14).Within(1e-8));
        Assert.That(offset.Inside(20, 13), Is.False);
    }

    [Test]
    public async Task CandidateAnalysisReportsAccessConflictWithoutChangingLiveMap()
    {
        var prepared = await Run("edits.prepare", new { baseRevision = _session.Revision, commands = new[] {
            new { command = "objects.place", arguments = new { typeName = "OreNode", x = 10, y = 10, anchor = "gridPoint" } },
            new { command = "objects.place", arguments = new { typeName = "CC_Tree01", x = 18, y = 10, anchor = "gridPoint" } }
        } });
        Data(prepared);
        var plan = (PreparedEditInfo)prepared.Data!;
        var before = await _session.CaptureSnapshotAsync();
        var result = Data(await Run("objects.analyze_space", new { footprints = Profiles(), preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash }));
        Assert.That(result.GetProperty("mapContentHash").GetString(), Is.EqualTo(plan.CandidateContentHash));
        Assert.That(result.GetProperty("overlapCount").GetInt32(), Is.EqualTo(1));
        Assert.That(result.GetProperty("overlaps")[0].GetProperty("firstBox").GetString(), Is.EqualTo("access"));
        Assert.That(result.GetProperty("clearUnderProfile").GetBoolean(), Is.False);
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        var routes = Data(await Run("terrain.analyze", new { profile = new { }, footprints = Profiles(),
            preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash,
            routes = new[] { new { from = new { x = 10, y = 10 }, to = new { x = 2, y = 2 } } } }));
        Assert.That(routes.GetProperty("mapContentHash").GetString(), Is.EqualTo(plan.CandidateContentHash));
        Assert.That(routes.GetProperty("routes")[0].GetProperty("route").GetProperty("status").GetString(), Is.EqualTo("blocked-endpoint"));
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        var unknown = Data(await Run("objects.analyze_space", new { footprints = Profiles().Take(1), preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash }));
        Assert.That(unknown.GetProperty("coverageComplete").GetBoolean(), Is.False);
        Assert.That(unknown.GetProperty("unknownTypes")[0].GetProperty("typeName").GetString(), Is.EqualTo("CC_Tree01"));
        Assert.That(unknown.GetProperty("clearUnderProfile").GetBoolean(), Is.False);
    }

    [Test]
    public async Task ScatterAvoidsBodiesAccessZonesAndExcludedCellsAndIsReproducible()
    {
        Data(await Run("objects.place", new { typeName = "OreNode", x = 16, y = 12, anchor = "gridPoint" }));
        var args = new { region = new { x = 1, y = 1, width = 30, height = 22 }, profile = new { waterLevel = 200 },
            footprints = Profiles(), count = 20, seed = 42, minDistanceCells = 2, typeNames = new[] { "CC_Tree01" },
            exclusions = new[] { new { x = 2, y = 2, width = 4, height = 20 } } };
        Data(await Run("objects.scatter", args));
        var snapshot = await _session.CaptureSnapshotAsync();
        var analysis = Data(await Run("objects.analyze_space", new { footprints = Profiles() }));
        Assert.That(analysis.GetProperty("clearUnderProfile").GetBoolean(), Is.True);
        var excluded = new OrientedFootprintBox(new FootprintBox { WidthCells = 4, DepthCells = 20 }, 4, 12, 0);
        var profiles = new ObjectFootprintSet(Profiles());
        foreach (var tree in _session.Facade.GetUnitObjects().Where(o => o.TypeName == "CC_Tree01"))
            Assert.That(profiles.At(tree.TypeName, tree.Position.X / 10d, tree.Position.Y / 10d, MapAngles.ToRadians(tree.Angle))[0].Overlaps(excluded), Is.False);
        Data(await Run("history.undo", new { }));
        Data(await Run("objects.scatter", args));
        // Undo restores allocation state; identical inputs reproduce the complete map bytes.
        var second = Data(await Run("objects.analyze_space", new { footprints = Profiles() }));
        Assert.That(second.GetProperty("clearUnderProfile").GetBoolean(), Is.True);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
    }

    [Test]
    public async Task WholeFootprintAvoidsWaterBeyondItsCenter()
    {
        Data(await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 8, height = 24 }, height = 180 }));
        var profiles = new[] { new ObjectFootprint { TypeName = "CC_Tree01", Boxes = new[] { new FootprintBox { WidthCells = 6, DepthCells = 6 } } } };
        Data(await Run("objects.scatter", new { region = new { x = 8, y = 1, width = 22, height = 22 },
            profile = new { waterLevel = 200 }, footprints = profiles, count = 4, seed = 731, typeNames = new[] { "CC_Tree01" } }));
        var set = new ObjectFootprintSet(profiles);
        foreach (var tree in _session.Facade.GetUnitObjects())
            Assert.That(set.At(tree.TypeName, tree.Position.X / 10d, tree.Position.Y / 10d, MapAngles.ToRadians(tree.Angle))[0].MinX, Is.GreaterThanOrEqualTo(8 - 1e-6));
    }

    [Test]
    public async Task AnalysisReportsBoundaryAndTruncationWithoutClaimingClearance()
    {
        Data(await Run("objects.place", new { typeName = "OreNode", x = 1, y = 1 }));
        Data(await Run("objects.place", new { typeName = "OreNode", x = 2, y = 2 }));
        var analysis = Data(await Run("objects.analyze_space", new { footprints = Profiles(), limit = 1 }));
        Assert.That(analysis.GetProperty("outsideCount").GetInt32(), Is.GreaterThan(1));
        Assert.That(analysis.GetProperty("outside").GetArrayLength(), Is.EqualTo(1));
        Assert.That(analysis.GetProperty("truncated").GetBoolean(), Is.True);
        Assert.That(analysis.GetProperty("clearUnderProfile").GetBoolean(), Is.False);
        Assert.That((await Run("objects.analyze_space", new { footprints = Profiles(), preparedPlanId = "missing" })).Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
    }

    [Test]
    public async Task MissingProfileOrImpossibleFootprintDoesNotPartiallyPlace()
    {
        var before = await _session.CaptureSnapshotAsync();
        var args = new { region = new { x = 1, y = 1, width = 30, height = 22 }, profile = new { waterLevel = 200 },
            footprints = Profiles().Take(1), count = 1, seed = 1, typeNames = new[] { "CC_Tree01" } };
        Assert.That((await Run("objects.scatter", args)).Error?.Code, Is.EqualTo("FOOTPRINT_MISSING"));
        var huge = new[] { new ObjectFootprint { TypeName = "CC_Tree01", Boxes = new[] { new FootprintBox { WidthCells = 50, DepthCells = 50 } } } };
        Assert.That((await Run("objects.scatter", new { args.region, args.profile, footprints = huge, args.count, args.seed, args.typeNames })).Error?.Code, Is.EqualTo("PLACEMENT_FAILED"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }
}
