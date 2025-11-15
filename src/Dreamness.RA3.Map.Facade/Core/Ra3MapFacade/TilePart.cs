using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    // ----------- texture ------------------
    
    private BlendTileDataAsset _blendTileData { get; set; }
    
    /// <summary>
    /// 获取指定坐标的地形纹理名称, 使用网格坐标
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>枚举值参考TextureEnum</returns>
    public string GetTileTexture(int x, int y)
    {
        return _blendTileData.GetTextureName(x, y);
    }
   
    /// <summary>
    /// 设置指定坐标的地形纹理名称, 使用网格坐标
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="texture">枚举值参考TextureEnum</param>
    public void SetTileTexture(int x, int y, string texture)
    {
        _blendTileData.SetTileTexture(x, y, texture, ra3Map.Context);
    }

    /// <summary>
    /// 注册支持的地形纹理
    /// </summary>
    /// <param name="textureName"></param>
    /// <param name="tgaFileName"></param>
    /// <param name="nrmFileName"></param>
    public void RegisterSupportedTexture(string textureName, string tgaFileName, string nrmFileName)
    {
        _worldInfoAsset.RegisterSupportedTexture(textureName, tgaFileName, nrmFileName);
    }
    
    public ushort GetTileBlend(int x, int y)
    {
        return _blendTileData.Blends[x, y];
    }

    public void SetTileBlend(int x, int y, ushort value)
    {
        _blendTileData.Blends[x, y] = value;
    }
    
    public ushort GetTileSingleEdgeBlend(int x, int y)
    {
        return _blendTileData.SingleEdgeBlends[x, y];
    }
    
    public void SetTileSingleEdgeBlend(int x, int y, ushort value)
    {
        _blendTileData.SingleEdgeBlends[x, y] = value;
    }
    
    public ushort GetTileCliffBlend(int x, int y)
    {
        return _blendTileData.CliffBlends[x, y];
    }
    
    public void SetTileCliffBlend(int x, int y, ushort value)
    {
        _blendTileData.CliffBlends[x, y] = value;
    }
    
    public bool GetTilePassageWidth(int x, int y)
    {
        return _blendTileData.PassageWidths[x, y];
    }
    
    public void SetTilePassageWidth(int x, int y, bool value)
    {
        _blendTileData.PassageWidths[x, y] = value;
    }

    public bool GetTileVisibility(int x, int y)
    {
        return _blendTileData.Visibilities[x, y];
    }
    
    public void SetTileVisibility(int x, int y, bool value)
    {
        _blendTileData.Visibilities[x, y] = value;
    }

    public bool GetTileBuildability(int x, int y)
    {
        return _blendTileData.Buildabilities[x, y];
    }
    
    public void SetTileBuildability(int x, int y, bool value)
    {
        _blendTileData.Buildabilities[x, y] = value;
    }
    
    public bool GetTileTiberiumGrowability(int x, int y)
    {
        return _blendTileData.TiberiumGrowabilities[x, y];
    }
    
    public void SetTileTiberiumGrowability(int x, int y, bool value)
    {
        _blendTileData.TiberiumGrowabilities[x, y] = value;
    }
    
    public byte GetTileDynamicShrubbery(int x, int y)
    {
        return _blendTileData.DynamicShrubberies[x, y];
    }
    
    public void SetTileDynamicShrubbery(int x, int y, byte value)
    {
        _blendTileData.DynamicShrubberies[x, y] = value;
    }
    
    
    // ---------- passability ----------------
    
    /// <summary>
    /// 设置指定坐标的地形通行属性, 使用网格坐标
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="passability">枚举值参考Passability</param>
    public void SetPassability(int x, int y, string passability)
    {
        _blendTileData.Passabilities[x, y] = Enum.Parse<BlendTileDataAsset.Passability>(passability, true);
    }
    
    /// <summary>
    /// 获取指定坐标的地形通行属性, 使用网格坐标
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>枚举值参考Passability</returns>
    public string GetPassability(int x, int y)
    {
        return _blendTileData.Passabilities[x, y].ToString();
    }
    
    /// <summary>
    /// 根据高度信息, 更新地图的通行属性数据, 建议在修改完高度图后调用
    /// </summary>
    public void UpdatePassabilityMap()
    {
        _blendTileData.UpdatePassabilityMap(ra3Map.Context);
    }
    
    // ---- init ----
    private void LoadBlendTileData()
    {
        _blendTileData = ra3Map.Context.BlendTileDataAsset;
        
        // for(int i = 0; i < _blendTileData.textures.Count; i++)
        // {
        //     textureIndexDict.Add(_blendTileData.textures[i].name, i);
        // }
    }
}