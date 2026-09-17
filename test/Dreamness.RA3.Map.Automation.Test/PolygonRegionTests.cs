using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class PolygonRegionTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private static PolygonVertex[] Vertices() => new[] { new PolygonVertex(2, 2), new(8, 2), new(8, 4), new(4, 4), new(4, 8), new(2, 8) };
    private static GridRegion Region() => new() { Kind = "polygon", Vertices = Vertices() };
    [SetUp] public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-polygon-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }
    [TearDown] public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string name, object args) => CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest
    { Command = name, SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Arguments = JsonSerializer.SerializeToElement(args) });
    private static void Ok(CommandResult result) => Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);

    [Test]
    public void ConcaveSelectionIsWindingIndependentAndHonorsMapBorder()
    {
        var region = Region();
        var cells = region.GetCells(_session.Facade);
        Assert.That(cells.Count, Is.EqualTo(20));
        Assert.That(cells, Does.Contain((6, 6)).And.Not.Contain((8, 8)));
        region.Vertices = Vertices().Reverse().ToArray();
        Assert.That(region.GetCells(_session.Facade), Is.EqualTo(cells));
        var polygon = new GridPolygon(Vertices());
        Assert.That(polygon.Contains(4, 5), Is.True);
        Assert.That(polygon.Contains(4.1, 5), Is.False);
        Assert.That(polygon.DistanceToBoundary(3, 3), Is.EqualTo(1));
    }

    [Test]
    public void DegenerateAndCrossingPolygonsReject()
    {
        foreach (var vertices in new[] {
            new[] { new PolygonVertex(1, 1), new(5, 5), new(1, 5), new(5, 1) },
            new[] { new PolygonVertex(1, 1), new(2, 2), new(3, 3) },
            new[] { new PolygonVertex(1, 1), new(2, 1), new(2, 2), new(1, 1) } })
            Assert.That(Assert.Throws<AutomationException>(() => new GridPolygon(vertices))!.Code, Is.EqualTo("INVALID_ARGUMENT"));
    }

    [Test]
    public async Task SculptScatterAndPersistentProtectionUseSamePolygon()
    {
        var before = _session.Facade.GetTerrainHeight(9, 9);
        Ok(await Run("terrain.sculpt", new { region = Region(), mode = "set", value = 350, falloff = .5 }));
        Assert.That(_session.Facade.GetTerrainHeight(9, 9), Is.EqualTo(before), "Concave notch must remain untouched.");
        Assert.That(_session.Facade.GetTerrainHeight(6, 6), Is.GreaterThan(before).And.LessThan(350));
        Ok(await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 32, height = 24 }, height = 260 }));
        Ok(await Run("objects.scatter", new { region = Region(), profile = new { waterLevel = 200 }, seed = 42, count = 4,
            minDistanceCells = 1, typeNames = new[] { "CC_Tree01" } }));
        var polygon = new GridPolygon(Vertices());
        Assert.That(_session.Facade.GetUnitObjects().All(o => polygon.Contains(o.Position.X / 10d, o.Position.Y / 10d)), Is.True);
        Ok(await Run("protections.add", new { id = "keep", region = Region(), layers = new[] { "terrain", "objects" } }));
        Ok(await Run("map.save", new { }));
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That(_session.ProtectionZones.Single().Region.Vertices, Is.EqualTo(Vertices()));
        var snapshot = await _session.CaptureSnapshotAsync();
        Assert.That((await Run("objects.place", new { typeName = "CC_Tree01", x = 3, y = 3 })).Status, Is.EqualTo("failed"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
        Ok(await Run("objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 }));
    }
}
