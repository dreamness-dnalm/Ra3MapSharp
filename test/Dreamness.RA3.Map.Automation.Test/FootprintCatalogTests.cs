using System.Text.Json;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class FootprintCatalogTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-footprints-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void Cleanup() => Directory.Delete(_root, true);

    private static FootprintCatalog Build()
    {
        var catalog = new FootprintCatalog
        {
            BuiltAtUtc = DateTimeOffset.UnixEpoch,
            SourceAlbumHash = "sha256:album",
            Failures = { new FootprintFailure("BB_POOL01", "BLOB_NOT_FOUND", "reads as ground") }
        };
        catalog.Entries["CC_Tree01"] = new FootprintEntry("CC_Tree01", 1.5, 1.25, 48, 40, 32,
            FootprintCatalog.SourceRendered, "sha256:map", "sha256:image");
        catalog.Entries["AM_WALL01"] = new FootprintEntry("AM_WALL01", 7, 0.5, 224, 16, 32,
            FootprintCatalog.SourceRendered, "sha256:map", "sha256:image");
        catalog.CatalogHash = catalog.ComputeHash();
        return catalog;
    }

    [Test]
    public void RoundTripsAndHashMovesWithContent()
    {
        var path = Path.Combine(_root, "footprints.json");
        var catalog = Build();
        catalog.Save(path);
        var loaded = FootprintCatalog.Load(path);

        Assert.That(loaded.Entries["CC_Tree01"].WidthCells, Is.EqualTo(1.5));
        Assert.That(loaded.Entries["CC_Tree01"].Source, Is.EqualTo(FootprintCatalog.SourceRendered));
        Assert.That(loaded.CatalogHash, Is.EqualTo(catalog.CatalogHash));
        Assert.That(loaded.Failures, Has.Count.EqualTo(1), "a failed measurement is part of the data");
        loaded.Entries.Remove("AM_WALL01");
        Assert.That(loaded.ComputeHash(), Is.Not.EqualTo(catalog.CatalogHash));
    }

    [Test]
    public void MeasuredEntryBecomesACentredBodyBox()
    {
        var profile = Build().Entries["CC_Tree01"].ToProfile();
        Assert.That(profile.TypeName, Is.EqualTo("CC_Tree01"));
        Assert.That(profile.Boxes, Has.Length.EqualTo(1));
        Assert.That(profile.Boxes[0].Label, Is.EqualTo("body"));
        Assert.That(profile.Boxes[0].WidthCells, Is.EqualTo(1.5));
        Assert.That(profile.Boxes[0].DepthCells, Is.EqualTo(1.25));
        Assert.That(profile.Boxes[0].BlocksMovement, Is.True);
        // A profile the geometric commands accept, which is the point of the conversion.
        Assert.That(new Geometry.ObjectFootprintSet(new[] { profile }).Contains("CC_Tree01"), Is.True);
    }

    [Test]
    public void OverridesWinOverMeasurementsButKeepThePixelDetail()
    {
        var catalogPath = Path.Combine(_root, "footprints.json");
        Build().Save(catalogPath);
        var overridesPath = Path.Combine(_root, "overrides.json");
        var overrides = new FootprintOverrides();
        overrides.Entries["CC_Tree01"] = new FootprintOverride(2, 2);
        File.WriteAllBytes(overridesPath, JsonSerializer.SerializeToUtf8Bytes(overrides));

        var loaded = FootprintCatalog.Load(catalogPath, overridesPath);
        var entry = loaded.Entries["CC_Tree01"];
        Assert.That(entry.Source, Is.EqualTo(FootprintCatalog.SourceDeclared));
        Assert.That(entry.WidthCells, Is.EqualTo(2));
        Assert.That(entry.WidthPixels, Is.EqualTo(48), "the measurement it replaced stays readable");
        Assert.That(loaded.Entries["AM_WALL01"].Source, Is.EqualTo(FootprintCatalog.SourceRendered));
    }

    [Test]
    public void TryProfilesRefusesAPartialSet()
    {
        var catalog = Build();
        Assert.That(catalog.TryProfiles(new[] { "CC_Tree01" }, out var one), Is.True);
        Assert.That(one, Has.Length.EqualTo(1));
        Assert.That(catalog.TryProfiles(new[] { "CC_Tree01", "NOT_MEASURED" }, out var none), Is.False);
        Assert.That(none, Is.Empty, "a partial set must not be handed to a caller expecting all of them");
    }

    [Test]
    public void ProfilesThrowsForAnUnmeasuredType()
    {
        var error = Assert.Throws<AutomationException>(() => Build().Profiles(new[] { "NOT_MEASURED" }));
        Assert.That(error!.Code, Is.EqualTo("FOOTPRINT_MISSING"));
    }

    [Test]
    public void ListFiltersByNameSourceAndSize()
    {
        var catalog = Build();
        Assert.That(Total(catalog.List(new FootprintListQuery())), Is.EqualTo(2));
        Assert.That(Total(catalog.List(new FootprintListQuery { Query = "Wall" })), Is.EqualTo(1));
        Assert.That(Total(catalog.List(new FootprintListQuery { MinCells = 5 })), Is.EqualTo(1));
        Assert.That(Total(catalog.List(new FootprintListQuery { MaxCells = 1 })), Is.EqualTo(1));
        Assert.That(Total(catalog.List(new FootprintListQuery { Source = FootprintCatalog.SourceDeclared })), Is.Zero);
        Assert.Throws<AutomationException>(() => catalog.List(new FootprintListQuery { Limit = 0 }));
    }

    private static int Total(object page) =>
        ((JsonElement)JsonSerializer.SerializeToElement(page, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
        .GetProperty("total").GetInt32();

    [Test]
    public void SetOverrideValidatesAndMarksTheSource()
    {
        var catalog = Build();
        Assert.Throws<AutomationException>(() => catalog.SetOverride("CC_Tree01", 0, 1));
        Assert.Throws<AutomationException>(() => catalog.SetOverride("", 1, 1));
        Assert.Throws<AutomationException>(() => catalog.SetOverride("CC_Tree01", double.NaN, 1));
        catalog.SetOverride("CC_Tree01", 2.5, 3);
        Assert.That(catalog.Entries["CC_Tree01"].Source, Is.EqualTo(FootprintCatalog.SourceDeclared));
        Assert.That(catalog.Entries["CC_Tree01"].WidthCells, Is.EqualTo(2.5));
    }
}

/// <summary>
/// The catalogue is meant to remove the per-call "footprints" argument, so these pin the
/// handover and its refusal to guess.
/// </summary>
public class ScatterFootprintResolutionTests
{
    private string _root = null!;
    private MapSession _session = null!;

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-scatter-fp-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }

    [TearDown]
    public void Cleanup()
    {
        _session.Dispose();
        Directory.Delete(_root, true);
    }

    private static FootprintCatalog Measured(params string[] typeNames)
    {
        var catalog = new FootprintCatalog { BuiltAtUtc = DateTimeOffset.UnixEpoch };
        foreach (var typeName in typeNames)
            catalog.Entries[typeName] = new FootprintEntry(typeName, 2, 2, 64, 64, 32,
                FootprintCatalog.SourceRendered, "sha256:map", "sha256:image");
        return catalog;
    }

    private async Task<JsonElement> Scatter(FootprintCatalog? footprints, string[] typeNames)
    {
        var executor = new CommandExecutor(CommandRegistry.CreateDefault(null, footprints));
        var result = await executor.ExecuteAsync(new CommandRequest
        {
            SessionId = _session.SessionId,
            ExpectedRevision = _session.Revision,
            Command = "objects.scatter",
            Arguments = JsonSerializer.SerializeToElement(new
            {
                region = new { x = 2, y = 2, width = 24, height = 16 },
                profile = new { waterLevel = 0 },
                seed = 7,
                count = 4,
                minDistanceCells = 1,
                typeNames
            })
        });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return JsonSerializer.SerializeToElement(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Test]
    public async Task MeasuredFootprintsAreUsedWithoutACallArgument()
    {
        var data = await Scatter(Measured("CC_Tree01"), new[] { "CC_Tree01" });
        Assert.That(data.GetProperty("footprintSource").GetString(), Is.EqualTo("catalog"));
        Assert.That(data.GetProperty("spacingModel").GetString(), Does.Contain("oriented-rectangles"));
    }

    [Test]
    public async Task AnIncompleteCatalogueLeavesOverlapCheckingOffAsBefore()
    {
        var data = await Scatter(Measured("AM_WALL01"), new[] { "CC_Tree01" });
        Assert.That(data.GetProperty("footprintSource").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(data.GetProperty("spacingModel").GetString(), Is.EqualTo("object-centers"));
        Assert.That(data.GetProperty("notEvaluated").EnumerateArray()
            .Select(e => e.GetString()), Does.Contain("objectFootprints"));
    }

    [Test]
    public async Task WithoutACatalogueNothingChanges()
    {
        var data = await Scatter(null, new[] { "CC_Tree01" });
        Assert.That(data.GetProperty("footprintSource").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(data.GetProperty("spacingModel").GetString(), Is.EqualTo("object-centers"));
    }
}
