using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

namespace Dreamness.Ra3.Map.Transform.Test.Commands;

public class RoadTransformTests
{
    [Test]
    public void Rotate_ShouldKeepRoadClassificationAndRawOptions()
    {
        var map = Ra3MapFacade.NewMap(20, 30, 0, 0);
        map.AddRoadObject("YucatanDirtRoad01", 50, 80, angle: 30, roadOption: 196);

        var command = new RotateTransformCommand(map, 90);
        command.Transform();

        var road = command.DestinationRa3MapFacade.GetRoadObjects().Single();
        Assert.Multiple(() =>
        {
            Assert.That(command.DestinationRa3MapFacade.GetUnitObjects(), Is.Empty);
            Assert.That(road.Options.RawValue, Is.EqualTo(196));
            Assert.That(road.Angle, Is.EqualTo(300).Within(0.001));
        });
    }
}
