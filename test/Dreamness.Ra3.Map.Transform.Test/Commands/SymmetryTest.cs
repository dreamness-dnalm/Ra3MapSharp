using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

namespace Dreamness.Ra3.Map.Transform.Test.Commands;

public class SymmetryTest
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "NewMap");

        var ra3MapFacade = SymmetryStrategy.Of(3, map, 2).Transform();
        ra3MapFacade.SaveAs(Ra3PathUtil.RA3MapFolder, "sy_out_4");
    }
}