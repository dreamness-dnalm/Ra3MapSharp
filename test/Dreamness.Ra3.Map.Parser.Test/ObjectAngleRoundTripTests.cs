using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;
namespace Dreamness.Ra3.Map.Parser.Test;
public class ObjectAngleRoundTripTests
{
    [TestCase(1.4145136f)]
    [TestCase(-1.4145136f)]
    [TestCase(7.123456f)]
    public void ParsedAngle_PreservesRawRadiansUntilEdited(float radians)
    {
        var context=new MapContext();
        var seed=ObjectAsset.OfObj("angle-test","Tree",new Vec3D(10,20,30),0,"","/team",context);
        var bytes=seed.ToBytes(context);
        // Asset header is int id + short version + int data length; XYZ are three floats.
        BitConverter.GetBytes(radians).CopyTo(bytes,22);
        using var stream=new MemoryStream(bytes);using var reader=new BinaryReader(stream);
        var parsed=(ObjectAsset)AssetParser.FromBinaryReader(reader,context);
        parsed.Angle=parsed.Angle;parsed.TypeName="ChangedTree";
        Assert.That(BitConverter.ToSingle(parsed.ToBytes(context),22),Is.EqualTo(radians));
        var clone=parsed.Clone(context);
        Assert.That(BitConverter.ToSingle(clone.ToBytes(context),22),Is.EqualTo(radians));
        clone.Angle=90;
        Assert.That(BitConverter.ToSingle(clone.ToBytes(context),22),Is.EqualTo((float)(90*Math.PI/180f)));
        Assert.That(BitConverter.ToSingle(parsed.ToBytes(context),22),Is.EqualTo(radians));
    }
}
