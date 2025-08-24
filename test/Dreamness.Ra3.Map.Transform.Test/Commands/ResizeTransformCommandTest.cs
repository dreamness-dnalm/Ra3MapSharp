using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

namespace Dreamness.Ra3.Map.Transform.Test.Commands;

public class ResizeTransformCommandTest
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var ra3MapFacade = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "官方地图_工业区_IndustrialStrength");

        var resizeTransformCommand = new ResizeTransformCommand(ra3MapFacade, 500, 900, -100, -100);
        resizeTransformCommand.Transform();

        var destinationRa3MapFacade = resizeTransformCommand.DestinationRa3MapFacade;
        destinationRa3MapFacade.SaveAs(Ra3PathUtil.RA3MapFolder, "out_resize_2");
    }
}