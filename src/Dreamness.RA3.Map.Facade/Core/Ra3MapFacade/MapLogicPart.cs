using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Trigger;
using Dreamness.Ra3.Map.Parser.Asset.Impl.MissionHotSpot;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Waypoint;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    /// <summary>Snapshot of live slot records. Indices are zero based; preserves the map's actual slot count.</summary>
    public List<MPPositionInfo> GetMultiplayerPositions() => GetEditableAsset<MPPositionListAsset>(AssetNameConst.MPPositionList)?.MPPositionInfos.ToList() ?? new();

    /// <summary>Initializes or resizes a slot list, preserving existing slots. RA3 normally has six slots; some editor maps have eight.</summary>
    public void ResizeMultiplayerPositions(int count)
    {
        if (count < 0 || count > 8) throw new ArgumentOutOfRangeException(nameof(count));
        var slots = EnsureEditableAsset(AssetNameConst.MPPositionList, MPPositionListAsset.Default).MPPositionInfos;
        while (slots.Count > count) slots.Remove(slots[slots.Count - 1]);
        while (slots.Count < count) slots.Add(MPPositionInfo.Of(true, true, true, uint.MaxValue, Array.Empty<string>(), ra3Map.Context));
    }

    public void SetMultiplayerPosition(int index, bool isHuman, bool isComputer, bool loadAIScript, uint team, string[]? sideRestrictions = null)
    {
        var slots = GetMultiplayerPositions();
        if (index < 0 || index >= slots.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var restrictions = sideRestrictions ?? Array.Empty<string>();
        if (restrictions.Any(s => s == null)) throw new ArgumentException("Restrictions cannot contain null.", nameof(sideRestrictions));
        var slot = slots[index];
        slot.IsHuman = isHuman; slot.IsComputer = isComputer; slot.LoadAIScript = loadAIScript; slot.Team = team;
        slot.SideRestrictions = restrictions;
    }

    public List<TriggerArea> GetTriggerAreas() => GetEditableAsset<TriggerAreasAsset>(AssetNameConst.TriggerAreas)?.Areas.ToList() ?? new();
    public TriggerArea? GetTriggerArea(string name) => GetTriggerAreas().FirstOrDefault(a => a.Name == name);

    /// <summary>Adds a polygon in world coordinates. Height is the raw final polygon int32 field.</summary>
    public TriggerArea AddTriggerArea(string name, IEnumerable<Vec2D> points, string layerName = "", int height = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A trigger name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(layerName);
        var vertices = points.ToList();
        if (vertices.Count < 3 || vertices.Any(p => p == null || !float.IsFinite(p.X) || !float.IsFinite(p.Y)))
            throw new ArgumentException("A polygon requires at least three finite points.", nameof(points));
        var existing = GetTriggerAreas();
        if (existing.Any(a => a.Name == name)) throw new ArgumentException("Duplicate trigger name.", nameof(name));
        var id = existing.Count == 0 ? 1 : checked(existing.Max(a => a.Id) + 1);
        var area = TriggerArea.Of(name, layerName, id, vertices, height);
        EnsureEditableAsset(AssetNameConst.TriggerAreas, TriggerAreasAsset.Default).Areas.Add(area);
        return area;
    }

    public bool RemoveTriggerArea(string name)
    {
        var asset = GetEditableAsset<TriggerAreasAsset>(AssetNameConst.TriggerAreas);
        var area = asset?.Areas.FirstOrDefault(a => a.Name == name);
        if (area == null) return false;
        asset!.Areas.Remove(area); return true;
    }

    public List<MissionHotSpot> GetMissionHotSpots() => GetEditableAsset<MissionHotSpotsAsset>(AssetNameConst.MissionHotSpots)?.HotSpots.ToList() ?? new();
    public MissionHotSpot? GetMissionHotSpot(string id) => GetMissionHotSpots().FirstOrDefault(h => h.Id == id);

    /// <summary>Title/description are string-table keys, e.g. HOTSPOT:MyTitle; runtime links are configured by scripts.</summary>
    public MissionHotSpot AddMissionHotSpot(string id, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A hotspot ID is required.", nameof(id));
        if (GetMissionHotSpot(id) != null) throw new ArgumentException("Duplicate hotspot ID.", nameof(id));
        var item = MissionHotSpot.Of(id, title, description);
        EnsureEditableAsset(AssetNameConst.MissionHotSpots, MissionHotSpotsAsset.Default).HotSpots.Add(item);
        return item;
    }

    public bool RemoveMissionHotSpot(string id)
    {
        var asset = GetEditableAsset<MissionHotSpotsAsset>(AssetNameConst.MissionHotSpots);
        var item = asset?.HotSpots.FirstOrDefault(h => h.Id == id);
        if (item == null) return false;
        asset!.HotSpots.Remove(item); return true;
    }

    /// <summary>Connections between waypoint IDs; existing GetWaypoints() still returns waypoint objects.</summary>
    public List<WaypointConnection> GetWaypointConnections() => GetEditableAsset<WaypointsListAsset>(AssetNameConst.WaypointsList)?.Connections.ToList() ?? new();

    /// <summary>Adds an ordered connection between two existing waypoint IDs.</summary>
    public WaypointConnection AddWaypointConnection(int fromWaypointId, int toWaypointId)
    {
        var ids = GetWaypoints().Select(w => w.WaypointID).ToHashSet();
        if (!ids.Contains(fromWaypointId) || !ids.Contains(toWaypointId))
            throw new ArgumentException("Both waypoint IDs must exist in this map.");
        if (fromWaypointId == toWaypointId) throw new ArgumentException("A waypoint cannot connect to itself.");
        var existing = GetWaypointConnections().FirstOrDefault(c => c.FromWaypointId == fromWaypointId && c.ToWaypointId == toWaypointId);
        if (existing != null) return existing;
        var connection = WaypointConnection.Of(fromWaypointId, toWaypointId);
        EnsureEditableAsset(AssetNameConst.WaypointsList, WaypointsListAsset.Default).Connections.Add(connection);
        return connection;
    }

    public WaypointConnection AddWaypointConnection(string fromWaypointName, string toWaypointName)
    {
        var waypoints = GetWaypoints();
        var from = waypoints.SingleOrDefault(w => w.WaypointName == fromWaypointName) ?? throw new KeyNotFoundException(fromWaypointName);
        var to = waypoints.SingleOrDefault(w => w.WaypointName == toWaypointName) ?? throw new KeyNotFoundException(toWaypointName);
        return AddWaypointConnection(from.WaypointID, to.WaypointID);
    }

    public bool RemoveWaypointConnection(int fromWaypointId, int toWaypointId)
    {
        var asset = GetEditableAsset<WaypointsListAsset>(AssetNameConst.WaypointsList);
        var connections = asset?.Connections.Where(c => c.FromWaypointId == fromWaypointId && c.ToWaypointId == toWaypointId).ToList();
        if (connections == null || connections.Count == 0) return false;
        foreach (var connection in connections) asset!.Connections.Remove(connection);
        return true;
    }
}
