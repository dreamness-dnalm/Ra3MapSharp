using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Commands.Objects;
using Dreamness.RA3.Map.Automation.Commands.Waypoints;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed partial class ValidateMapHandler : ICommandHandler
{
    public string Name => "map.validate";
    public CommandEffect Effect => CommandEffect.Query;
    public async Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<ValidationArgs>(arguments);
        ValidateAccessRules(args.ResourceAccess);
        if (args.ExpectedPlayers is < 1 or > 6 || args.Profile == null || args.Footprints == null
            || args.Resources == null || args.Resources.Length > 32 || args.Routes == null || args.Routes.Length > 16
            || args.BuildAreas == null || args.BuildAreas.Length > 16
            || args.BuildAreas.Any(a => a == null || string.IsNullOrWhiteSpace(a.Id) || a.Id.Length > 64 || a.Region == null
                || !double.IsFinite(a.MaxHeightDifference) || a.MaxHeightDifference < 0
                || (a.PlayerSlot.HasValue && (a.PlayerSlot < 1 || a.PlayerSlot > args.ExpectedPlayers)))
            || args.BuildAreas.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != args.BuildAreas.Length
            || args.Routes.Any(r => r == null || r.From == null || r.To == null)
            || args.Resources.Any(r => r == null || !ObjectHandler.ValidTypeName(r.TypeName) || r.MinCount < 0 || r.MaxCount < r.MinCount)
            || args.Resources.Select(r => r.TypeName).Distinct(StringComparer.Ordinal).Count() != args.Resources.Length)
            throw new AutomationException("INVALID_ARGUMENT", "需要1–6个预期玩家、profile、footprints；资源规则最多32项且类型唯一，路线最多16条；建造区域最多16项，id唯一且不超过64字符、区域必填、高度差有限非负、玩家编号在预期范围内。");
        var space = JsonSerializer.SerializeToElement(await session.AnalyzeObjectSpaceAsync(new AnalyzeObjectSpaceArgs
        { Footprints = args.Footprints, PreparedPlanId = args.PreparedPlanId, PlanHash = args.PlanHash }, token), AutomationJson.Options);
        return await session.ReadAnalysisMapAsync(args.PreparedPlanId, args.PlanHash, (map, revision, hash) =>
        {
            var checks = new List<object>(); var failures = 0;
            void Check(string id, bool passed, object details)
            {
                if (!passed) failures++;
                checks.Add(new { id, status = passed ? "passed" : "failed", details });
            }
            if (space.GetProperty("mapContentHash").GetString() != hash) throw new AutomationException("SNAPSHOT_CHANGED", "验收来源不一致。");
            var starts = map.GetWaypoints().Where(w => w.WaypointName.StartsWith("Player_", StringComparison.OrdinalIgnoreCase)
                && w.WaypointName.EndsWith("_Start", StringComparison.OrdinalIgnoreCase)).ToArray();
            var slots = starts.Select(w => PlayerStartHandler.Slot(w.WaypointName)).ToArray();
            Check("player-start-slots", starts.Length == args.ExpectedPlayers && slots.All(s => s.HasValue)
                && slots.Select(s => s!.Value).OrderBy(s => s).SequenceEqual(Enumerable.Range(1, args.ExpectedPlayers)),
                new { expected = args.ExpectedPlayers, actualNames = starts.Select(s => s.WaypointName).ToArray() });
            var invalidStarts = starts.Where(w => !float.IsFinite(w.Position.X) || !float.IsFinite(w.Position.Y) || !float.IsFinite(w.Position.Z)
                || w.Position.X < 0 || w.Position.Y < 0 || w.Position.X >= map.MapPlayableWidth * 10 || w.Position.Y >= map.MapPlayableHeight * 10).ToArray();
            Check("player-start-bounds", invalidStarts.Length == 0, invalidStarts.Select(w => w.WaypointName).ToArray());
            var duplicateCenters = starts.GroupBy(w => (w.Position.X, w.Position.Y)).Where(g => g.Count() > 1)
                .Select(g => g.Select(w => w.WaypointName).ToArray()).ToArray();
            Check("player-start-centers", duplicateCenters.Length == 0, duplicateCenters);
            var playerNames = map.GetPlayers().Select(p => p.Name).ToArray();
            var players = playerNames.ToHashSet(StringComparer.Ordinal);
            var missingPlayers = Enumerable.Range(1, args.ExpectedPlayers).Select(i => "Player_" + i).Where(p => !players.Contains(p)).ToArray();
            var duplicatePlayers = playerNames.GroupBy(p => p, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Check("player-definitions", missingPlayers.Length == 0 && duplicatePlayers.Length == 0, new { missingPlayers, duplicatePlayers });
            var teams = map.GetTeams().GroupBy(t => t.FullName).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            var objects = map.GetUnitObjects();
            var invalidOwners = objects.Where(o => !teams.TryGetValue(o.BelongToTeam, out var candidates)
                || candidates.Length != 1 || !players.Contains(candidates[0].OwnerPlayerName))
                .Select(o => new { o.UniqueId, o.TypeName, ownerTeam = o.BelongToTeam }).ToArray();
            Check("object-owners", invalidOwners.Length == 0, new { count = invalidOwners.Length, items = invalidOwners.Take(100).ToArray() });
            foreach (var resource in args.Resources)
            {
                var count = objects.Count(o => o.TypeName == resource.TypeName);
                Check("resource-count:" + resource.TypeName, count >= resource.MinCount && count <= resource.MaxCount,
                    new { actual = count, resource.MinCount, resource.MaxCount });
            }
            Check("object-space", space.GetProperty("clearUnderProfile").GetBoolean(), space);
            var covered = space.GetProperty("coverageComplete").GetBoolean();
            var grid = new TerrainTraversal(map, args.Profile, token, covered ? args.Footprints : null);
            var footprintSet = new ObjectFootprintSet(args.Footprints);
            foreach (var rule in args.ResourceAccess)
            {
                var definition = args.Footprints.SingleOrDefault(f => f.TypeName == rule.TypeName);
                var access = definition?.Boxes.SingleOrDefault(b => b.Label == rule.AccessLabel);
                if (access == null || access.BlocksMovement)
                    throw new AutomationException("INVALID_ARGUMENT", "resourceAccess 需要对应类型、label 且 blocksMovement=false 的预留矩形。");
            }
            var validStarts = invalidStarts.Length == 0 && starts.Length == args.ExpectedPlayers
                && slots.Where(s => s.HasValue).Select(s => s!.Value).OrderBy(s => s).SequenceEqual(Enumerable.Range(1, args.ExpectedPlayers));
            if (args.ResourceAccess.Length > 0)
            {
                if (covered && validStarts)
                {
                    var origins = starts.OrderBy(w => PlayerStartHandler.Slot(w.WaypointName))
                        .Select(w => new RoutePoint((int)(w.Position.X / 10), (int)(w.Position.Y / 10))).ToArray();
                    CheckResourceAccess(map, grid, footprintSet, origins, args.ResourceAccess, Check, token);
                }
                else foreach (var rule in args.ResourceAccess)
                    Check("resource-access:" + rule.TypeName, false, new { reason = "需要完整占地和有效出生点。", evaluated = false });
            }
            var occupied = new bool[grid.Width, grid.Height];
            if (args.BuildAreas.Length > 0)
                foreach (var box in objects.Where(o => footprintSet.Contains(o.TypeName)).SelectMany(o =>
                    footprintSet.At(o.TypeName, o.Position.X / 10d, o.Position.Y / 10d, MapAngles.ToRadians(o.Angle))))
                {
                    for (var y = (int)Math.Max(0, Math.Floor(box.MinY)); y < Math.Min(grid.Height, Math.Ceiling(box.MaxY)); y++)
                    {
                        token.ThrowIfCancellationRequested();
                        for (var x = (int)Math.Max(0, Math.Floor(box.MinX)); x < Math.Min(grid.Width, Math.Ceiling(box.MaxX)); x++)
                            if (!occupied[x, y]) occupied[x, y] = box.Overlaps(new OrientedFootprintBox(
                                new FootprintBox { WidthCells = 1, DepthCells = 1 }, x + .5, y + .5, 0));
                    }
                }
            foreach (var area in args.BuildAreas)
            {
                token.ThrowIfCancellationRequested();
                var cells = area.Region!.GetCells(map);
                var heights = cells.Select(c => map.GetTerrainHeight(c.X, c.Y)).ToArray();
                var minHeight = heights.Min(); var maxHeight = heights.Max();
                var outside = 0; var blocked = 0; var reserved = 0; var disconnected = 0;
                var matchingStarts = starts.Where(w => PlayerStartHandler.Slot(w.WaypointName) == area.PlayerSlot).ToArray();
                var component = area.PlayerSlot.HasValue && matchingStarts.Length == 1 && invalidStarts.Length == 0
                    ? grid.ComponentAt((int)(matchingStarts[0].Position.X / 10), (int)(matchingStarts[0].Position.Y / 10)) : 0;
                foreach (var cell in cells)
                {
                    token.ThrowIfCancellationRequested();
                    var x = cell.X - map.MapBorderWidth; var y = cell.Y - map.MapBorderWidth;
                    if (x < 0 || y < 0 || x >= grid.Width || y >= grid.Height) { outside++; continue; }
                    if (grid.Blocked[x, y]) blocked++;
                    if (area.PlayerSlot.HasValue && (component <= 0 || grid.ComponentAt(x, y) != component)) disconnected++;
                    if (occupied[x, y]) reserved++;
                }
                Check("build-area:" + area.Id, covered && outside == 0 && blocked == 0 && reserved == 0 && disconnected == 0
                    && maxHeight - minHeight <= area.MaxHeightDifference,
                    new { area.Region, area.PlayerSlot, cells = cells.Count, minHeight, maxHeight, area.MaxHeightDifference,
                        outsidePlayableCells = outside, blockedCells = blocked, occupiedOrReservedCells = reserved,
                        disconnectedCells = disconnected, footprintCoverageComplete = covered, model = "selected-whole-cells-under-explicit-profile" });
            }
            if (covered)
            {
                if (invalidStarts.Length == 0)
                {
                    var components = starts.Select(w => grid.ComponentAt((int)(w.Position.X / 10), (int)(w.Position.Y / 10))).ToArray();
                    Check("player-start-connectivity", components.Length == args.ExpectedPlayers && components.All(c => c > 0)
                        && components.Distinct().Count() == 1, new { components, model = "four-neighbor-square-clearance" });
                }
                for (var i = 0; i < args.Routes.Length; i++)
                {
                    var route = args.Routes[i];
                    var result = grid.FindRoute(route.From!, route.To!, 2048, token);
                    Check("route:" + i, result.Status == "found", new { route.From, route.To, result });
                }
            }
            else checks.Add(new { id = "routes", status = "not-evaluated", details = "缺少对象占地定义。" });
            return new { schemaVersion = 1, status = failures == 0 ? "passed" : "failed", failedChecks = failures,
                revision, mapContentHash = hash, args.PreparedPlanId, args.PlanHash,
                profileHash = ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(new { args.ExpectedPlayers, args.Profile, args.Footprints, args.Resources, args.Routes, args.BuildAreas, args.ResourceAccess }, AutomationJson.Options)),
                checks, validationScope = "requested-static-rules", resourcesChecked = args.Resources.Length, buildAreasChecked = args.BuildAreas.Length,
                resourceAccessRulesChecked = args.ResourceAccess.Length,
                notEvaluated = new[] { "engineBehavior", "engineBuildability", "resourceYieldAndStrategicBalance", "scriptSemantics", "visualQuality", "profileAccuracy" } };
        }, token);
    }
    private sealed class ValidationArgs
    {
        public int ExpectedPlayers { get; set; }
        public TerrainMovementProfile? Profile { get; set; }
        public ObjectFootprint[]? Footprints { get; set; }
        public ResourceRule[] Resources { get; set; } = Array.Empty<ResourceRule>();
        public ResourceAccessRule[] ResourceAccess { get; set; } = Array.Empty<ResourceAccessRule>();
        public ValidationRoute[] Routes { get; set; } = Array.Empty<ValidationRoute>();
        public BuildAreaRule[] BuildAreas { get; set; } = Array.Empty<BuildAreaRule>();
        public string? PreparedPlanId { get; set; }
        public string? PlanHash { get; set; }
    }
    private sealed class BuildAreaRule
    {
        public string Id { get; set; } = "";
        public GridRegion? Region { get; set; }
        public double MaxHeightDifference { get; set; } = 1;
        public int? PlayerSlot { get; set; }
    }
    private sealed class ResourceRule
    {
        public string TypeName { get; set; } = "";
        public int MinCount { get; set; }
        public int MaxCount { get; set; } = int.MaxValue;
    }
    private sealed class ValidationRoute
    {
        public RoutePoint? From { get; set; }
        public RoutePoint? To { get; set; }
    }
}
