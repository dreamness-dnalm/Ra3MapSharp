using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

internal sealed class RampTerrainHandler : ICommandHandler
{
    public string Name => "terrain.ramp";
    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<RampArgs>(arguments);
        if (args.Polyline == null || args.Polyline.Length < 2 || args.Polyline.Length > 64
            || !float.IsFinite(args.WidthCells) || args.WidthCells < 2
            || !float.IsFinite(args.TransitionCells) || args.TransitionCells < 0
            || !float.IsFinite(args.MaxSlopeDegrees) || args.MaxSlopeDegrees < 0 || args.MaxSlopeDegrees >= 90)
            throw new AutomationException("INVALID_ARGUMENT", "坡道需要2–64个点、宽度至少2格、非负过渡宽度，以及0–90度之间的坡度限制。");
        var startHeight = args.StartHeight ?? throw new AutomationException("INVALID_ARGUMENT", "startHeight 必填。");
        var endHeight = args.EndHeight ?? throw new AutomationException("INVALID_ARGUMENT", "endHeight 必填。");
        TerrainHeight.Normalize(startHeight);
        TerrainHeight.Normalize(endHeight);
        var radius = args.WidthCells / 2d;
        var outer = radius + args.TransitionCells;
        var cumulative = new double[args.Polyline.Length];
        for (var i = 0; i < args.Polyline.Length; i++)
        {
            var p = args.Polyline[i];
            if (p == null || !float.IsFinite(p.X) || !float.IsFinite(p.Y)
                || p.X - outer < 0 || p.Y - outer < 0
                || p.X + outer > map.MapPlayableWidth || p.Y + outer > map.MapPlayableHeight)
                throw new AutomationException("INVALID_ARGUMENT", "坡道及圆头过渡带必须完全位于可玩区域。");
            if (i == 0) continue;
            var previous = args.Polyline[i - 1];
            var length = Math.Sqrt(Math.Pow(p.X - previous.X, 2) + Math.Pow(p.Y - previous.Y, 2));
            if (length < .001) throw new AutomationException("INVALID_ARGUMENT", "坡道相邻点不能重合。");
            cumulative[i] = cumulative[i - 1] + length;
        }
        var totalLength = cumulative[^1];
        var limit = Math.Tan(args.MaxSlopeDegrees * Math.PI / 180);
        if (Math.Abs(endHeight - startHeight) / (totalLength * 10) > limit + 1e-6)
            throw new AutomationException("SLOPE_LIMIT_EXCEEDED", "路径长度不足以满足端点高度差与坡度限制。");
        var left = (int)Math.Floor(args.Polyline.Min(p => p.X) - outer);
        var right = (int)Math.Ceiling(args.Polyline.Max(p => p.X) + outer);
        var bottom = (int)Math.Floor(args.Polyline.Min(p => p.Y) - outer);
        var top = (int)Math.Ceiling(args.Polyline.Max(p => p.Y) + outer);
        var changes = new Dictionary<(int X, int Y), float>();
        var core = new HashSet<(int X, int Y)>();
        for (var y = bottom; y < top; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = left; x < right; x++)
            {
                var bestDistance = double.PositiveInfinity;
                double station = 0;
                for (var i = 1; i < args.Polyline.Length; i++)
                {
                    var a = args.Polyline[i - 1]; var b = args.Polyline[i];
                    var dx = (double)b.X - a.X; var dy = (double)b.Y - a.Y;
                    var t = Math.Clamp(((x + .5 - a.X) * dx + (y + .5 - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
                    var distance = Math.Sqrt(Math.Pow(x + .5 - a.X - t * dx, 2) + Math.Pow(y + .5 - a.Y - t * dy, 2));
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    station = cumulative[i - 1] + t * (cumulative[i] - cumulative[i - 1]);
                }
                if (bestDistance > outer) continue;
                var weight = bestDistance <= radius ? 1 : 1 - (bestDistance - radius) / args.TransitionCells;
                weight = weight * weight * (3 - 2 * weight);
                var target = startHeight + (endHeight - startHeight) * station / totalLength;
                var old = map.GetTerrainHeight(x + map.MapBorderWidth, y + map.MapBorderWidth);
                changes[(x, y)] = TerrainHeight.Normalize((float)(old + (target - old) * weight));
                if (bestDistance <= radius) core.Add((x, y));
            }
        }
        double maxGradient = 0;
        foreach (var cell in core)
        foreach (var offset in new[] { (X: 1, Y: 0), (X: 0, Y: 1) })
        {
            var neighbor = (cell.X + offset.X, cell.Y + offset.Y);
            if (core.Contains(neighbor)) maxGradient = Math.Max(maxGradient, Math.Abs(changes[cell] - changes[neighbor]) / 10d);
        }
        if (maxGradient > limit + 1e-6)
            throw new AutomationException("SLOPE_LIMIT_EXCEEDED", "量化后的核心坡度超限；增加长度、简化折线或降低高度差。");
        foreach (var pair in changes)
            map.SetTerrainHeight(pair.Key.X + map.MapBorderWidth, pair.Key.Y + map.MapBorderWidth, pair.Value);
        return Task.FromResult<object?>(new RampResult(changes.Count, core.Count, totalLength, Math.Atan(maxGradient) * 180 / Math.PI,
            false, new[] { "externalConnections", "transitionSlope", "objectCollision", "gameMovementRules" })
            { Cells = changes.Keys.Select(c => (c.X + map.MapBorderWidth, c.Y + map.MapBorderWidth)).ToArray() });
    }

    private sealed class RampArgs
    {
        public Point[]? Polyline { get; set; }
        public float WidthCells { get; set; } = 4;
        public float TransitionCells { get; set; } = 2;
        public float? StartHeight { get; set; }
        public float? EndHeight { get; set; }
        public float MaxSlopeDegrees { get; set; } = 35;
    }
    private sealed class Point { public float X { get; set; } public float Y { get; set; } }
}
