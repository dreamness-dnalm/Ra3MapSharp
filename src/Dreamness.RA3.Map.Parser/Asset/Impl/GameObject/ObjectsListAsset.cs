using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class ObjectsListAsset: BaseAsset
{
    public WritableList<ObjectAsset> MapObjectList { get; set; } = new();

    private HashSet<string> _uniqueIdSet = new HashSet<string>();

    private int maxWaypointId = -1;

    private int maxObjectId = -1;

    /// <summary>
    /// 普通单位/场景物体，不含路径点和 World Builder 路径节点。
    /// </summary>
    public IEnumerable<ObjectAsset> GetRegularObjects()
    {
        return MapObjectList.Where(asset => !asset.IsWaypoint && !asset.IsRoad);
    }

    /// <summary>
    /// 仅路径点。
    /// </summary>
    public IEnumerable<ObjectAsset> GetWaypointObjects()
    {
        return MapObjectList.Where(asset => asset.IsWaypoint);
    }

    /// <summary>
    /// 仅 World Builder 路径节点。
    /// </summary>
    public IEnumerable<ObjectAsset> GetRoadObjects()
    {
        return MapObjectList.Where(asset => asset.IsRoad);
    }
    
    // TODO: 路径点类型
    private ObjectAsset AddWaypoint(int id, string name, Vec3D position, BaseContext context)
    {
        if (id <= maxWaypointId)
        {
            throw new System.Exception("Waypoint ID must be greater than the last used ID.");
        }

        var asset = ObjectAsset.OfWaypoint(position, id, name, context);
        MapObjectList.Add(asset);
        
        MarkModified();

        maxWaypointId = id;
        return asset;
    }
    
    public ObjectAsset AddWaypoint(string name, Vec3D position, BaseContext context)
    {
        var id = maxWaypointId + 1;
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
        var (id, uniqueId) = GetNextUniqueId(typeName);

        var asset = ObjectAsset.OfObj(uniqueId, typeName, position, angle, objName, belongToTeam, context);
        MapObjectList.Add(asset);
        _uniqueIdSet.Add(uniqueId);
        MarkModified();

        maxObjectId = id;
        return asset;
    }

    public ObjectAsset AddRoad(BaseContext context, string typeName, Vec3D position,
        RoadOptions options, float angle = 0,
        string belongToTeam = "PlyrNeutral/teamPlyrNeutral")
    {
        if (!options.IsRoad)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "A road node must have a non-zero option value.");
        }

        var (id, uniqueId) = GetNextUniqueId(typeName);
        var asset = ObjectAsset.OfRoad(uniqueId, typeName, position, angle, options, belongToTeam, context);
        MapObjectList.Add(asset);
        _uniqueIdSet.Add(uniqueId);
        MarkModified();

        maxObjectId = id;
        return asset;
    }

    private (int id, string uniqueId) GetNextUniqueId(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("A non-empty object type name is required.", nameof(typeName));
        }
        var id = maxObjectId + 1;
        var uniqueId = typeName + " " + id;

        while (_uniqueIdSet.Contains(uniqueId))
        {
            id++;
            uniqueId = typeName + " " + id;
        }

        return (id, uniqueId);
    }
    
    public void Remove(ObjectAsset asset)
    {
        if (!MapObjectList.GetAssets().Contains(asset))
        {
            return;
        }

        if (!asset.IsWaypoint)
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
            if (mapObject.IsWaypoint)
            {
                var waypointId = mapObject.Properties.GetProperty<int>("waypointID");
                maxWaypointId = Math.Max(maxWaypointId, waypointId);
            }
            else
            {
                _uniqueIdSet.Add(mapObject.UniqueId);
            }
            MapObjectList.Add(mapObject, ignoreModified: true);
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
