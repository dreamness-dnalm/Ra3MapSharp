using System.Text.Json;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

/// <summary>
/// Bands vary their material per position. Selecting per axis-aligned block made two earlier
/// attempts look generated -- any block size shows as a grid of squares -- so the choice is now a
/// smooth noise field. These pin the property that matters: neighbouring cells agree, which is
/// what makes regions contiguous instead of per-cell noise.
/// </summary>
public class TextureBandTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-bands-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 128, 128, 4);
    }

    [TearDown]
    public void Cleanup()
    {
        _session.Dispose();
        Directory.Delete(_root, true);
    }

    private async Task<CommandResult> Run(string command, object args) => await _executor.ExecuteAsync(
        new CommandRequest
        {
            SessionId = _session.SessionId,
            ExpectedRevision = _session.Revision,
            Command = command,
            Arguments = JsonSerializer.SerializeToElement(args)
        });

    private async Task Raise(int height)
    {
        var raised = await Run("terrain.set_height", new { region = new { x = 0, y = 0, width = 128, height = 128 }, height });
        Assert.That(raised.Status, Is.EqualTo("succeeded"), raised.Error?.Message);
    }

    [Test]
    public async Task BandsPaintByElevationAndReportTheSplit()
    {
        await Raise(300);
        var result = await Run("texture.paint_by_height", new
        {
            region = new { x = 0, y = 0, width = 128, height = 128 },
            bands = new object[]
            {
                new { maxHeight = 280, texture = "Gravel_Yucatan01" },
                new { maxHeight = 320, textureNames = new[] { "Grass_Yucatan02", "Grass_Yucatan05" } },
                new { maxHeight = 9999, texture = "Rock_Yucatan01" }
            }
        });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var data = JsonSerializer.SerializeToElement(result.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(data.GetProperty("painted").GetInt32(), Is.EqualTo(128 * 128));
        Assert.That(data.GetProperty("minHeight").GetDouble(), Is.EqualTo(300));
        Assert.That(data.GetProperty("model").GetString(), Is.EqualTo("elevation-bands-v1"));
        // Flat terrain at 300 lands entirely in the middle band, so both its variants are used.
        Assert.That(data.GetProperty("byTexture").EnumerateObject().Count(), Is.GreaterThan(1));
    }

    [Test]
    public async Task VariantChoiceProducesContiguousRegionsNotPerCellNoise()
    {
        await Raise(300);
        var result = await Run("texture.paint_by_height", new
        {
            region = new { x = 0, y = 0, width = 128, height = 128 },
            bands = new object[]
            {
                new { maxHeight = 9999, textureNames = new[]
                    { "Grass_Yucatan02", "Grass_Yucatan05", "Grass_Yucatan07", "Dirt_Yucatan03" },
                    variantBlockCells = 20 }
            }
        });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var profile = MapArtProfile.Measure(_session.Facade, null, 30000, CancellationToken.None);
        // Per-cell random choice would give a median patch of 1; a noise field gives real regions.
        Assert.That(profile.Patches.MedianPatchCells, Is.GreaterThan(8),
            "variants must form regions, not single-cell speckle; got " + profile.Patches.MedianPatchCells);
        Assert.That(profile.DistinctTextures, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task BandsMustAscendAndNameRealTextures()
    {
        await Raise(300);
        var descending = await Run("texture.paint_by_height", new
        {
            region = new { x = 0, y = 0, width = 32, height = 32 },
            bands = new object[] { new { maxHeight = 400, texture = "Grass_Yucatan02" }, new { maxHeight = 300, texture = "Rock_Yucatan01" } }
        });
        Assert.That(descending.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"), "bands must ascend");

        var unknown = await Run("texture.paint_by_height", new
        {
            region = new { x = 0, y = 0, width = 32, height = 32 },
            bands = new object[] { new { maxHeight = 400, texture = "Made_Up01" } }
        });
        Assert.That(unknown.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"), "textures are checked against the library");

        var empty = await Run("texture.paint_by_height", new { region = new { x = 0, y = 0, width = 32, height = 32 }, bands = Array.Empty<object>() });
        Assert.That(empty.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
    }
}
