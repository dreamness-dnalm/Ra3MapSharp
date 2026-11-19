using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class HeightEncodingTests
{
    [TestCase(0, 0f)]
    [TestCase(1, 0.0390625f)]
    [TestCase(128, 5f)]
    [TestCase(255, 9.9609375f)]
    [TestCase(256, 10f)]
    [TestCase(5376, 210f)]
    [TestCase(65535, 2559.9609375f)]
    public void KnownHeightCodes_MatchEngineScale(int code, float height)
    {
        Assert.That(StreamExtension.FromSageFloat16((ushort)code), Is.EqualTo(height));
        Assert.That(StreamExtension.ToSageFloat16(height), Is.EqualTo((ushort)code));
    }

    [Test]
    public void EveryHeightCode_RoundTripsExactly()
    {
        for (var code = 0; code <= ushort.MaxValue; code++)
        {
            var height = StreamExtension.FromSageFloat16((ushort)code);
            Assert.That(StreamExtension.ToSageFloat16(height), Is.EqualTo((ushort)code), $"Code {code}");
        }
    }

    [TestCase(0.01953125f, 1)]
    [TestCase(0.05859375f, 2)]
    [TestCase(9.96f, 255)]
    [TestCase(9.99f, 256)]
    [TestCase(123.4f, 3159)]
    public void ArbitraryHeight_RoundsToNearestStep(float height, int expectedCode)
    {
        var code = StreamExtension.ToSageFloat16(height);
        Assert.That(code, Is.EqualTo((ushort)expectedCode));
        Assert.That(Math.Abs(StreamExtension.FromSageFloat16(code) - height),
            Is.LessThanOrEqualTo(StreamExtension.SageFloat16Scale / 2));
    }

    [Test]
    public void ValuesAdjacentToMidpoint_RoundOnTheirOwnSide()
    {
        const float midpoint = 2000.01953125f;
        Assert.That(StreamExtension.ToSageFloat16(MathF.BitDecrement(midpoint)), Is.EqualTo(51200));
        Assert.That(StreamExtension.ToSageFloat16(midpoint), Is.EqualTo(51201));
        Assert.That(StreamExtension.ToSageFloat16(MathF.BitIncrement(midpoint)), Is.EqualTo(51201));
    }

    public static IEnumerable<float> InvalidHeights()
    {
        yield return -1f;
        yield return MathF.BitDecrement(0f);
        yield return MathF.BitIncrement(StreamExtension.SageFloat16MaxValue);
        yield return 2560f;
        yield return float.MaxValue;
        yield return float.NaN;
        yield return float.PositiveInfinity;
        yield return float.NegativeInfinity;
    }

    [TestCaseSource(nameof(InvalidHeights))]
    public void InvalidHeight_IsRejectedBeforeWriting(float height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamExtension.ToSageFloat16(height));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteSageFloat16(height));
        Assert.That(stream.Length, Is.Zero);
        var grid = new ElevationDim2Array(new[,] { { height } });
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ToBytes(new MapContext()));
    }

    [Test]
    public void StreamMethods_WriteLittleEndianAndReadExactlyTwoBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Assert.That(writer.WriteSageFloat16(210f), Is.EqualTo(5376));
        writer.Write((byte)0xAB);
        Assert.That(stream.ToArray(), Is.EqualTo(new byte[] { 0x00, 0x15, 0xAB }));
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        Assert.That(reader.ReadSageFloat16(), Is.EqualTo(210f));
        Assert.That(reader.ReadByte(), Is.EqualTo(0xAB));
    }

    [Test]
    public void HeightAsset_EditSaveReload_PreservesOtherCellsAndUsesCorrectEncoding()
    {
        var context = new MapContext();
        var asset = HeightMapDataAsset.Default(2, 2, 0, context);
        asset.Elevations[0, 0] = 9.9609375f;
        asset.Elevations[1, 0] = 10f;
        asset.Elevations[0, 1] = 5f;
        asset.Elevations[1, 1] = StreamExtension.SageFloat16MaxValue;
        var original = asset.ToBytes(context);
        var loaded = ParseHeightAsset(original, context);
        Assert.That(loaded.Elevations.ToBytes(context),
            Is.EqualTo(new byte[] { 255, 0, 0, 1, 128, 0, 255, 255 }));

        // Force serialization even though all decoded heights are unchanged.
        loaded.MarkModified();
        Assert.That(loaded.ToBytes(context), Is.EqualTo(original));
        loaded.Elevations[1, 0] = 9.99f;
        loaded.Elevations[0, 1] = 123.4f;
        var reloaded = ParseHeightAsset(loaded.ToBytes(context), context);
        Assert.That(reloaded.Elevations[0, 0], Is.EqualTo(9.9609375f));
        Assert.That(reloaded.Elevations[1, 0], Is.EqualTo(10f));
        Assert.That(reloaded.Elevations[0, 1], Is.EqualTo(123.3984375f));
        Assert.That(reloaded.Elevations[1, 1], Is.EqualTo(StreamExtension.SageFloat16MaxValue));
    }

    private static HeightMapDataAsset ParseHeightAsset(byte[] bytes, MapContext context)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        var asset = AssetParser.FromBinaryReader(reader, context);
        Assert.That(asset.Errored, Is.False, asset.ErrorException?.ToString());
        Assert.That(reader.BaseStream.Position, Is.EqualTo(reader.BaseStream.Length));
        return (HeightMapDataAsset)asset;
    }
}
