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
        LoadMissionObjectPart();
    }

    /// <summary>
    /// 创建新地图, 地图实际尺寸 = 可游玩区域尺寸 + 2 * 边界尺寸
    /// </summary>
    /// <param name="playableWidth">可游玩区域宽度</param>
    /// <param name="playableHeight">可游玩区域高度</param>
    /// <param name="border">边界</param>
    /// <param name="initPlayerStartWaypointCnt">初始化玩家出生点数量</param>
    /// <param name="defaultTexture">默认纹理</param>
    /// <returns></returns>
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
    
    /// <summary>
    /// 加载地图
    /// </summary>
    /// <param name="parentPath">地图所在父目录, 一般情况下请调用Ra3PathUtil.RA3MapFolder</param>
    /// <param name="mapName">地图名字</param>
    /// <returns></returns>
    public static Ra3MapFacade Open(string parentPath, string mapName)
    {
        return Open(Path.Combine(parentPath, mapName, mapName + ".map"));
    }
    
    /// <summary>
    /// 加载地图
    /// </summary>
    /// <param name="mapFilePath">地图文件路径</param>
    /// <returns></returns>
    public static Ra3MapFacade Open(string mapFilePath)
    {
        return new Ra3MapFacade(Ra3Map.Open(mapFilePath));
    }

    /// <summary>
    /// 保存地图
    /// </summary>
    /// <param name="compress">是否压缩,非特殊情况请保持压缩</param>
    public void Save(bool compress = true)
    {
        ra3Map.Save(compress);
    }

    /// <summary>
    /// 另存为地图
    /// </summary>
    /// <param name="outputPath">输出目录</param>
    /// <param name="mapName">地图名字</param>
    /// <param name="compress">是否压缩,非特殊情况请保持压缩</param>
    public void SaveAs(string outputPath, string mapName, bool compress = true)
    {
        var mapFilePath = Path.Combine(outputPath, mapName, mapName + ".map");
        SaveAs(mapFilePath, compress);
        
    }

    /// <summary>
    /// 另存为地图
    /// </summary>
    /// <param name="mapFilePath">地图文件路径</param>
    /// <param name="compress">是否压缩,非特殊情况请保持压缩</param>
    public void SaveAs(string mapFilePath, bool compress = true)
    {
        ra3Map.SaveAs(mapFilePath, compress);
    }

}