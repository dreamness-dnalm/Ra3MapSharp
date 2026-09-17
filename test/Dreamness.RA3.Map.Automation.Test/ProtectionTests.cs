using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class ProtectionTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-protection-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 24, 24, 4);
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private Task<CommandResult> Protect(params string[] layers) => Run("protections.add", new
    { id = "base", region = new { x = 4, y = 4, width = 6, height = 6 }, layers });

    [TestCase(false)]
    [TestCase(true)]
    public async Task MetadataOnlyChangeSurvivesReopenAndUndoRedo(bool save)
    {
        Assert.That((await Protect("terrain")).Status, Is.EqualTo("succeeded"));
        Assert.That(_session.Dirty, Is.True);
        if (save) { await Run("map.save", new { }); Assert.That(_session.Dirty, Is.False); }
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        var args = new { region = new { x = 5, y = 5, width = 1, height = 1 }, height = 300 };
        Assert.That((await Run("terrain.set_height", args)).Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        await Run("history.undo", new { });
        var list = JsonSerializer.SerializeToElement((await Run("protections.list", new { })).Data);
        Assert.That(list.GetProperty("zones").GetArrayLength(), Is.Zero);
        await Run("history.redo", new { });
        Assert.That((await Run("terrain.set_height", args)).Error?.Code, Is.EqualTo("PROTECTED_REGION"));
    }

    [Test]
    public async Task LayerIsolationAndFailedBatchRollback()
    {
        await Protect("terrain");
        Assert.That((await Run("objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 })).Status, Is.EqualTo("succeeded"));
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("batch.execute", new { commands = new object[] {
            new { command = "objects.place", arguments = new { typeName = "CC_Tree01", x = 15, y = 15 } },
            new { command = "terrain.set_height", arguments = new { region = new { x = 0, y = 0, width = 24, height = 24 }, height = 300 } }
        }});
        Assert.That(result.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test]
    public async Task CannotMoveObjectIntoOrOutOfProtectedRegion()
    {
        var placed = await Run("objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 });
        var id = JsonSerializer.SerializeToElement(placed.Data).GetProperty("objectId").GetString();
        await Protect("objects");
        Assert.That((await Run("objects.move", new { objectId = id, x = 15, y = 15 })).Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That((await Run("objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 })).Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That((await Run("objects.place", new { typeName = "CC_Tree01", x = 15, y = 15 })).Status, Is.EqualTo("succeeded"));
        await Run("protections.remove", new { id = "base" });
        Assert.That((await Run("objects.move", new { objectId = id, x = 15, y = 15 })).Status, Is.EqualTo("succeeded"));
    }

    [Test]
    public async Task TexturePaintCannotChangeProtectedCells()
    {
        await Protect("textures");
        var result = await Run("texture.paint", new { region = new { x = 5, y = 5, width = 2, height = 2 }, texture = "Dirt_Romania01" });
        Assert.That(result.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        result = await Run("texture.paint", new { region = new { x = 16, y = 16, width = 2, height = 2 }, texture = "Dirt_Romania01" });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task BatchCannotBypassProtectionByAddingOrRemovingIt(bool add)
    {
        if (!add) await Protect("terrain");
        var beforeRevision = _session.Revision;
        var result = await Run("batch.execute", new { commands = new object[] {
            new { command = add ? "protections.add" : "protections.remove", arguments = new {
                id = "base", region = new { x = 4, y = 4, width = 6, height = 6 }, layers = new[] { "terrain" } } },
            new { command = "terrain.set_height", arguments = new { region = new { x = 5, y = 5, width = 1, height = 1 }, height = 300 } }
        }});
        Assert.That(result.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That(_session.Revision, Is.EqualTo(beforeRevision));
        var list = JsonSerializer.SerializeToElement((await Run("protections.list", new { })).Data);
        Assert.That(list.GetProperty("zones").GetArrayLength(), Is.EqualTo(add ? 0 : 1));
    }

    [Test]
    public async Task ScatterAutomaticallyAvoidsObjectProtection()
    {
        await Protect("objects");
        var result = await Run("objects.scatter", new { region = new { x = 0, y = 0, width = 24, height = 24 },
            profile = new { waterLevel = 200 }, seed = 12, count = 40, typeNames = new[] { "CC_Tree01" } });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        Assert.That(_session.Facade.GetUnitObjects().Any(o => o.Position.X >= 40 && o.Position.X < 100
            && o.Position.Y >= 40 && o.Position.Y < 100), Is.False);
    }
}
