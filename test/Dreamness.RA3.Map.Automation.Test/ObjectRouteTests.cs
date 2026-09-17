using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class ObjectRouteTests
{
    [Test]
    public async Task ObjectWallSplitsTerrainAndReservationRemainsWalkable()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-routes-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var session = await MapSessionManager.CreateAsync(root, "Test", 16, 16, 4);
            var map = session.Facade;
            map.AddUnitObject("OreNode", 80, 80);
            var from = new RoutePoint(2, 8); var to = new RoutePoint(13, 8);
            var terrain = new TerrainTraversal(map, new TerrainMovementProfile());
            Assert.That(terrain.FindRoute(from, to).Steps, Is.EqualTo(11));
            var wall = new FootprintBox { WidthCells = 2, DepthCells = 16 };
            var profiles = new[] { new ObjectFootprint { TypeName = "OreNode", Boxes = new[] { wall } } };
            var blocked = new TerrainTraversal(map, new TerrainMovementProfile(), footprints: profiles);
            Assert.That(blocked.ObjectBlockedCells, Is.EqualTo(32));
            Assert.That(blocked.FindRoute(from, to).Status, Is.EqualTo("disconnected"));
            Assert.That(blocked.FindRoute(new RoutePoint(8, 8), to).Status, Is.EqualTo("blocked-endpoint"));
            wall.DepthCells = 10;
            var detour = new TerrainTraversal(map, new TerrainMovementProfile(), footprints: profiles).FindRoute(from, to, 3);
            Assert.That(detour.Status, Is.EqualTo("found"));
            Assert.That(detour.Steps, Is.GreaterThan(11));
            Assert.That(detour.Cells.Count, Is.EqualTo(3));
            Assert.That(detour.Truncated, Is.True);
            wall.BlocksMovement = false;
            var reservation = new TerrainTraversal(map, new TerrainMovementProfile(), footprints: profiles);
            Assert.That(reservation.FindRoute(from, to).Steps, Is.EqualTo(11));
            Assert.That(reservation.ObjectBlockedCells, Is.Zero);
            map.AddUnitObject("CC_Tree01", 40, 40);
            Assert.That(Assert.Throws<AutomationException>(() => new TerrainTraversal(map, new TerrainMovementProfile(), footprints: profiles))!.Code,
                Is.EqualTo("FOOTPRINT_MISSING"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
