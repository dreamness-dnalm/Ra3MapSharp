using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class WaypointTypeTests
{
    [TestCase(WaypointType.Normal, 0, "wp_Normal")]
    [TestCase(WaypointType.Portal, 1, "wp_Portal")]
    [TestCase(WaypointType.WalkPortal, 2, "wp_WalkPortal")]
    [TestCase(WaypointType.ClimbPortal, 3, "wp_ClimbPortal")]
    [TestCase(WaypointType.PreClimbPortal, 4, "wp_PreClimbPortal")]
    [TestCase(WaypointType.Beacon, 5, "wp_Beacon")]
    [TestCase(WaypointType.Spline, 6, "wp_Spline")]
    [TestCase(WaypointType.FakePathfindPortal, 7, "wp_FakePathfindPortal")]
    [TestCase(WaypointType.MineshaftPortal, 8, "wp_MineshaftPortal")]
    public void KnownType_ShouldMatchRawValueAndConventionalName(
        WaypointType type,
        int rawValue,
        string waypointName)
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)type, Is.EqualTo(rawValue));
            Assert.That(type.ToWaypointName(), Is.EqualTo(waypointName));
            Assert.That(WaypointTypeExtensions.TryParseWaypointName(waypointName, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(type));
        });
    }

    [TestCase("Player_1_Start")]
    [TestCase("wp_Unknown")]
    [TestCase("wp_1")]
    [TestCase("WP_Portal")]
    [TestCase("")]
    [TestCase(null)]
    public void NonConventionalName_ShouldNotParse(string? waypointName)
    {
        Assert.Multiple(() =>
        {
            Assert.That(WaypointTypeExtensions.TryParseWaypointName(waypointName, out var parsed), Is.False);
            Assert.That(parsed, Is.EqualTo(default(WaypointType)));
        });
    }

    [Test]
    public void TypedProperty_ShouldKeepLegacyRawPropertyAndPersist()
    {
        var context = new MapContext();
        var source = ObjectAsset.OfWaypoint(
            new Vec3D(10, 20, 0),
            1,
            "wp_PreClimbPortal",
            context);
        var wrapper = new WaypointWrap(source);

        Assert.Multiple(() =>
        {
            Assert.That(source.WaypointType, Is.EqualTo(WaypointType.Normal));
            Assert.That(wrapper.WaypointType, Is.EqualTo(0));
            Assert.That(wrapper.WaypointTypeEnum, Is.EqualTo(WaypointType.Normal));
        });

        wrapper.WaypointTypeEnum = WaypointType.PreClimbPortal;

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (ObjectAsset)AssetParser.FromBinaryReader(reader, context);
        var parsedWrapper = new WaypointWrap(parsed);

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Properties.GetProperty<int>("waypointType"), Is.EqualTo(4));
            Assert.That(parsed.WaypointType, Is.EqualTo(WaypointType.PreClimbPortal));
            Assert.That(parsedWrapper.WaypointType, Is.EqualTo(4));
            Assert.That(parsedWrapper.WaypointTypeEnum, Is.EqualTo(WaypointType.PreClimbPortal));
        });

        parsedWrapper.WaypointType = 8;
        Assert.That(parsedWrapper.WaypointTypeEnum, Is.EqualTo(WaypointType.MineshaftPortal));
    }
}
