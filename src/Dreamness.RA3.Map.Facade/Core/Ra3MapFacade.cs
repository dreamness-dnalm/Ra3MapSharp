using Dreamness.Ra3.Map.Parser.Core.Map;


namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    public Ra3Map ra3Map;

    private Ra3MapFacade(Ra3Map ra3Map)
    {
        this.ra3Map = ra3Map;
        
        // LOAD Assets
        LoadWorldInfo();
        LoadHeightMapData();
        LoadPlayer();
        LoadTeams();
        LoadObjectsList();
        LoadBlendTileData();
    }

    public static Ra3MapFacade NewMap(int playableWidth, int playableHeight,
        int border,
        int initPlayerStartWaypointCnt = 2,
        string defaultTexture = "Dirt_Yucatan03")
    {
        var map = new Ra3MapFacade(Ra3Map.NewMap(playableWidth, playableHeight, border, defaultTexture));
        
        Random random = new Random();
        
        for (int i = 1; i <= initPlayerStartWaypointCnt; i++)
        {
            map.AddPlayerStartWaypoint(i, 
                random.NextSingle() * playableWidth * 10f,
                random.NextSingle() * playableHeight * 10f);
        }
        
        return map;
    }
    
    public static Ra3MapFacade Open(string parentPath, string mapName)
    {
        return new Ra3MapFacade(Ra3Map.Open(Path.Combine(parentPath, mapName, mapName + ".map")));
    }

    public void Save(bool compress = true)
    {
        ra3Map.Save(compress);
    }

    public void SaveAs(string outputPath, string mapName, bool compress = true)
    {
        var mapFilePath = Path.Combine(outputPath, mapName, mapName + ".map");
        ra3Map.SaveAs(mapFilePath, compress);
    }

}