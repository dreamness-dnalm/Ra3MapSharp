# Ra3MapSharp 纹理混合查询功能说明

## 概述

本文档介绍为 Ra3MapSharp 项目新增的纹理混合（Texture Blending）查询功能。这些功能旨在帮助地图编辑器获取、分析和展示纹理混合信息。

## 添加的核心组件

### 1. 数据传输对象 (DTOs)

位置：`src/Dreamness.RA3.Map.Facade/Models/BlendDetailInfo.cs`

#### BlendDetailInfo
完整的混合信息，包含：
- 坐标位置 (X, Y)
- 主纹理和次要纹理的名称及索引
- 混合方向（英文名称和中文描述）
- 子瓦片坐标 (SubTileX, SubTileY)
- 魔数字段 (MagicI3, MagicI4)

#### BlendStatistics
混合统计信息，包含：
- 总混合数量和唯一BlendInfo数量
- 按混合方向分类的统计
- 按纹理对分类的统计
- 涉及混合的纹理列表
- 混合覆盖率百分比

#### BlendPosition
轻量级的混合位置信息：
- 坐标 (X, Y)
- 混合方向
- BlendInfo索引

#### BlendMapSnapshot
用于可视化的整个地图快照：
- 地图宽度和高度
- 混合索引数组
- 主纹理索引数组
- BlendInfo快照列表
- 纹理名称列表

### 2. 查询方法 (Ra3MapFacade)

位置：`src/Dreamness.RA3.Map.Facade/Core/Ra3MapFacade/TilePart.cs`

#### 详细信息查询

```csharp
// 获取指定位置的详细混合信息
BlendDetailInfo? GetBlendDetailInfo(int x, int y)

// 获取混合方向的友好名称和中文描述
(string name, string description) GetBlendDirectionInfo(BlendInfo.BlendDirectionEnum direction)
```

#### 位置查询

```csharp
// 获取所有有混合的位置列表
List<BlendPosition> GetAllBlendPositions()

// 获取指定区域内的混合位置列表
List<BlendPosition> GetBlendsInRegion(int startX, int startY, int endX, int endY)
```

#### 统计信息

```csharp
// 获取地图的混合统计信息
BlendStatistics GetBlendStatistics()

// 获取总混合数量（有混合的网格数）
int GetTotalBlendCount()

// 获取唯一BlendInfo对象的数量
int GetUniqueBlendInfoCount()
```

#### 纹理相关查询

```csharp
// 获取某个位置涉及的纹理名称
(string primaryTexture, string? secondaryTexture) GetTexturesInvolved(int x, int y)

// 获取使用特定纹理对的所有混合位置
List<BlendPosition> GetBlendsByTexturePair(string primaryTextureName, string secondaryTextureName)
```

#### 批量查询

```csharp
// 获取整个地图的混合数据快照（用于可视化）
BlendMapSnapshot GetBlendMapSnapshot()

// 检查指定位置是否有混合
bool HasBlendAt(int x, int y)
```

## 使用示例

### 示例 1：获取某个位置的详细混合信息

```csharp
var map = Ra3MapFacade.Open(mapPath, mapName);

// 获取位置 (50, 50) 的详细混合信息
var blendDetail = map.GetBlendDetailInfo(50, 50);

if (blendDetail != null)
{
    Console.WriteLine($"位置: ({blendDetail.X}, {blendDetail.Y})");
    Console.WriteLine($"主纹理: {blendDetail.PrimaryTextureName}");
    Console.WriteLine($"次要纹理: {blendDetail.SecondaryTextureName}");
    Console.WriteLine($"混合方向: {blendDetail.BlendDirectionDescription}");
    Console.WriteLine($"子瓦片: ({blendDetail.SubTileX}, {blendDetail.SubTileY})");
}
else
{
    Console.WriteLine("该位置没有混合");
}
```

### 示例 2：获取地图的混合统计信息

```csharp
var map = Ra3MapFacade.Open(mapPath, mapName);

// 获取统计信息
var stats = map.GetBlendStatistics();

Console.WriteLine($"总混合数量: {stats.TotalBlendCount}");
Console.WriteLine($"唯一BlendInfo数量: {stats.UniqueBlendInfoCount}");
Console.WriteLine($"混合覆盖率: {stats.BlendCoveragePercentage:F2}%");

Console.WriteLine("\n按方向分类:");
foreach (var kvp in stats.BlendsByDirection)
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value} 个");
}

Console.WriteLine("\n按纹理对分类:");
foreach (var kvp in stats.BlendsByTexturePair)
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value} 个");
}

Console.WriteLine("\n涉及的纹理:");
foreach (var texture in stats.TexturesInvolved)
{
    Console.WriteLine($"  - {texture}");
}
```

### 示例 3：获取某个区域的所有混合

```csharp
var map = Ra3MapFacade.Open(mapPath, mapName);

// 获取区域 (10, 10) 到 (50, 50) 的所有混合
var blends = map.GetBlendsInRegion(10, 10, 50, 50);

Console.WriteLine($"在区域内找到 {blends.Count} 个混合");

foreach (var blend in blends.Take(5)) // 只显示前5个
{
    var (dirName, dirDesc) = map.GetBlendDirectionInfo(blend.Direction);
    Console.WriteLine($"  位置 ({blend.X}, {blend.Y}): {dirDesc}");
}
```

### 示例 4：查找使用特定纹理对的混合

```csharp
var map = Ra3MapFacade.Open(mapPath, mapName);

// 查找 Dirt_Yucatan03 到 Grass_Yucatan01 的混合
var positions = map.GetBlendsByTexturePair("Dirt_Yucatan03", "Grass_Yucatan01");

Console.WriteLine($"找到 {positions.Count} 个使用该纹理对的混合");

foreach (var pos in positions)
{
    Console.WriteLine($"  位置: ({pos.X}, {pos.Y})");
}
```

### 示例 5：获取整个地图的混合快照（用于可视化）

```csharp
var map = Ra3MapFacade.Open(mapPath, mapName);

// 获取整个地图的快照
var snapshot = map.GetBlendMapSnapshot();

Console.WriteLine($"地图大小: {snapshot.Width}x{snapshot.Height}");
Console.WriteLine($"BlendInfo数量: {snapshot.BlendInfos.Count}");
Console.WriteLine($"纹理数量: {snapshot.TextureNames.Count}");

// 遍历地图，可视化混合分布
for (int y = 0; y < snapshot.Height; y++)
{
    for (int x = 0; x < snapshot.Width; x++)
    {
        ushort blendIndex = snapshot.BlendIndices[x, y];
        if (blendIndex != 0)
        {
            // 有混合，在编辑器中绘制特殊标记
            var blendInfo = snapshot.BlendInfos[blendIndex - 1];
            // ... 绘制逻辑
        }
    }
}
```

### 示例 6：检查某个位置是否有混合

```csharp
var map = Ra3MapFacade.Open(mapPath, mapName);

int x = 30, y = 40;
if (map.HasBlendAt(x, y))
{
    Console.WriteLine($"位置 ({x}, {y}) 有混合");

    // 获取详细信息
    var detail = map.GetBlendDetailInfo(x, y);
    Console.WriteLine($"混合: {detail.PrimaryTextureName} -> {detail.SecondaryTextureName}");
}
else
{
    Console.WriteLine($"位置 ({x}, {y}) 没有混合");
}
```

## 混合方向说明

混合方向枚举包含12种类型：

### 基本方向（8个）
- **TopLeft (左上角)**: 次要纹理在左上角
- **Top (上边)**: 次要纹理在上边
- **TopRight (右上角)**: 次要纹理在右上角
- **Right (右边)**: 次要纹理在右边
- **BottomRight (右下角)**: 次要纹理在右下角
- **Bottom (下边)**: 次要纹理在下边
- **BottomLeft (左下角)**: 次要纹理在左下角
- **Left (左边)**: 次要纹理在左边

### 除外方向（4个）
- **ExceptTopLeft (除左上角外)**: 除左上角外都是次要纹理
- **ExceptTopRight (除右上角外)**: 除右上角外都是次要纹理
- **ExceptBottomRight (除右下角外)**: 除右下角外都是次要纹理
- **ExceptBottomLeft (除左下角外)**: 除左下角外都是次要纹理

## 性能考虑

1. **GetAllBlendPositions()** 和 **GetBlendStatistics()** 会遍历整个地图，对于大地图可能需要一些时间
2. **GetBlendsInRegion()** 只遍历指定区域，性能更好
3. **GetBlendMapSnapshot()** 会复制整个地图的数据，适合一次性获取后缓存使用
4. **GetBlendDetailInfo()** 和 **HasBlendAt()** 是 O(1) 操作，性能很好

## 注意事项

1. 所有坐标使用的是网格坐标（不是世界坐标）
2. 混合索引是 1-based（0表示无混合）
3. 子瓦片坐标范围是 0-7（对应 8x8 的子瓦片网格）
4. 魔数字段应该总是：I3=0xFFFFFFFF, I4=0x7ACDCD00

## 单元测试

已创建完整的单元测试文件：`test/Dreamness.Ra3.Map.Facade.Test/BlendQueryTests.cs`

测试覆盖：
- 详细信息查询
- 方向信息查询
- 位置查询
- 区域查询
- 统计信息
- 纹理相关查询
- 快照功能
- 辅助方法
- 集成测试

## 集成到地图编辑器的建议

1. **地图加载时获取统计信息**
   ```csharp
   var stats = map.GetBlendStatistics();
   // 在UI中显示总体信息
   ```

2. **鼠标悬停显示详细信息**
   ```csharp
   var detail = map.GetBlendDetailInfo(mouseX, mouseY);
   if (detail != null)
   {
       // 显示工具提示
   }
   ```

3. **区域选择工具**
   ```csharp
   var blends = map.GetBlendsInRegion(selectStartX, selectStartY, selectEndX, selectEndY);
   // 高亮显示选中区域的混合
   ```

4. **纹理过滤器**
   ```csharp
   var positions = map.GetBlendsByTexturePair(selectedPrimary, selectedSecondary);
   // 只显示使用特定纹理对的混合
   ```

5. **混合可视化层**
   ```csharp
   var snapshot = map.GetBlendMapSnapshot();
   // 创建一个覆盖层，用不同颜色显示不同类型的混合
   ```

## 总结

这套查询API提供了完整的纹理混合信息访问能力：
- ✅ 单点详细信息查询
- ✅ 批量位置查询
- ✅ 统计信息分析
- ✅ 纹理关系查询
- ✅ 快照和可视化支持
- ✅ 性能优化的区域查询

可以满足地图编辑器的各种需求，从简单的信息展示到复杂的混合分析和可视化。
