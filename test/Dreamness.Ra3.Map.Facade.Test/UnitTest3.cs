using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest3
{
    [Test]
    public void DeleteObj()
    {
        var mapName = "the_train_0_1_dev";
        
        var map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);

        var unitObjectWraps = map.GetUnitObjects();

        // foreach (var o in unitObjectWraps)
        // {
        //     map.Remove(o);
        // }

        // map.AddUnitObject("AlliedPowerPlant", 200, 200);
        
        map.Remove(map.GetPlayer("Player_1"));
        
        map.SaveAs(Ra3PathUtil.RA3MapFolder, mapName + "_delobj6");
    }
}