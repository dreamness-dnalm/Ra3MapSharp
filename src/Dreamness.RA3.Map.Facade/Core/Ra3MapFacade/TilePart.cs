using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Facade.Models;

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

    // ----------- texture blending ------------------

    /// <summary>
    /// 获取纹理在8x8子瓦片网格中的位置
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <returns>子瓦片坐标(subX, subY), 范围0-7</returns>
    public (int subX, int subY) GetSubTilePosition(int x, int y)
    {
        return _blendTileData.GetSubTilePosition(x, y);
    }

    /// <summary>
    /// 获取指定坐标的纹理混合信息
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <returns>混合信息对象, 如果不存在混合则返回null</returns>
    public BlendInfo? GetBlendInfo(int x, int y)
    {
        return _blendTileData.GetBlendInfo(x, y);
    }

    /// <summary>
    /// 添加或更新纹理混合（使用纹理索引）
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <param name="secondaryTextureIndex">次要纹理索引</param>
    /// <param name="direction">混合方向</param>
    public void AddBlend(int x, int y, int secondaryTextureIndex, BlendInfo.BlendDirectionEnum direction)
    {
        _blendTileData.AddBlend(x, y, secondaryTextureIndex, direction);
    }

    /// <summary>
    /// 添加或更新纹理混合（使用纹理名称）
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <param name="secondaryTextureName">次要纹理名称，参考TextureEnum</param>
    /// <param name="direction">混合方向</param>
    public void AddBlend(int x, int y, string secondaryTextureName, BlendInfo.BlendDirectionEnum direction)
    {
        _blendTileData.AddBlend(x, y, secondaryTextureName, direction, ra3Map.Context);
    }

    /// <summary>
    /// 移除指定坐标的纹理混合
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    public void RemoveBlend(int x, int y)
    {
        _blendTileData.RemoveBlend(x, y);
    }

    /// <summary>
    /// 自动检测并应用纹理混合（单个网格）
    /// 分析周围8个邻居，自动确定合适的混合方向
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    public void AutoDetectBlend(int x, int y)
    {
        _blendTileData.AutoDetectBlend(x, y);
    }

    /// <summary>
    /// 自动检测并应用纹理混合（矩形区域）
    /// </summary>
    /// <param name="startX">起始X坐标（包含）</param>
    /// <param name="startY">起始Y坐标（包含）</param>
    /// <param name="endX">结束X坐标（不包含）</param>
    /// <param name="endY">结束Y坐标（不包含）</param>
    public void AutoDetectBlendsInRegion(int startX, int startY, int endX, int endY)
    {
        _blendTileData.AutoDetectBlendsInRegion(startX, startY, endX, endY);
    }

    /// <summary>
    /// 自动检测并应用纹理混合（整个地图）
    /// 会跳过地图边缘的1格边界
    /// </summary>
    public void AutoDetectBlendsEntireMap()
    {
        _blendTileData.AutoDetectBlendsEntireMap();
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

    // ---------- blend query functions for editor ----------------

    /// <summary>
    /// 获取指定位置的详细混合信息
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <returns>详细混合信息，如果不存在混合则返回null</returns>
    public BlendDetailInfo? GetBlendDetailInfo(int x, int y)
    {
        var blendInfo = _blendTileData.GetBlendInfo(x, y);
        if (blendInfo == null)
        {
            return null;
        }

        var primaryTexIndex = _blendTileData.GetTexture(x, y);

        // 边界检查：验证主纹理索引
        if (primaryTexIndex < 0 || primaryTexIndex >= _blendTileData.Textures.Count)
        {
            return null;
        }

        var primaryTexName = _blendTileData.Textures[primaryTexIndex].Name;

        // 从SecondaryTextureTile计算次要纹理索引
        var (subX, subY) = _blendTileData.GetSubTilePosition(x, y);
        int rowFirst = y % 8 / 2 * 16 + y % 2 * 2;
        int tileOffset = x % 8 / 2 * 4 + x % 2 + rowFirst;
        int secondaryTexIndex = (blendInfo.SecondaryTextureTile - tileOffset) / 64;

        // 边界检查：验证次要纹理索引
        if (secondaryTexIndex < 0 || secondaryTexIndex >= _blendTileData.Textures.Count)
        {
            return null;
        }

        string secondaryTexName = _blendTileData.Textures[secondaryTexIndex].Name;

        var (dirName, dirDesc) = GetBlendDirectionInfo(blendInfo.BlendDirection);

        return new BlendDetailInfo
        {
            X = x,
            Y = y,
            PrimaryTextureName = primaryTexName,
            SecondaryTextureName = secondaryTexName,
            PrimaryTextureIndex = primaryTexIndex,
            SecondaryTextureIndex = secondaryTexIndex,
            BlendDirection = blendInfo.BlendDirection,
            BlendDirectionName = dirName,
            BlendDirectionDescription = dirDesc,
            SecondaryTextureTile = blendInfo.SecondaryTextureTile,
            SubTileX = subX,
            SubTileY = subY,
            MagicI3 = blendInfo.I3,
            MagicI4 = blendInfo.I4
        };
    }

    /// <summary>
    /// 获取混合方向的友好名称和描述
    /// </summary>
    /// <param name="direction">混合方向枚举</param>
    /// <returns>(英文名称, 中文描述)</returns>
    public (string name, string description) GetBlendDirectionInfo(BlendInfo.BlendDirectionEnum direction)
    {
        return direction switch
        {
            BlendInfo.BlendDirectionEnum.BottomRight => ("BottomRight", "右下角"),
            BlendInfo.BlendDirectionEnum.Bottom => ("Bottom", "下边"),
            BlendInfo.BlendDirectionEnum.BottomLeft => ("BottomLeft", "左下角"),
            BlendInfo.BlendDirectionEnum.Right => ("Right", "右边"),
            BlendInfo.BlendDirectionEnum.Left => ("Left", "左边"),
            BlendInfo.BlendDirectionEnum.TopRight => ("TopRight", "右上角"),
            BlendInfo.BlendDirectionEnum.Top => ("Top", "上边"),
            BlendInfo.BlendDirectionEnum.TopLeft => ("TopLeft", "左上角"),
            BlendInfo.BlendDirectionEnum.ExceptBottomRight => ("ExceptBottomRight", "除右下角外"),
            BlendInfo.BlendDirectionEnum.ExceptBottomLeft => ("ExceptBottomLeft", "除左下角外"),
            BlendInfo.BlendDirectionEnum.ExceptTopRight => ("ExceptTopRight", "除右上角外"),
            BlendInfo.BlendDirectionEnum.ExceptTopLeft => ("ExceptTopLeft", "除左上角外"),
            _ => ("Unknown", "未知")
        };
    }

    /// <summary>
    /// 获取所有有混合的位置列表
    /// </summary>
    /// <returns>混合位置列表</returns>
    public List<BlendPosition> GetAllBlendPositions()
    {
        var positions = new List<BlendPosition>();

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                ushort blendIndex = _blendTileData.Blends[x, y];
                if (blendIndex != 0)
                {
                    var blendInfo = _blendTileData.BlendInfos[blendIndex - 1];
                    positions.Add(new BlendPosition
                    {
                        X = x,
                        Y = y,
                        Direction = blendInfo.BlendDirection,
                        BlendInfoIndex = blendIndex - 1
                    });
                }
            }
        }

        return positions;
    }

    /// <summary>
    /// 获取指定区域内的混合位置列表
    /// </summary>
    /// <param name="startX">起始X坐标（包含）</param>
    /// <param name="startY">起始Y坐标（包含）</param>
    /// <param name="endX">结束X坐标（不包含）</param>
    /// <param name="endY">结束Y坐标（不包含）</param>
    /// <returns>混合位置列表</returns>
    public List<BlendPosition> GetBlendsInRegion(int startX, int startY, int endX, int endY)
    {
        var positions = new List<BlendPosition>();

        // 边界检查
        startX = Math.Max(0, startX);
        startY = Math.Max(0, startY);
        endX = Math.Min(MapWidth, endX);
        endY = Math.Min(MapHeight, endY);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                ushort blendIndex = _blendTileData.Blends[x, y];
                if (blendIndex != 0)
                {
                    var blendInfo = _blendTileData.BlendInfos[blendIndex - 1];
                    positions.Add(new BlendPosition
                    {
                        X = x,
                        Y = y,
                        Direction = blendInfo.BlendDirection,
                        BlendInfoIndex = blendIndex - 1
                    });
                }
            }
        }

        return positions;
    }

    /// <summary>
    /// 获取地图的混合统计信息
    /// </summary>
    /// <returns>混合统计信息</returns>
    public BlendStatistics GetBlendStatistics()
    {
        var stats = new BlendStatistics
        {
            UniqueBlendInfoCount = _blendTileData.BlendInfos.Count
        };

        var directionCounts = new Dictionary<BlendInfo.BlendDirectionEnum, int>();
        var texturePairCounts = new Dictionary<string, int>();
        var texturesSet = new HashSet<string>();
        int totalCells = 0;

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                ushort blendIndex = _blendTileData.Blends[x, y];
                if (blendIndex != 0)
                {
                    totalCells++;
                    var blendInfo = _blendTileData.BlendInfos[blendIndex - 1];

                    // 统计方向
                    if (!directionCounts.ContainsKey(blendInfo.BlendDirection))
                    {
                        directionCounts[blendInfo.BlendDirection] = 0;
                    }
                    directionCounts[blendInfo.BlendDirection]++;

                    // 统计纹理对
                    var primaryTex = _blendTileData.GetTextureName(x, y);

                    // 计算次要纹理
                    int rowFirst = y % 8 / 2 * 16 + y % 2 * 2;
                    int tileOffset = x % 8 / 2 * 4 + x % 2 + rowFirst;
                    int secondaryTexIndex = (blendInfo.SecondaryTextureTile - tileOffset) / 64;

                    // 边界检查：验证次要纹理索引
                    if (secondaryTexIndex < 0 || secondaryTexIndex >= _blendTileData.Textures.Count)
                    {
                        continue; // 跳过无效的混合数据
                    }

                    string secondaryTex = _blendTileData.Textures[secondaryTexIndex].Name;

                    string pairKey = $"{primaryTex} -> {secondaryTex}";
                    if (!texturePairCounts.ContainsKey(pairKey))
                    {
                        texturePairCounts[pairKey] = 0;
                    }
                    texturePairCounts[pairKey]++;

                    texturesSet.Add(primaryTex);
                    texturesSet.Add(secondaryTex);
                }
            }
        }

        stats.TotalBlendCount = totalCells;
        stats.BlendCoveragePercentage = (double)totalCells / (MapWidth * MapHeight) * 100.0;

        // 转换方向统计
        foreach (var kvp in directionCounts)
        {
            var (name, _) = GetBlendDirectionInfo(kvp.Key);
            stats.BlendsByDirection[name] = kvp.Value;
        }

        stats.BlendsByTexturePair = texturePairCounts;
        stats.TexturesInvolved = texturesSet.ToList();

        return stats;
    }

    /// <summary>
    /// 获取某个位置涉及的纹理名称
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <returns>(主纹理名称, 次要纹理名称)，如果没有混合则次要纹理为null</returns>
    public (string primaryTexture, string? secondaryTexture) GetTexturesInvolved(int x, int y)
    {
        var primaryTex = _blendTileData.GetTextureName(x, y);

        var blendInfo = _blendTileData.GetBlendInfo(x, y);
        if (blendInfo == null)
        {
            return (primaryTex, null);
        }

        // 计算次要纹理
        int rowFirst = y % 8 / 2 * 16 + y % 2 * 2;
        int tileOffset = x % 8 / 2 * 4 + x % 2 + rowFirst;
        int secondaryTexIndex = (blendInfo.SecondaryTextureTile - tileOffset) / 64;

        // 边界检查：验证次要纹理索引
        if (secondaryTexIndex < 0 || secondaryTexIndex >= _blendTileData.Textures.Count)
        {
            return (primaryTex, null);
        }

        string secondaryTex = _blendTileData.Textures[secondaryTexIndex].Name;

        return (primaryTex, secondaryTex);
    }

    /// <summary>
    /// 获取使用特定纹理对的所有混合位置
    /// </summary>
    /// <param name="primaryTextureName">主纹理名称</param>
    /// <param name="secondaryTextureName">次要纹理名称</param>
    /// <returns>混合位置列表</returns>
    public List<BlendPosition> GetBlendsByTexturePair(string primaryTextureName, string secondaryTextureName)
    {
        var positions = new List<BlendPosition>();

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                ushort blendIndex = _blendTileData.Blends[x, y];
                if (blendIndex != 0)
                {
                    var (primary, secondary) = GetTexturesInvolved(x, y);
                    if (primary == primaryTextureName && secondary == secondaryTextureName)
                    {
                        var blendInfo = _blendTileData.BlendInfos[blendIndex - 1];
                        positions.Add(new BlendPosition
                        {
                            X = x,
                            Y = y,
                            Direction = blendInfo.BlendDirection,
                            BlendInfoIndex = blendIndex - 1
                        });
                    }
                }
            }
        }

        return positions;
    }

    /// <summary>
    /// 获取整个地图的混合数据快照（用于可视化）
    /// </summary>
    /// <returns>混合地图快照</returns>
    public BlendMapSnapshot GetBlendMapSnapshot()
    {
        var snapshot = new BlendMapSnapshot
        {
            Width = MapWidth,
            Height = MapHeight,
            BlendIndices = new ushort[MapWidth, MapHeight],
            PrimaryTextureIndices = new ushort[MapWidth, MapHeight]
        };

        // 复制混合索引和主纹理索引
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                snapshot.BlendIndices[x, y] = _blendTileData.Blends[x, y];
                snapshot.PrimaryTextureIndices[x, y] = _blendTileData.GetTexture(x, y);
            }
        }

        // 复制BlendInfo快照
        foreach (var blendInfo in _blendTileData.BlendInfos)
        {
            // 计算次要纹理索引（使用第一个像素点的坐标，这里简化处理）
            // 注意：实际使用时每个位置可能不同，这里仅供参考
            int secondaryTexIndex = blendInfo.SecondaryTextureTile / 64;

            snapshot.BlendInfos.Add(new BlendInfoSnapshot
            {
                SecondaryTextureIndex = secondaryTexIndex,
                Direction = blendInfo.BlendDirection,
                SecondaryTextureTile = blendInfo.SecondaryTextureTile
            });
        }

        // 复制纹理名称列表
        foreach (var texture in _blendTileData.Textures)
        {
            snapshot.TextureNames.Add(texture.Name);
        }

        return snapshot;
    }

    /// <summary>
    /// 获取总混合数量（有混合的网格数）
    /// </summary>
    /// <returns>混合网格数量</returns>
    public int GetTotalBlendCount()
    {
        int count = 0;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if (_blendTileData.Blends[x, y] != 0)
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// 获取唯一BlendInfo对象的数量
    /// </summary>
    /// <returns>BlendInfo数量</returns>
    public int GetUniqueBlendInfoCount()
    {
        return _blendTileData.BlendInfos.Count;
    }

    /// <summary>
    /// 检查指定位置是否有混合
    /// </summary>
    /// <param name="x">网格X坐标</param>
    /// <param name="y">网格Y坐标</param>
    /// <returns>true表示有混合，false表示无混合</returns>
    public bool HasBlendAt(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
        {
            return false;
        }
        return _blendTileData.Blends[x, y] != 0;
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