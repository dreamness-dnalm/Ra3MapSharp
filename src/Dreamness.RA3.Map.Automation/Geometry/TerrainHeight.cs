using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Automation.Geometry;

internal static class TerrainHeight
{
    internal static float Normalize(float height)
    {
        if (!float.IsFinite(height) || height < 0 || height > StreamExtension.FromSageFloat16(ushort.MaxValue))
            throw new AutomationException("INVALID_ARGUMENT", "地形高度不在可序列化范围内。");
        var safe = Math.Min(height, MathF.Floor(height / 10f) * 10f + StreamExtension.FromSageFloat16(255));
        return StreamExtension.FromSageFloat16(StreamExtension.ToSageFloat16(safe));
    }
}
