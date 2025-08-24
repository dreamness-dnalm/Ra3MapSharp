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

    public int MapWidth
    {
        get => _heightMapDataAsset.MapWidth;
    }

    public int MapHeight
    {
        get => _heightMapDataAsset.MapHeight;
    }

    public int MapBorderWidth
    {
        get => _heightMapDataAsset.BorderWidth;
    }

    public int MapPlayableWidth
    {
        get => _heightMapDataAsset.MapPlayableWidth;
    }

    public int MapPlayableHeight
    {
        get => _heightMapDataAsset.MapPlayableHeight;
    }

    public int Area
    {
        get => _heightMapDataAsset.Area;
    }
    
    private ElevationDim2Array HeightData
    {
        get => _heightMapDataAsset.Elevations;
    }

    private WritableList<BorderData> Borders
    {
        get => _heightMapDataAsset.Borders;
    }
    
    // ----------- height --------------
    
    public float SetTerrainHeight(int x, int y, float height)
    {
        return HeightData[x, y] = height;
    }
    
    public float GetTerrainHeight(int x, int y)
    {
        return HeightData[x, y];
    }
    
    // -------- border --------------
    public List<BorderData> GetBorders()
    {
        return Borders.GetAssets();
    }
    
    public void SetBorder(BorderData border, int x1, int y1, int x2, int y2)
    {
        border.X1 = x1;
        border.Y1 = y1;
        border.X2 = x2;
        border.Y2 = y2;
    }
    
    public void AddBorder(int x1, int y1, int x2, int y2)
    {
        var b = new BorderData(x1, y1, x2, y2);
        Borders.Add(b);
    }

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