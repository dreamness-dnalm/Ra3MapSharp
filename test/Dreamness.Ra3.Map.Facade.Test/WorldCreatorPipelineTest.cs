using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using SharpEXR;
using TinyEXR;

namespace Dreamness.Ra3.Map.Facade.Test;

public class WorldCreatorPipelineTest
{
    private string assetPath = "N:\\workspace\\ra3\\world_creator_workspace\\m1\\out";


    [Test]
    public void LoadHeightMap_Exr()
    {
        var filePath = Path.Combine(assetPath, "m1_Height Map_500x600_0_0.exr");

        var exrFile = EXRFile.FromFile(filePath);
        var part = exrFile.Parts[0];

        var dataWindow = part.DataWindow;
        var width = dataWindow.Width;
        var height = dataWindow.Height;
        
        Console.WriteLine(width + ", " + height);

        // var floats = part.GetFloats(ChannelConfiguration.RGB, false, GammaEncoding.Linear, false);
        
        // Console.WriteLine(floats[0]);
        
        // float[] rgb = part.GetFloats(ChannelConfiguration.RGB,
        //     includeAlpha: false,
        //     GammaEncoding.Linear,   // 保持线性，不做 sRGB 变换
        //     premultiplyAlpha: false);
        //
        // // 拆出 R 通道作为高度（rgb 长度 = width * height * 3）
        // float[] heights = new float[width * height];
        // for (int i = 0, j = 0; i < heights.Length; i++, j += 3)
        //     heights[i] = rgb[j];   // R

        // var heightChannel = part.FloatChannels["R"];
        //
        // part.Close();
        //
        // var ra3MapFacade = Ra3MapFacade.NewMap(width, height, 0, 2);
        //
        // for(int y = 0; y < height; y++)
        // {
        //     for(int x = 0; x < width; x++)
        //     {
        //         var value = heightChannel[x + width * y];
        //         Console.WriteLine(value);
        //         ra3MapFacade.SetTerrainHeight(x, y, value);
        //     }
        // }
        //
        // ra3MapFacade.SaveAs(Ra3PathUtil.RA3MapFolder, "m1_out");
        

    }



    [Test]
    public void LoadHeightMap_Exr2()
    {
        // var filePath = Path.Combine(assetPath, "m1_Height Map_5000x6000_0_0.exr");

        var filePath = @"C:\Users\mmmmm\Documents\World Creator\Export\ra3_template_Height Map_5000x6000_0_0.exr";
        
        Exr.LoadEXR(filePath, out float[] rgba, out int width, out int height);
        
        Console.WriteLine(width + ", " + height);
        

        
        float[,] heights = new float[width, height];
        
        for(int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                var value = rgba[(x + width * y) * 4];
                Console.WriteLine(value);
                // ra3MapFacade.SetTerrainHeight(x, height - y - 1, value);
                heights[x, height - y - 1] = value;
            }
        }

        float[,] heights2 = DownscaleArea(heights, 10);
        
        var mapWidth = heights2.GetLength(0);
        var mapHeight = heights2.GetLength(1);
        
        var ra3MapFacade = Ra3MapFacade.NewMap(mapWidth, mapHeight, 0, 2);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                var value = heights2[x, y];
                ra3MapFacade.SetTerrainHeight(x, y, value);
            }
        }
        
        ra3MapFacade.SaveAs(Ra3PathUtil.RA3MapFolder, "m1_out7");
    }
    
    public static float[,] DownscaleArea(float[,] src, int factor)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));

        int h = src.GetLength(0);
        int w = src.GetLength(1);
        if (h % factor != 0 || w % factor != 0)
            throw new ArgumentException("源数组的宽高必须能被 factor 整除。");

        int nh = h / factor;
        int nw = w / factor;
        var dst = new float[nh, nw];

        // 并行行块加速（可按需要去掉 Parallel）
        Parallel.For(0, nh, oy =>
        {
            int sy0 = oy * factor;
            for (int ox = 0; ox < nw; ox++)
            {
                int sx0 = ox * factor;
                double sum = 0.0;
                for (int dy = 0; dy < factor; dy++)
                {
                    int sy = sy0 + dy;
                    for (int dx = 0; dx < factor; dx++)
                    {
                        int sx = sx0 + dx;
                        sum += src[sy, sx];
                    }
                }
                dst[oy, ox] = (float)(sum / (factor * factor));
            }
        });

        return dst;
    }
    
    public static float[,] ResizeBilinear(float[,] src, float factor)
    {
        int h = src.GetLength(0);
        int w = src.GetLength(1);
        
        var newH = (int)(h * factor);
        var newW = (int)(w * factor);
        
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (newH <= 0 || newW <= 0) throw new ArgumentOutOfRangeException();

        
        var dst = new float[newH, newW];

        // 将目标像素中心映射回源空间
        double scaleY = (double)(h) / newH;
        double scaleX = (double)(w) / newW;

        for (int y = 0; y < newH; y++)
        {
            double sy = (y + 0.5) * scaleY - 0.5;
            int y0 = (int)Math.Floor(sy);
            int y1 = Math.Min(y0 + 1, h - 1);
            y0 = Math.Max(y0, 0);
            double wy = sy - y0;

            for (int x = 0; x < newW; x++)
            {
                double sx = (x + 0.5) * scaleX - 0.5;
                int x0 = (int)Math.Floor(sx);
                int x1 = Math.Min(x0 + 1, w - 1);
                x0 = Math.Max(x0, 0);
                double wx = sx - x0;

                // 四邻域插值
                double v00 = src[y0, x0];
                double v01 = src[y0, x1];
                double v10 = src[y1, x0];
                double v11 = src[y1, x1];

                double v0 = v00 * (1 - wx) + v01 * wx;
                double v1 = v10 * (1 - wx) + v11 * wx;
                dst[y, x] = (float)(v0 * (1 - wy) + v1 * wy);
            }
        }

        return dst;
    }
    
    public static float[,] Scale(float[,] src, double factor, int? maxDegreeOfParallelism = null)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor), "factor 必须 > 0");

        int h = src.GetLength(0);
        int w = src.GetLength(1);

        int newH = Math.Max(1, (int)Math.Round(h * factor));
        int newW = Math.Max(1, (int)Math.Round(w * factor));
        if (newH == h && newW == w) return (float[,])src.Clone();

        var dst = new float[newH, newW];

        var (y0Arr, y1Arr, wyArr) = BuildAxisLUT(h, newH);
        var (x0Arr, x1Arr, wxArr) = BuildAxisLUT(w, newW);

        var po = new ParallelOptions();
        if (maxDegreeOfParallelism.HasValue)
            po.MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism.Value);

        Parallel.For(0, newH, po, y =>
        {
            int y0 = y0Arr[y];
            int y1 = y1Arr[y];
            double wy = wyArr[y];
            double invWy = 1.0 - wy;

            for (int x = 0; x < newW; x++)
            {
                int x0 = x0Arr[x];
                int x1 = x1Arr[x];
                double wx = wxArr[x];
                double invWx = 1.0 - wx;

                double v00 = src[y0, x0];
                double v01 = src[y0, x1];
                double v10 = src[y1, x0];
                double v11 = src[y1, x1];

                double v0 = v00 * invWx + v01 * wx;
                double v1 = v10 * invWx + v11 * wx;

                dst[y, x] = (float)(v0 * invWy + v1 * wy);
            }
        });

        return dst;
    }
    
    private static (int[] i0, int[] i1, double[] wi) BuildAxisLUT(int oldLen, int newLen)
    {
        var i0 = new int[newLen];
        var i1 = new int[newLen];
        var wi = new double[newLen];

        double scale = (double)oldLen / newLen;

        for (int i = 0; i < newLen; i++)
        {
            double s = (i + 0.5) * scale - 0.5;
            int t0 = (int)Math.Floor(s);
            int t1 = t0 + 1;

            if (t0 < 0) { t0 = 0; s = 0.0; }
            if (t1 >= oldLen) t1 = oldLen - 1;

            double w = s - t0;
            if (w < 0) w = 0;
            if (w > 1) w = 1;

            i0[i] = t0;
            i1[i] = t1;
            wi[i] = w;
        }

        return (i0, i1, wi);
    }
    
    /**
     *
     *290 0.283  
     *300 0.292
     * 500 0.488 
     *  900 0.878 1.0250569476082
     */
    // 1.024590163934426
    // 1.027397260273973
    // 1.024734982332155
    // 1.024
}