using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;

namespace Dreamness.Ra3.Map.Facade.Models;

/// <summary>
/// 纹理混合详细信息 - 用于地图编辑器展示
/// </summary>
public class BlendDetailInfo
{
    /// <summary>
    /// X坐标
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y坐标
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 主纹理名称
    /// </summary>
    public string PrimaryTextureName { get; set; } = string.Empty;

    /// <summary>
    /// 次要纹理名称
    /// </summary>
    public string SecondaryTextureName { get; set; } = string.Empty;

    /// <summary>
    /// 主纹理索引
    /// </summary>
    public int PrimaryTextureIndex { get; set; }

    /// <summary>
    /// 次要纹理索引
    /// </summary>
    public int SecondaryTextureIndex { get; set; }

    /// <summary>
    /// 混合方向
    /// </summary>
    public BlendInfo.BlendDirectionEnum BlendDirection { get; set; }

    /// <summary>
    /// 混合方向名称（英文）
    /// </summary>
    public string BlendDirectionName { get; set; } = string.Empty;

    /// <summary>
    /// 混合方向描述（中文）
    /// </summary>
    public string BlendDirectionDescription { get; set; } = string.Empty;

    /// <summary>
    /// 次要纹理瓦片索引
    /// </summary>
    public int SecondaryTextureTile { get; set; }

    /// <summary>
    /// 子瓦片X坐标（0-7）
    /// </summary>
    public int SubTileX { get; set; }

    /// <summary>
    /// 子瓦片Y坐标（0-7）
    /// </summary>
    public int SubTileY { get; set; }

    /// <summary>
    /// 魔数字段I3（通常为0xFFFFFFFF）
    /// </summary>
    public uint MagicI3 { get; set; }

    /// <summary>
    /// 魔数字段I4（通常为0x7ACDCD00）
    /// </summary>
    public uint MagicI4 { get; set; }
}

/// <summary>
/// 纹理混合统计信息
/// </summary>
public class BlendStatistics
{
    /// <summary>
    /// 总混合数量
    /// </summary>
    public int TotalBlendCount { get; set; }

    /// <summary>
    /// 唯一的BlendInfo对象数量
    /// </summary>
    public int UniqueBlendInfoCount { get; set; }

    /// <summary>
    /// 按混合方向分类的统计（方向名称 -> 数量）
    /// </summary>
    public Dictionary<string, int> BlendsByDirection { get; set; } = new();

    /// <summary>
    /// 按纹理对分类的统计（"主纹理-次要纹理" -> 数量）
    /// </summary>
    public Dictionary<string, int> BlendsByTexturePair { get; set; } = new();

    /// <summary>
    /// 涉及混合的纹理列表
    /// </summary>
    public List<string> TexturesInvolved { get; set; } = new();

    /// <summary>
    /// 混合覆盖率（有混合的网格数/总网格数）
    /// </summary>
    public double BlendCoveragePercentage { get; set; }
}

/// <summary>
/// 混合位置信息（轻量级）
/// </summary>
public class BlendPosition
{
    /// <summary>
    /// X坐标
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y坐标
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 混合方向
    /// </summary>
    public BlendInfo.BlendDirectionEnum Direction { get; set; }

    /// <summary>
    /// BlendInfo在列表中的索引（0-based）
    /// </summary>
    public int BlendInfoIndex { get; set; }
}

/// <summary>
/// 混合地图快照数据 - 用于可视化
/// </summary>
public class BlendMapSnapshot
{
    /// <summary>
    /// 地图宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 地图高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 混合索引数组（0表示无混合，非0表示BlendInfo索引+1）
    /// </summary>
    public ushort[,] BlendIndices { get; set; } = new ushort[0, 0];

    /// <summary>
    /// 主纹理索引数组
    /// </summary>
    public ushort[,] PrimaryTextureIndices { get; set; } = new ushort[0, 0];

    /// <summary>
    /// 所有BlendInfo的快照
    /// </summary>
    public List<BlendInfoSnapshot> BlendInfos { get; set; } = new();

    /// <summary>
    /// 纹理名称列表（索引对应纹理索引）
    /// </summary>
    public List<string> TextureNames { get; set; } = new();
}

/// <summary>
/// BlendInfo快照（简化版本）
/// </summary>
public class BlendInfoSnapshot
{
    /// <summary>
    /// 次要纹理索引
    /// </summary>
    public int SecondaryTextureIndex { get; set; }

    /// <summary>
    /// 混合方向
    /// </summary>
    public BlendInfo.BlendDirectionEnum Direction { get; set; }

    /// <summary>
    /// 次要纹理瓦片索引
    /// </summary>
    public int SecondaryTextureTile { get; set; }
}
