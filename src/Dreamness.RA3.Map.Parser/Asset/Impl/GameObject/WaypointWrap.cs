namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class WaypointWrap: ObjectWrap
{
    public WaypointWrap(ObjectAsset obj) : base(obj)
    {
    }
    
    public int WaypointID
    {
        get => Properties.GetProperty<int>("waypointID");
        set => Properties.PutProperty("waypointID", value);
    }
    
    // TODO: 检测是否重复? 是否要修改uniqueid
    public string WaypointName
    {
        get => Properties.GetProperty<string>("waypointName");
        set => Properties.PutProperty("waypointName", value);
    }
    
    // TODO: 解析 waypointTypeOption
    
    // TODO: 解析枚举 waypointType
    
    public int WaypointType
    {
        get => Properties.GetProperty<int>("waypointType");
        set => Properties.PutProperty("waypointType", value);
    }
    
    // TODO: 解析更多
    
    
}