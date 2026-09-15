using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class ObjectClassificationTests
{
    [Test]
    public void ObjectViews_ShouldSeparateRegularObjectsWaypointsAndRoads()
    {
        var context = new MapContext();
        var objects = ObjectsListAsset.Default(context);

        var regular = objects.AddObj(context, "AlliedPowerPlant", new Vec3D(10, 20, 0));
        var waypoint = objects.AddWaypoint("SplitWaypoint", new Vec3D(30, 40, 0), context);
        var road = objects.AddRoad(
            context,
            "YucatanDirtRoad01",
            new Vec3D(50, 60, 0),
            new RoadOptions(194));

        // 即使路径点被误写了 road option，仍应归类为路径点。
        waypoint.RoadOption = 194;

        Assert.Multiple(() =>
        {
            Assert.That(waypoint.IsRoad, Is.False);
            Assert.That(objects.GetRegularObjects().Single(), Is.SameAs(regular));
            Assert.That(objects.GetWaypointObjects().Single(), Is.SameAs(waypoint));
            Assert.That(objects.GetRoadObjects().Single(), Is.SameAs(road));
            Assert.That(objects.MapObjectList, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void RoadObject_ShouldUseDedicatedWrapperAndPreserveRawOptions()
    {
        var context = new MapContext();
        var source = ObjectAsset.OfRoad(
            "YucatanDirtRoad01 0",
            "YucatanDirtRoad01",
            new Vec3D(10, 20, 0),
            45,
            new RoadOptions(196),
            "PlyrNeutral/teamPlyrNeutral",
            context);

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (ObjectAsset)AssetParser.FromBinaryReader(reader, context);
        var wrapped = ObjectWrap.Of(parsed);

        Assert.Multiple(() =>
        {
            Assert.That(parsed.IsRoad, Is.True);
            Assert.That(parsed.IsWaypoint, Is.False);
            Assert.That(parsed.RoadOption, Is.EqualTo(196));
            Assert.That(parsed.Properties.PropertyNames, Does.Not.Contain("objectName"));
            Assert.That(wrapped, Is.TypeOf<RoadObjectWrap>());
            Assert.That(((RoadObjectWrap)wrapped).Options.RawValue, Is.EqualTo(196));
            Assert.That(((RoadObjectWrap)wrapped).Options.ContainsBits(128), Is.True);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ((RoadObjectWrap)wrapped).Options = new RoadOptions(0));
    }

    [Test]
    public void RoadObject_ParsedOptionEdit_ShouldPropagateAndPersist()
    {
        var context = new MapContext();
        var source = ObjectsListAsset.Default(context);
        source.AddRoad(
            context,
            "YucatanDirtRoad01",
            new Vec3D(10, 20, 0),
            new RoadOptions(2));

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (ObjectsListAsset)AssetParser.FromBinaryReader(reader, context);
        var road = (RoadObjectWrap)ObjectWrap.Of(parsed.MapObjectList.Single());

        Assert.That(parsed._modified, Is.False);
        road.Options = new RoadOptions(68);
        Assert.Multiple(() =>
        {
            Assert.That(road.Obj._modified, Is.True);
            Assert.That(parsed.MapObjectList._modified, Is.True);
            Assert.That(parsed._modified, Is.True);
        });

        using var stream2 = new MemoryStream(parsed.ToBytes(context));
        using var reader2 = new BinaryReader(stream2);
        var reopened = (ObjectsListAsset)AssetParser.FromBinaryReader(reader2, context);
        Assert.That(reopened.MapObjectList.Single().RoadOption, Is.EqualTo(68));
    }
}
