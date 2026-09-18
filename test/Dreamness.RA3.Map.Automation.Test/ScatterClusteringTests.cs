using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

/// <summary>
/// Scatter used to reach its spacing by minimum-distance repulsion, which produces an evenly
/// spread layout. Measured against shipped maps that is wrong: they cluster, with a clumping
/// index around 3 where even spacing lands below 1. These pin both behaviours.
/// </summary>
public class ScatterClusteringTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-scatter-clump-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 192, 192, 4);
    }

    [TearDown]
    public void Cleanup()
    {
        _session.Dispose();
        Directory.Delete(_root, true);
    }

    private async Task<JsonElement> Scatter(object extra)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["region"] = new { x = 10, y = 10, width = 170, height = 170 },
            ["profile"] = new { waterLevel = 0 },
            ["seed"] = 20260918,
            ["count"] = 240,
            ["minDistanceCells"] = 2,
            ["typeNames"] = new[] { "CC_Tree01" }
        };
        foreach (var pair in (Dictionary<string, object?>)extra) arguments[pair.Key] = pair.Value;
        var result = await _executor.ExecuteAsync(new CommandRequest
        {
            SessionId = _session.SessionId,
            ExpectedRevision = _session.Revision,
            Command = "objects.scatter",
            Arguments = JsonSerializer.SerializeToElement(arguments)
        });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return JsonSerializer.SerializeToElement(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Test]
    public async Task EvenSpacingUnderDispersesAndSaysSo()
    {
        var data = await Scatter(new Dictionary<string, object?>());
        Assert.That(data.GetProperty("clustering").GetString(), Is.EqualTo("uniform"));
        var index = data.GetProperty("clumpingIndex").GetDouble();
        Assert.That(index, Is.LessThan(1.6),
            $"an evenly spaced scatter should look under-dispersed, not clumped; got {index}");
    }

    [Test]
    public async Task SeededGrovesReachTheClumpingShippedMapsShow()
    {
        var data = await Scatter(new Dictionary<string, object?>
        {
            ["clusters"] = 12,
            ["clusterRadiusCells"] = 9,
            ["clusterSpacingCells"] = 34
        });
        Assert.That(data.GetProperty("clustering").GetString(), Is.EqualTo("clustered"));
        Assert.That(data.GetProperty("clusters").GetInt32(), Is.GreaterThan(4));
        var index = data.GetProperty("clumpingIndex").GetDouble();
        // The corpus sits at 2.39-3.81 (P25-P75); the tool has to be able to reach that band.
        Assert.That(index, Is.GreaterThanOrEqualTo(2.4),
            $"seeded groves should reach the clustered range shipped maps show; got {index}");
    }

    [Test]
    public async Task ClusteringStillPlacesEveryRequestedObject()
    {
        var data = await Scatter(new Dictionary<string, object?>
        {
            ["clusters"] = 8,
            ["clusterRadiusCells"] = 10,
            ["clusterSpacingCells"] = 40
        });
        Assert.That(data.GetProperty("placed").GetInt32(), Is.EqualTo(240));
    }

    [Test]
    public async Task ClusterArgumentsAreValidated()
    {
        Assert.That((await Attempt(new Dictionary<string, object?> { ["clusters"] = -1 })).Error?.Code,
            Is.EqualTo("INVALID_ARGUMENT"), "clusters below zero");
        Assert.That((await Attempt(new Dictionary<string, object?> { ["clusters"] = 12, ["clusterRadiusCells"] = 0 })).Error?.Code,
            Is.EqualTo("INVALID_ARGUMENT"), "a grove needs a radius");
        Assert.That((await Attempt(new Dictionary<string, object?> { ["clusters"] = 12, ["clusterSpacingCells"] = 1 })).Error?.Code,
            Is.EqualTo("INVALID_ARGUMENT"), "groves must be separable");
    }

    private async Task<CommandResult> Attempt(object extra)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["region"] = new { x = 10, y = 10, width = 40, height = 40 },
            ["profile"] = new { waterLevel = 0 },
            ["seed"] = 1,
            ["count"] = 4,
            ["minDistanceCells"] = 2,
            ["typeNames"] = new[] { "CC_Tree01" }
        };
        foreach (var pair in (Dictionary<string, object?>)extra) arguments[pair.Key] = pair.Value;
        return await _executor.ExecuteAsync(new CommandRequest
        {
            SessionId = _session.SessionId,
            ExpectedRevision = _session.Revision,
            Command = "objects.scatter",
            Arguments = JsonSerializer.SerializeToElement(arguments)
        });
    }
}
