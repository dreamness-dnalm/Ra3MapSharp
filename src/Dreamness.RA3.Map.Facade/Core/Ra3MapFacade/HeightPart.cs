using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{

    private HeightMapDataAsset _heightMapDataAsset;
    
    // ---------  basic map info  ---------

    /// <summary>
    /// 地图宽度（包含边界）
    /// </summary>
    public int MapWidth
    {
        get => _heightMapDataAsset.MapWidth;
    }

    /// <summary>
    /// 地图高度（包含边界）
    /// </summary>
    public int MapHeight
    {
        get => _heightMapDataAsset.MapHeight;
    }

    /// <summary>
    /// 边界宽度
    /// </summary>
    public int MapBorderWidth
    {
        get => _heightMapDataAsset.BorderWidth;
    }

    /// <summary>
    /// 可游玩区域宽度
    /// </summary>
    public int MapPlayableWidth
    {
        get => _heightMapDataAsset.MapPlayableWidth;
    }

    /// <summary>
    /// 可游玩区域高度
    /// </summary>
    public int MapPlayableHeight
    {
        get => _heightMapDataAsset.MapPlayableHeight;
    }

    /// <summary>
    /// 地图面积
    /// </summary>
    public int Area
    {
        get => _heightMapDataAsset.Area;
    }
    
    /// <summary>
    /// 高度数据
    /// </summary>
    private ElevationDim2Array HeightData
    {
        get => _heightMapDataAsset.Elevations;
    }

    /// <summary>
    /// 边界数据
    /// </summary>
    private WritableList<BorderData> Borders
    {
        get => _heightMapDataAsset.Borders;
    }
    
    // ----------- height --------------
    
    /// <summary>
    /// 设置地形高度 (使用网格坐标)
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    public float SetTerrainHeight(int x, int y, float height)
    {
        return HeightData[x, y] = height;
    }
    
    /// <summary>
    /// 获取地形高度 (使用网格坐标)
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public float GetTerrainHeight(int x, int y)
    {
        return HeightData[x, y];
    }
    
    // -------- border --------------
    
    /// <summary>
    /// 获取所有边界
    /// </summary>
    /// <returns></returns>
    public List<BorderData> GetBorders()
    {
        return Borders.GetAssets();
    }
    
    /// <summary>
    /// 设置边界, 左下角(x1, y1), 右上角(x2, y2), (使用网格坐标)
    /// </summary>
    /// <param name="border"></param>
    /// <param name="x1">左下角x</param>
    /// <param name="y1">左下角y</param>
    /// <param name="x2">右上角x</param>
    /// <param name="y2">右上角y</param>
    public void SetBorder(BorderData border, int x1, int y1, int x2, int y2)
    {
        border.X1 = x1;
        border.Y1 = y1;
        border.X2 = x2;
        border.Y2 = y2;
    }
    
    /// <summary>
    /// 添加边界, 左下角(x1, y1), 右上角(x2, y2), (使用网格坐标)
    /// </summary>
    /// <param name="x1">左下角x</param>
    /// <param name="y1">左下角y</param>
    /// <param name="x2">右上角x</param>
    /// <param name="y2">右上角y</param>
    public void AddBorder(int x1, int y1, int x2, int y2)
    {
        var b = new BorderData(x1, y1, x2, y2);
        Borders.Add(b);
    }

    /// <summary>
    /// 移除边界
    /// </summary>
    /// <param name="border"></param>
    public void RemoveBorder(BorderData border)
    {
        Borders.Remove(border);
    }

    // ---- init ----
    
    private void LoadHeightMapData()
    {
        _heightMapDataAsset = ra3Map.Context.HeightMapDataAsset;
    }
}