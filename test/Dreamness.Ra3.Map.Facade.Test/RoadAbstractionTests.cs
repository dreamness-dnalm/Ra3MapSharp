using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.Ra3.Map.Facade.Test;

public class RoadAbstractionTests
{
    [Test]
    public void RoadObjects_ShouldNotAppearAsUnitsAndShouldRoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Ra3MapSharp_Road_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var map = Ra3MapFacade.NewMap(32, 32, 4, 0);
            var road = map.AddRoadObject("YucatanDirtRoad01", 100, 120, angle: 30, roadOption: 194);
            map.AddUnitObject("AlliedPowerPlant", 150, 160);

            Assert.Multiple(() =>
            {
                Assert.That(map.GetRoadObjects(), Has.Count.EqualTo(1));
                Assert.That(map.GetUnitObjects(), Has.Count.EqualTo(1));
                Assert.That(road.Options.RawValue, Is.EqualTo(194));
            });

            var path = Path.Combine(tempDir, "road.map");
            map.SaveAs(path, compress: true);
            var reopened = Ra3MapFacade.Open(path);
            var reopenedRoad = reopened.GetRoadObjects().Single();

            Assert.Multiple(() =>
            {
                Assert.That(reopened.GetUnitObjects(), Has.Count.EqualTo(1));
                Assert.That(reopenedRoad.TypeName, Is.EqualTo("YucatanDirtRoad01"));
                Assert.That(reopenedRoad.Options.RawValue, Is.EqualTo(194));
                Assert.That(reopenedRoad.Angle, Is.EqualTo(30).Within(0.001));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
