using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    /// <summary>
    /// Gets road-tool nodes. Static road scenery with RoadOption == 0 remains
    /// a normal unit/scenery object because it does not participate in the
    /// WorldBuilder road-node format.
    /// </summary>
    public List<RoadObjectWrap> GetRoadObjects()
    {
        return _objectsList
            .GetRoadObjects()
            .Select(o => new RoadObjectWrap(o))
            .ToList();
    }

    /// <summary>
    /// Adds a WorldBuilder road node. The raw road option is preserved because
    /// its individual bit meanings are not yet fully documented.
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

    public void RemoveRoadObject(RoadObjectWrap road)
    {
        ArgumentNullException.ThrowIfNull(road);
        _objectsList.Remove(road.Obj);
    }
}
