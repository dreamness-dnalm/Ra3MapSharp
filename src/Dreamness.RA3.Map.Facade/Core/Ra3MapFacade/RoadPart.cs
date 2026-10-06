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

    /// <summary>Add adjacent road start/end nodes, preserving Z and additional option bits.</summary>
    public (RoadObjectWrap Start, RoadObjectWrap End) AddRoadSegment(string typeName,
        Vec3D start, Vec3D end, int additionalOptions = 0, float startAngle = 0, float endAngle = 0)
        => AddPathPair(typeName, start, end, RoadOptions.RoadStartBit, RoadOptions.RoadEndBit,
            additionalOptions, startAngle, endAngle);

    /// <summary>Add adjacent bridge start/end nodes, independent from ordinary road pairs.</summary>
    public (RoadObjectWrap Start, RoadObjectWrap End) AddBridgeSegment(string typeName,
        Vec3D start, Vec3D end, int additionalOptions = 0, float startAngle = 0, float endAngle = 0)
        => AddPathPair(typeName, start, end, RoadOptions.BridgeStartBit, RoadOptions.BridgeEndBit,
            additionalOptions, startAngle, endAngle);

    private (RoadObjectWrap Start, RoadObjectWrap End) AddPathPair(string typeName, Vec3D start,
        Vec3D end, int startBit, int endBit, int additionalOptions, float startAngle, float endAngle)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("A type name is required.", nameof(typeName));
        const int endpointBits = RoadOptions.RoadStartBit | RoadOptions.RoadEndBit
            | RoadOptions.BridgeStartBit | RoadOptions.BridgeEndBit;
        if ((additionalOptions & endpointBits) != 0)
            throw new ArgumentException("Additional options cannot contain endpoint bits.", nameof(additionalOptions));
        if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) || !float.IsFinite(start.Z)
            || !float.IsFinite(end.X) || !float.IsFinite(end.Y) || !float.IsFinite(end.Z)
            || !float.IsFinite(startAngle) || !float.IsFinite(endAngle))
            throw new ArgumentException("Positions and angles must be finite.");
        var first = AddRoadObject(typeName, start.X, start.Y, start.Z, startAngle, additionalOptions | startBit);
        var last = AddRoadObject(typeName, end.X, end.Y, end.Z, endAngle, additionalOptions | endBit);
        return (first, last);
    }

    /// <summary>
    /// Reorder the complete existing road-node set. Does not add/remove nodes or move non-road objects.
    /// Validates the permutation before modifying the map; IDs and unknown option bits are preserved.
    /// </summary>
    public void ReorderRoadObjects(IEnumerable<RoadObjectWrap> orderedRoads)
    {
        ArgumentNullException.ThrowIfNull(orderedRoads);
        var ordered = orderedRoads.ToArray();
        var current = GetRoadObjects();
        var expected = new HashSet<ObjectAsset>(current.Select(r => r.Obj), ReferenceEqualityComparer.Instance);
        if (ordered.Length != current.Count || ordered.Any(r => r is null || !expected.Remove(r.Obj))
            || expected.Count != 0)
            throw new ArgumentException("Provide every existing road node exactly once.", nameof(orderedRoads));
        var allObjects = _objectsList.MapObjectList.ToArray();
        var next = 0;
        for (var i = 0; i < allObjects.Length; i++)
            if (allObjects[i].IsRoad) allObjects[i] = ordered[next++].Obj;
        // Unsubscribe the old set before subscribing the reordered set, avoiding duplicate subscriptions.
        _objectsList.MapObjectList.Clear();
        foreach (var obj in allObjects) _objectsList.MapObjectList.Add(obj);
        _objectsList.MarkModified();
    }
}
