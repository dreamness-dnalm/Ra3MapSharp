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

    /// <summary>
    /// 移除指定 UniqueId 的物体/路径点
    /// </summary>
    /// <param name="uniqueId"></param>
    public void RemoveByUniqueId(string uniqueId)
    {
        var o = GetObjectByUniqueId(uniqueId);
        if (o != null)
        {
            _objectsList.MapObjectList.Remove(o);
        }
    }

    /// <summary>
    /// 移除物体/路径点
    /// </summary>
    /// <param name="o"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
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

    /// <summary>
    /// 获取所有单位物体
    /// </summary>
    /// <returns></returns>
    public List<UnitObjectWrap> GetUnitObjects()
    {
        return GetAllObjects()
            .Where(o => o is UnitObjectWrap)
            .Select(o => o as UnitObjectWrap)
            .ToList();
    }
    
    /// <summary>
    /// 添加单位物体
    /// </summary>
    /// <param name="typeName">单位类型名称, 坐标使用世界坐标</param>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="z">Z 坐标</param>
    /// <returns></returns>
    public UnitObjectWrap AddUnitObject(string typeName, float x, float y, float z = 0)
    {
        var o = _objectsList.AddObj(ra3Map.Context, typeName, new Vec3D(x, y, z));
        return UnitObjectWrap.Of(o) as UnitObjectWrap;
    }
    
    // ------------- waypoint ----------------

    /// <summary>
    /// 获取所有路径点
    /// </summary>
    /// <returns></returns>
    public List<WaypointWrap> GetWaypoints()
    {
        return GetAllObjects()
            .Where(o => o is WaypointWrap)
            .Select(o => o as WaypointWrap)
            .ToList();
    }
    
    /// <summary>
    /// 添加路径点, 指定名称, 坐标使用世界坐标
    /// </summary>
    /// <param name="name"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public WaypointWrap AddWaypoint(string name, float x, float y, float z = 0)
    {
        var o = _objectsList.AddWaypoint(name, new Vec3D(x, y, z), ra3Map.Context);
        return WaypointWrap.Of(o) as WaypointWrap;
    }

    /// <summary>
    /// 添加路径点, 坐标使用世界坐标
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public WaypointWrap AddWaypoint(float x, float y, float z = 0)
    {
        var o = _objectsList.AddWaypoint(new Vec3D(x, y, z), ra3Map.Context);
        return WaypointWrap.Of(o) as WaypointWrap;
    }
    
    /// <summary>
    /// 添加玩家起始点路径点, 坐标使用世界坐标
    /// </summary>
    /// <param name="playerIndex"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
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