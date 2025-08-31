using System;
using System.IO;
using Dreamness.Ra3.Map.Facade.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

public static class PreviewExtension
{
    /// <summary>
    /// 生成彩色高度图（PNG, RGB），返回字节数组。
    /// 水域(<200)蓝色系、陆地分段渐变；自动适配 min/max。
    /// </summary>
    public static byte[] GetPreviewImage(this Ra3MapFacade ra3Map)
    {
        const int WaterLevel = 200;
        const bool FlipY = true;   // 如上下颠倒可改为 false

        int width = ra3Map.MapWidth;
        int height = ra3Map.MapHeight;
        if (width <= 0 || height <= 0) return Array.Empty<byte>();

        // ---------- 扫描 min/max ----------
        double minH = double.MaxValue, maxH = double.MinValue;
        var hBuf = new double[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double h = ra3Map.GetTerrainHeight(x, y);
            hBuf[y * width + x] = h;
            if (h < minH) minH = h;
            if (h > maxH) maxH = h;
        }

        bool flat = maxH <= minH;
        double invRange = flat ? 0.0 : 1.0 / (maxH - minH);

        using var img = new Image<Rgb24>(width, height);

        // 颜色工具
        static Rgb24 Lerp(Rgb24 a, Rgb24 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            byte r = (byte)Math.Round(a.R + (b.R - a.R) * t);
            byte g = (byte)Math.Round(a.G + (b.G - a.G) * t);
            byte bch = (byte)Math.Round(a.B + (b.B - a.B) * t);
            return new Rgb24(r, g, bch);
        }

        // 水域配色（更深水更暗蓝）
        static Rgb24 WaterColor(double norm)
        {
            // norm 是 0~1 的整体归一化，这里只用于微调亮度对比
            // 深蓝(海沟) → 蓝 → 淡蓝(浅滩)
            var deep = new Rgb24( 10,  35,  95);
            var mid  = new Rgb24( 25, 100, 180);
            var lite = new Rgb24(145, 200, 255);
            // 让更低的高度更靠近 deep
            float t = (float)Math.Clamp(norm, 0.0, 1.0);
            // 把 t 拉伸一下提升对比
            t = (float)Math.Pow(t, 0.8);
            // 两段渐变：0~0.5 deep→mid，0.5~1 mid→lite
            if (t < 0.5f) return Lerp(deep, mid, t / 0.5f);
            return Lerp(mid, lite, (t - 0.5f) / 0.5f);
        }

        // 陆地配色（草地→黄绿→褐→岩→雪）
        static Rgb24 LandColor(double norm)
        {
            // 分段关键点颜色（可按喜好调整）
            var low        = new Rgb24( 30, 110,  40);  // 深绿
            var grass      = new Rgb24( 60, 160,  60);  // 绿
            var plateau    = new Rgb24(170, 160,  60);  // 黄褐
            var mountain   = new Rgb24(120, 100,  80);  // 棕岩
            var highRock   = new Rgb24(160, 160, 160);  // 灰岩
            var snow       = new Rgb24(250, 250, 250);  // 雪

            float t = (float)Math.Clamp(norm, 0.0, 1.0);

            if (t < 0.20f) return Lerp(low, grass, t / 0.20f);
            if (t < 0.45f) return Lerp(grass, plateau, (t - 0.20f) / 0.25f);
            if (t < 0.65f) return Lerp(plateau, mountain, (t - 0.45f) / 0.20f);
            if (t < 0.85f) return Lerp(mountain, highRock, (t - 0.65f) / 0.20f);
            return Lerp(highRock, snow, (t - 0.85f) / 0.15f);
        }

        // 是否岸线（提升边界清晰度）
        bool IsShore(int x, int y)
        {
            double c = hBuf[y * width + x];
            bool cWater = c < WaterLevel;
            // 4邻域
            if (y > 0)
            {
                bool w = hBuf[(y - 1) * width + x] < WaterLevel;
                if (w != cWater) return true;
            }
            if (y < height - 1)
            {
                bool w = hBuf[(y + 1) * width + x] < WaterLevel;
                if (w != cWater) return true;
            }
            if (x > 0)
            {
                bool w = hBuf[y * width + (x - 1)] < WaterLevel;
                if (w != cWater) return true;
            }
            if (x < width - 1)
            {
                bool w = hBuf[y * width + (x + 1)] < WaterLevel;
                if (w != cWater) return true;
            }
            return false;
        }

        // 主循环：着色
        for (int y = 0; y < height; y++)
        {
            int dstY = FlipY ? (height - 1 - y) : y;

            for (int x = 0; x < width; x++)
            {
                double h = hBuf[y * width + x];
                double norm = flat ? 0.5 : Math.Clamp((h - minH) * invRange, 0.0, 1.0);

                Rgb24 rgb = (h < WaterLevel) ? WaterColor(norm) : LandColor(norm);

                // 岸线加深：仅对水侧像素做“描边”
                if (h < WaterLevel && IsShore(x, y))
                {
                    // 把颜色稍微压暗，突出边界
                    rgb = new Rgb24(
                        (byte)(rgb.R * 0.35),
                        (byte)(rgb.G * 0.35),
                        (byte)(rgb.B * 0.45));
                }

                img[x, dstY] = rgb;
            }
        }

        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder { BitDepth = PngBitDepth.Bit8, ColorType = PngColorType.Rgb });
        return ms.ToArray();
    }
}
