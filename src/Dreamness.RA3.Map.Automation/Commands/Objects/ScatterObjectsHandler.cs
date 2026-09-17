using System.Text.Json;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Objects;

internal sealed class ScatterObjectsHandler : ICommandHandler
{
    private readonly ObjectCatalog? _catalog;
    public ScatterObjectsHandler(ObjectCatalog? catalog) => _catalog = catalog;
    public string Name => "objects.scatter";
    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var map = session.Facade;
        var args = AutomationJson.Deserialize<ScatterArgs>(arguments);
        if (args.Region == null || args.Profile == null || args.Seed == null || args.Count < 1 || args.Count > 2000
            || !float.IsFinite(args.MinDistanceCells) || args.MinDistanceCells < 1
            || args.TypeNames == null || args.TypeNames.Length < 1 || args.TypeNames.Length > 64
            || args.Exclusions == null || args.Exclusions.Length > 100)
            throw new AutomationException("INVALID_ARGUMENT", "散布需要 region、profile、seed、1–2000个对象、至少1格间距和1–64个类型；禁放区最多100个。");
        foreach (var type in args.TypeNames)
        {
            if (!ObjectHandler.ValidTypeName(type)) throw new AutomationException("INVALID_ARGUMENT", "typeNames 包含非法普通对象资源名。");
            _catalog?.Require(type, args.CatalogHash);
        }
        if (_catalog == null && args.CatalogHash != null)
            throw new AutomationException("CATALOG_UNAVAILABLE", "当前未加载目录。");
        var cells = args.Region.Cells(map);
        var polygon = args.Region.Kind == "polygon" ? new GridPolygon(args.Region.Vertices) : null;
        var excluded = new HashSet<(int X, int Y)>();
        foreach (var region in args.Exclusions)
        {
            if (region == null) throw new AutomationException("INVALID_ARGUMENT", "禁放区域不能为null。");
            excluded.UnionWith(region.Cells(map));
        }
        foreach (var zone in session.ProtectionZones.Where(zone => zone.Layers.Contains("objects")))
            excluded.UnionWith(zone.Region.Cells(map));
        var traversal = new TerrainTraversal(map, args.Profile, token);
        var border = map.MapBorderWidth;
        var footprintSet = args.Footprints == null ? null : new ObjectFootprintSet(args.Footprints);
        var occupied = new List<OrientedFootprintBox>();
        if (footprintSet != null)
        {
            var existing = map.GetUnitObjects();
            if (existing.Count + args.Count > 2000) throw new AutomationException("LIMIT_EXCEEDED", "占地散布当前最多2000个普通对象。");
            foreach (var type in existing.Select(o => o.TypeName).Concat(args.TypeNames).Distinct(StringComparer.Ordinal))
                if (!footprintSet.Contains(type)) throw new AutomationException("FOOTPRINT_MISSING", "占地散布必须覆盖已有及待放置对象类型: " + type);
            foreach (var obj in existing)
                occupied.AddRange(footprintSet.At(obj.TypeName, obj.Position.X / 10d, obj.Position.Y / 10d, MapAngles.ToRadians(obj.Angle)));
        }
        bool FootprintHasSpace(OrientedFootprintBox[] boxes)
        {
            foreach (var box in boxes)
            {
                if (!box.Inside(map.MapPlayableWidth, map.MapPlayableHeight) || occupied.Any(box.Overlaps)) return false;
                // Conservatively check every blocked cell intersected by the rotated rectangle,
                // including access rectangles; checking only the object center is insufficient.
                for (var y = Math.Max(0, (int)Math.Floor(box.MinY)); y < Math.Min(map.MapPlayableHeight, (int)Math.Ceiling(box.MaxY)); y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (var x = Math.Max(0, (int)Math.Floor(box.MinX)); x < Math.Min(map.MapPlayableWidth, (int)Math.Ceiling(box.MaxX)); x++)
                        if ((traversal.Blocked[x, y] || excluded.Contains((x + border, y + border)))
                            && box.Overlaps(new OrientedFootprintBox(new FootprintBox { WidthCells = 1, DepthCells = 1 }, x + .5, y + .5, 0))) return false;
                }
            }
            return true;
        }
        cells.RemoveAll(c => c.X < border || c.Y < border || c.X >= map.MapWidth - border || c.Y >= map.MapHeight - border
            || excluded.Contains(c) || traversal.Blocked[c.X - border, c.Y - border]);
        if (cells.Count == 0) throw new AutomationException("PLACEMENT_FAILED", "区域没有符合条件的格子。");
        var buckets = new Dictionary<(int X, int Y), List<(float X, float Y)>>();
        var spacing = args.MinDistanceCells;
        (int X, int Y) Bucket(float x, float y) => ((int)Math.Floor(x / spacing), (int)Math.Floor(y / spacing));
        void Insert(float x, float y)
        {
            var key = Bucket(x, y);
            if (!buckets.TryGetValue(key, out var values)) buckets.Add(key, values = new());
            values.Add((x, y));
        }
        foreach (var obj in map.GetUnitObjects()) Insert(obj.Position.X / 10f, obj.Position.Y / 10f);
        bool HasSpace(float x, float y)
        {
            var key = Bucket(x, y);
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                if (buckets.TryGetValue((key.X + dx, key.Y + dy), out var values))
                    foreach (var p in values)
                        if (Math.Pow(x - p.X, 2) + Math.Pow(y - p.Y, 2) < (double)spacing * spacing) return false;
            return true;
        }
        // Explicit PRNG algorithm avoids depending on System.Random implementation versions.
        uint state = unchecked((uint)args.Seed.Value) ^ 0x9e3779b9;
        double Random()
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return state / 4294967296d;
        }
        var placements = new List<(float X, float Y, string Type, float Angle)>();
        var attempts = 0;
        for (; attempts < args.Count * 100 && placements.Count < args.Count; attempts++)
        {
            token.ThrowIfCancellationRequested();
            var cell = cells[(int)(Random() * cells.Count)];
            var mapX = cell.X + .05f + (float)Random() * .9f;
            var mapY = cell.Y + .05f + (float)Random() * .9f;
            if (polygon != null)
            {
                var offset = args.Region.Space == "playableGrid" ? border : 0;
                if (!polygon.Contains(mapX - offset, mapY - offset)) continue;
            }
            if (args.Region.Kind == "circle")
            {
                var offset = args.Region.Space == "playableGrid" ? border : 0;
                if (Math.Pow(mapX - args.Region.CenterX - offset, 2) + Math.Pow(mapY - args.Region.CenterY - offset, 2)
                    > Math.Pow(args.Region.Radius, 2)) continue;
            }
            var x = mapX - border; var y = mapY - border;
            if (!HasSpace(x, y)) continue;
            var type = args.TypeNames[(int)(Random() * args.TypeNames.Length)];
            var angle = (float)(Random() * Math.PI * 2);
            if (footprintSet != null)
            {
                var boxes = footprintSet.At(type, x, y, angle);
                if (!FootprintHasSpace(boxes)) continue;
                occupied.AddRange(boxes);
            }
            placements.Add((x, y, type, angle));
            Insert(x, y);
        }
        if (placements.Count != args.Count)
            throw new AutomationException("PLACEMENT_FAILED", $"在有限采样次数内只能放置 {placements.Count}/{args.Count} 个对象；请降低数量、间距或扩大区域。");
        var ids = new List<string>();
        foreach (var p in placements)
        {
            token.ThrowIfCancellationRequested();
            var obj = map.AddUnitObject(p.Type, p.X * 10, p.Y * 10, 0);
            obj.Angle = MapAngles.ToDegrees(p.Angle);
            ids.Add(session.Handles.RegisterNewUnit());
        }
        return Task.FromResult<object?>(new { objectIds = ids, placed = ids.Count, attempts, seed = args.Seed,
            algorithm = "lcg32-cell-jitter-v2", catalogHash = _catalog?.ContentHash,
            assetValidation = _catalog == null ? "unverified" : "editor-declared",
            exclusionModel = "whole-selected-cells", spacingModel = footprintSet == null ? "object-centers" : "object-centers-and-explicit-oriented-rectangles-v1",
            footprintProfileHash = args.Footprints == null ? null : ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(args.Footprints, AutomationJson.Options)),
            notEvaluated = footprintSet == null ? new[] { "objectFootprints", "exactCollision", "gameMovementRules", "automaticGroundAttachment" }
                : new[] { "profileAccuracy", "engineCollision", "miningBehavior", "gameMovementRules", "automaticGroundAttachment" },
            waterEvaluated = args.Profile.WaterLevel.HasValue });
    }

    private sealed class ScatterArgs
    {
        public GridRegion? Region { get; set; }
        public GridRegion[] Exclusions { get; set; } = Array.Empty<GridRegion>();
        public TerrainMovementProfile? Profile { get; set; }
        public int? Seed { get; set; }
        public int Count { get; set; }
        public float MinDistanceCells { get; set; } = 2;
        public string[]? TypeNames { get; set; }
        public string? CatalogHash { get; set; }
        public ObjectFootprint[]? Footprints { get; set; }
    }
}
