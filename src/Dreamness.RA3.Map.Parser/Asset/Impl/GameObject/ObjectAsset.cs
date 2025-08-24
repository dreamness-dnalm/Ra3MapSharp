using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class ObjectAsset: BaseAsset
{

    private Vec3D _position;
    
    public Vec3D Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                ObservableUtil.Unsubscribe(_position, this);
                _position = value;
                ObservableUtil.Subscribe(_position, this);
                MarkModified();
            }
        }
    }
    
    private float _angle;

    public float Angle
    {
        get => _angle;
        set
        {
            if (_angle != value)
            {
                _angle = value;
                MarkModified();
            }
        }
    }

    private int _roadOption;

    public int RoadOption
    {
        get => _roadOption;
        set
        {
            if (_roadOption != value)
            {
                _roadOption = value;
                MarkModified();
            }
        }
    }

    private string _typeName;
    public string TypeName
    {
        get => _typeName;
        set
        {
            if (_typeName != value)
            {
                _typeName = value;
                MarkModified();
            }
        }
    }

    public string UniqueId => Properties.GetProperty<string>("uniqueID");

    public AssetProperties Properties { get; private set; }
    
    public bool IsWaypoint => _typeName == "*Waypoints/Waypoint";
    
    public override short GetVersion()
    {
        return 3;
    }

    public override string GetName()
    {
        return AssetNameConst.Object;
    }

    public static ObjectAsset OfObj(string uniqueId, string typeName, Vec3D position, float angle, string name, string belongToTeam,
        BaseContext context)
    {
        var asset = new ObjectAsset();
        asset.ApplyBasicInfo(context);
        
        asset._position = position;
        asset._angle = angle;
        asset._roadOption = 0;
        asset._typeName = typeName;
        asset.Properties = AssetProperties.FromDict(
            new Dictionary<string, object>
            {
                {"objectInitialHealth", 100},
                {"objectEnabled", true},
                {"objectIndestructible", false},
                {"objectUnsellable", false},
                {"objectPowered", true},
                {"objectRecruitableAI", true},
                {"objectTargetable", false},
                {"objectSleeping", false},
                {"objectBasePriority", 40},
                {"objectBasePhase", 1},
                {"originalOwner", belongToTeam},
                {"uniqueID", uniqueId},
                {"objectName", name},
                {"objectLayer", ""}
            }, context);
        
        asset.MarkModified();
        return asset;
    }

    public static ObjectAsset OfWaypoint(Vec3D position, int waypointId, string name, BaseContext context)
    {
        var asset = new ObjectAsset();
        asset.ApplyBasicInfo(context);
        
        asset._position = position;
        asset._angle = 0;
        asset._roadOption = 0;
        asset._typeName = "*Waypoints/Waypoint";
        
        asset.Properties = AssetProperties.FromDict(
            new Dictionary<string, object>
            {
                {"objectInitialHealth", 100},
                {"objectEnabled", true},
                {"objectIndestructible", false},
                {"objectUnsellable", false},
                {"objectPowered", true},
                {"objectRecruitableAI", true},
                {"objectTargetable", false},
                {"objectSleeping", false},
                {"objectBasePriority", 40},
                {"objectBasePhase", 1},
                {"waypointID", waypointId},
                {"waypointName", name},
                {"originalOwner", "/team"},
                {"uniqueID", name},
                {"objectLayer", ""},
                {"waypointTypeOption", ""},
                {"waypointType", 1}
            }, context);
        
        asset.MarkModified();
        return asset;
    }


    public ObjectAsset Clone(BaseContext context)
    {
        var asset = new ObjectAsset();
        asset.ApplyBasicInfo(context);
        
        asset._position = _position;
        asset._angle = _angle;
        asset._roadOption = _roadOption;
        asset._typeName = _typeName;
        asset.Properties = Properties.Clone(context);
        
        asset.MarkModified();
        return asset;
    }
    

    protected override void _Parse(BaseContext context)
    {
        var memoryStream = new MemoryStream(Data);
        var binaryReader = new BinaryReader(memoryStream);
        
        _position = binaryReader.ReadVec3D();
        ObservableUtil.Subscribe(_position, this);
        _angle = binaryReader.ReadSingle() * 180f / (float)Math.PI;
        _roadOption = binaryReader.ReadInt32();
        _typeName = binaryReader.ReadDefaultString();
        Properties = AssetProperties.FromBinaryReader(binaryReader, context);
        ObservableUtil.Subscribe(_position, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.WriteVec3D(_position, context);
        binaryWriter.Write((float)(_angle * Math.PI / 180f));
        binaryWriter.Write(_roadOption);
        binaryWriter.WriteDefaultString(_typeName);
        binaryWriter.Write(Properties.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}