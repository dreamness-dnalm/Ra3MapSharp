using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class ResourceAccessTests
{
    private string _root = null!;
    private MapSession _session = null!;
    [SetUp] public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-resource-access-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
        Data(await Run("starts.place", new { playerSlot = 1, x = 4, y = 12 }));
        Data(await Run("starts.place", new { playerSlot = 2, x = 27, y = 12 }));
        foreach (var x in new[] { 4, 27 }) Data(await Run("objects.place", new { typeName = "OreNode", x, y = 5, anchor = "gridPoint" }));
    }
    [TearDown] public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private static JsonElement Data(CommandResult result)
    {
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return JsonSerializer.SerializeToElement(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    private static object Footprints() => new[] { new { typeName = "OreNode", boxes = new[] {
        new { label = "body", widthCells = 2, depthCells = 2, offsetYCells = 0, blocksMovement = true },
        new { label = "access", widthCells = 2, depthCells = 2, offsetYCells = 3, blocksMovement = false } } } };
    private object Args(string? planId = null, string? planHash = null, int nearest = 1) => new {
        expectedPlayers = 2, profile = new { }, footprints = Footprints(), preparedPlanId = planId, planHash,
        resourceAccess = new[] { new { typeName = "OreNode", nearestCount = nearest, maxRankDistanceSpreadCells = 0 } } };
    private static JsonElement Access(JsonElement report) => report.GetProperty("checks").EnumerateArray()
        .Single(c => c.GetProperty("id").GetString() == "resource-access:OreNode");

    [Test] public async Task EqualCountsButCandidateDetourFailsDistanceFairnessWithoutEditingActiveMap()
    {
        var before = await _session.CaptureSnapshotAsync();
        var baseline = Data(await Run("map.validate", Args()));
        Assert.That(Access(baseline).GetProperty("status").GetString(), Is.EqualTo("passed"));
        Assert.That(Access(baseline).GetProperty("details").GetProperty("players")[0].GetProperty("nearestDistanceCells")[0].GetInt32(), Is.EqualTo(4));
        var prepared = await Run("edits.prepare", new { baseRevision = _session.Revision, commands = new[] {
            new { command = "terrain.set_height", arguments = new { region = new { x = 0, y = 10, width = 10, height = 1 }, height = 400 } } } });
        Data(prepared); var plan = (PreparedEditInfo)prepared.Data!;
        var candidate = Data(await Run("map.validate", Args(plan.PreparedPlanId, plan.PlanHash)));
        var check = Access(candidate);
        Assert.That(check.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(check.GetProperty("details").GetProperty("rankDistanceSpreadCells")[0].GetInt32(), Is.GreaterThan(0));
        Assert.That(check.GetProperty("details").GetProperty("unreachableResources").GetInt32(), Is.Zero);
        Assert.That(candidate.GetProperty("mapContentHash").GetString(), Is.EqualTo(plan.CandidateContentHash));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test] public async Task UnreachableResourceAndInsufficientRanksCannotPass()
    {
        var tooMany = Data(await Run("map.validate", Args(nearest: 3)));
        Assert.That(Access(tooMany).GetProperty("details").GetProperty("enoughReachableResourcesPerPlayer").GetBoolean(), Is.False);
        Data(await Run("terrain.set_height", new { region = new { x = 3, y = 7, width = 2, height = 2 }, height = 400 }));
        var blocked = Access(Data(await Run("map.validate", Args())));
        Assert.That(blocked.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(blocked.GetProperty("details").GetProperty("unreachableResources").GetInt32(), Is.EqualTo(1));
    }

    [Test] public async Task MissingOrMovementBlockingAccessLabelIsRejected()
    {
        foreach (var label in new[] { "missing", "body" })
        {
            var result = await Run("map.validate", new { expectedPlayers = 2, profile = new { }, footprints = Footprints(),
                resourceAccess = new[] { new { typeName = "OreNode", accessLabel = label } } });
            Assert.That(result.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        }
    }

    [Test] public async Task DistanceLimitAndUnknownFootprintsFailExplicitly()
    {
        var limited = Data(await Run("map.validate", new { expectedPlayers = 2, profile = new { }, footprints = Footprints(),
            resourceAccess = new[] { new { typeName = "OreNode", maxDistanceCells = 3 } } }));
        Assert.That(Access(limited).GetProperty("status").GetString(), Is.EqualTo("failed"));
        Data(await Run("objects.place", new { typeName = "CC_Tree01", x = 15, y = 20 }));
        var missing = Access(Data(await Run("map.validate", Args())));
        Assert.That(missing.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(missing.GetProperty("details").GetProperty("evaluated").GetBoolean(), Is.False);
    }
}
