namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class WaypointWrap: ObjectWrap
{
    public WaypointWrap(ObjectAsset obj) : base(obj)
    {
    }
    
    /// <summary>
    /// 路径点ID
    /// </summary>
    public int WaypointID
    {
        get => Properties.GetProperty<int>("waypointID");
        set => Properties.PutProperty("waypointID", value);
    }
    
    /// <summary>
    /// 路径点名称
    /// </summary>
    // TODO: 检测是否重复? 是否要修改uniqueid
    public string WaypointName
    {
        get => Properties.GetProperty<string>("waypointName");
        set => Properties.PutProperty("waypointName", value);
    }
    
    /// <summary>First route label; absent properties read as an empty string.</summary>
    public string PathLabel1
    {
        get => Properties.GetProperty("waypointPathLabel1", "");
        set => Properties.PutProperty("waypointPathLabel1", value ?? throw new ArgumentNullException(nameof(value)));
    }

    public string PathLabel2
    {
        get => Properties.GetProperty("waypointPathLabel2", "");
        set => Properties.PutProperty("waypointPathLabel2", value ?? throw new ArgumentNullException(nameof(value)));
    }

    public string PathLabel3
    {
        get => Properties.GetProperty("waypointPathLabel3", "");
        set => Properties.PutProperty("waypointPathLabel3", value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>The waypoint's route flag, independent of the ordered connection records.</summary>
    public bool PathBiDirectional
    {
        get => Properties.GetProperty("waypointPathBiDirectional", false);
        set => Properties.PutProperty("waypointPathBiDirectional", value);
    }

    /// <summary>Raw type-specific setting displayed by WorldBuilder.</summary>
    public string WaypointTypeOption
    {
        get => Properties.GetProperty("waypointTypeOption", "");
        set => Properties.PutProperty("waypointTypeOption", value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>
    /// WorldBuilder combo order for <c>waypointType</c>. The stored integer is the combo index.
    /// A missing property reads as <see cref="WaypointTypeKind.Normal"/>. Values outside this list stay raw integers.
    /// </summary>
    public enum WaypointTypeKind
    {
        Normal = 0,
        Portal = 1,
        WalkPortal = 2,
        ClimbPortal = 3,
        PreClimbPortal = 4,
        Beacon = 5,
        SplineCatmullRom = 6,
        FakePathfindPortal = 7,
        MineshaftPortal = 8
    }

    /// <summary>Display strings from WorldBuilder_Mod_1.12.exe, in combo order.</summary>
    public static readonly string[] WaypointTypeNames =
    {
        "Normal",
        "Portal",
        "WalkPortal",
        "ClimbPortal",
        "PreClimbPortal",
        "Beacon",
        "Spline (CatmullRom)",
        "FakePathfindPortal (don't use)",
        "MineshaftPortal (don't use)"
    };

    /// <summary>
    /// 路径点类型的原始整数值。缺失时按 0（Normal）读取，未知整数原样保留。
    /// 新代码可使用 <see cref="WaypointTypeEnum"/>。
    /// </summary>
    public int WaypointType
    {
        get => Properties.GetProperty<int>("waypointType");
        set => Properties.PutProperty("waypointType", value);
    }

    /// <summary>
    /// 路径点类型的强类型枚举视图。
    /// </summary>
    public WaypointType WaypointTypeEnum
    {
        get => Obj.WaypointType;
        set => Obj.WaypointType = value;
    }

    public bool TryGetKnownWaypointType(out WaypointTypeKind kind)
    {
        if ((uint)WaypointType >= (uint)WaypointTypeNames.Length)
        {
            kind = default;
            return false;
        }

        kind = (WaypointTypeKind)WaypointType;
        return true;
    }
    
    // TODO: 解析更多
    
    
}
