using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

namespace Dreamness.RA3.Map.Automation.Session;

internal sealed class ObjectHandleTable
{
    private readonly List<string> _waypointIds = new();
    private readonly List<string> _unitIds = new();
    public IReadOnlyList<string> UnitObjectIds => _unitIds;
    private int _next = 1;

    public IReadOnlyList<string> WaypointObjectIds => _waypointIds;

    public int NextHandle
    {
        get => _next;
        set => _next = Math.Max(1, value);
    }

    public string RegisterNewWaypoint()
    {
        var id = "wp-" + _next++;
        _waypointIds.Add(id);
        return id;
    }

    public void Remove(string objectId)
    {
        if (!_waypointIds.Remove(objectId))
        {
            throw new AutomationException(
                "OBJECT_NOT_FOUND",
                $"未找到路径点: {objectId}",
                details: new Dictionary<string, string> { ["objectId"] = objectId });
        }
    }

    public WaypointWrap Resolve(Ra3MapFacade facade, string objectId)
    {
        var index = _waypointIds.IndexOf(objectId);
        if (index < 0)
        {
            throw new AutomationException(
                "OBJECT_NOT_FOUND",
                $"未找到路径点: {objectId}",
                details: new Dictionary<string, string> { ["objectId"] = objectId });
        }

        var waypoints = facade.GetWaypoints();
        if (index >= waypoints.Count)
        {
            throw new AutomationException(
                "VALIDATION_FAILED",
                "路径点句柄与地图对象数量不一致。");
        }

        return waypoints[index];
    }

    public string RegisterNewUnit()
    {
        var id = "obj-" + _next++;
        _unitIds.Add(id);
        return id;
    }

    public UnitObjectWrap ResolveUnit(Ra3MapFacade facade, string objectId)
    {
        var index = _unitIds.IndexOf(objectId);
        if (index < 0) throw new AutomationException("OBJECT_NOT_FOUND", $"未找到普通对象: {objectId}");
        EnsureMatches(facade);
        return facade.GetUnitObjects()[index];
    }

    public void RemoveUnit(string objectId)
    {
        if (!_unitIds.Remove(objectId))
            throw new AutomationException("OBJECT_NOT_FOUND", $"未找到普通对象: {objectId}");
    }

    public void Import(IReadOnlyList<string> waypointObjectIds, int nextHandle, IReadOnlyList<string> unitObjectIds)
    {
        _unitIds.Clear();
        _unitIds.AddRange(unitObjectIds);
        _waypointIds.Clear();
        _waypointIds.AddRange(waypointObjectIds);
        _next = Math.Max(nextHandle, 1);
        foreach (var id in _waypointIds.Concat(_unitIds))
        {
            var separator = id.IndexOf('-');
            if (separator > 0 && int.TryParse(id.AsSpan(separator + 1), out var n))
            {
                _next = Math.Max(_next, n + 1);
            }
        }
    }

    public void RebuildFromMap(Ra3MapFacade facade)
    {
        _unitIds.Clear();
        _waypointIds.Clear();
        foreach (var _ in facade.GetWaypoints())
        {
            _waypointIds.Add("wp-" + _next++);
        }
        foreach (var _ in facade.GetUnitObjects()) RegisterNewUnit();
    }

    public void EnsureMatches(Ra3MapFacade facade)
    {
        if (_waypointIds.Count != facade.GetWaypoints().Count || _unitIds.Count != facade.GetUnitObjects().Count
            || _waypointIds.Concat(_unitIds).Distinct(StringComparer.Ordinal).Count() != _waypointIds.Count + _unitIds.Count
            || _waypointIds.Any(id => !Valid(id, "wp-")) || _unitIds.Any(id => !Valid(id, "obj-")))
        {
            throw new AutomationException(
                "VALIDATION_FAILED",
                "对象句柄无效或数量与地图不一致。");
        }
    }

    private static bool Valid(string id, string prefix) => id != null && id.StartsWith(prefix, StringComparison.Ordinal)
        && int.TryParse(id.AsSpan(prefix.Length), out var n) && n > 0 && n < int.MaxValue;
}
