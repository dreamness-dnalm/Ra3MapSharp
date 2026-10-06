using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Geometry;

public sealed class TerrainMovementProfile
{
    public float MaxSlopeDegrees { get; set; } = 35;
    public float? WaterLevel { get; set; }
    public float MaxWaterDepth { get; set; }
    public int ClearanceCells { get; set; }
    public bool RespectPassability { get; set; } = true;
}

/// <summary>Conservative terrain and optional explicit object rectangles; not the game pathfinder.</summary>
public sealed class TerrainTraversal
{
    public int Width { get; }
    public int Height { get; }
    public bool[,] Blocked { get; }
    public int[,] Components { get; }
    public IReadOnlyList<int> ComponentSizes { get; }
    public int BlockedCells { get; }
    public float MaximumSlopeDegrees { get; }
    public int ObjectBlockedCells { get; }
    private static readonly (int X, int Y)[] Directions = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    public TerrainTraversal(Ra3MapFacade map, TerrainMovementProfile profile, CancellationToken token = default, ObjectFootprint[]? footprints = null)
    {
        if (!float.IsFinite(profile.MaxSlopeDegrees) || profile.MaxSlopeDegrees < 0 || profile.MaxSlopeDegrees >= 90
            || !float.IsFinite(profile.MaxWaterDepth) || profile.MaxWaterDepth < 0
            || (profile.WaterLevel.HasValue && !float.IsFinite(profile.WaterLevel.Value))
            || profile.ClearanceCells < 0 || profile.ClearanceCells > 32)
            throw new AutomationException("INVALID_ARGUMENT", "移动 profile 的坡度、水深或净空参数无效（净空范围0–32格）。");
        Width = map.MapPlayableWidth;
        Height = map.MapPlayableHeight;
        var raw = new bool[Width, Height];
        var border = map.MapBorderWidth;
        var maxGradient = Math.Tan(profile.MaxSlopeDegrees * Math.PI / 180);
        double maximum = 0;
        for (var y = 0; y < Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < Width; x++)
            {
                var h = map.GetTerrainHeight(x + border, y + border);
                double gradient = 0;
                foreach (var (dx, dy) in Directions)
                    if (Inside(x + dx, y + dy)) gradient = Math.Max(gradient,
                        Math.Abs(h - map.GetTerrainHeight(x + dx + border, y + dy + border)) / 10d);
                maximum = Math.Max(maximum, gradient);
                var flag = map.GetPassability(x + border, y + border);
                raw[x, y] = gradient > maxGradient + 1e-6
                    || (profile.WaterLevel.HasValue && profile.WaterLevel.Value - h > profile.MaxWaterDepth)
                    || (profile.RespectPassability && flag is "Impassable" or "ImpassableToPlayers");
            }
        }
        MaximumSlopeDegrees = (float)(Math.Atan(maximum) * 180 / Math.PI);
        if (footprints != null)
        {
            var set = new ObjectFootprintSet(footprints);
            var objects = map.GetUnitObjects();
            if (objects.Count > 2000) throw new AutomationException("LIMIT_EXCEEDED", "含对象占地的路线分析最多2000个普通对象。");
            var occupied = new bool[Width, Height];
            foreach (var obj in objects)
            {
                token.ThrowIfCancellationRequested();
                foreach (var box in set.MovementAt(obj.TypeName, obj.Position.X / 10d, obj.Position.Y / 10d, MapAngles.ToRadians(obj.Angle)))
                for (var y = Math.Max(0, (int)Math.Floor(box.MinY)); y < Math.Min(Height, (int)Math.Ceiling(box.MaxY)); y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (var x = Math.Max(0, (int)Math.Floor(box.MinX)); x < Math.Min(Width, (int)Math.Ceiling(box.MaxX)); x++)
                        if (!occupied[x, y] && box.Overlaps(new OrientedFootprintBox(new FootprintBox { WidthCells = 1, DepthCells = 1 }, x + .5, y + .5, 0)))
                        { occupied[x, y] = true; raw[x, y] = true; ObjectBlockedCells++; }
                }
            }
        }
        Blocked = new bool[Width, Height];
        // Square dilation deliberately overestimates circular clearance, including map edges.
        // A summed-area table makes clearance independent of the selected radius at runtime.
        var sum = new int[Width + 1, Height + 1];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            sum[x + 1, y + 1] = (raw[x, y] ? 1 : 0) + sum[x, y + 1] + sum[x + 1, y] - sum[x, y];
        var radius = profile.ClearanceCells;
        var blockedCount = 0;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var left = Math.Max(0, x - radius); var bottom = Math.Max(0, y - radius);
            var right = Math.Min(Width, x + radius + 1); var top = Math.Min(Height, y + radius + 1);
            Blocked[x, y] = x < radius || y < radius || x + radius >= Width || y + radius >= Height
                || sum[right, top] - sum[left, top] - sum[right, bottom] + sum[left, bottom] > 0;
            if (Blocked[x, y]) blockedCount++;
        }
        BlockedCells = blockedCount;
        Components = new int[Width, Height];
        var sizes = new List<int>();
        var queue = new Queue<(int X, int Y)>();
        for (var y = 0; y < Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < Width; x++)
            {
                if (Blocked[x, y] || Components[x, y] != 0) continue;
                var id = sizes.Count + 1;
                Components[x, y] = id;
                queue.Enqueue((x, y));
                var count = 0;
                while (queue.TryDequeue(out var cell))
                {
                    if ((count++ & 4095) == 0) token.ThrowIfCancellationRequested();
                    foreach (var (dx, dy) in Directions)
                    {
                        var nx = cell.X + dx; var ny = cell.Y + dy;
                        if (!Inside(nx, ny) || Blocked[nx, ny] || Components[nx, ny] != 0) continue;
                        Components[nx, ny] = id;
                        queue.Enqueue((nx, ny));
                    }
                }
                sizes.Add(count);
            }
        }
        ComponentSizes = sizes.AsReadOnly();
    }

    public int ComponentAt(int x, int y)
    {
        if (!Inside(x, y)) throw new AutomationException("INVALID_ARGUMENT", "诊断点超出可玩区域。");
        return Components[x, y];
    }

    public GridRoute FindRoute(RoutePoint from, RoutePoint to, int maxReturnedCells = 2048, CancellationToken token = default)
    {
        if (from == null || to == null || maxReturnedCells is < 1 or > 10000)
            throw new AutomationException("INVALID_ARGUMENT", "路线需要 from/to，返回格数为1–10000。");
        var startComponent = ComponentAt(from.X, from.Y);
        var endComponent = ComponentAt(to.X, to.Y);
        if (startComponent == 0 || endComponent == 0) return new GridRoute("blocked-endpoint", null, null, Array.Empty<RoutePoint>(), false);
        if (startComponent != endComponent) return new GridRoute("disconnected", null, null, Array.Empty<RoutePoint>(), false);
        var previous = new Dictionary<(int X, int Y), (int X, int Y)>();
        var start = (from.X, from.Y); var end = (to.X, to.Y);
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start); previous[start] = start;
        while (queue.TryDequeue(out var cell))
        {
            token.ThrowIfCancellationRequested();
            if (cell == end) break;
            foreach (var (dx, dy) in Directions)
            {
                var next = (X: cell.X + dx, Y: cell.Y + dy);
                if (!Inside(next.X, next.Y) || Blocked[next.X, next.Y] || previous.ContainsKey(next)) continue;
                previous[next] = cell; queue.Enqueue(next);
            }
        }
        var path = new List<RoutePoint>();
        for (var cell = end; ; cell = previous[cell])
        {
            path.Add(new RoutePoint(cell.Item1, cell.Item2));
            if (cell == start) break;
        }
        path.Reverse();
        return new GridRoute("found", path.Count - 1, (path.Count - 1) * 10d, path.Take(maxReturnedCells).ToArray(), path.Count > maxReturnedCells);
    }

    /// <summary>Shortest four-neighbor step counts; -1 means unreachable, including a blocked origin.</summary>
    public int[,] DistancesFrom(RoutePoint from, CancellationToken token = default)
    {
        if (from == null) throw new AutomationException("INVALID_ARGUMENT", "距离分析需要起点。");
        var component = ComponentAt(from.X, from.Y);
        var distances = new int[Width, Height];
        for (var y = 0; y < Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < Width; x++) distances[x, y] = -1;
        }
        if (component == 0) return distances;
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((from.X, from.Y)); distances[from.X, from.Y] = 0;
        while (queue.TryDequeue(out var cell))
        {
            token.ThrowIfCancellationRequested();
            foreach (var (dx, dy) in Directions)
            {
                var x = cell.X + dx; var y = cell.Y + dy;
                if (!Inside(x, y) || Blocked[x, y] || distances[x, y] >= 0) continue;
                distances[x, y] = distances[cell.X, cell.Y] + 1; queue.Enqueue((x, y));
            }
        }
        return distances;
    }

    private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}

public sealed record RoutePoint(int X, int Y);
public sealed record GridRoute(string Status, int? Steps, double? DistanceWorldUnits, IReadOnlyList<RoutePoint> Cells, bool Truncated);
