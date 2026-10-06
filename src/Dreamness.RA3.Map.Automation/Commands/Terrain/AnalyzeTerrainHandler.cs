using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

internal sealed class AnalyzeTerrainHandler : ICommandHandler
{
    public string Name => "terrain.analyze";
    public CommandEffect Effect => CommandEffect.Query;

    public async Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<AnalyzeArgs>(arguments);
        if (args.Profile == null || args.Points == null || args.Points.Length > 100 || args.Routes == null || args.Routes.Length > 16
            || args.Routes.Any(r => r == null || r.From == null || r.To == null) || args.MaxRouteCells is < 1 or > 10000)
            throw new AutomationException("INVALID_ARGUMENT", "需要 profile，points 最多100项，routes 最多16项；每条需要 from/to，maxRouteCells 为1–10000。");
        return await session.ReadAnalysisMapAsync(args.PreparedPlanId, args.PlanHash, (map, revision, mapContentHash) =>
        {
        var grid = new TerrainTraversal(map, args.Profile, cancellationToken, args.Footprints);
        var points = args.Points.Select(p => new { p.X, p.Y, component = grid.ComponentAt(p.X, p.Y) }).ToArray();
        var notEvaluated = new List<string> { "objectCollision", "bridges", "gameMovementRules", "buildability" };
        if (!args.Profile.WaterLevel.HasValue) notEvaluated.Add("waterDepth");
        if (args.Footprints != null) { notEvaluated.Remove("objectCollision"); notEvaluated.AddRange(new[] { "engineCollision", "profileAccuracy", "waypointsAndRoads" }); }
        return new
        {
            revision, mapContentHash, args.PreparedPlanId, args.PlanHash, profile = args.Profile, space = "playableGrid",
            model = args.Footprints == null ? "terrain-only-four-neighbor-square-clearance" : "terrain-and-explicit-object-rectangles-four-neighbor-square-clearance-v1", grid.Width, grid.Height,
            grid.ObjectBlockedCells,
            footprintProfileHash = args.Footprints == null ? null : ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(args.Footprints, AutomationJson.Options)),
            grid.BlockedCells, grid.MaximumSlopeDegrees, componentSizes = grid.ComponentSizes,
            points, allPointsConnected = points.Length < 2 ? (bool?)null : points[0].component > 0 && points.All(p => p.component == points[0].component),
            routes = args.Routes.Select(r => new { r.From, r.To, route = grid.FindRoute(r.From!, r.To!, args.MaxRouteCells, cancellationToken) }).ToArray(),
            objectCount = map.GetUnitObjects().Count, notEvaluated
        };
        }, cancellationToken);
    }

    private sealed class AnalyzeArgs
    {
        public TerrainMovementProfile? Profile { get; set; }
        public GridPoint[] Points { get; set; } = Array.Empty<GridPoint>();
        public ObjectFootprint[]? Footprints { get; set; }
        public RouteArgs[] Routes { get; set; } = Array.Empty<RouteArgs>();
        public int MaxRouteCells { get; set; } = 2048;
        public string? PreparedPlanId { get; set; }
        public string? PlanHash { get; set; }
    }

    private sealed class RouteArgs
    {
        public RoutePoint? From { get; set; }
        public RoutePoint? To { get; set; }
    }

    private sealed class GridPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
