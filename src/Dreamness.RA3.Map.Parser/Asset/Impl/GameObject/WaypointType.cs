namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

/// <summary>
/// World Builder 路径点类型。
/// 枚举名与约定路径点名称 <c>wp_&lt;waypoint_type&gt;</c> 的后半部分一致。
/// </summary>
public enum WaypointType
{
    Normal = 0,
    Portal = 1,
    WalkPortal = 2,
    ClimbPortal = 3,
    PreClimbPortal = 4,
    Beacon = 5,
    Spline = 6,
    FakePathfindPortal = 7,
    MineshaftPortal = 8
}

/// <summary>
/// 路径点类型与 World Builder 约定名称之间的转换。
/// </summary>
public static class WaypointTypeExtensions
{
    private const string WaypointNamePrefix = "wp_";

    /// <summary>
    /// 生成 World Builder 约定的路径点名称，例如 <c>wp_PreClimbPortal</c>。
    /// </summary>
    public static string ToWaypointName(this WaypointType waypointType)
    {
        return WaypointNamePrefix + waypointType;
    }

    /// <summary>
    /// 尝试从 World Builder 约定名称解析路径点类型。
    /// </summary>
    public static bool TryParseWaypointName(string? waypointName, out WaypointType waypointType)
    {
        waypointType = default;

        if (waypointName == null ||
            !waypointName.StartsWith(WaypointNamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var typeName = waypointName.AsSpan(WaypointNamePrefix.Length);
        if (!Enum.TryParse(typeName, ignoreCase: false, out WaypointType parsedType) ||
            !Enum.IsDefined(parsedType) ||
            !typeName.SequenceEqual(parsedType.ToString()))
        {
            return false;
        }

        waypointType = parsedType;
        return true;
    }
}
