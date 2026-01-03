using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest4
{
    [Test]
    public void FindError()
    {
        var mapName = "A-00";

        var ra3MapFacade = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);

        var contextWorldInfoAsset = ra3MapFacade.ra3Map.Context.WorldInfoAsset;

        // var terrainTextureStrings = contextWorldInfoAsset.Properties.GetProperty<string>("terrainTextureStrings");
        //
        // Console.WriteLine(terrainTextureStrings.Split(';').Length);
        // Console.WriteLine(terrainTextureStrings);

        // Console.WriteLine(ra3MapFacade);
        
        
        HashSet<string> textureSet = new HashSet<string>();
        for (int x = 0; x < ra3MapFacade.MapWidth; x++)
        {
            for(int y = 0; y < ra3MapFacade.MapHeight; y++)
            {
                var texture = ra3MapFacade.GetTileTexture(x, y);
                textureSet.Add(texture);
            }
        }
        Console.WriteLine("Used texture cnt: " + textureSet.Count);
    }
}