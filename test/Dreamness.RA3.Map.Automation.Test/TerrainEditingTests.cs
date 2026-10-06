using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class TerrainEditingTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "RA3-TerrainTools-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 16, 12, 4);
    }

    [TearDown]
    public void TearDown() { _session.Dispose(); Directory.Delete(_root, true); }

    private Task<CommandResult> Run(string command, object arguments) => _executor.ExecuteAsync(new CommandRequest
    {
        SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command,
        Arguments = JsonSerializer.SerializeToElement(arguments)
    });

    [Test]
    public void CircleUsesCellCentersAndBorderOffset()
    {
        var region = new GridRegion { Kind = "circle", CenterX = 2, CenterY = 2, Radius = 1 };
        Assert.That(region.Cells(_session.Facade), Is.EquivalentTo(new[] { (5, 5), (5, 6), (6, 5), (6, 6) }));
        region.CenterX = .5f;
        Assert.Throws<AutomationException>(() => region.Cells(_session.Facade));
    }

    [Test]
    public async Task SculptChangesOnlySelectedCellsAndPreservesEnvironmentAcrossUndoRedo()
    {
        var before = _session.Facade;
        var original = before.GetTerrainHeight(7, 7);
        var environment = before.ra3Map.Context.AssetDict.Where(kv => kv.Key.Contains("Water") || kv.Key.Contains("Lighting"))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Data.ToArray());
        var result = await Run("terrain.sculpt", new { region = new { kind = "circle", centerX = 3, centerY = 3, radius = 1 }, mode = "set", value = 281.234f });
        Assert.That(result.Status, Is.EqualTo("succeeded"));
        Assert.That(_session.Facade.GetTerrainHeight(6, 6), Is.EqualTo(TerrainHeight.Normalize(281.234f)));
        Assert.That(_session.Facade.GetTerrainHeight(8, 7), Is.EqualTo(original));
        foreach (var pair in environment) Assert.That(_session.Facade.ra3Map.Context.AssetDict[pair.Key].Data, Is.EqualTo(pair.Value));
        Assert.That((await Run("history.undo", new { })).Status, Is.EqualTo("succeeded"));
        Assert.That(_session.Facade.GetTerrainHeight(6, 6), Is.EqualTo(original));
        Assert.That((await Run("history.redo", new { })).Status, Is.EqualTo("succeeded"));
        Assert.That(_session.Facade.GetTerrainHeight(6, 6), Is.EqualTo(TerrainHeight.Normalize(281.234f)));
    }

    [Test]
    public async Task SmoothReadsPreviousIterationAndDoesNotWriteHalo()
    {
        await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 16, height = 12 }, height = 270 });
        await Run("terrain.set_height", new { region = new { x = 6, y = 5, width = 1, height = 1 }, height = 360 });
        var result = await Run("terrain.smooth", new { region = new { x = 5, y = 4, width = 3, height = 3 }, iterations = 1, strength = 1 });
        Assert.That(result.Status, Is.EqualTo("succeeded"));
        for (var y = 4; y < 7; y++)
        for (var x = 5; x < 8; x++)
            Assert.That(_session.Facade.GetTerrainHeight(x + 4, y + 4), Is.EqualTo(280));
        Assert.That(_session.Facade.GetTerrainHeight(8, 9), Is.EqualTo(270));
    }

    [Test]
    public async Task InvalidSculptCannotPartiallyCommit()
    {
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("terrain.sculpt", new { region = new { x = 0, y = 0, width = 16, height = 12 }, mode = "raise", value = -5000 });
        Assert.That(result.Status, Is.EqualTo("failed"));
        Assert.That(_session.Revision, Is.EqualTo(0));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test]
    public async Task TexturePaintPersistsAndUndoRestoresAndRejectsUnknownTexture()
    {
        var original = _session.Facade.GetTileTexture(6, 6);
        var result = await Run("texture.paint", new { region = new { x = 2, y = 2, width = 4, height = 3 }, texture = "Dirt_Romania01", autoBlend = true });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        Assert.That(_session.Facade.GetTileTexture(6, 6), Is.EqualTo("Dirt_Romania01"));
        Assert.That(_session.Facade.GetTileTexture(5, 6), Is.EqualTo(original));
        Assert.That((await Run("history.undo", new { })).Status, Is.EqualTo("succeeded"));
        Assert.That(_session.Facade.GetTileTexture(6, 6), Is.EqualTo(original));
        var revision = _session.Revision;
        result = await Run("texture.paint", new { region = new { x = 2, y = 2, width = 4, height = 3 }, texture = "MadeUpTexture" });
        Assert.That(result.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.EqualTo(revision));
    }
}
