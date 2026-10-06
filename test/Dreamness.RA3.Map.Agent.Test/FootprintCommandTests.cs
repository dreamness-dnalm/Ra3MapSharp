using System.Text.Json;
using Dreamness.RA3.Map.Agent;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using NUnit.Framework;

namespace Dreamness.RA3.Map.Agent.Test;

public class FootprintCommandTests
{
    private static FootprintCatalog Measured()
    {
        var catalog = new FootprintCatalog { BuiltAtUtc = DateTimeOffset.UnixEpoch, SourceAlbumHash = "sha256:album" };
        catalog.Entries["CC_Tree01"] = new FootprintEntry("CC_Tree01", 1.5, 1.25, 48, 40, 32,
            FootprintCatalog.SourceRendered, "sha256:map", "sha256:image");
        catalog.CatalogHash = catalog.ComputeHash();
        return catalog;
    }

    [Test]
    public async Task FootprintCommandsServeMeasuredValuesAndPersistOverrides()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-fp-cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var catalogPath = Path.Combine(root, "footprints.json");
            Measured().Save(catalogPath);
            var overridesPath = Path.Combine(root, "footprints.overrides.json");
            await using var runtime = new AgentRuntime(footprints: FootprintCatalog.Load(catalogPath),
                footprintOverridesPath: overridesPath);
            async Task<CommandResult> Call(string command, object args) => await runtime.ExecuteAsync(
                new CommandRequest { Command = command, Arguments = JsonSerializer.SerializeToElement(args) });

            var get = JsonSerializer.SerializeToElement((await Call("footprints.get", new { typeName = "CC_Tree01" })).Data, AgentJson.Options);
            Assert.That(get.GetProperty("widthCells").GetDouble(), Is.EqualTo(1.5));
            Assert.That(get.GetProperty("source").GetString(), Is.EqualTo(FootprintCatalog.SourceRendered));
            Assert.That(get.GetProperty("batchImageHash").GetString(), Is.EqualTo("sha256:image"));

            Assert.That((await Call("footprints.get", new { typeName = "NOT_MEASURED" })).Error?.Code, Is.EqualTo("FOOTPRINT_MISSING"));
            Assert.That((await Call("footprints.get", new { })).Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));

            var list = JsonSerializer.SerializeToElement((await Call("footprints.list", new { minCells = 1 })).Data, AgentJson.Options);
            Assert.That(list.GetProperty("total").GetInt32(), Is.EqualTo(1));

            var set = await Call("footprints.set", new { typeName = "CC_Tree01", widthCells = 3, depthCells = 2 });
            Assert.That(set.Status, Is.EqualTo("succeeded"), set.Error?.Message);
            Assert.That(File.Exists(overridesPath), Is.True, "an override must survive a rebuild of the measurements");

            var reloaded = FootprintCatalog.Load(catalogPath, overridesPath);
            Assert.That(reloaded.Entries["CC_Tree01"].Source, Is.EqualTo(FootprintCatalog.SourceDeclared));
            Assert.That(reloaded.Entries["CC_Tree01"].WidthCells, Is.EqualTo(3));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public async Task FootprintCommandsWithoutACatalogueSaySo()
    {
        await using var runtime = new AgentRuntime();
        foreach (var command in new[] { "footprints.get", "footprints.list", "footprints.set" })
            Assert.That((await runtime.ExecuteAsync(new CommandRequest { Command = command })).Error?.Code,
                Is.EqualTo("FOOTPRINT_UNAVAILABLE"), command);
    }

    [Test]
    public async Task SearchReportsTheMeasuredSizeAlongsideTheImage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-fp-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var categoryPath = Path.Combine(root, "ObjectCategory.json");
            await File.WriteAllTextAsync(categoryPath,
                "[{\"englishName\":\"Trees\",\"chineseName\":\"树草\",\"subObjects\":[\"CC_Tree01\"]}]");
            var assets = AssetCatalog.Build(ObjectCatalog.Load(categoryPath),
                ThumbnailIndex.ReadCategories(categoryPath), null, DateTimeOffset.UnixEpoch) ;
            assets.Footprints = Measured();

            await using var runtime = new AgentRuntime(assets: assets);
            var result = await runtime.ExecuteAsync(new CommandRequest
            {
                Command = "assets.search",
                Arguments = JsonSerializer.SerializeToElement(new { kind = "object", query = "CC_Tree01" })
            });
            var item = JsonSerializer.SerializeToElement(result.Data, AgentJson.Options)
                .GetProperty("items")[0];
            Assert.That(item.GetProperty("footprintWidthCells").GetDouble(), Is.EqualTo(1.5));
            Assert.That(item.GetProperty("footprintDepthCells").GetDouble(), Is.EqualTo(1.25));
        }
        finally { Directory.Delete(root, true); }
    }
}
