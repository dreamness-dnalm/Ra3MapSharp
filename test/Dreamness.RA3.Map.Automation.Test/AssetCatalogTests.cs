using System.Text;
using System.Text.Json;
using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Automation.Test;

public class AssetCatalogTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void Cleanup() => Directory.Delete(_root, true);

    private ObjectCatalog Catalog()
    {
        var categoryPath = Path.Combine(_root, "ObjectCategory.json");
        File.WriteAllText(categoryPath, JsonSerializer.Serialize(new object[]
        {
            new { englishName = "trees", chineseName = "树草", subObjects = new[] { "CC_Tree01", "CC_Tree02" } },
            new { englishName = "walls", chineseName = "墙", subObjects = new[] { "AM_WALL01" } }
        }), new UTF8Encoding(false));
        var translationsPath = Path.Combine(_root, "ObjWndTrans.json");
        File.WriteAllText(translationsPath, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Crate"] = "箱子"
        }), new UTF8Encoding(false));
        return ObjectCatalog.Load(categoryPath, translationsPath);
    }

    private ThumbnailIndex Thumbnails()
    {
        var directory = Path.Combine(_root, "objectScreenShot");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "CC_Tree01.jpg"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(directory, "AlliedAirfield.jpg"), new byte[] { 4, 5, 6, 7 });
        return ThumbnailIndex.Scan(directory);
    }

    private AssetCatalog Build() => AssetCatalog.Build(Catalog(),
        ThumbnailIndex.ReadCategories(Path.Combine(_root, "ObjectCategory.json")), Thumbnails(), DateTimeOffset.UnixEpoch);

    [Test]
    public void SplitDerivesSurfaceThemeAndVariant()
    {
        Assert.That(TextureSemantics.Split("Grass_Yucatan03"),
            Is.EqualTo(("Grass", "Yucatan", 3, TextureSemantics.KindGround)));
        Assert.That(TextureSemantics.Split("Transition_Hawaii01").Item4, Is.EqualTo(TextureSemantics.KindTransition));
        Assert.That(TextureSemantics.Split("Cliff_Iceland02").Item4, Is.EqualTo(TextureSemantics.KindCliff));
        Assert.That(TextureSemantics.Split("Reef_Easter01").Item4, Is.EqualTo(TextureSemantics.KindShore));
        Assert.That(TextureSemantics.Split("SteelDeck04").Item4, Is.EqualTo(TextureSemantics.KindOther));
        Assert.That(TextureSemantics.Split("RA3Grid1").Item4, Is.EqualTo(TextureSemantics.KindOther));
        Assert.That(TextureSemantics.Split("Dock_Romania01").Item4, Is.EqualTo(TextureSemantics.KindStructure));
    }

    [Test]
    public void PaveIsFoldedIntoPavement()
    {
        var assets = TextureSemantics.DescribeAll(new[] { "Pave_Heidelberg01", "Pavement_Heidelberg02" });
        Assert.That(assets.Select(a => a.Surface), Is.EqualTo(new[] { "Pavement", "Pavement" }));
    }

    [Test]
    public void TruncatedThemeIsMergedButACamelCaseNeighbourIsNot()
    {
        // 'Heidel' is a clipped 'Heidelberg' in the shipped library; 'GenevaClockA' is a
        // different place, so merging it would hide the real Geneva textures.
        var assets = TextureSemantics.DescribeAll(new[]
        {
            "Pave_Heidel01", "Pavement_Heidelberg01", "Grass_Geneva01", "Grass_GenevaClockA"
        });
        var themes = assets.ToDictionary(a => a.Name, a => a.Theme, StringComparer.Ordinal);
        Assert.That(themes["Pave_Heidel01"], Is.EqualTo("Heidelberg"));
        Assert.That(themes["Pavement_Heidelberg01"], Is.EqualTo("Heidelberg"));
        Assert.That(themes["Grass_Geneva01"], Is.EqualTo("Geneva"));
        Assert.That(themes["Grass_GenevaClockA"], Is.EqualTo("GenevaClockA"));
    }

    [Test]
    public void BuildCountsObjectsCategoriesAndScreenshotCoverage()
    {
        var catalog = Build();
        Assert.That(catalog.Objects.Select(o => o.TypeName),
            Is.EqualTo(new[] { "AM_WALL01", "CC_Tree01", "CC_Tree02", "Crate" }));
        Assert.That(catalog.Categories.Select(c => c.ChineseName), Is.EquivalentTo(new[] { "树草", "墙" }));
        Assert.That(catalog.Objects.Single(o => o.TypeName == "CC_Tree01").HasEditorScreenshot, Is.True);
        Assert.That(catalog.Objects.Single(o => o.TypeName == "AM_WALL01").HasEditorScreenshot, Is.False);
        // AlliedAirfield.jpg matches no declared object, so it is counted as unmatched.
        Assert.That(catalog.Thumbnails, Is.EqualTo(new ThumbnailCoverage(2, 1, 4, 1)));
        Assert.That(catalog.Textures, Has.Count.EqualTo(406));
    }

    [Test]
    public void CatalogHashIsStableForIdenticalInputAndChangesWithContent()
    {
        var first = Build();
        var second = Build();
        Assert.That(second.CatalogHash, Is.EqualTo(first.CatalogHash));
        Assert.That(first.CatalogHash, Does.StartWith("sha256:"));
        second.Textures.Add(new TextureAsset("Made_Up01", "Made", "Up", 1, TextureSemantics.KindGround, false, false));
        Assert.That(second.ComputeHash(), Is.Not.EqualTo(first.CatalogHash));
    }

    [Test]
    public void SaveAndLoadRoundTripsAndInfoIsSerializable()
    {
        var path = Path.Combine(_root, "catalog.json");
        Build().Save(path);
        var loaded = AssetCatalog.Load(path);
        Assert.That(loaded.Objects, Has.Count.EqualTo(4));
        // Info() feeds assets.catalog_info, so it must survive a real serialization pass.
        var payload = JsonSerializer.Serialize(loaded.Info());
        Assert.That(payload, Does.Contain("catalogHash"));
    }

    [Test]
    public void LoadToleratesABomBecauseEditorsWriteOne()
    {
        var path = Path.Combine(_root, "catalog.json");
        Build().Save(path);
        File.WriteAllBytes(path, new byte[] { 239, 187, 191 }.Concat(File.ReadAllBytes(path)).ToArray());
        Assert.That(AssetCatalog.Load(path).CatalogHash, Is.Not.Empty);
    }

    [Test]
    public void SearchFiltersByKindSurfaceThemeCategoryAndScreenshot()
    {
        var catalog = Build();
        // No explicit kind: a surface filter implies textures, so objects stay out of it.
        var transitions = (JsonElement)JsonSerializer.SerializeToElement(catalog.Search(new AssetSearchQuery { Surface = "Transition", Limit = 1 }));
        Assert.That(transitions.GetProperty("total").GetInt32(), Is.EqualTo(45));
        Assert.That(transitions.GetProperty("kind").GetString(), Is.EqualTo("texture"));

        var trees = (JsonElement)JsonSerializer.SerializeToElement(catalog.Search(new AssetSearchQuery { Kind = "object", Category = "树草" }));
        Assert.That(trees.GetProperty("total").GetInt32(), Is.EqualTo(2));

        var missing = (JsonElement)JsonSerializer.SerializeToElement(catalog.Search(new AssetSearchQuery { Kind = "object", OnlyMissingScreenshot = true }));
        Assert.That(missing.GetProperty("total").GetInt32(), Is.EqualTo(3));

        var named = (JsonElement)JsonSerializer.SerializeToElement(catalog.Search(new AssetSearchQuery { Query = "Tree", Kind = "object" }));
        Assert.That(named.GetProperty("total").GetInt32(), Is.EqualTo(2));

        var byTheme = (JsonElement)JsonSerializer.SerializeToElement(catalog.Search(new AssetSearchQuery { Theme = "Yucatan" }));
        Assert.That(byTheme.GetProperty("total").GetInt32(), Is.GreaterThan(0));
        Assert.That(byTheme.GetProperty("objectMatches").GetInt32(), Is.Zero);
    }

    [Test]
    public void AlbumRoundTripsAndItsHashMovesWithContent()
    {
        var path = Path.Combine(_root, "album.json");
        var album = new AssetAlbum
        {
            BuiltAtUtc = DateTimeOffset.UnixEpoch, GridCells = 64, SpacingCells = 14,
            TileCells = 6, SourceImageEdge = 2048, TileEdge = 128
        };
        album.Batches.Add(new AssetAlbumBatch(0, 1, "sha256:map", "sha256:image", "sha256:renderer", "scene.overview.png"));
        album.Entries["CC_Tree01"] = new AssetAlbumEntry("CC_Tree01", "CC_Tree01.png", 3, "sha256:tile", 6, 6, 0);
        album.AlbumHash = album.ComputeHash();
        album.Save(path);

        var loaded = AssetAlbum.Load(path);
        Assert.That(loaded.Entries["CC_Tree01"].File, Is.EqualTo("CC_Tree01.png"));
        Assert.That(loaded.AlbumHash, Is.EqualTo(album.AlbumHash));
        Assert.That(loaded.RootDirectory, Is.EqualTo(Path.GetDirectoryName(path)));
        Assert.That(loaded.TilePath("CC_Tree01.png"), Is.EqualTo(Path.Combine(Path.GetDirectoryName(path)!, "tiles", "CC_Tree01.png")));

        loaded.Failures.Add(new AssetAlbumFailure("CC_Tree02", "INVALID_ARGUMENT", "nope"));
        Assert.That(loaded.ComputeHash(), Is.Not.EqualTo(album.AlbumHash), "failures are part of the album identity");
    }

    [Test]
    public void AlbumIsOptionalForTheCatalogue()
    {
        var catalog = Build();
        Assert.That(catalog.Album, Is.Null);
        // The host serializes with web defaults (camelCase), so mirror that here rather than the
        // PascalCase a record property keeps under the default options.
        var json = JsonSerializer.SerializeToElement(catalog.Search(new AssetSearchQuery { Kind = "object", Query = "CC_Tree01" }),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var item = json.GetProperty("items")[0];
        Assert.That(item.GetProperty("hasAlbum").GetBoolean(), Is.False);
        Assert.That(item.GetProperty("albumFile").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void SearchRejectsAnUnknownKindAndBadPaging()
    {
        var catalog = Build();
        Assert.Throws<AutomationException>(() => catalog.Search(new AssetSearchQuery { Kind = "grass" }));
        Assert.Throws<AutomationException>(() => catalog.Search(new AssetSearchQuery { Limit = 0 }));
        Assert.Throws<AutomationException>(() => catalog.Search(new AssetSearchQuery { Offset = -1 }));
    }
}
