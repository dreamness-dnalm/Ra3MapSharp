using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.enums;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;

namespace Dreamness.Ra3.Map.Facade.Test;

/// <summary>
/// 纹理混合查询函数的单元测试
/// </summary>
public class BlendQueryTests
{
    private Ra3MapFacade? _testMap;
    private const string TestMapName = "BlendQueryTest_TempMap";
    private const string PrimaryTexture = "Dirt_Yucatan03";
    private const string SecondaryTexture = "Grass_Yucatan01";
    private const string TertiaryTexture = "Grass_Yucatan02";

    [SetUp]
    public void Setup()
    {
        // 创建测试地图
        _testMap = Ra3MapFacade.NewMap(64, 64, 8, 2, PrimaryTexture);

        // 添加第二种和第三种纹理
        _testMap.SetTileTexture(0, 0, SecondaryTexture);
        _testMap.SetTileTexture(0, 0, TertiaryTexture);
        _testMap.SetTileTexture(0, 0, PrimaryTexture); // 恢复默认

        // 创建一些测试混合
        CreateTestBlendPattern();

        Console.WriteLine($"[Setup] Created test map: {_testMap.MapWidth}x{_testMap.MapHeight}");
    }

    private void CreateTestBlendPattern()
    {
        // 创建一个3x3的草地区域
        for (int y = 20; y <= 22; y++)
        {
            for (int x = 20; x <= 22; x++)
            {
                _testMap!.SetTileTexture(x, y, SecondaryTexture);
            }
        }

        // 创建一个3x3的沙地区域
        for (int y = 30; y <= 32; y++)
        {
            for (int x = 30; x <= 32; x++)
            {
                _testMap!.SetTileTexture(x, y, TertiaryTexture);
            }
        }

        // 自动检测混合
        _testMap!.AutoDetectBlendsEntireMap();
    }

    [TearDown]
    public void TearDown()
    {
        _testMap = null;
        Console.WriteLine("[TearDown] Test completed");
    }

    #region GetBlendDetailInfo Tests

    [Test]
    public void GetBlendDetailInfo_WithBlend_ReturnsCompleteInfo()
    {
        // Arrange - 在草地边界应该有混合
        int x = 20, y = 20;

        // Act
        var detailInfo = _testMap!.GetBlendDetailInfo(x, y);

        // Assert
        Assert.That(detailInfo, Is.Not.Null, "Should return detail info when blend exists");
        Assert.That(detailInfo!.X, Is.EqualTo(x));
        Assert.That(detailInfo.Y, Is.EqualTo(y));
        Assert.That(detailInfo.PrimaryTextureName, Is.Not.Null.And.Not.Empty);
        Assert.That(detailInfo.SecondaryTextureName, Is.Not.Null.And.Not.Empty);
        Assert.That(detailInfo.BlendDirectionName, Is.Not.Null.And.Not.Empty);
        Assert.That(detailInfo.BlendDirectionDescription, Is.Not.Null.And.Not.Empty);
        Assert.That(detailInfo.SubTileX, Is.InRange(0, 7));
        Assert.That(detailInfo.SubTileY, Is.InRange(0, 7));

        Console.WriteLine($"[GetBlendDetailInfo] Position ({x},{y}):");
        Console.WriteLine($"  Primary: {detailInfo.PrimaryTextureName} (Index: {detailInfo.PrimaryTextureIndex})");
        Console.WriteLine($"  Secondary: {detailInfo.SecondaryTextureName} (Index: {detailInfo.SecondaryTextureIndex})");
        Console.WriteLine($"  Direction: {detailInfo.BlendDirectionName} ({detailInfo.BlendDirectionDescription})");
        Console.WriteLine($"  SubTile: ({detailInfo.SubTileX}, {detailInfo.SubTileY})");
        Console.WriteLine($"  Magic: I3=0x{detailInfo.MagicI3:X8}, I4=0x{detailInfo.MagicI4:X8}");
    }

    [Test]
    public void GetBlendDetailInfo_NoBlend_ReturnsNull()
    {
        // Arrange - 远离混合区域的位置
        int x = 50, y = 50;

        // Act
        var detailInfo = _testMap!.GetBlendDetailInfo(x, y);

        // Assert
        Assert.That(detailInfo, Is.Null, "Should return null when no blend exists");
        Console.WriteLine($"[GetBlendDetailInfo] No blend at ({x},{y}) as expected");
    }

    [Test]
    public void GetBlendDetailInfo_MagicNumbers_AreCorrect()
    {
        // Arrange - 找到任何有混合的位置
        var allPositions = _testMap!.GetAllBlendPositions();
        Assert.That(allPositions.Count, Is.GreaterThan(0), "Need at least one blend to test");

        var testPos = allPositions[0];

        // Act
        var detailInfo = _testMap.GetBlendDetailInfo(testPos.X, testPos.Y);

        // Assert
        Assert.That(detailInfo, Is.Not.Null);
        Assert.That(detailInfo!.MagicI3, Is.EqualTo(0xFFFFFFFF), "Magic I3 should be 0xFFFFFFFF");
        Assert.That(detailInfo.MagicI4, Is.EqualTo(0x7ACDCD00), "Magic I4 should be 0x7ACDCD00");
        Console.WriteLine($"[GetBlendDetailInfo] Magic numbers verified at ({testPos.X},{testPos.Y})");
    }

    #endregion

    #region GetBlendDirectionInfo Tests

    [Test]
    public void GetBlendDirectionInfo_AllDirections_HaveValidInfo()
    {
        // Arrange
        var directions = new[]
        {
            BlendInfo.BlendDirectionEnum.BottomRight,
            BlendInfo.BlendDirectionEnum.Bottom,
            BlendInfo.BlendDirectionEnum.BottomLeft,
            BlendInfo.BlendDirectionEnum.Right,
            BlendInfo.BlendDirectionEnum.Left,
            BlendInfo.BlendDirectionEnum.TopRight,
            BlendInfo.BlendDirectionEnum.Top,
            BlendInfo.BlendDirectionEnum.TopLeft,
            BlendInfo.BlendDirectionEnum.ExceptBottomRight,
            BlendInfo.BlendDirectionEnum.ExceptBottomLeft,
            BlendInfo.BlendDirectionEnum.ExceptTopRight,
            BlendInfo.BlendDirectionEnum.ExceptTopLeft
        };

        // Act & Assert
        foreach (var direction in directions)
        {
            var (name, description) = _testMap!.GetBlendDirectionInfo(direction);

            Assert.That(name, Is.Not.Null.And.Not.Empty, $"Direction {direction} should have a name");
            Assert.That(description, Is.Not.Null.And.Not.Empty, $"Direction {direction} should have a description");
            Console.WriteLine($"[GetBlendDirectionInfo] {direction} => {name} ({description})");
        }
    }

    #endregion

    #region GetAllBlendPositions Tests

    [Test]
    public void GetAllBlendPositions_ReturnsAllBlends()
    {
        // Act
        var positions = _testMap!.GetAllBlendPositions();

        // Assert
        Assert.That(positions.Count, Is.GreaterThan(0), "Should have at least some blends");

        // 验证返回的位置确实有混合
        foreach (var pos in positions.Take(5)) // 只检查前5个
        {
            Assert.That(_testMap.HasBlendAt(pos.X, pos.Y), Is.True,
                $"Position ({pos.X},{pos.Y}) should have a blend");
        }

        Console.WriteLine($"[GetAllBlendPositions] Found {positions.Count} blend positions");
    }

    [Test]
    public void GetAllBlendPositions_CountMatchesTotalBlendCount()
    {
        // Act
        var positions = _testMap!.GetAllBlendPositions();
        var totalCount = _testMap.GetTotalBlendCount();

        // Assert
        Assert.That(positions.Count, Is.EqualTo(totalCount),
            "GetAllBlendPositions count should match GetTotalBlendCount");
        Console.WriteLine($"[GetAllBlendPositions] Count verified: {positions.Count} == {totalCount}");
    }

    #endregion

    #region GetBlendsInRegion Tests

    [Test]
    public void GetBlendsInRegion_WithinBlendArea_ReturnsSomeBlends()
    {
        // Arrange - 草地区域周围
        int startX = 18, startY = 18;
        int endX = 24, endY = 24;

        // Act
        var positions = _testMap!.GetBlendsInRegion(startX, startY, endX, endY);

        // Assert
        Assert.That(positions.Count, Is.GreaterThan(0), "Should find blends in grass area");

        // 验证所有返回的位置都在指定区域内
        foreach (var pos in positions)
        {
            Assert.That(pos.X, Is.InRange(startX, endX - 1));
            Assert.That(pos.Y, Is.InRange(startY, endY - 1));
        }

        Console.WriteLine($"[GetBlendsInRegion] Found {positions.Count} blends in region ({startX},{startY})-({endX},{endY})");
    }

    [Test]
    public void GetBlendsInRegion_OutsideBlendArea_ReturnsEmpty()
    {
        // Arrange - 远离混合区域
        int startX = 50, startY = 50;
        int endX = 60, endY = 60;

        // Act
        var positions = _testMap!.GetBlendsInRegion(startX, startY, endX, endY);

        // Assert
        Assert.That(positions.Count, Is.EqualTo(0), "Should not find blends in empty area");
        Console.WriteLine($"[GetBlendsInRegion] No blends in region ({startX},{startY})-({endX},{endY}) as expected");
    }

    [Test]
    public void GetBlendsInRegion_OutOfBounds_ClampsCorrectly()
    {
        // Arrange - 超出地图边界的范围
        int startX = -10, startY = -10;
        int endX = _testMap!.MapWidth + 10;
        int endY = _testMap.MapHeight + 10;

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            var positions = _testMap.GetBlendsInRegion(startX, startY, endX, endY);
            Assert.That(positions.Count, Is.GreaterThanOrEqualTo(0));
        }, "Should handle out-of-bounds coordinates gracefully");

        Console.WriteLine($"[GetBlendsInRegion] Out-of-bounds handled correctly");
    }

    #endregion

    #region GetBlendStatistics Tests

    [Test]
    public void GetBlendStatistics_ReturnsCompleteStats()
    {
        // Act
        var stats = _testMap!.GetBlendStatistics();

        // Assert
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.TotalBlendCount, Is.GreaterThan(0), "Should have some blends");
        Assert.That(stats.UniqueBlendInfoCount, Is.GreaterThan(0), "Should have some unique blend infos");
        Assert.That(stats.BlendsByDirection.Count, Is.GreaterThan(0), "Should have direction statistics");
        Assert.That(stats.BlendsByTexturePair.Count, Is.GreaterThan(0), "Should have texture pair statistics");
        Assert.That(stats.TexturesInvolved.Count, Is.GreaterThan(0), "Should have textures involved");
        Assert.That(stats.BlendCoveragePercentage, Is.GreaterThan(0).And.LessThanOrEqualTo(100));

        Console.WriteLine($"[GetBlendStatistics] Statistics:");
        Console.WriteLine($"  Total Blends: {stats.TotalBlendCount}");
        Console.WriteLine($"  Unique BlendInfos: {stats.UniqueBlendInfoCount}");
        Console.WriteLine($"  Coverage: {stats.BlendCoveragePercentage:F2}%");
        Console.WriteLine($"  Textures Involved: {string.Join(", ", stats.TexturesInvolved)}");
        Console.WriteLine($"  Directions:");
        foreach (var kvp in stats.BlendsByDirection)
        {
            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine($"  Texture Pairs:");
        foreach (var kvp in stats.BlendsByTexturePair)
        {
            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
        }
    }

    [Test]
    public void GetBlendStatistics_CountsMatchOtherMethods()
    {
        // Act
        var stats = _testMap!.GetBlendStatistics();
        var totalCount = _testMap.GetTotalBlendCount();
        var uniqueCount = _testMap.GetUniqueBlendInfoCount();

        // Assert
        Assert.That(stats.TotalBlendCount, Is.EqualTo(totalCount),
            "Stats total count should match GetTotalBlendCount");
        Assert.That(stats.UniqueBlendInfoCount, Is.EqualTo(uniqueCount),
            "Stats unique count should match GetUniqueBlendInfoCount");

        Console.WriteLine($"[GetBlendStatistics] Counts verified");
    }

    #endregion

    #region GetTexturesInvolved Tests

    [Test]
    public void GetTexturesInvolved_WithBlend_ReturnsBothTextures()
    {
        // Arrange - 找到一个有混合的位置
        var positions = _testMap!.GetAllBlendPositions();
        Assert.That(positions.Count, Is.GreaterThan(0));
        var testPos = positions[0];

        // Act
        var (primary, secondary) = _testMap.GetTexturesInvolved(testPos.X, testPos.Y);

        // Assert
        Assert.That(primary, Is.Not.Null.And.Not.Empty);
        Assert.That(secondary, Is.Not.Null.And.Not.Empty);
        Assert.That(primary, Is.Not.EqualTo(secondary), "Primary and secondary should be different");

        Console.WriteLine($"[GetTexturesInvolved] At ({testPos.X},{testPos.Y}): {primary} -> {secondary}");
    }

    [Test]
    public void GetTexturesInvolved_NoBlend_ReturnsOnlyPrimary()
    {
        // Arrange - 没有混合的位置
        int x = 50, y = 50;

        // Act
        var (primary, secondary) = _testMap!.GetTexturesInvolved(x, y);

        // Assert
        Assert.That(primary, Is.Not.Null.And.Not.Empty);
        Assert.That(secondary, Is.Null, "Should not have secondary texture without blend");

        Console.WriteLine($"[GetTexturesInvolved] At ({x},{y}): {primary} (no blend)");
    }

    #endregion

    #region GetBlendsByTexturePair Tests

    [Test]
    public void GetBlendsByTexturePair_ExistingPair_ReturnsPositions()
    {
        // Arrange - 获取统计信息找到一个存在的纹理对
        var stats = _testMap!.GetBlendStatistics();
        Assert.That(stats.BlendsByTexturePair.Count, Is.GreaterThan(0));

        var firstPair = stats.BlendsByTexturePair.First();
        var parts = firstPair.Key.Split(" -> ");
        var primary = parts[0];
        var secondary = parts[1];

        // Act
        var positions = _testMap.GetBlendsByTexturePair(primary, secondary);

        // Assert
        Assert.That(positions.Count, Is.EqualTo(firstPair.Value),
            $"Should find {firstPair.Value} positions for pair {firstPair.Key}");

        Console.WriteLine($"[GetBlendsByTexturePair] Found {positions.Count} blends for {primary} -> {secondary}");
    }

    [Test]
    public void GetBlendsByTexturePair_NonExistentPair_ReturnsEmpty()
    {
        // Arrange - 使用不存在的纹理对
        string primary = "NonExistent1";
        string secondary = "NonExistent2";

        // Act
        var positions = _testMap!.GetBlendsByTexturePair(primary, secondary);

        // Assert
        Assert.That(positions.Count, Is.EqualTo(0), "Should return empty for non-existent pair");
        Console.WriteLine($"[GetBlendsByTexturePair] No blends for non-existent pair as expected");
    }

    #endregion

    #region GetBlendMapSnapshot Tests

    [Test]
    public void GetBlendMapSnapshot_ReturnsCompleteSnapshot()
    {
        // Act
        var snapshot = _testMap!.GetBlendMapSnapshot();

        // Assert
        Assert.That(snapshot, Is.Not.Null);
        Assert.That(snapshot.Width, Is.EqualTo(_testMap.MapWidth));
        Assert.That(snapshot.Height, Is.EqualTo(_testMap.MapHeight));
        Assert.That(snapshot.BlendIndices.GetLength(0), Is.EqualTo(_testMap.MapWidth));
        Assert.That(snapshot.BlendIndices.GetLength(1), Is.EqualTo(_testMap.MapHeight));
        Assert.That(snapshot.PrimaryTextureIndices.GetLength(0), Is.EqualTo(_testMap.MapWidth));
        Assert.That(snapshot.PrimaryTextureIndices.GetLength(1), Is.EqualTo(_testMap.MapHeight));
        Assert.That(snapshot.BlendInfos.Count, Is.EqualTo(_testMap.GetUniqueBlendInfoCount()));
        Assert.That(snapshot.TextureNames.Count, Is.GreaterThan(0));

        Console.WriteLine($"[GetBlendMapSnapshot] Snapshot created:");
        Console.WriteLine($"  Size: {snapshot.Width}x{snapshot.Height}");
        Console.WriteLine($"  BlendInfos: {snapshot.BlendInfos.Count}");
        Console.WriteLine($"  Textures: {snapshot.TextureNames.Count}");
    }

    [Test]
    public void GetBlendMapSnapshot_IndicesMatchOriginal()
    {
        // Act
        var snapshot = _testMap!.GetBlendMapSnapshot();

        // Assert - 随机检查几个位置
        var random = new Random(42);
        for (int i = 0; i < 10; i++)
        {
            int x = random.Next(_testMap.MapWidth);
            int y = random.Next(_testMap.MapHeight);

            ushort originalBlend = _testMap.GetTileBlend(x, y);
            ushort snapshotBlend = snapshot.BlendIndices[x, y];

            Assert.That(snapshotBlend, Is.EqualTo(originalBlend),
                $"Blend index at ({x},{y}) should match");
        }

        Console.WriteLine($"[GetBlendMapSnapshot] Random sample verification passed");
    }

    #endregion

    #region Helper Method Tests

    [Test]
    public void GetTotalBlendCount_MatchesManualCount()
    {
        // Arrange - 手动计数
        int manualCount = 0;
        for (int y = 0; y < _testMap!.MapHeight; y++)
        {
            for (int x = 0; x < _testMap.MapWidth; x++)
            {
                if (_testMap.HasBlendAt(x, y))
                {
                    manualCount++;
                }
            }
        }

        // Act
        int totalCount = _testMap.GetTotalBlendCount();

        // Assert
        Assert.That(totalCount, Is.EqualTo(manualCount),
            "GetTotalBlendCount should match manual count");
        Console.WriteLine($"[GetTotalBlendCount] Count verified: {totalCount}");
    }

    [Test]
    public void HasBlendAt_CorrectlyIdentifiesBlends()
    {
        // Arrange
        var positions = _testMap!.GetAllBlendPositions();
        Assert.That(positions.Count, Is.GreaterThan(0));

        // Act & Assert - 已知有混合的位置
        var posWithBlend = positions[0];
        Assert.That(_testMap.HasBlendAt(posWithBlend.X, posWithBlend.Y), Is.True,
            "Should return true for position with blend");

        // 已知没有混合的位置
        Assert.That(_testMap.HasBlendAt(50, 50), Is.False,
            "Should return false for position without blend");

        // 边界外
        Assert.That(_testMap.HasBlendAt(-1, -1), Is.False,
            "Should return false for out-of-bounds position");
        Assert.That(_testMap.HasBlendAt(_testMap.MapWidth, _testMap.MapHeight), Is.False,
            "Should return false for out-of-bounds position");

        Console.WriteLine($"[HasBlendAt] Verification passed");
    }

    #endregion

    #region Integration Test

    [Test]
    public void IntegrationTest_AllQueryFunctions_WorkTogether()
    {
        Console.WriteLine("[Integration] Testing all query functions together...");

        // 1. 获取统计信息
        var stats = _testMap!.GetBlendStatistics();
        Console.WriteLine($"  Stats: {stats.TotalBlendCount} blends, {stats.UniqueBlendInfoCount} unique");

        // 2. 获取所有位置
        var allPositions = _testMap.GetAllBlendPositions();
        Console.WriteLine($"  All positions: {allPositions.Count} found");

        // 3. 获取详细信息
        if (allPositions.Count > 0)
        {
            var detail = _testMap.GetBlendDetailInfo(allPositions[0].X, allPositions[0].Y);
            Console.WriteLine($"  Detail at ({allPositions[0].X},{allPositions[0].Y}): " +
                            $"{detail?.PrimaryTextureName} -> {detail?.SecondaryTextureName}");

            // 4. 使用该纹理对查询
            if (detail != null)
            {
                var pairPositions = _testMap.GetBlendsByTexturePair(
                    detail.PrimaryTextureName, detail.SecondaryTextureName);
                Console.WriteLine($"  Positions for pair: {pairPositions.Count}");
            }
        }

        // 5. 获取快照
        var snapshot = _testMap.GetBlendMapSnapshot();
        Console.WriteLine($"  Snapshot: {snapshot.Width}x{snapshot.Height}");

        // 6. 区域查询
        var regionBlends = _testMap.GetBlendsInRegion(18, 18, 24, 24);
        Console.WriteLine($"  Region (18,18)-(24,24): {regionBlends.Count} blends");

        // Assert - 确保所有数据一致
        Assert.That(stats.TotalBlendCount, Is.EqualTo(allPositions.Count));
        Assert.That(snapshot.BlendInfos.Count, Is.EqualTo(stats.UniqueBlendInfoCount));

        Console.WriteLine("[Integration] All query functions work correctly together!");
    }

    #endregion
}
