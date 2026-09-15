using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    /// <summary>
    /// 获取路径工具节点。RoadOption 为 0 的静态道路场景仍作为普通单位/场景物体，
    /// 因为它们不参与 World Builder 的路径节点格式。
    /// </summary>
    public List<RoadObjectWrap> GetRoadObjects()
    {
        return _objectsList
            .GetRoadObjects()
            .Select(o => new RoadObjectWrap(o))
            .ToList();
    }

    /// <summary>
    /// 添加 World Builder 路径节点。原始 road option 原样保留，各位含义尚未完全文档化。
    /// </summary>
    public RoadObjectWrap AddRoadObject(string typeName, float x, float y, float z = 0,
        float angle = 0, int roadOption = 2)
    {
        var o = _objectsList.AddRoad(
            ra3Map.Context,
            typeName,
            new Vec3D(x, y, z),
            new RoadOptions(roadOption),
            angle);
        return RoadObjectWrap.Of(o);
    }

    /// <summary>
    /// 移除指定路径节点。
    /// </summary>
    public void RemoveRoadObject(RoadObjectWrap road)
    {
        ArgumentNullException.ThrowIfNull(road);
        _objectsList.Remove(road.Obj);
    }
}
