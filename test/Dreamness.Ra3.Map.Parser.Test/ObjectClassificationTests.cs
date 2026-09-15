using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
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

        // Even malformed waypoint data carrying a road option remains a waypoint.
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
}
