using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private ObjectsListAsset _objectsList { get; set; }

    private List<ObjectWrap> GetAllObjects()
    {
        return _objectsList
            .MapObjectList
            .Select(o => ObjectWrap.Of(o))
            .ToList();
    }

    private ObjectAsset? GetObjectByUniqueId(string uniqueId)
    {
        var objectAssets = GetAllObjects()
            .Where(o => o.UniqueId == uniqueId)
            .Select(o => o.Obj)
            .ToList();
        if (objectAssets.Count == 0)
        {
            return null;
        }
        else
        {
            return objectAssets[0];
        }
    }

    public void RemoveByUniqueId(string uniqueId)
    {
        var o = GetObjectByUniqueId(uniqueId);
        if (o != null)
        {
            _objectsList.MapObjectList.Remove(o);
        }
    }

    public void Remove(object o)
    {
        if (o == null)
        {
            throw new ArgumentNullException("o");
        }

        if (o is ObjectWrap objectWrap)
        {
            RemoveByUniqueId((o as ObjectWrap).UniqueId);
        }
        else if (o is PlayerData playerData)
        {
            _sideListAsset.PlayerDataList.Remove(playerData);
        }
        else if (o is TeamAsset teamAsset)
        {
            _teamsAsset.TeamList.Remove(teamAsset);
        }
        else
        {
            throw new ArgumentException("Unsupported type for Remove: " + o.GetType());
        }
    }
    
    
    // ---------- objects ----------------

    
    public List<UnitObjectWrap> GetUnitObjects()
    {
        return GetAllObjects()
            .Where(o => o is UnitObjectWrap)
            .Select(o => o as UnitObjectWrap)
            .ToList();
    }
    
    public UnitObjectWrap AddUnitObject(string typeName, float x, float y, float z = 0)
    {
        var o = _objectsList.AddObj(ra3Map.Context, typeName, new Vec3D(x, y, z));
        return UnitObjectWrap.Of(o) as UnitObjectWrap;
    }
    
    // ------------- waypoint ----------------

    public List<WaypointWrap> GetWaypoints()
    {
        return GetAllObjects()
            .Where(o => o is WaypointWrap)
            .Select(o => o as WaypointWrap)
            .ToList();
    }
    
    public WaypointWrap AddWaypoint(string name, float x, float y, float z = 0)
    {
        var o = _objectsList.AddWaypoint(name, new Vec3D(x, y, z), ra3Map.Context);
        return WaypointWrap.Of(o) as WaypointWrap;
    }

    public WaypointWrap AddWaypoint(float x, float y, float z = 0)
    {
        var o = _objectsList.AddWaypoint(new Vec3D(x, y, z), ra3Map.Context);
        return WaypointWrap.Of(o) as WaypointWrap;
    }
    
    public WaypointWrap AddPlayerStartWaypoint(int playerIndex, float x, float y, float z = 0)
    {
        var o = _objectsList.AddPlayerStartWaypoint(playerIndex, new Vec3D(x, y, z), ra3Map.Context);
        return WaypointWrap.Of(o) as WaypointWrap;
    }

    
    // ---- init ----
    private void LoadObjectsList()
    {
        _objectsList =  ra3Map.Context.ObjectsListAsset;
    }
}