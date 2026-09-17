using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class MapValidationTests
{
    private string _root = null!;
    private MapSession _session = null!;
    [SetUp] public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-validation-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
        await Run("starts.place", new { playerSlot = 1, x = 8, y = 8 });
        await Run("starts.place", new { playerSlot = 2, x = 24, y = 16 });
    }
    [TearDown] public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string name, object args) => CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest
    { Command = name, SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Arguments = JsonSerializer.SerializeToElement(args) });
    private static JsonElement Data(CommandResult result)
    {
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return JsonSerializer.SerializeToElement(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    private static object[] Footprints() => new object[] { new { typeName = "CC_Tree01", boxes = new[] { new { widthCells = 4, depthCells = 4 } } } };

    [Test]
    public async Task RequestedRulesHaveExplicitFailureAndStableProvenance()
    {
        var before = await _session.CaptureSnapshotAsync();
        var args = new { expectedPlayers = 2, profile = new { waterLevel = 200 }, footprints = Footprints() };
        var passed = Data(await Run("map.validate", args));
        Assert.That(passed.GetProperty("status").GetString(), Is.EqualTo("passed"));
        Assert.That(passed.GetProperty("mapContentHash").GetString(), Is.EqualTo(before.ContentHash));
        var failed = Data(await Run("map.validate", new { args.expectedPlayers, args.profile, args.footprints,
            resources = new[] { new { typeName = "OreNode", minCount = 8, maxCount = 8 } } }));
        Assert.That(failed.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(failed.GetProperty("checks").EnumerateArray().Any(c => c.GetProperty("id").GetString() == "resource-count:OreNode"
            && c.GetProperty("status").GetString() == "failed"), Is.True);
        Assert.That(failed.GetProperty("profileHash").GetString(), Is.Not.EqualTo(passed.GetProperty("profileHash").GetString()));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test]
    public async Task CandidateBlockedSpawnFailsWhileActiveMapStillPasses()
    {
        var prepared = await Run("edits.prepare", new { baseRevision = _session.Revision, commands = new[] {
            new { command = "objects.place", arguments = new { typeName = "CC_Tree01", x = 8, y = 8 } } } });
        Data(prepared);
        var plan = (PreparedEditInfo)prepared.Data!;
        var args = new { expectedPlayers = 2, profile = new { waterLevel = 200 }, footprints = Footprints() };
        var candidate = Data(await Run("map.validate", new { args.expectedPlayers, args.profile, args.footprints,
            preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash }));
        Assert.That(candidate.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(candidate.GetProperty("mapContentHash").GetString(), Is.EqualTo(plan.CandidateContentHash));
        Assert.That(Data(await Run("map.validate", args)).GetProperty("status").GetString(), Is.EqualTo("passed"));
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
    }

    [Test]
    public async Task UnknownFootprintsCannotProduceSuccessfulValidation()
    {
        Data(await Run("objects.place", new { typeName = "OreNode", x = 16, y = 12 }));
        var result = Data(await Run("map.validate", new { expectedPlayers = 2, profile = new { }, footprints = Footprints() }));
        Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(result.GetProperty("checks").EnumerateArray().Any(c => c.GetProperty("status").GetString() == "not-evaluated"), Is.True);
    }

    [Test]
    public async Task BuildAreaRejectsWalkableReservationsAndHeightSpreadWithoutMutation()
    {
        var region = new { kind = "rectangle", space = "playableGrid", x = 12, y = 10, width = 4, height = 4 };
        var footprints = new[] { new { typeName = "CC_Tree01", boxes = new[] {
            new { widthCells = 4, depthCells = 4, blocksMovement = false } } } };
        var args = new { expectedPlayers = 2, profile = new { }, footprints,
            buildAreas = new[] { new { id = "base", region, playerSlot = 1, maxHeightDifference = 1 } } };
        Assert.That(Data(await Run("map.validate", args)).GetProperty("status").GetString(), Is.EqualTo("passed"));
        Data(await Run("objects.place", new { typeName = "CC_Tree01", x = 14, y = 12, anchor = "gridPoint" }));
        var before = await _session.CaptureSnapshotAsync();
        var report = Data(await Run("map.validate", args));
        var check = report.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "build-area:base");
        Assert.That(check.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(check.GetProperty("details").GetProperty("blockedCells").GetInt32(), Is.Zero);
        Assert.That(check.GetProperty("details").GetProperty("occupiedOrReservedCells").GetInt32(), Is.EqualTo(16));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Data(await Run("terrain.set_height", new { region = new { kind = "rectangle", space = "playableGrid", x = 12, y = 10, width = 1, height = 1 }, height = 310 }));
        report = Data(await Run("map.validate", args));
        check = report.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "build-area:base");
        Assert.That(check.GetProperty("details").GetProperty("maxHeight").GetDouble() - check.GetProperty("details").GetProperty("minHeight").GetDouble(), Is.GreaterThan(1));
    }

    [Test]
    public async Task BuildAreaMapGridBorderFailsAndDuplicateIdsAreRejected()
    {
        var area = new { id = "border", region = new { kind = "rectangle", space = "mapGrid", x = 0, y = 0, width = 4, height = 4 } };
        var report = Data(await Run("map.validate", new { expectedPlayers = 2, profile = new { }, footprints = Footprints(), buildAreas = new[] { area } }));
        var check = report.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "build-area:border");
        Assert.That(check.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(check.GetProperty("details").GetProperty("outsidePlayableCells").GetInt32(), Is.EqualTo(16));
        var invalid = await Run("map.validate", new { expectedPlayers = 2, profile = new { }, footprints = Footprints(), buildAreas = new[] { area, area } });
        Assert.That(invalid.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
    }
}
