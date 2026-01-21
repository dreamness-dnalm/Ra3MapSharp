using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.enums;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest8
{
    [Test]
    public void tile()
    {
        var ra3Map = Ra3MapFacade.NewMap(500, 500, 0);
        

        
        for(int x = 100; x < 400; x++)
        {
            for(int y = 100; y < 400; y++)
            {
                ra3Map.SetTileTexture(x, y, TextureEnum.Grass_CapeCod01.ToString());
            }
        }
        
        ra3Map.AutoDetectBlendsEntireMap();
        
        
        ra3Map.SaveAs(Ra3PathUtil.RA3MapFolder, "u_05", false);
    }

    [Test]
    public void tile_info()
    {
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "官方地图_雪犁_SnowPlow");
        

        
        for(int x = 0; x < ra3Map.MapWidth; x++)
        {
            for(int y = 0; y < ra3Map.MapHeight; y++)
            {
                Console.WriteLine("pos: ({0}, {1})", x, y);
                ra3Map.GetBlendDetailInfo(x, y);
            }
        }
        
        // ra3Map.AutoDetectBlendsEntireMap();
        
        
        // ra3Map.SaveAs(Ra3PathUtil.RA3MapFolder, "u_05", false);
    }
}