using System.Text.Json;
using Dreamness.RA3.Map.Agent;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using NUnit.Framework;

namespace Dreamness.RA3.Map.Agent.Test;

public class CatalogTests
{
    [Test]
    public async Task SearchAndPlacementUseSameImmutableCatalogueAndRejectUnknownOrStaleNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "ObjectCategory.json");
            await File.WriteAllTextAsync(path, "[{\"englishName\":\"Trees\",\"chineseName\":\"树\",\"subObjects\":[\"CC_Tree01\",\"CC_Tree01\"]}]");
            var catalog = ObjectCatalog.Load(path);
            Assert.That(catalog.Count, Is.EqualTo(1));
            await using var runtime = new AgentRuntime(catalog: catalog);
            async Task<CommandResult> Call(string command, object args, int? revision = null) => await runtime.ExecuteAsync(new CommandRequest
            { Command = command, SessionId = "$current", ExpectedRevision = revision, Arguments = JsonSerializer.SerializeToElement(args) });
            var search = await Call("assets.objects", new { query = "树" });
            Assert.That((await Call("assets.objects", new { limit = 0 })).Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
            var data = JsonSerializer.SerializeToElement(search.Data, AgentJson.Options);
            Assert.That(data.GetProperty("items")[0].GetProperty("typeName").GetString(), Is.EqualTo("CC_Tree01"));
            Assert.That((await Call("map.create", new { parentPath = root, mapName = "Test", playableWidth = 16, playableHeight = 16 })).Status, Is.EqualTo("succeeded"));
            Assert.That((await Call("objects.place", new { typeName = "MadeUpTree", x = 1, y = 1 }, 0)).Error?.Code, Is.EqualTo("ASSET_NOT_FOUND"));
            Assert.That((await Call("objects.scatter", new { typeNames = new[] { "MadeUpTree" }, seed = 1, count = 2,
                region = new { x = 1, y = 1, width = 8, height = 8 }, profile = new { waterLevel = 200 } }, 0)).Error?.Code, Is.EqualTo("ASSET_NOT_FOUND"));
            Assert.That((await Call("objects.place", new { typeName = "CC_Tree01", catalogHash = "stale", x = 1, y = 1 }, 0)).Error?.Code, Is.EqualTo("CATALOG_CONFLICT"));
            var placed = await Call("objects.place", new { typeName = "CC_Tree01", catalogHash = catalog.ContentHash, x = 1, y = 1 }, 0);
            Assert.That(placed.Status, Is.EqualTo("succeeded"), placed.Error?.Message);
            Assert.That(JsonSerializer.SerializeToElement(placed.Data, AgentJson.Options).GetProperty("assetValidation").GetString(), Is.EqualTo("editor-declared"));
            await File.WriteAllTextAsync(path, "[]");
            Assert.That(catalog.Count, Is.EqualTo(1), "A running host uses a fixed catalogue snapshot.");
            Assert.That(ObjectCatalog.Load(path).ContentHash, Is.Not.EqualTo(catalog.ContentHash));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public void TranslationSupplementAddsResourceNamesAndParticipatesInIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-catalog-extra-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var category = Path.Combine(root, "ObjectCategory.json");
            var translation = Path.Combine(root, "ObjWndTrans.json");
            File.WriteAllText(category, "[{\"subObjects\":[\"CC_Tree01\"]}]");
            File.WriteAllText(translation, "{\"OreNode\":\"矿脉\"}");
            var simple = ObjectCatalog.Load(category);
            var full = ObjectCatalog.Load(category, translation);
            Assert.That(simple.Contains("OreNode"), Is.False);
            Assert.That(full.Contains("OreNode"), Is.True);
            Assert.That(full.Sources.Count, Is.EqualTo(2));
            Assert.That(full.ContentHash, Is.Not.EqualTo(simple.ContentHash));
            var search = JsonSerializer.SerializeToElement(full.Search("矿脉"), AgentJson.Options);
            Assert.That(search.GetProperty("items")[0].GetProperty("typeName").GetString(), Is.EqualTo("OreNode"));
            File.WriteAllText(translation, "{\"OreNode\":\"矿\",\"OilDerrick\":\"油井\"}");
            Assert.That(full.Contains("OilDerrick"), Is.False);
            Assert.That(ObjectCatalog.Load(category, translation).ContentHash, Is.Not.EqualTo(full.ContentHash));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public async Task MissingCatalogReturnsStructuredError()
    {
        await using var runtime = new AgentRuntime();
        var result = await runtime.ExecuteAsync(new CommandRequest { Command = "assets.objects" });
        Assert.That(result.Error?.Code, Is.EqualTo("CATALOG_UNAVAILABLE"));
    }

    [Test]
    public async Task MissingAssetCatalogReturnsStructuredError()
    {
        await using var runtime = new AgentRuntime();
        Assert.That((await runtime.ExecuteAsync(new CommandRequest { Command = "assets.catalog_info" })).Error?.Code,
            Is.EqualTo("CATALOG_UNAVAILABLE"));
        Assert.That((await runtime.ExecuteAsync(new CommandRequest { Command = "assets.search" })).Error?.Code,
            Is.EqualTo("CATALOG_UNAVAILABLE"));
    }

    [Test]
    public async Task AssetCatalogCommandsServeInfoAndSearch()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var categoryPath = Path.Combine(root, "ObjectCategory.json");
            await File.WriteAllTextAsync(categoryPath,
                "[{\"englishName\":\"Trees\",\"chineseName\":\"树草\",\"subObjects\":[\"CC_Tree01\"]}]");
            var screenshots = Path.Combine(root, "objectScreenShot");
            Directory.CreateDirectory(screenshots);
            await File.WriteAllBytesAsync(Path.Combine(screenshots, "CC_Tree01.jpg"), new byte[] { 1, 2, 3 });
            var assets = AssetCatalog.Build(ObjectCatalog.Load(categoryPath),
                ThumbnailIndex.ReadCategories(categoryPath), ThumbnailIndex.Scan(screenshots), DateTimeOffset.UnixEpoch);

            await using var runtime = new AgentRuntime(assets: assets);
            async Task<JsonElement> Call(string command, object args) => JsonSerializer.SerializeToElement(
                (await runtime.ExecuteAsync(new CommandRequest
                { Command = command, Arguments = JsonSerializer.SerializeToElement(args) })).Data, AgentJson.Options);

            var info = await Call("assets.catalog_info", new { });
            Assert.That(info.GetProperty("catalogHash").GetString(), Is.EqualTo(assets.CatalogHash));
            Assert.That(info.GetProperty("counts").GetProperty("objects").GetInt32(), Is.EqualTo(1));
            Assert.That(info.GetProperty("counts").GetProperty("textures").GetInt32(), Is.EqualTo(406));
            Assert.That(info.GetProperty("thumbnails").GetProperty("objectsWithScreenshot").GetInt32(), Is.EqualTo(1));

            var search = await Call("assets.search", new { surface = "Transition" });
            Assert.That(search.GetProperty("kind").GetString(), Is.EqualTo("texture"),
                "a surface filter implies textures and must not drag every object in");
            Assert.That(search.GetProperty("objectMatches").GetInt32(), Is.Zero);
            Assert.That(search.GetProperty("total").GetInt32(), Is.GreaterThan(0));

            var objects = await Call("assets.search", new { kind = "object", category = "树草" });
            Assert.That(objects.GetProperty("total").GetInt32(), Is.EqualTo(1));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public void WithoutBomDropsOnlyALeadingMarker()
    {
        Assert.That(AgentJson.WithoutBom("{\"a\":1}"), Is.EqualTo("{\"a\":1}"));
        Assert.That(AgentJson.WithoutBom("\uFEFF{\"a\":1}"), Is.EqualTo("{\"a\":1}"));
        Assert.That(AgentJson.WithoutBom(""), Is.EqualTo(""));
        Assert.That(AgentJson.WithoutBom("\uFEFF"), Is.EqualTo(""));
    }
}
