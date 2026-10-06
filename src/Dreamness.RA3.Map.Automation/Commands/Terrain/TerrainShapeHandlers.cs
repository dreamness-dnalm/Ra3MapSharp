using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

internal sealed class SculptTerrainHandler : ICommandHandler
{
    public string Name => "terrain.sculpt";
    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<SculptArgs>(arguments);
        if (args.Region == null) throw new AutomationException("INVALID_ARGUMENT", "region 必填。");
        if (args.Mode is not ("set" or "raise")) throw new AutomationException("INVALID_ARGUMENT", "mode 必须为 set 或 raise。");
        if (!float.IsFinite(args.Value)) throw new AutomationException("INVALID_ARGUMENT", "value 必须有限。");
        if (args.Mode == "set") TerrainHeight.Normalize(args.Value);
        if (args.Falloff is < 0 or > 1 || !float.IsFinite(args.Falloff))
            throw new AutomationException("INVALID_ARGUMENT", "falloff 必须在 0 到 1 之间。");
        var cells = args.Region.Cells(map);
        var polygon = args.Region.Kind == "polygon" ? new GridPolygon(args.Region.Vertices) : null;
        var offset = args.Region.Space == "playableGrid" ? map.MapBorderWidth : 0;
        var results = new List<(int X, int Y, float Height)>();
        foreach (var (x, y) in cells)
        {
            token.ThrowIfCancellationRequested();
            var weight = 1f;
            if (args.Falloff > 0)
            {
                var px = x - offset + .5;
                var py = y - offset + .5;
                double fraction;
                if (polygon != null)
                    fraction = 1 - polygon.DistanceToBoundary(px, py) / (Math.Min(polygon.MaxX - polygon.MinX, polygon.MaxY - polygon.MinY) / 2);
                else if (args.Region.Kind == "circle")
                    fraction = Math.Sqrt(Math.Pow(px - args.Region.CenterX, 2) + Math.Pow(py - args.Region.CenterY, 2)) / args.Region.Radius;
                else
                    fraction = Math.Max(Math.Abs((px - args.Region.X) / args.Region.Width * 2 - 1),
                        Math.Abs((py - args.Region.Y) / args.Region.Height * 2 - 1));
                var t = (float)Math.Clamp((1 - fraction) / args.Falloff, 0, 1);
                weight = t * t * (3 - 2 * t);
            }
            var old = map.GetTerrainHeight(x, y);
            var value = args.Mode == "raise" ? old + args.Value * weight : old + (args.Value - old) * weight;
            results.Add((x, y, TerrainHeight.Normalize(value)));
        }
        foreach (var cell in results) map.SetTerrainHeight(cell.X, cell.Y, cell.Height);
        return Task.FromResult<object?>(new SculptResult(results.Count, false) { Cells = cells });
    }
}

internal sealed class SmoothTerrainHandler : ICommandHandler
{
    public string Name => "terrain.smooth";
    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<SmoothArgs>(arguments);
        if (args.Region == null || args.Iterations is < 1 or > 32 || !float.IsFinite(args.Strength) || args.Strength is < 0 or > 1)
            throw new AutomationException("INVALID_ARGUMENT", "region 必填；iterations 为 1–32；strength 为 0–1。");
        var cells = args.Region.Cells(map);
        var values = new Dictionary<(int X, int Y), float>();
        foreach (var cell in cells)
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var x = cell.X + dx; var y = cell.Y + dy;
                if (x >= 0 && y >= 0 && x < map.MapWidth && y < map.MapHeight)
                    values[(x, y)] = map.GetTerrainHeight(x, y);
            }
        for (var iteration = 0; iteration < args.Iterations; iteration++)
        {
            var next = new Dictionary<(int X, int Y), float>(values);
            foreach (var cell in cells)
            {
                token.ThrowIfCancellationRequested();
                double sum = 0; var count = 0;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    if (values.TryGetValue((cell.X + dx, cell.Y + dy), out var value)) { sum += value; count++; }
                next[cell] = values[cell] + ((float)(sum / count) - values[cell]) * args.Strength;
            }
            values = next;
        }
        foreach (var cell in cells) map.SetTerrainHeight(cell.X, cell.Y, TerrainHeight.Normalize(values[cell]));
        return Task.FromResult<object?>(new { affectedCells = cells.Count, passabilityUpdated = false });
    }
}

internal sealed class SculptArgs
{
    public GridRegion? Region { get; set; }
    public string Mode { get; set; } = "set";
    public float Value { get; set; }
    public float Falloff { get; set; }
}

internal sealed class SmoothArgs
{
    public GridRegion? Region { get; set; }
    public int Iterations { get; set; } = 1;
    public float Strength { get; set; } = 1;
}
