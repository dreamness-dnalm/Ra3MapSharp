using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

namespace Dreamness.Ra3.Map.Transform.Test.Commands;

public class WaypointSymmetryTests
{
    [Test]
    public void Symmetry_ShouldKeepAllWaypointsWhenSourceNamesAreDuplicated()
    {
        var map = Ra3MapFacade.NewMap(20, 20, 0, 0);
        map.AddWaypoint("SharedWaypoint", 20, 40);
        map.AddWaypoint("SharedWaypoint", 30, 60);

        var transformed = SymmetryStrategy.Of(3, map, templateAreaIndex: 1).Transform();
        var waypoints = transformed.GetWaypoints();

        Assert.Multiple(() =>
        {
            Assert.That(waypoints, Has.Count.EqualTo(4));
            Assert.That(
                waypoints.Count(waypoint => waypoint.WaypointName == "SharedWaypoint_clone_1"),
                Is.EqualTo(2));
            Assert.That(
                waypoints.Count(waypoint => waypoint.WaypointName == "SharedWaypoint_clone_2"),
                Is.EqualTo(2));
            Assert.That(waypoints.Select(waypoint => waypoint.WaypointID), Is.Unique);
        });
    }

    [Test]
    public void RemoveWrapper_ShouldRemoveTheExactDuplicateWaypoint()
    {
        var map = Ra3MapFacade.NewMap(20, 20, 0, 0);
        var first = map.AddWaypoint("SharedWaypoint", 20, 40);
        var second = map.AddWaypoint("SharedWaypoint", 30, 60);

        map.Remove(second);

        var remaining = map.GetWaypoints().Single();
        Assert.Multiple(() =>
        {
            Assert.That(remaining.Obj, Is.SameAs(first.Obj));
            Assert.That(remaining.Position.X, Is.EqualTo(20));
            Assert.That(remaining.Position.Y, Is.EqualTo(40));
        });
    }
}
