using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest2
{
    [Test]
    public void t()
    {
        
        var m = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "BrainVehicle03");

        var o = m.AddUnitObject("JapanPsychicInhibitor", 2100, 4360);
        o.BelongToTeam = "Player_1/teamPlayer_1";
        o.ObjName = "obj0";
        m.GetUnitObjects()
            .ForEach(o =>
            {
                Console.WriteLine(o.TypeName);
            });
         m.Save();
    }
}