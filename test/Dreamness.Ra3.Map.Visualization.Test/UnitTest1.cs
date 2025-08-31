using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Visualization.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }
    
    /// <summary>
    /// 测试函数：生成高度图并保存为 PNG 文件
    /// </summary>
    public static void SaveHeightMapToFile(Ra3MapFacade ra3Map, string filePath)
    {
        // 调用扩展方法得到 PNG 的字节数组
        byte[] pngData = ra3Map.GetPreviewImage();

        // 确保目录存在
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // 写入文件
        File.WriteAllBytes(filePath, pngData);
        Console.WriteLine($"高度图已保存: {filePath}");
    }

    [Test]
    public void Test1()
    {
        Ra3MapFacade ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "官方地图_工业区_IndustrialStrength");

        SaveHeightMapToFile(ra3Map, "HeightMap.png");
    }
}