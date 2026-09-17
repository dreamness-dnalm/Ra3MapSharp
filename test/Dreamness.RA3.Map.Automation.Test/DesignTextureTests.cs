using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class DesignTextureTests
{
    private string _root = null!;
    private MapSession _session = null!;
    [SetUp] public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-texture-entities-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }
    [TearDown] public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private static object Paint(string id, string texture, string[]? dependencies = null) => new { id, kind = "texture", dependsOn = dependencies,
        parameters = new { region = new { x = 8, y = 8, width = 4, height = 4 }, texture } };
    private async Task Apply(object patch)
    {
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var plan = (PreparedEditInfo)result.Data!;
        result = await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
    }
    private string Tiles() => JsonSerializer.Serialize(Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 32).Select(y => new {
        x, y, tile = _session.Facade.ra3Map.Context.BlendTileDataAsset.Tiles[x,y], blend = _session.Facade.GetTileBlend(x,y),
        single = _session.Facade.GetTileSingleEdgeBlend(x,y), cliff = _session.Facade.GetTileCliffBlend(x,y) })));

    [Test] public async Task LayeredTexturesSurviveReopenAreIdempotentAndRestoreExactCells()
    {
        var original = Tiles();
        await Apply(new { upsert = new[] { Paint("base", "Dirt_Yucatan02") } });
        var baseTiles = Tiles();
        Assert.That(_session.DesignEntities["base"].Textures!.Count, Is.EqualTo(36));
        await Apply(new { upsert = new[] { Paint("overlay", "Rock_Yucatan01", new[] { "base" }) } });
        await Run("map.save", new { });
        _session.Dispose(); _session = await MapSessionManager.OpenAsync(_root, "Test");
        var snapshot = await _session.CaptureSnapshotAsync();
        await Apply(new { upsert = new[] { Paint("overlay", "Rock_Yucatan01", new[] { "base" }) } });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
        await Apply(new { remove = new[] { "overlay" } });
        Assert.That(Tiles(), Is.EqualTo(baseTiles));
        await Run("history.undo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
        await Run("history.redo", new { });
        await Apply(new { remove = new[] { "base" } });
        Assert.That(Tiles(), Is.EqualTo(original));
    }

    [Test] public async Task OverlapAndManualHaloChangesRejectWithoutChangingActiveMap()
    {
        await Apply(new { upsert = new[] { Paint("base", "Dirt_Yucatan02") } });
        var snapshot = await _session.CaptureSnapshotAsync();
        var result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new[] { Paint("other", "Rock_Yucatan01") } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_OVERLAP"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
        await Run("texture.paint", new { region = new { x = 7, y = 7, width = 1, height = 1 }, texture = "Rock_Yucatan01", autoBlend = false });
        snapshot = await _session.CaptureSnapshotAsync();
        result = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { remove = new[] { "base" } } });
        Assert.That(result.Error?.Code, Is.EqualTo("ENTITY_CONFLICT"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
    }

    [Test] public async Task RepaintingUniformRegionClearsObsoleteBlendEdges()
    {
        await Run("texture.paint", new { region = new { x = 8, y = 8, width = 4, height = 4 }, texture = "Dirt_Yucatan02" });
        Assert.That(Enumerable.Range(11, 6).SelectMany(x => Enumerable.Range(11, 6).Select(y => _session.Facade.GetTileBlend(x, y))).Any(b => b != 0), Is.True);
        await Run("texture.paint", new { region = new { x = 0, y = 0, width = 32, height = 24 }, texture = "Dirt_Yucatan02" });
        Assert.That(Enumerable.Range(11, 6).SelectMany(x => Enumerable.Range(11, 6).Select(y => _session.Facade.GetTileBlend(x, y))).All(b => b == 0), Is.True);
    }

    [TestCase("rectangle")]
    [TestCase("circle")]
    [TestCase("polygon")]
    public async Task DerivedTextureReflectsSelectedCellsAndPropagatesAfterReopen(string kind)
    {
        object region = kind switch {
            "circle" => new { kind, centerX = 7.5, centerY = 8.5, radius = 3.5 },
            "polygon" => new { kind, vertices = new[] { new { x = 3, y = 3 }, new { x = 10, y = 3 }, new { x = 5, y = 10 } } },
            _ => new { kind, space = "mapGrid", x = 7, y = 7, width = 4, height = 5 }
        };
        object Source(string texture) => new { id = "source", kind = "texture", parameters = new { region, texture } };
        var derived = new { id = "mirror", kind = "derived", parameters = new { sourceEntityId = "source", transform = "rotate180" } };
        var original = Tiles();
        await Apply(new { upsert = new object[] { derived, Source("Dirt_Yucatan02") } });
        var source = _session.DesignEntities["source"].Textures!;
        var target = _session.DesignEntities["mirror"].Textures!;
        Assert.That(target.Select(c => (c.X, c.Y)), Is.EquivalentTo(source.Select(c => (39 - c.X, 31 - c.Y))));
        foreach (var c in source)
            Assert.That(_session.Facade.GetTileTexture(39 - c.X, 31 - c.Y), Is.EqualTo(_session.Facade.GetTileTexture(c.X, c.Y)));
        await Run("map.save", new { });
        _session.Dispose(); _session = await MapSessionManager.OpenAsync(_root, "Test");
        var before = await _session.CaptureSnapshotAsync();
        await Apply(new { upsert = new[] { Source("Rock_Yucatan01") } });
        Assert.That(_session.DesignEntities["mirror"].Textures!.Any(c => _session.Facade.GetTileTexture(c.X, c.Y) == "Rock_Yucatan01"), Is.True);
        await Run("history.undo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Run("history.redo", new { });
        await Apply(new { remove = new[] { "source", "mirror" } });
        Assert.That(Tiles(), Is.EqualTo(original));
    }

    [Test] public async Task DerivedTextureRejectsSharedHaloAndProtectedTarget()
    {
        var derived = new { id = "mirror", kind = "derived", parameters = new { sourceEntityId = "source", transform = "rotate180" } };
        var source = new { id = "source", kind = "texture", parameters = new {
            region = new { x = 13, y = 10, width = 3, height = 4 }, texture = "Dirt_Yucatan02" } };
        var before = await _session.CaptureSnapshotAsync();
        var failed = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new object[] { source, derived } } });
        Assert.That(failed.Error?.Code, Is.EqualTo("SYMMETRY_OVERLAP"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Run("protections.add", new { id = "target", region = new { x = 20, y = 12, width = 4, height = 4 }, layers = new[] { "textures" } });
        before = await _session.CaptureSnapshotAsync();
        failed = await Run("design.prepare", new { baseRevision = _session.Revision, patch = new { upsert = new object[] { Paint("source", "Dirt_Yucatan02"), derived } } });
        Assert.That(failed.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test] public async Task TwoTextureRotationsReturnToSourceAndUnwindInDependencyOrder()
    {
        var original = Tiles();
        var mirror = new { id = "mirror", kind = "derived", parameters = new { sourceEntityId = "source", transform = "rotate180" } };
        var twice = new { id = "twice", kind = "derived", parameters = new { sourceEntityId = "mirror", transform = "rotate180" } };
        await Apply(new { upsert = new object[] { twice, mirror, Paint("source", "Dirt_Yucatan02") } });
        Assert.That(_session.DesignEntities["twice"].Textures!.Select(c => (c.X, c.Y)),
            Is.EquivalentTo(_session.DesignEntities["source"].Textures!.Select(c => (c.X, c.Y))));
        await Apply(new { upsert = new[] { Paint("source", "Rock_Yucatan01") } });
        Assert.That(_session.DesignEntities["twice"].Textures!.Any(c => _session.Facade.GetTileTexture(c.X, c.Y) == "Rock_Yucatan01"), Is.True);
        await Apply(new { remove = new[] { "source", "mirror", "twice" } });
        Assert.That(Tiles(), Is.EqualTo(original));
    }
}
