using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class HeightEncodingTests
{
    [Test]
    public void EveryStoredHeightSurvivesDecodeEncodeExactly()
    {
        for (var value = 0; value <= ushort.MaxValue; value++)
        {
            var decoded = StreamExtension.FromSageFloat16((ushort)value);
            var encoded = StreamExtension.ToSageFloat16(decoded);
            if (encoded != value) Assert.Fail($"Height code {value} became {encoded} after decoding to {decoded}.");
        }
    }

    [TestCase(9.99f, 255)]
    [TestCase(10f, 256)]
    [TestCase(2559.99f, 65535)]
    public void FractionalGapsClampInsteadOfWrapping(float value, int expected)
    {
        Assert.That(StreamExtension.ToSageFloat16(value), Is.EqualTo(expected));
    }

    [TestCase(-1f)]
    [TestCase(2560f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidHeightCannotSilentlyWrap(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamExtension.ToSageFloat16(value));
    }
}
