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
    
    // TODO: 解析 waypointTypeOption
    
    /// <summary>
    /// 路径点类型的原始整数值。为保持 API 兼容而保留；
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
    
    // TODO: 解析更多
    
    
}
