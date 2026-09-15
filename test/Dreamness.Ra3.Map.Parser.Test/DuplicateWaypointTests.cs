using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class DuplicateWaypointTests
{
    [Test]
    public void ObjectsList_ShouldAllowDuplicateWaypointNamesAndAssignDistinctIds()
    {
        var context = new MapContext();
        var objects = ObjectsListAsset.Default(context);

        var first = objects.AddWaypoint("SharedWaypoint", new Vec3D(10, 20, 0), context);
        var second = objects.AddWaypoint("SharedWaypoint", new Vec3D(30, 40, 0), context);

        Assert.Multiple(() =>
        {
            Assert.That(objects.GetWaypointObjects().Count(), Is.EqualTo(2));
            Assert.That(first.Properties.GetProperty<string>("waypointName"), Is.EqualTo("SharedWaypoint"));
            Assert.That(second.Properties.GetProperty<string>("waypointName"), Is.EqualTo("SharedWaypoint"));
            Assert.That(first.Properties.GetProperty<int>("waypointID"), Is.EqualTo(0));
            Assert.That(second.Properties.GetProperty<int>("waypointID"), Is.EqualTo(1));
        });

        using var stream = new MemoryStream(objects.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var reopened = (ObjectsListAsset)AssetParser.FromBinaryReader(reader, context);

        Assert.That(
            reopened.GetWaypointObjects().Select(asset => asset.UniqueId),
            Is.EqualTo(new[] { "SharedWaypoint", "SharedWaypoint" }));
    }
}
