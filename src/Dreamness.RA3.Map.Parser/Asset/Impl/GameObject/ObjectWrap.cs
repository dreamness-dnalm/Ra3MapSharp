using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public abstract class ObjectWrap
{
    public ObjectAsset Obj { get; private set; }

    protected ObjectWrap(ObjectAsset obj)
    {
        Obj = obj;
    }

    public Vec3D Position
    {
        get => Obj.Position;
        set
        {
            Obj.Position = value;
        }
    }
    
    // TODO: what's road option?
    
    public string UniqueId => Obj.UniqueId;
    
    public AssetProperties Properties => Obj.Properties;
    

    
    // TODO: what's objectLayer?


    public static ObjectWrap Of(ObjectAsset obj)
    {
        if (obj.IsWaypoint)
        {
            return new WaypointWrap(obj);
        }
        else
        {
            return new UnitObjectWrap(obj);
        }
    }
}