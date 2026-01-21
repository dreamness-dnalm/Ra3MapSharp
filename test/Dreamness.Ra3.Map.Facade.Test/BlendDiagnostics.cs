using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;

namespace Dreamness.Ra3.Map.Facade.Test;

/// <summary>
/// 诊断工具：创建简单的纹理混合测试地图
/// </summary>
public class BlendDiagnostics
{
    [Test]
    public void CreateSimpleBlendTestMap()
    {
        Console.WriteLine("=== 创建简单混合测试地图（使用修复后的魔数）===");

        // 创建一个小地图
        var testMap = Ra3MapFacade.NewMap(32, 32, 4, 2, "Dirt_Yucatan03");

        // 添加第二种纹理
        testMap.SetTileTexture(10, 10, "Grass_Yucatan01");
        testMap.SetTileTexture(10, 10, "Dirt_Yucatan03"); // 恢复

        Console.WriteLine("创建一个3x3的草地区域在泥土中...");

        // 在中心创建一个3x3的草地区域
        for (int y = 15; y <= 17; y++)
        {
            for (int x = 15; x <= 17; x++)
            {
                testMap.SetTileTexture(x, y, "Grass_Yucatan01");
            }
        }

        Console.WriteLine("运行自动混合检测...");
        testMap.AutoDetectBlendsInRegion(14, 14, 19, 19);

        // 检查生成的混合
        int blendCount = 0;
        Console.WriteLine("\n生成的混合:");
        for (int y = 14; y < 19; y++)
        {
            for (int x = 14; x < 19; x++)
            {
                var blendInfo = testMap.GetBlendInfo(x, y);
                if (blendInfo != null)
                {
                    Console.WriteLine($"({x},{y}): {blendInfo.BlendDirection} - I3=0x{blendInfo.I3:X8}, I4=0x{blendInfo.I4:X8}");

                    // 验证魔数
                    Assert.That(blendInfo.I3, Is.EqualTo(0xFFFFFFFF),
                        $"BlendInfo at ({x},{y}) has wrong I3 magic number!");
                    Assert.That(blendInfo.I4, Is.EqualTo(0x7ACDCD00),
                        $"BlendInfo at ({x},{y}) has wrong I4 magic number!");

                    blendCount++;
                }
            }
        }

        Console.WriteLine($"\n总共生成 {blendCount} 个混合");
        Console.WriteLine("✓ 所有BlendInfo的魔数验证通过！");

        // 保存地图
        testMap.SaveAs(Ra3PathUtil.RA3MapFolder, "BlendTest_FixedMagicNumbers");
        Console.WriteLine($"\n地图已保存到: {Ra3PathUtil.RA3MapFolder}\\BlendTest_FixedMagicNumbers");
        Console.WriteLine("请在RA3地图编辑器中打开此地图，检查是否有混合效果");
        Console.WriteLine("\n如果之前的地图没有混合效果，现在应该可以正常显示了！");
    }

    [Test]
    public void ReprocessExistingMap()
    {
        Console.WriteLine("=== 重新处理现有地图（修复魔数）===");

        // 打开现有地图
        var existingMap = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "tile_test_01");

        Console.WriteLine("重新运行自动混合检测（使用正确的魔数）...");
        existingMap.AutoDetectBlendsEntireMap();

        // 验证魔数
        int validBlends = 0;
        for (int y = 1; y < existingMap.MapHeight - 1; y++)
        {
            for (int x = 1; x < existingMap.MapWidth - 1; x++)
            {
                var blendInfo = existingMap.GetBlendInfo(x, y);
                if (blendInfo != null)
                {
                    Assert.That(blendInfo.I3, Is.EqualTo(0xFFFFFFFF),
                        $"BlendInfo at ({x},{y}) has wrong I3!");
                    Assert.That(blendInfo.I4, Is.EqualTo(0x7ACDCD00),
                        $"BlendInfo at ({x},{y}) has wrong I4!");
                    validBlends++;
                }
            }
        }

        Console.WriteLine($"验证了 {validBlends} 个混合信息，魔数全部正确！");

        // 保存
        existingMap.SaveAs(Ra3PathUtil.RA3MapFolder, "tile_test_FIXED");
        Console.WriteLine($"\n地图已保存到: {Ra3PathUtil.RA3MapFolder}\\tile_test_FIXED");
        Console.WriteLine("这个地图文件中的所有BlendInfo现在都有正确的魔数了！");
    }
}
