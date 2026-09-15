using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest9
{
    [Test]
    public void tile()
    {
        var ra3Map = Ra3MapFacade.NewMap(playableWidth:500, playableHeight:500, border:0, initPlayerStartWaypointCnt:0);
        

        for(int x = 100; x < 400; x++)
        {
            for(int y = 100; y < 400; y++)
            {
                ra3Map.SetTerrainHeight(x, y, 150);
            }
        }


        ra3Map.SaveAs(Ra3PathUtil.RA3MapFolder, "u_052");
    }
}