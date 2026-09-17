using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class ScatterTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-scatter-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });

    private object Args(int count = 25, int seed = 42) => new
    {
        region = new { x = 1, y = 1, width = 28, height = 20 }, seed, count, minDistanceCells = 3,
        typeNames = new[] { "CC_Tree01", "CC_Bush01" }, profile = new { waterLevel = 200, maxSlopeDegrees = 35 },
        exclusions = new[] { new { x = 14, y = 0, width = 4, height = 24 } }
    };

    private object[] Placements() => _session.Facade.GetUnitObjects()
        .Select(o => (object)(o.TypeName, o.Position.X, o.Position.Y, o.Position.Z, o.Angle)).ToArray();

    [Test]
    public async Task DeterministicAcrossUndoAndKeepsExistingObjectDistanceAndExclusion()
    {
        await Run("objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 });
        var result = await Run("objects.scatter", Args());
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var objects = _session.Facade.GetUnitObjects();
        Assert.That(objects.Count, Is.EqualTo(26));
        Assert.That(objects.Skip(1).All(o => o.Angle >= 0 && o.Angle < 360), Is.True);
        Assert.That(objects.Skip(1).Max(o => o.Angle), Is.GreaterThan(180), "Seed 42 must cover more than the first few degrees.");
        foreach (var o in objects.Skip(1)) Assert.That(o.Position.X < 140 || o.Position.X >= 180, Is.True);
        for (var i = 0; i < objects.Count; i++)
        for (var j = i + 1; j < objects.Count; j++)
            Assert.That(Math.Pow(objects[i].Position.X - objects[j].Position.X, 2)
                + Math.Pow(objects[i].Position.Y - objects[j].Position.Y, 2), Is.GreaterThanOrEqualTo(900 - .01));
        var expected = Placements();
        await Run("history.undo", new { });
        Assert.That(_session.Facade.GetUnitObjects().Count, Is.EqualTo(1));
        Assert.That((await Run("objects.scatter", Args())).Status, Is.EqualTo("succeeded"));
        Assert.That(Placements(), Is.EqualTo(expected));
    }

    [Test]
    public async Task ExhaustedPlacementLeavesNoObjectsOrRevisionChange()
    {
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("objects.scatter", Args(500));
        Assert.That(result.Error?.Code, Is.EqualTo("PLACEMENT_FAILED"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.Revision, Is.EqualTo(before.Revision));
    }

    [Test]
    public async Task AvoidsExplicitWaterAndStaysInsideCircle()
    {
        await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 16, height = 24 }, height = 185 });
        var result = await Run("objects.scatter", new { region = new { kind = "circle", centerX = 16, centerY = 12, radius = 10 },
            profile = new { waterLevel = 200 }, seed = -2, count = 12, minDistanceCells = 2, typeNames = new[] { "CC_Tree01" } });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        foreach (var o in _session.Facade.GetUnitObjects())
        {
            Assert.That(o.Position.X, Is.GreaterThan(160));
            Assert.That(Math.Pow(o.Position.X / 10 - 16, 2) + Math.Pow(o.Position.Y / 10 - 12, 2), Is.LessThanOrEqualTo(100));
        }
    }
}
