using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Transform.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var ra3MapFacade = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "trans_苦战无人岛困难版1.120250914_122043");
        
        ra3MapFacade.AddWaypoint("Player_1_Start", 10, 10);
        ra3MapFacade.AddWaypoint("Player_2_Start", 100, 100);
        
        Console.WriteLine(ra3MapFacade);
        
        ra3MapFacade.SaveAs(Ra3PathUtil.RA3MapFolder, "trans_out_苦战无人岛困难版1.120250914_122043");
    }
}