using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Geometry;

namespace Dreamness.RA3.Map.Automation.Test;

public class TerrainTraversalTests
{
    private static Ra3MapFacade Flat()
    {
        var map = Ra3MapFacade.NewMap(12, 10, 2, 0);
        for (var y = 0; y < map.MapHeight; y++)
        for (var x = 0; x < map.MapWidth; x++)
        { map.SetTerrainHeight(x, y, 260); map.SetPassability(x, y, "Passable"); }
        return map;
    }

    [Test]
    public void FlatMapIsConnectedAndClearanceExcludesBoundary()
    {
        var map = Flat();
        var grid = new TerrainTraversal(map, new TerrainMovementProfile());
        Assert.That(grid.ComponentSizes, Is.EqualTo(new[] { 120 }));
        grid = new TerrainTraversal(map, new TerrainMovementProfile { ClearanceCells = 1 });
        Assert.That(grid.ComponentSizes, Is.EqualTo(new[] { 80 }));
        Assert.That(grid.ComponentAt(0, 5), Is.Zero);
    }

    [Test]
    public void CliffSeparatesSidesAndUsesDegrees()
    {
        var map = Flat();
        for (var y = 0; y < 10; y++)
        for (var x = 6; x < 12; x++) map.SetTerrainHeight(x + 2, y + 2, 270);
        var grid = new TerrainTraversal(map, new TerrainMovementProfile { MaxSlopeDegrees = 44 });
        Assert.That(grid.MaximumSlopeDegrees, Is.EqualTo(45).Within(.001));
        Assert.That(grid.ComponentSizes.Count, Is.EqualTo(2));
        Assert.That(grid.ComponentAt(2, 3), Is.Not.EqualTo(grid.ComponentAt(9, 3)));
        grid = new TerrainTraversal(map, new TerrainMovementProfile { MaxSlopeDegrees = 45 });
        Assert.That(grid.ComponentSizes.Count, Is.EqualTo(1));
    }

    [Test]
    public void WaterThresholdIsExplicitAndNoDiagonalCornerCutting()
    {
        var map = Flat();
        map.SetTerrainHeight(4, 4, 190);
        var grid = new TerrainTraversal(map, new TerrainMovementProfile { MaxSlopeDegrees = 89, WaterLevel = 200 });
        Assert.That(grid.ComponentAt(2, 2), Is.Zero);
        grid = new TerrainTraversal(map, new TerrainMovementProfile { MaxSlopeDegrees = 89, WaterLevel = 200, MaxWaterDepth = 10 });
        Assert.That(grid.ComponentAt(2, 2), Is.Positive);
        map = Flat();
        map.SetPassability(3, 2, "Impassable");
        map.SetPassability(2, 3, "ImpassableToPlayers");
        grid = new TerrainTraversal(map, new TerrainMovementProfile());
        Assert.That(grid.ComponentAt(0, 0), Is.Not.EqualTo(grid.ComponentAt(1, 1)));
        grid = new TerrainTraversal(map, new TerrainMovementProfile { RespectPassability = false });
        Assert.That(grid.ComponentSizes.Count, Is.EqualTo(1));
    }

    [Test]
    public void ClearanceClosesNarrowPassageAndDoesNotModifyMap()
    {
        var map = Flat();
        for (var y = 0; y < 10; y++) if (y != 5) map.SetPassability(8, y + 2, "Impassable");
        var before = map.ra3Map.Context.ToBytes();
        var point = new TerrainTraversal(map, new TerrainMovementProfile());
        Assert.That(point.ComponentSizes.Count, Is.EqualTo(1));
        var wide = new TerrainTraversal(map, new TerrainMovementProfile { ClearanceCells = 1 });
        Assert.That(wide.ComponentSizes.Count, Is.EqualTo(2));
        Assert.That(map.ra3Map.Context.ToBytes(), Is.EqualTo(before));
    }

    [TestCase(-1)]
    [TestCase(90)]
    [TestCase(float.NaN)]
    public void InvalidSlopeIsRejected(float slope) => Assert.Throws<AutomationException>(() =>
        new TerrainTraversal(Flat(), new TerrainMovementProfile { MaxSlopeDegrees = slope }));
}
