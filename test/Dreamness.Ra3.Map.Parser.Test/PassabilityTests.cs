using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;

namespace Dreamness.Ra3.Map.Parser.Test;

public class PassabilityTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(16)]
    [TestCase(17)]
    public void BooleanRowsHaveIndependentPadding(int width)
    {
        var values = new bool[width, 3];
        for (var x = 0; x < width; x++) values[x, 0] = true;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        Dreamness.Ra3.Map.Parser.Util.IOUtility.WriteArray(writer, values);
        Assert.That(stream.Length, Is.EqualTo((width + 7) / 8 * 3));
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        Assert.That(Dreamness.Ra3.Map.Parser.Util.IOUtility.ReadArray<bool>(reader, width, 3), Is.EqualTo(values));
    }
    private static (MapContext Context, HeightMapDataAsset Heights, BlendTileDataAsset Tiles) Create()
    {
        var context = new MapContext();
        var heights = HeightMapDataAsset.Default(9, 9, 0, context);
        context.RegisterAsset(heights);
        var tiles = BlendTileDataAsset.Default(9, 9, 0, "Dirt_Yucatan03", context);
        return (context, heights, tiles);
    }

    [Test]
    public void FlatBoundaryDoesNotBecomeBlockedAndFortyFiveDegreesMeansDegrees()
    {
        var (context, heights, tiles) = Create();
        tiles.UpdatePassabilityMap(context);
        for (var y = 0; y < 9; y++)
        for (var x = 0; x < 9; x++) Assert.That(tiles.Passabilities[x, y], Is.EqualTo(BlendTileDataAsset.Passability.Passable));
        heights.Elevations[4, 4] = 222; // 12 / 10 > tan(45 degrees), but < tan(45 radians).
        tiles.UpdatePassabilityMap(context, 45, false);
        Assert.That(tiles.Passabilities[4, 4], Is.EqualTo(BlendTileDataAsset.Passability.Impassable));
        heights.Elevations[4, 4] = 220;
        tiles.UpdatePassabilityMap(context, 45, false);
        Assert.That(tiles.Passabilities[4, 4], Is.EqualTo(BlendTileDataAsset.Passability.Passable));
    }

    [Test]
    public void HaloIsSymmetricAndSpecialFlagsSurviveAndResultsPersist()
    {
        var (context, heights, tiles) = Create();
        heights.Elevations[4, 4] = 300;
        tiles.Passabilities[2, 4] = BlendTileDataAsset.Passability.ExtraPassable;
        tiles.Passabilities[8, 8] = BlendTileDataAsset.Passability.ImpassableToPlayers;
        tiles.Passabilities[8, 7] = BlendTileDataAsset.Passability.ImpassableToAirUnits;
        using var stream = new MemoryStream(tiles.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (BlendTileDataAsset)AssetParser.FromBinaryReader(reader, context);
        parsed.UpdatePassabilityMap(context);
        Assert.That(parsed._modified, Is.True);
        using var next = new MemoryStream(parsed.ToBytes(context));
        using var nextReader = new BinaryReader(next);
        var reopened = (BlendTileDataAsset)AssetParser.FromBinaryReader(nextReader, context);
        for (var y = 0; y < 9; y++)
        for (var x = 0; x < 9; x++)
        {
            var expected = Math.Abs(x - 4) + Math.Abs(y - 4) <= 2
                ? BlendTileDataAsset.Passability.Impassable : BlendTileDataAsset.Passability.Passable;
            if (x == 2 && y == 4) expected = BlendTileDataAsset.Passability.ExtraPassable;
            if (x == 8 && y == 8) expected = BlendTileDataAsset.Passability.ImpassableToPlayers;
            if (x == 8 && y == 7) expected = BlendTileDataAsset.Passability.ImpassableToAirUnits;
            Assert.That(reopened.Passabilities[x, y], Is.EqualTo(expected), $"({x},{y})");
        }
    }

    [Test]
    public void MirroredTerrainProducesMirroredFlags()
    {
        var (context, heights, tiles) = Create();
        var (otherContext, otherHeights, otherTiles) = Create();
        var random = new Random(61);
        for (var y = 0; y < 9; y++)
        for (var x = 0; x < 9; x++)
            heights.Elevations[x, y] = otherHeights.Elevations[8 - x, 8 - y] = random.Next(210, 235);
        tiles.UpdatePassabilityMap(context);
        otherTiles.UpdatePassabilityMap(otherContext);
        for (var y = 0; y < 9; y++)
        for (var x = 0; x < 9; x++) Assert.That(tiles.Passabilities[x, y], Is.EqualTo(otherTiles.Passabilities[8 - x, 8 - y]));
    }
}
