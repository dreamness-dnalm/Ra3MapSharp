using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Automation.Test;

public class CliffTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    private static readonly object Region = new { x = 0, y = 0, width = 32, height = 24 };
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-cliffs-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
        await Run("terrain.set_height", new { region = Region, height = 100 });
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private Task<CommandResult> Step() => Run("terrain.set_height", new { region = new { x = 16, y = 0, width = 16, height = 24 }, height = 200 });
    private object Args(int maxObjects = 2000) => new { region = Region, seed = 42, maxObjects,
        style = new { name = "test-rock", pieces = new[] { new { typeName = "TestCliff", lengthCells = 2, depthCells = 1,
            angleOffsetDegrees = 90, normalOffsetCells = .25, zOffset = 3 } } } };
    private object[] Placements() => _session.Facade.GetUnitObjects()
        .Select(o => (object)(o.TypeName, o.Position.X, o.Position.Y, o.Position.Z, o.Angle)).ToArray();

    [Test]
    public async Task FlatTerrainAndSmallRampHaveNoCliffsQueryDoesNotMutate()
    {
        var revision = _session.Revision;
        Assert.That(CliffLines.Detect(_session.Facade, new GridRegion { Width = 32, Height = 24 }, 20, 60), Is.Empty);
        Assert.That((await Run("terrain.detect_cliffs", new { region = Region })).Status, Is.EqualTo("succeeded"));
        Assert.That(_session.Revision, Is.EqualTo(revision));
        await Run("terrain.set_height", new { region = new { x = 16, y = 0, width = 16, height = 24 }, height = 110 });
        Assert.That(CliffLines.Detect(_session.Facade, new GridRegion { Width = 32, Height = 24 }, 20, 60), Is.Empty);
    }

    [Test]
    public async Task DirectedStepAndMapGridEquivalentAndClosedPlateau()
    {
        await Step();
        var lines = CliffLines.Detect(_session.Facade, new GridRegion { Width = 32, Height = 24 }, 20, 60);
        Assert.That(lines, Has.Length.EqualTo(1));
        Assert.That(lines[0].Closed, Is.False);
        Assert.That(lines[0].Segments.All(s => s.From.X == 15.5 && s.To.Y < s.From.Y && s.LowHeight == 100 && s.HighHeight == 200), Is.True);
        var mapGrid = CliffLines.Detect(_session.Facade, new GridRegion { Space = "mapGrid", X = 4, Y = 4, Width = 32, Height = 24 }, 20, 60);
        Assert.That(mapGrid[0].Segments, Is.EqualTo(lines[0].Segments));
        await Run("terrain.set_height", new { region = Region, height = 100 });
        await Run("terrain.set_height", new { region = new { x = 10, y = 8, width = 8, height = 8 }, height = 200 });
        lines = CliffLines.Detect(_session.Facade, new GridRegion { Width = 32, Height = 24 }, 20, 60);
        Assert.That(lines, Has.Length.EqualTo(1));
        Assert.That(lines[0].Closed, Is.True);
        Assert.That(lines[0].LengthCells, Is.EqualTo(32));
    }

    [Test]
    public async Task PlacementUsesTerrainRelativeHeightUphillOffsetAndAngleCorrectionAndReplaysAfterUndo()
    {
        await Step();
        var result = await Run("objects.place_cliffs", Args());
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var objects = _session.Facade.GetUnitObjects();
        Assert.That(objects.Count, Is.GreaterThan(5));
        Assert.That(objects.All(o => Math.Abs(o.Position.X - 157.5) < .001 && o.Position.Z == 3 && o.Angle == 0), Is.True);
        var expected = Placements();
        await Run("history.undo", new { });
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        await Run("history.redo", new { });
        Assert.That(Placements(), Is.EqualTo(expected));
        await Run("history.undo", new { });
        Assert.That((await Run("objects.place_cliffs", Args())).Status, Is.EqualTo("succeeded"));
        Assert.That(Placements(), Is.EqualTo(expected));
    }

    [Test]
    public async Task ClosedPlateauPlacesFourOuterCornersAndReservesStraightSpans()
    {
        await Run("terrain.set_height", new { region = new { x = 10, y = 8, width = 8, height = 8 }, height = 200 });
        var result = await Run("objects.place_cliffs", new { region = Region, seed = 1,
            style = new { name = "corner-test", pieces = new[] {
                new { typeName = "TestStraight", role = "straight", lengthCells = 1, depthCells = 1 },
                new { typeName = "TestCorner", role = "outerCorner", lengthCells = 1, depthCells = 1 }
            } } });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var objects = _session.Facade.GetUnitObjects();
        Assert.That(objects.Count(o => o.TypeName == "TestCorner"), Is.EqualTo(4));
        Assert.That(objects.Where(o => o.TypeName == "TestCorner").Select(o => o.Angle).OrderBy(a => a),
            Is.EqualTo(new[] { 0f, 90f, 180f, 270f }));
        Assert.That(objects.Count(o => o.TypeName == "TestStraight"), Is.EqualTo(28));
    }

    [Test]
    public async Task LowBaseConvertsAbsoluteHeightToOffsetAndPivotCorrectionPreservesCoverage()
    {
        await Step();
        var args = new { region = Region, seed = 42, baseHeight = "low",
            style = new { name = "pivot", pieces = new[] { new { typeName = "TestCliff", lengthCells = 2, depthCells = 1,
                normalOffsetCells = .25, originOffsetAlongCells = .5, originOffsetNormalCells = .25, zOffset = 3 } } } };
        Assert.That((await Run("objects.place_cliffs", args)).Status, Is.EqualTo("succeeded"));
        // Directed seam runs south; the left/uphill direction is east. Origin x=16,
        // terrain underneath is 200, target absolute height is 100+3 => serialized Z=-97.
        Assert.That(_session.Facade.GetUnitObjects().All(o => Math.Abs(o.Position.X - 160) < .001 && Math.Abs(o.Position.Z + 97) < .001), Is.True);
        var before = Placements();
        Assert.That((await Run("objects.place_cliffs", args)).Status, Is.EqualTo("succeeded"));
        Assert.That(Placements(), Is.EqualTo(before), "Existing occupancy must undo the origin correction.");
    }

    [Test]
    public async Task MeasuredDimensionsAccountForModelAngleAndExistingPiecesPreventDuplicates()
    {
        await Step();
        var footprints = new FootprintCatalog();
        footprints.SetOverride("TestCliff", 1, 2);
        var executor = new CommandExecutor(CommandRegistry.CreateDefault(footprints: footprints));
        async Task<CommandResult> Place() => await executor.ExecuteAsync(new CommandRequest
        {
            SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = "objects.place_cliffs",
            Arguments = JsonSerializer.SerializeToElement(new { region = Region, seed = 42,
                style = new { name = "measured", pieces = new[] { new { typeName = "TestCliff", angleOffsetDegrees = 90 } } } })
        });
        Assert.That((await Place()).Status, Is.EqualTo("succeeded"));
        var expected = Placements();
        Assert.That(expected.Length, Is.EqualTo(11));
        Assert.That((await Place()).Status, Is.EqualTo("succeeded"));
        Assert.That(Placements(), Is.EqualTo(expected));
    }

    [Test]
    public async Task MissingDimensionsAndObjectLimitLeaveMapUnchanged()
    {
        await Step();
        var before = await _session.CaptureSnapshotAsync();
        var missing = await Run("objects.place_cliffs", new { region = Region, seed = 1,
            style = new { name = "unknown", pieces = new[] { new { typeName = "TestCliff" } } } });
        Assert.That(missing.Error?.Code, Is.EqualTo("FOOTPRINT_MISSING"));
        Assert.That((await Run("objects.place_cliffs", Args(1))).Error?.Code, Is.EqualTo("LIMIT_EXCEEDED"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.Revision, Is.EqualTo(before.Revision));
    }

    [Test]
    public async Task ProtectionSkipsIntersectingFootprintsAndPreparedEditDoesNotCommitUntilApply()
    {
        await Step();
        Assert.That((await Run("protections.add", new { id = "reserve", region = new { x = 14, y = 8, width = 4, height = 8 }, layers = new[] { "objects" } })).Status, Is.EqualTo("succeeded"));
        var prepared = await Run("edits.prepare", new { baseRevision = _session.Revision,
            commands = new[] { new { command = "objects.place_cliffs", arguments = Args() } } });
        Assert.That(prepared.Status, Is.EqualTo("succeeded"), prepared.Error?.Message);
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        var data = JsonSerializer.SerializeToElement(prepared.Data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var applied = await Run("edits.apply", new { preparedPlanId = data.GetProperty("preparedPlanId").GetString(), planHash = data.GetProperty("planHash").GetString() });
        Assert.That(applied.Status, Is.EqualTo("succeeded"), applied.Error?.Message);
        Assert.That(_session.Facade.GetUnitObjects().Count, Is.GreaterThan(2));
        Assert.That(_session.Facade.GetUnitObjects().All(o => o.Position.Y / 10 + 1 <= 8 || o.Position.Y / 10 - 1 >= 16), Is.True);
    }
}
