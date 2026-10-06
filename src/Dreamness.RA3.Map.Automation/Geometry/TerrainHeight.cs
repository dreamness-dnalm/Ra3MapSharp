using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Automation.Geometry;

internal static class TerrainHeight
{
    /// <summary>Playable-grid bilinear height at an object origin. Engine triangulation may differ on nonplanar cells.</summary>
    internal static double Sample(Dreamness.Ra3.Map.Facade.Core.Ra3MapFacade map, double x, double y)
    {
        var mx = x + map.MapBorderWidth; var my = y + map.MapBorderWidth;
        var x0 = (int)Math.Floor(mx); var y0 = (int)Math.Floor(my);
        var x1 = Math.Min(x0 + 1, map.MapWidth - 1); var y1 = Math.Min(y0 + 1, map.MapHeight - 1);
        var tx = mx - x0; var ty = my - y0;
        return (1 - ty) * ((1 - tx) * map.GetTerrainHeight(x0, y0) + tx * map.GetTerrainHeight(x1, y0))
            + ty * ((1 - tx) * map.GetTerrainHeight(x0, y1) + tx * map.GetTerrainHeight(x1, y1));
    }

    internal static float Normalize(float height)
    {
        if (!float.IsFinite(height) || height < 0 || height > StreamExtension.FromSageFloat16(ushort.MaxValue))
            throw new AutomationException("INVALID_ARGUMENT", "地形高度不在可序列化范围内。");
        var safe = Math.Min(height, MathF.Floor(height / 10f) * 10f + StreamExtension.FromSageFloat16(255));
        return StreamExtension.FromSageFloat16(StreamExtension.ToSageFloat16(safe));
    }
}
