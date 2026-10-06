namespace Dreamness.RA3.Map.Automation.Geometry;

/// <summary>The agent protocol uses radians; Parser/Facade Angle properties use degrees.</summary>
public static class MapAngles
{
    public static float ToDegrees(float radians)
    {
        var value = (float)(radians * (180d / Math.PI));
        if (!float.IsFinite(radians) || !float.IsFinite(value))
            throw new AutomationException("INVALID_ARGUMENT", "朝向必须可转换为有限度数。");
        return value;
    }
    public static float ToRadians(float degrees)
    {
        if (!float.IsFinite(degrees)) throw new AutomationException("INVALID_ARGUMENT", "地图朝向不是有限度数。");
        return (float)(degrees * (Math.PI / 180d));
    }
}
