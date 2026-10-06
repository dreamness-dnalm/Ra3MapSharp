using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Commands.Objects;
using Dreamness.RA3.Map.Automation.Geometry;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed partial class ValidateMapHandler
{
    private sealed class ResourceAccessRule
    {
        public string TypeName { get; set; } = "";
        public string AccessLabel { get; set; } = "access";
        public int NearestCount { get; set; } = 1;
        public int MaxDistanceCells { get; set; } = int.MaxValue;
        public int MaxRankDistanceSpreadCells { get; set; }
        public bool RequireAllResourcesReachable { get; set; } = true;
    }

    private static void ValidateAccessRules(ResourceAccessRule[] rules)
    {
        if (rules == null || rules.Length > 16 || rules.Any(r => r == null || !ObjectHandler.ValidTypeName(r.TypeName)
                || string.IsNullOrWhiteSpace(r.AccessLabel) || r.AccessLabel.Length > 64 || r.NearestCount is < 1 or > 32
                || r.MaxDistanceCells < 0 || r.MaxRankDistanceSpreadCells < 0)
            || rules.Select(r => r.TypeName).Distinct(StringComparer.Ordinal).Count() != rules.Length)
            throw new AutomationException("INVALID_ARGUMENT", "resourceAccess 最多16项且类型唯一；accessLabel 长度1–64，nearestCount 为1–32，距离阈值非负。");
    }

    private static void CheckResourceAccess(Ra3MapFacade map, TerrainTraversal grid, ObjectFootprintSet footprints,
        RoutePoint[] origins, ResourceAccessRule[] rules, Action<string, bool, object> check, CancellationToken token)
    {
        // One BFS per player, shared across all resource rules. Distances count planar cells, not travel time.
        var fields = origins.Select(p => grid.DistancesFrom(p, token)).ToArray();
        foreach (var rule in rules)
        {
            var targets = map.GetUnitObjects().Where(o => o.TypeName == rule.TypeName).ToArray();
            var distances = new List<int?[]>();
            var rows = new List<object>();
            foreach (var target in targets)
            {
                token.ThrowIfCancellationRequested();
                var box = footprints.At(target.TypeName, target.Position.X / 10d, target.Position.Y / 10d,
                    MapAngles.ToRadians(target.Angle)).Single(b => b.Label == rule.AccessLabel);
                var best = Enumerable.Repeat<int?>(null, origins.Length).ToArray();
                var accessCells = 0;
                for (var y = (int)Math.Max(0, Math.Floor(box.MinY)); y < Math.Min(grid.Height, Math.Ceiling(box.MaxY)); y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (var x = (int)Math.Max(0, Math.Floor(box.MinX)); x < Math.Min(grid.Width, Math.Ceiling(box.MaxX)); x++)
                    {
                        if (grid.Blocked[x, y] || !box.ContainsPoint(x + .5, y + .5)) continue;
                        accessCells++;
                        for (var p = 0; p < fields.Length; p++)
                            if (fields[p][x, y] >= 0 && (!best[p].HasValue || fields[p][x, y] < best[p])) best[p] = fields[p][x, y];
                    }
                }
                distances.Add(best);
                rows.Add(new { resourceIndex = rows.Count, target.UniqueId, x = target.Position.X, y = target.Position.Y,
                    space = "world", accessCells, distanceCellsByPlayerSlot = best });
            }
            var nearest = Enumerable.Range(0, origins.Length).Select(p => distances.Select(d => d[p]).Where(d => d.HasValue)
                .Select(d => d!.Value).OrderBy(d => d).Take(rule.NearestCount).ToArray()).ToArray();
            var enough = nearest.All(n => n.Length == rule.NearestCount);
            var spreads = enough ? Enumerable.Range(0, rule.NearestCount)
                .Select(rank => nearest.Max(n => n[rank]) - nearest.Min(n => n[rank])).ToArray() : Array.Empty<int>();
            var unreachable = distances.Count(d => d.All(v => !v.HasValue));
            check("resource-access:" + rule.TypeName,
                enough && nearest.All(n => n.All(d => d <= rule.MaxDistanceCells))
                && spreads.All(d => d <= rule.MaxRankDistanceSpreadCells)
                && (!rule.RequireAllResourcesReachable || unreachable == 0),
                new { rule, resources = targets.Length, unreachableResources = unreachable,
                    players = nearest.Select((n, i) => new { playerSlot = i + 1, origin = origins[i], nearestDistanceCells = n }).ToArray(),
                    rankDistanceSpreadCells = spreads, enoughReachableResourcesPerPlayer = enough,
                    items = rows.Take(100).ToArray(), truncated = rows.Count > 100,
                    model = "four-neighbor-shortest-distance-to-access-cell-center", evaluated = true });
        }
    }
}
