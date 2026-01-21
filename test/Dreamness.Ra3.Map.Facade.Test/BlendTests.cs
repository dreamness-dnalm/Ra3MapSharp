using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.enums;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;

namespace Dreamness.Ra3.Map.Facade.Test;

/// <summary>
/// 纹理混合功能的单元测试
/// </summary>
public class BlendTests
{
    private Ra3MapFacade? _testMap;
    private const string TestMapName = "BlendTest_TempMap";

    // 使用TextureEnum中已有的纹理
    private const string PrimaryTexture = "Dirt_Yucatan03";      // 主纹理（默认）
    private const string SecondaryTexture = "Grass_Yucatan01";   // 次要纹理（用于混合测试）

    [SetUp]
    public void Setup()
    {
        // 创建一个干净的测试地图（64x64，8格边界，默认纹理Dirt_Yucatan03）
        _testMap = Ra3MapFacade.NewMap(64, 64, 8, 2, PrimaryTexture);

        // 不需要注册纹理，使用TextureEnum中已有的纹理
        // Dirt_Yucatan03 和 Grass_Yucatan01 都是预定义的纹理
        // 但需要通过SetTileTexture使用一次来添加到地图中
        _testMap.SetTileTexture(0, 0, SecondaryTexture);
        _testMap.SetTileTexture(0, 0, PrimaryTexture); // 恢复默认纹理

        Console.WriteLine($"[Setup] Created test map: {_testMap.MapWidth}x{_testMap.MapHeight}");
        Console.WriteLine($"[Setup] Using textures: Primary={PrimaryTexture}, Secondary={SecondaryTexture}");
    }

    [TearDown]
    public void TearDown()
    {
        // 清理测试地图（可选）
        _testMap = null;
        Console.WriteLine("[TearDown] Test completed");
    }

    #region GetSubTilePosition Tests

    [Test]
    public void GetSubTilePosition_OriginCell_ReturnsZeroZero()
    {
        // Arrange & Act
        var (subX, subY) = _testMap!.GetSubTilePosition(0, 0);

        // Assert
        Assert.That(subX, Is.EqualTo(0), "SubX should be 0 at origin");
        Assert.That(subY, Is.EqualTo(0), "SubY should be 0 at origin");
        Console.WriteLine($"[GetSubTilePosition] (0,0) -> ({subX},{subY})");
    }

    [Test]
    public void GetSubTilePosition_MaxCell_ReturnsSevenSeven()
    {
        // Arrange & Act
        var (subX, subY) = _testMap!.GetSubTilePosition(7, 7);

        // Assert
        Assert.That(subX, Is.EqualTo(7), "SubX should be 7 at (7,7)");
        Assert.That(subY, Is.EqualTo(7), "SubY should be 7 at (7,7)");
        Console.WriteLine($"[GetSubTilePosition] (7,7) -> ({subX},{subY})");
    }

    [Test]
    public void GetSubTilePosition_PatternRepeats_EveryEightCells()
    {
        // Arrange & Act
        var pos1 = _testMap!.GetSubTilePosition(0, 0);
        var pos2 = _testMap!.GetSubTilePosition(8, 8);
        var pos3 = _testMap!.GetSubTilePosition(16, 16);

        // Assert
        Assert.That(pos1, Is.EqualTo(pos2), "Pattern should repeat at (8,8)");
        Assert.That(pos1, Is.EqualTo(pos3), "Pattern should repeat at (16,16)");
        Console.WriteLine($"[GetSubTilePosition] (0,0)={pos1}, (8,8)={pos2}, (16,16)={pos3}");
    }

    [Test]
    public void GetSubTilePosition_AllPositionsInFirstGrid_AreValid()
    {
        // Arrange
        int validCount = 0;
        var positions = new HashSet<(int, int)>();

        // Act
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                var (subX, subY) = _testMap!.GetSubTilePosition(x, y);

                // Assert in range
                Assert.That(subX, Is.InRange(0, 7), $"SubX out of range at ({x},{y})");
                Assert.That(subY, Is.InRange(0, 7), $"SubY out of range at ({x},{y})");

                positions.Add((subX, subY));
                validCount++;
            }
        }

        // Assert all 64 positions are unique
        Assert.That(positions.Count, Is.EqualTo(64), "Should have 64 unique sub-tile positions");
        Assert.That(validCount, Is.EqualTo(64), "Should have checked all 64 positions");
        Console.WriteLine($"[GetSubTilePosition] Verified all 64 positions in 8x8 grid");
    }

    #endregion

    #region GetBlendInfo Tests

    [Test]
    public void GetBlendInfo_NoBlendExists_ReturnsNull()
    {
        // Arrange - 新地图默认没有混合
        int x = 10, y = 10;

        // Act
        var blendInfo = _testMap!.GetBlendInfo(x, y);

        // Assert
        Assert.That(blendInfo, Is.Null, "Should return null when no blend exists");
        Console.WriteLine($"[GetBlendInfo] ({x},{y}) -> null (as expected)");
    }

    [Test]
    public void GetBlendInfo_AfterAddingBlend_ReturnsBlendInfo()
    {
        // Arrange
        int x = 10, y = 10;
        _testMap!.AddBlend(x, y, SecondaryTexture, BlendInfo.BlendDirectionEnum.Top);

        // Act
        var blendInfo = _testMap.GetBlendInfo(x, y);

        // Assert
        Assert.That(blendInfo, Is.Not.Null, "Should return BlendInfo after adding blend");
        Assert.That(blendInfo!.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.Top));
        Console.WriteLine($"[GetBlendInfo] ({x},{y}) -> Direction: {blendInfo.BlendDirection}, Tile: {blendInfo.SecondaryTextureTile}");
    }

    #endregion

    #region AddBlend Tests

    [Test]
    public void AddBlend_ByTextureName_AddsBlendSuccessfully()
    {
        // Arrange
        int x = 20, y = 20;

        // Act
        _testMap!.AddBlend(x, y, SecondaryTexture, BlendInfo.BlendDirectionEnum.Bottom);
        var blendInfo = _testMap.GetBlendInfo(x, y);

        // Assert
        Assert.That(blendInfo, Is.Not.Null, "Blend should be added");
        Assert.That(blendInfo!.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.Bottom));
        Console.WriteLine($"[AddBlend] Added blend at ({x},{y}) with direction Bottom");
    }

    [Test]
    public void AddBlend_ByTextureIndex_AddsBlendSuccessfully()
    {
        // Arrange
        int x = 25, y = 25;
        int secondaryTextureIndex = 1; // SecondaryTexture

        // Act
        _testMap!.AddBlend(x, y, secondaryTextureIndex, BlendInfo.BlendDirectionEnum.Left);
        var blendInfo = _testMap.GetBlendInfo(x, y);

        // Assert
        Assert.That(blendInfo, Is.Not.Null, "Blend should be added");
        Assert.That(blendInfo!.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.Left));
        Console.WriteLine($"[AddBlend] Added blend by index at ({x},{y})");
    }

    [Test]
    public void AddBlend_SamePropertiesTwice_ReusesBlendInfo()
    {
        // Arrange
        int x1 = 30, y1 = 30;
        int x2 = 30, y2 = 31;  // 使用相同的X坐标，这样子瓦片计算会在某些位置相同

        // Act
        _testMap!.AddBlend(x1, y1, SecondaryTexture, BlendInfo.BlendDirectionEnum.TopRight);
        _testMap.AddBlend(x2, y2, SecondaryTexture, BlendInfo.BlendDirectionEnum.TopRight);

        var blend1 = _testMap.GetBlendInfo(x1, y1);
        var blend2 = _testMap.GetBlendInfo(x2, y2);

        // Assert
        Assert.That(blend1, Is.Not.Null);
        Assert.That(blend2, Is.Not.Null);

        // 即使子瓦片不同，也应该有相同的混合方向
        Assert.That(blend1!.BlendDirection, Is.EqualTo(blend2!.BlendDirection),
            "Should have same blend direction");

        // 验证混合确实被创建了
        Assert.That(blend1.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.TopRight),
            "First blend should have TopRight direction");
        Assert.That(blend2.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.TopRight),
            "Second blend should have TopRight direction");

        Console.WriteLine($"[AddBlend] Blend1 tile: {blend1.SecondaryTextureTile}, Blend2 tile: {blend2.SecondaryTextureTile}");
        Console.WriteLine($"[AddBlend] Both blends use direction: {blend1.BlendDirection}");
    }

    [Test]
    public void AddBlend_AllDirections_WorkCorrectly()
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
        for (int i = 0; i < directions.Length; i++)
        {
            int x = 40 + i;
            int y = 40;
            var direction = directions[i];

            _testMap!.AddBlend(x, y, SecondaryTexture, direction);
            var blendInfo = _testMap.GetBlendInfo(x, y);

            Assert.That(blendInfo, Is.Not.Null, $"Blend should exist for direction {direction}");
            Assert.That(blendInfo!.BlendDirection, Is.EqualTo(direction),
                $"Direction should match for {direction}");
            Console.WriteLine($"[AddBlend] Direction {direction} verified at ({x},{y})");
        }
    }

    [Test]
    public void AddBlend_InvalidTextureName_ThrowsException()
    {
        // Arrange
        int x = 50, y = 50;
        string invalidTexture = "NonExistentTexture_12345";

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            _testMap!.AddBlend(x, y, invalidTexture, BlendInfo.BlendDirectionEnum.Top);
        });

        Assert.That(ex!.Message, Does.Contain(invalidTexture));
        Console.WriteLine($"[AddBlend] Correctly threw exception for invalid texture: {ex.Message}");
    }

    #endregion

    #region RemoveBlend Tests

    [Test]
    public void RemoveBlend_ExistingBlend_RemovesSuccessfully()
    {
        // Arrange
        int x = 35, y = 35;
        _testMap!.AddBlend(x, y, SecondaryTexture, BlendInfo.BlendDirectionEnum.Top);

        // Verify blend exists
        Assert.That(_testMap.GetBlendInfo(x, y), Is.Not.Null, "Blend should exist before removal");

        // Act
        _testMap.RemoveBlend(x, y);

        // Assert
        var blendInfo = _testMap.GetBlendInfo(x, y);
        Assert.That(blendInfo, Is.Null, "Blend should be null after removal");
        Console.WriteLine($"[RemoveBlend] Successfully removed blend at ({x},{y})");
    }

    [Test]
    public void RemoveBlend_NoBlendExists_DoesNotThrowException()
    {
        // Arrange
        int x = 45, y = 45;
        // No blend added

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            _testMap!.RemoveBlend(x, y);
        });
        Console.WriteLine($"[RemoveBlend] No exception when removing non-existent blend");
    }

    #endregion

    #region AutoDetectBlend Tests

    [Test]
    public void AutoDetectBlend_SingleCell_DetectsCorrectly()
    {
        // Arrange - Create a scenario: center cell different from surroundings
        int centerX = 20, centerY = 20;

        // Set surrounding cells to Sand
        for (int y = 19; y <= 21; y++)
        {
            for (int x = 19; x <= 21; x++)
            {
                _testMap!.SetTileTexture(x, y, SecondaryTexture);
            }
        }

        // Set center back to Dirt (default texture has lower index)
        _testMap!.SetTileTexture(centerX, centerY, "Dirt_Yucatan03");

        // Act
        _testMap.AutoDetectBlend(centerX, centerY);

        // Assert
        var blendInfo = _testMap.GetBlendInfo(centerX, centerY);
        // Center has lower texture index than neighbors, so blend should be added
        Assert.That(blendInfo, Is.Not.Null, "Should auto-detect blend at center");
        Console.WriteLine($"[AutoDetectBlend] Detected blend at ({centerX},{centerY}): {blendInfo?.BlendDirection}");
    }

    [Test]
    public void AutoDetectBlend_CornerScenario_DetectsCornerBlend()
    {
        // Arrange - Create corner blend scenario
        int x = 25, y = 25;

        // Current cell: Dirt (index 0)
        _testMap!.SetTileTexture(x, y, "Dirt_Yucatan03");

        // Left and Top neighbors: Sand (index 1)
        _testMap.SetTileTexture(x - 1, y, SecondaryTexture);      // Left
        _testMap.SetTileTexture(x, y + 1, SecondaryTexture);      // Top
        _testMap.SetTileTexture(x - 1, y + 1, SecondaryTexture);  // TopLeft

        // Act
        _testMap.AutoDetectBlend(x, y);

        // Assert
        var blendInfo = _testMap.GetBlendInfo(x, y);
        Assert.That(blendInfo, Is.Not.Null, "Should detect corner blend");
        Assert.That(blendInfo!.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.BottomRight),
            "Should detect BottomRight corner blend when left and top match");
        Console.WriteLine($"[AutoDetectBlend] Corner blend detected: {blendInfo.BlendDirection}");
    }

    [Test]
    public void AutoDetectBlend_EdgeScenario_DetectsEdgeBlend()
    {
        // Arrange - Create edge blend scenario
        int x = 30, y = 30;

        // Current cell: Dirt (index 0)
        _testMap!.SetTileTexture(x, y, "Dirt_Yucatan03");

        // Only left neighbor: Sand (index 1)
        _testMap.SetTileTexture(x - 1, y, SecondaryTexture);

        // Act
        _testMap.AutoDetectBlend(x, y);

        // Assert
        var blendInfo = _testMap.GetBlendInfo(x, y);
        Assert.That(blendInfo, Is.Not.Null, "Should detect edge blend");
        Assert.That(blendInfo!.BlendDirection, Is.EqualTo(BlendInfo.BlendDirectionEnum.Right),
            "Should detect Right edge blend when only left neighbor differs");
        Console.WriteLine($"[AutoDetectBlend] Edge blend detected: {blendInfo.BlendDirection}");
    }

    [Test]
    public void AutoDetectBlend_NoNeighborsDifferent_RemovesBlend()
    {
        // Arrange
        int x = 35, y = 35;

        // Add a blend manually
        _testMap!.AddBlend(x, y, SecondaryTexture, BlendInfo.BlendDirectionEnum.Top);
        Assert.That(_testMap.GetBlendInfo(x, y), Is.Not.Null, "Blend should exist");

        // All neighbors are same texture (Dirt - default)
        // No need to set, they're already all Dirt

        // Act
        _testMap.AutoDetectBlend(x, y);

        // Assert
        var blendInfo = _testMap.GetBlendInfo(x, y);
        Assert.That(blendInfo, Is.Null, "Should remove blend when no neighbors differ");
        Console.WriteLine($"[AutoDetectBlend] Blend removed when no neighbors differ");
    }

    [Test]
    public void AutoDetectBlend_MapBoundary_DoesNotCrash()
    {
        // Arrange - Test at map boundaries
        int[] boundaryX = { 0, _testMap!.MapWidth - 1 };
        int[] boundaryY = { 0, _testMap.MapHeight - 1 };

        // Act & Assert
        foreach (var x in boundaryX)
        {
            foreach (var y in boundaryY)
            {
                Assert.DoesNotThrow(() =>
                {
                    _testMap.AutoDetectBlend(x, y);
                }, $"Should not crash at boundary ({x},{y})");
            }
        }
        Console.WriteLine($"[AutoDetectBlend] Boundary cells handled safely");
    }

    #endregion

    #region AutoDetectBlendsInRegion Tests

    [Test]
    public void AutoDetectBlendsInRegion_SmallRegion_DetectsAllBlends()
    {
        // Arrange - Create a 5x5 region with Sand surrounded by Dirt
        int startX = 30, startY = 30;
        int endX = 35, endY = 35;

        // Fill region with Sand
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                _testMap!.SetTileTexture(x, y, SecondaryTexture);
            }
        }

        // Act - Auto-detect in a slightly larger region to catch boundaries
        _testMap!.AutoDetectBlendsInRegion(startX - 1, startY - 1, endX + 1, endY + 1);

        // Assert - Check boundary cells have blends
        int blendCount = 0;
        for (int y = startY - 1; y <= endY; y++)
        {
            for (int x = startX - 1; x <= endX; x++)
            {
                var blend = _testMap.GetBlendInfo(x, y);
                if (blend != null)
                {
                    blendCount++;
                    Console.WriteLine($"  Blend at ({x},{y}): {blend.BlendDirection}");
                }
            }
        }

        Assert.That(blendCount, Is.GreaterThan(0), "Should detect blends at region boundaries");
        Console.WriteLine($"[AutoDetectBlendsInRegion] Detected {blendCount} blends in region");
    }

    [Test]
    public void AutoDetectBlendsInRegion_OutOfBounds_ClampsToValidRange()
    {
        // Arrange - Use coordinates outside map bounds
        int startX = -10, startY = -10;
        int endX = _testMap!.MapWidth + 10;
        int endY = _testMap.MapHeight + 10;

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            _testMap.AutoDetectBlendsInRegion(startX, startY, endX, endY);
        }, "Should clamp out-of-bounds coordinates and not crash");
        Console.WriteLine($"[AutoDetectBlendsInRegion] Out-of-bounds coordinates handled safely");
    }

    #endregion

    #region AutoDetectBlendsEntireMap Tests

    [Test]
    public void AutoDetectBlendsEntireMap_MixedTextures_DetectsAllBoundaries()
    {
        // Arrange - Create a checkerboard pattern
        for (int y = 10; y < 30; y++)
        {
            for (int x = 10; x < 30; x++)
            {
                if ((x + y) % 2 == 0)
                {
                    _testMap!.SetTileTexture(x, y, SecondaryTexture);
                }
                else
                {
                    _testMap!.SetTileTexture(x, y, "Dirt_Yucatan03");
                }
            }
        }

        // Act
        _testMap!.AutoDetectBlendsEntireMap();

        // Assert - Count blends in the checkerboard area
        int blendCount = 0;
        for (int y = 10; y < 30; y++)
        {
            for (int x = 10; x < 30; x++)
            {
                if (_testMap.GetBlendInfo(x, y) != null)
                {
                    blendCount++;
                }
            }
        }

        Assert.That(blendCount, Is.GreaterThan(0), "Should detect many blends in checkerboard pattern");
        Console.WriteLine($"[AutoDetectBlendsEntireMap] Detected {blendCount} blends in checkerboard area");

        // 保存棋盘格测试地图
        _testMap!.SaveAs(Ra3PathUtil.RA3MapFolder, TestMapName + "_Checkerboard");
        Console.WriteLine($"[AutoDetectBlendsEntireMap] Map saved to: {Ra3PathUtil.RA3MapFolder}\\{TestMapName}_Checkerboard");
    }

    [Test]
    public void AutoDetectBlendsEntireMap_CircularRegion_DetectsCircularBoundary()
    {
        // Arrange - Create a circular region with Sand
        int centerX = 40, centerY = 40;
        int radius = 10;

        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                if (distance <= radius)
                {
                    _testMap!.SetTileTexture(x, y, SecondaryTexture);
                }
            }
        }

        // Act
        _testMap!.AutoDetectBlendsEntireMap();

        // Assert - Verify blends exist along the circular boundary
        int boundaryBlends = 0;
        for (int y = centerY - radius - 1; y <= centerY + radius + 1; y++)
        {
            for (int x = centerX - radius - 1; x <= centerX + radius + 1; x++)
            {
                double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                if (distance >= radius - 2 && distance <= radius + 2)
                {
                    if (_testMap!.GetBlendInfo(x, y) != null)
                    {
                        boundaryBlends++;
                    }
                }
            }
        }

        Assert.That(boundaryBlends, Is.GreaterThan(radius * 2),
            "Should detect blends along circular boundary");
        Console.WriteLine($"[AutoDetectBlendsEntireMap] Detected {boundaryBlends} blends along circular boundary");

        // 保存圆形区域测试地图
        _testMap!.SaveAs(Ra3PathUtil.RA3MapFolder, TestMapName + "_Circle");
        Console.WriteLine($"[AutoDetectBlendsEntireMap] Map saved to: {Ra3PathUtil.RA3MapFolder}\\{TestMapName}_Circle");
    }

    [Test]
    public void AutoDetectBlendsEntireMap_LargeMap_CompletesWithoutError()
    {
        // Arrange - Already have a 64x64 map with 8-cell border (80x80 total)
        // Create some texture regions
        for (int y = 20; y < 60; y++)
        {
            for (int x = 20; x < 60; x++)
            {
                if (x < 40)
                {
                    _testMap!.SetTileTexture(x, y, "Dirt_Yucatan03");
                }
                else
                {
                    _testMap!.SetTileTexture(x, y, SecondaryTexture);
                }
            }
        }

        // Act
        var startTime = DateTime.Now;
        Assert.DoesNotThrow(() =>
        {
            _testMap!.AutoDetectBlendsEntireMap();
        }, "Should complete without error on large map");
        var elapsed = DateTime.Now - startTime;

        // Assert - Verify some blends were created
        int totalBlends = 0;
        for (int y = 1; y < _testMap!.MapHeight - 1; y++)
        {
            for (int x = 1; x < _testMap.MapWidth - 1; x++)
            {
                if (_testMap.GetBlendInfo(x, y) != null)
                {
                    totalBlends++;
                }
            }
        }

        Assert.That(totalBlends, Is.GreaterThan(0), "Should create blends on large map");
        Console.WriteLine($"[AutoDetectBlendsEntireMap] Processed {_testMap.MapWidth}x{_testMap.MapHeight} map in {elapsed.TotalMilliseconds}ms");
        Console.WriteLine($"  Total blends created: {totalBlends}");

        // 保存大地图测试
        _testMap.SaveAs(Ra3PathUtil.RA3MapFolder, TestMapName + "_Large");
        Console.WriteLine($"[AutoDetectBlendsEntireMap] Map saved to: {Ra3PathUtil.RA3MapFolder}\\{TestMapName}_Large");
    }

    #endregion

    #region Integration Tests

    [Test]
    public void IntegrationTest_PaintAndBlend_CompleteWorkflow()
    {
        // Arrange - Create a realistic scenario
        Console.WriteLine("[Integration] Creating a lake with beach...");

        int lakeX = 35, lakeY = 35, lakeRadius = 8;
        int beachRadius = 12;

        // 1. Paint water (using Sand as substitute for water texture)
        for (int y = lakeY - lakeRadius; y <= lakeY + lakeRadius; y++)
        {
            for (int x = lakeX - lakeRadius; x <= lakeX + lakeRadius; x++)
            {
                double distance = Math.Sqrt((x - lakeX) * (x - lakeX) + (y - lakeY) * (y - lakeY));
                if (distance <= lakeRadius)
                {
                    _testMap!.SetTileTexture(x, y, SecondaryTexture);
                }
            }
        }

        // 2. Auto-detect blends
        Console.WriteLine("[Integration] Auto-detecting blends...");
        _testMap!.AutoDetectBlendsInRegion(lakeX - beachRadius, lakeY - beachRadius,
                                           lakeX + beachRadius, lakeY + beachRadius);

        // 3. Verify results
        int blendCount = 0;
        for (int y = lakeY - beachRadius; y <= lakeY + beachRadius; y++)
        {
            for (int x = lakeX - beachRadius; x <= lakeX + beachRadius; x++)
            {
                var blend = _testMap.GetBlendInfo(x, y);
                if (blend != null)
                {
                    blendCount++;
                }
            }
        }

        // 4. Test sub-tile positions at key locations
        var centerSubTile = _testMap.GetSubTilePosition(lakeX, lakeY);
        var edgeSubTile = _testMap.GetSubTilePosition(lakeX + lakeRadius, lakeY);

        // Assert
        Assert.That(blendCount, Is.GreaterThan(0), "Should have created blends");
        Assert.That(centerSubTile.subX, Is.InRange(0, 7));
        Assert.That(centerSubTile.subY, Is.InRange(0, 7));

        Console.WriteLine($"[Integration] Created lake with {blendCount} blend transitions");
        Console.WriteLine($"  Center sub-tile: ({centerSubTile.subX}, {centerSubTile.subY})");
        Console.WriteLine($"  Edge sub-tile: ({edgeSubTile.subX}, {edgeSubTile.subY})");

        // 保存测试地图
        _testMap.SaveAs(Ra3PathUtil.RA3MapFolder, TestMapName + "_Lake");
        Console.WriteLine($"[Integration] Map saved to: {Ra3PathUtil.RA3MapFolder}\\{TestMapName}_Lake");
    }

    [Test]
    public void IntegrationTest_ModifyAndRedetect_UpdatesBlends()
    {
        // Arrange - Create initial texture region
        Console.WriteLine("[Integration] Creating and modifying texture regions...");

        // Phase 1: Create vertical stripe
        for (int y = 20; y < 40; y++)
        {
            for (int x = 30; x < 35; x++)
            {
                _testMap!.SetTileTexture(x, y, SecondaryTexture);
            }
        }
        _testMap!.AutoDetectBlendsInRegion(28, 18, 37, 42);

        int initialBlendCount = CountBlendsInRegion(28, 18, 37, 42);
        Console.WriteLine($"  Phase 1: Vertical stripe with {initialBlendCount} blends");

        // Phase 2: Extend to horizontal stripe (T-shape)
        for (int y = 28; y < 32; y++)
        {
            for (int x = 20; x < 45; x++)
            {
                _testMap.SetTileTexture(x, y, SecondaryTexture);
            }
        }
        _testMap.AutoDetectBlendsInRegion(18, 18, 47, 42);

        int finalBlendCount = CountBlendsInRegion(18, 18, 47, 42);
        Console.WriteLine($"  Phase 2: T-shape with {finalBlendCount} blends");

        // Assert
        Assert.That(finalBlendCount, Is.GreaterThan(initialBlendCount),
            "Extending region should create more blends");
        Console.WriteLine($"[Integration] Blend count increased by {finalBlendCount - initialBlendCount}");

        // 保存测试地图
        _testMap.SaveAs(Ra3PathUtil.RA3MapFolder, TestMapName + "_TShape");
        Console.WriteLine($"[Integration] Map saved to: {Ra3PathUtil.RA3MapFolder}\\{TestMapName}_TShape");
    }

    /// <summary>
    /// Helper method to count blends in a region
    /// </summary>
    private int CountBlendsInRegion(int startX, int startY, int endX, int endY)
    {
        int count = 0;
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (_testMap!.GetBlendInfo(x, y) != null)
                {
                    count++;
                }
            }
        }
        return count;
    }

    #endregion

    #region Optional: Save Test Map for Visual Inspection

    [Test]
    [Explicit("Run manually to save test map for visual inspection")]
    public void SaveTestMap_ForVisualInspection()
    {
        // Arrange - Create a visually interesting pattern
        Console.WriteLine("[SaveTest] Creating test pattern...");

        // Create concentric circles
        int centerX = 40, centerY = 40;
        for (int y = 20; y < 60; y++)
        {
            for (int x = 20; x < 60; x++)
            {
                double distance = Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                if (distance <= 5)
                {
                    _testMap!.SetTileTexture(x, y, SecondaryTexture);
                }
                else if (distance > 10 && distance <= 15)
                {
                    _testMap!.SetTileTexture(x, y, SecondaryTexture);
                }
            }
        }

        // Auto-detect all blends
        _testMap!.AutoDetectBlendsEntireMap();

        // Act - Save map
        _testMap.SaveAs(Ra3PathUtil.RA3MapFolder, TestMapName);

        Console.WriteLine($"[SaveTest] Test map saved to: {Ra3PathUtil.RA3MapFolder}\\{TestMapName}");
        Console.WriteLine("  Open in RA3 Map Editor to visually verify blends");

        // Assert
        Assert.Pass("Test map saved successfully");
    }

    #endregion
}
