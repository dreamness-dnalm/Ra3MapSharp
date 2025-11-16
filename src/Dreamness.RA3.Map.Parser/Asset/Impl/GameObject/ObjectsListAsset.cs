using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class ObjectsListAsset: BaseAsset
{
    public WritableList<ObjectAsset> MapObjectList { get; set; } = new();

    private HashSet<string> _waypointNameSet = new HashSet<string>();
    private HashSet<string> _uniqueIdSet = new HashSet<string>();

    private int maxWaypointId = -1;

    private int maxObjectId = -1;
    
    // TODO: 路径点类型
    private ObjectAsset AddWaypoint(int id, string name, Vec3D position, BaseContext context)
    {
        if (id <= maxWaypointId)
        {
            throw new System.Exception("Waypoint ID must be greater than the last used ID.");
        }

        if (_waypointNameSet.Contains(name))
        {
            throw new System.Exception($"Waypoint name '{name}' already exists.");
        }
        
        var asset = ObjectAsset.OfWaypoint(position, id, name, context);
        MapObjectList.Add(asset);

        _waypointNameSet.Add(name);
        
        MarkModified();

        maxObjectId = id;
        return asset;
    }
    
    public ObjectAsset AddWaypoint(string name, Vec3D position, BaseContext context)
    {
        var id = maxWaypointId + 1;
        // _waypointNameSet.Add(name);
        return AddWaypoint(id, name, position, context);
    }
    
    public ObjectAsset AddWaypoint(Vec3D position, BaseContext context)
    {
        var id = maxWaypointId + 1;
        var name = $"Waypoint {id}";
        return AddWaypoint(id, name, position, context);
    }
    
    public ObjectAsset AddPlayerStartWaypoint(int playerId, Vec3D position, BaseContext context)
    {
        if (playerId < 0 || playerId > 6)
        {
            throw new System.Exception("Player ID must be between 1 and 6.");
        }
        
        var name = $"Player_{playerId}_Start";
        return AddWaypoint(name, position, context);
    }

    public ObjectAsset AddObj(BaseContext context, string typeName, Vec3D position, float angle = 0,
        string belongToTeam = "PlyrNeutral/teamPlyrNeutral", string objName = "")
    {
        int id = maxObjectId + 1;
        var uniqueId = typeName + " " + id;

        while (_uniqueIdSet.Contains(uniqueId))
        {
            id++;
            uniqueId = typeName + " " + id;
        }
        
        var asset = ObjectAsset.OfObj(uniqueId, typeName, position, angle, objName, belongToTeam, context);
        MapObjectList.Add(asset);
        MarkModified();

        maxObjectId = id;
        return asset;
    }
    
    public void Remove(ObjectAsset asset)
    {
        if (asset.IsWaypoint)
        {
            _waypointNameSet.Remove(asset.UniqueId);
        }
        else
        {
            _uniqueIdSet.Remove(asset.UniqueId);
        }
        MapObjectList.Remove(asset);
        MarkModified();
    }

    public void Add(ObjectAsset asset, bool autoId)
    {
        if (asset.IsWaypoint)
        {
            if (autoId)
            {
                maxWaypointId++;
                asset.Properties.SetProperty("waypointID", maxWaypointId);
            }

            if (_waypointNameSet.Contains(asset.UniqueId))
            {
                throw new System.Exception($"Waypoint name '{asset.UniqueId}' already exists.");
            }
            _waypointNameSet.Add(asset.UniqueId);
            maxWaypointId = Math.Max(maxWaypointId, asset.Properties.GetProperty<int>("waypointID"));
            MapObjectList.Add(asset);
        }
        else
        {
            if (autoId)
            {
                while (_uniqueIdSet.Contains(asset.UniqueId))
                {
                    maxObjectId++;
                    asset.Properties.PutProperty("uniqueID", asset.TypeName + " " + maxObjectId);
                }
            }

            if (_uniqueIdSet.Contains(asset.UniqueId))
            {
                throw new System.Exception($"Unique ID '{asset.UniqueId}' already exists.");
            }

            _uniqueIdSet.Add(asset.UniqueId);
            MapObjectList.Add(asset);
        }
    }
    
    public override short GetVersion()
    {
        return 3;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.ObjectsList;
    }

    protected override void _Parse(BaseContext context)
    {
        var memoryStream = new MemoryStream(Data);
        var binaryReader = new BinaryReader(memoryStream);

        while (binaryReader.BaseStream.Position < DataSize)
        {
            var mapObject = (ObjectAsset)AssetParser.FromBinaryReader(binaryReader, context);
            _uniqueIdSet.Add(mapObject.UniqueId);
            if (mapObject.IsWaypoint)
            {
                var waypointId = mapObject.Properties.GetProperty<int>("waypointID");
                maxWaypointId = Math.Max(maxWaypointId, waypointId);
            }
            MapObjectList.Add(mapObject);
        }

        maxObjectId = MapObjectList.Count;
        
        ObservableUtil.Subscribe(MapObjectList, this);
        
        binaryReader.Close();
    }

    protected override byte[] Deparse(BaseContext context)
    {
        return MapObjectList.ToBytes(context);
    }
    
    public static ObjectsListAsset Default(BaseContext context)
    {
        var asset = new ObjectsListAsset();
        asset.ApplyBasicInfo(context);
        asset.MarkModified();
        return asset;
    }
}