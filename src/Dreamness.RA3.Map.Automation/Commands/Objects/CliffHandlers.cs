using System.Text.Json;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Objects;

internal sealed class CliffHandler : ICommandHandler
{
    private readonly ObjectCatalog? _catalog;
    private readonly FootprintCatalog? _footprints;
    public CliffHandler(string name, ObjectCatalog? catalog, FootprintCatalog? footprints)
    { Name = name; _catalog = catalog; _footprints = footprints; }
    public string Name { get; }
    public CommandEffect Effect => Name == "terrain.detect_cliffs" ? CommandEffect.Query : CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<CliffArgs>(arguments);
        if (args.Region == null) throw new AutomationException("INVALID_ARGUMENT", "需要 region。");
        var map = session.Facade;
        var lines = CliffLines.Detect(map, args.Region, args.MinDrop, args.MinSlopeDegrees, token);
        if (Effect == CommandEffect.Query)
            return Task.FromResult<object?>(new { space = "playableGrid", model = "directed-dual-grid-height-discontinuity-v1",
                args.MinDrop, args.MinSlopeDegrees, highGroundSide = "left", lines,
                segmentCount = lines.Sum(l => l.Segments.Length),
                notEvaluated = new[] { "wideSteepSlopes", "engineCliffFlags", "modelFit" } });
        if (args.Style == null || string.IsNullOrWhiteSpace(args.Style.Name) || args.Style.Name.Length > 128
            || args.Style.Pieces == null || args.Style.Pieces.Length is < 1 or > 64 || args.Seed == null
            || !double.IsFinite(args.GapCells) || args.GapCells < 0 || args.GapCells > 128
            || args.MaxObjects is < 1 or > 2000 || args.Exclusions == null || args.Exclusions.Length > 100
            || args.BaseHeight is not ("terrain" or "low" or "high")
            || !double.IsFinite(args.MaxBendDegrees) || args.MaxBendDegrees < 0 || args.MaxBendDegrees > 180)
            throw new AutomationException("INVALID_ARGUMENT", "需要带1–64种 pieces 的命名 style、seed；gapCells 0–128、maxObjects 1–2000、baseHeight terrain/low/high、maxBendDegrees 0–180。");
        if (_catalog == null && args.CatalogHash != null) throw new AutomationException("CATALOG_UNAVAILABLE", "当前未加载素材目录。");
        var pieces = new List<ResolvedPiece>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in args.Style.Pieces)
        {
            if (p == null || !ObjectHandler.ValidTypeName(p.TypeName) || !names.Add(p.TypeName)
                || !double.IsFinite(p.AngleOffsetDegrees) || Math.Abs(p.AngleOffsetDegrees) > 360
                || !double.IsFinite(p.NormalOffsetCells) || Math.Abs(p.NormalOffsetCells) > 128
                || !double.IsFinite(p.OriginOffsetAlongCells) || Math.Abs(p.OriginOffsetAlongCells) > 128
                || !double.IsFinite(p.OriginOffsetNormalCells) || Math.Abs(p.OriginOffsetNormalCells) > 128
                || p.Role is not ("straight" or "outerCorner" or "innerCorner")
                || !double.IsFinite(p.ZOffset) || Math.Abs(p.ZOffset) > 4096)
                throw new AutomationException("INVALID_ARGUMENT", "pieces 类型必须唯一，角度、法线偏移与高度偏移需要有限且在允许范围内。");
            _catalog?.Require(p.TypeName, args.CatalogHash);
            var measured = _footprints?.Find(p.TypeName);
            var cos = Math.Abs(Math.Cos(p.AngleOffsetDegrees * Math.PI / 180));
            var sin = Math.Abs(Math.Sin(p.AngleOffsetDegrees * Math.PI / 180));
            var width = p.LengthCells ?? (measured == null ? (double?)null : measured.WidthCells * cos + measured.DepthCells * sin);
            var depth = p.DepthCells ?? (measured == null ? (double?)null : measured.WidthCells * sin + measured.DepthCells * cos);
            if (!width.HasValue || !depth.HasValue)
                throw new AutomationException("FOOTPRINT_MISSING", "请提供 lengthCells/depthCells 或测量占地: " + p.TypeName);
            if (!double.IsFinite(width.Value) || !double.IsFinite(depth.Value) || width.Value < .25 || width.Value > 4096 || depth.Value <= 0 || depth.Value > 4096)
                throw new AutomationException("INVALID_ARGUMENT", "lengthCells 为0.25–4096，depthCells 为正且最多4096格。");
            pieces.Add(new(p, width.Value, depth.Value, p.LengthCells.HasValue && p.DepthCells.HasValue ? "caller" : "catalog-or-mixed"));
        }
        var straightPieces = pieces.Where(p => p.Spec.Role == "straight").ToArray();
        if (straightPieces.Length == 0) throw new AutomationException("INVALID_ARGUMENT", "style 至少需要一个 straight 物体。");
        var border = map.MapBorderWidth;
        var selected = new HashSet<(int X, int Y)>(args.Region.GetCells(map).Select(c => (c.X - border, c.Y - border)));
        var excluded = new HashSet<(int X, int Y)>();
        foreach (var region in args.Exclusions.Concat(session.ProtectionZones.Where(z => z.Layers.Contains("objects")).Select(z => z.Region)))
        {
            if (region == null) throw new AutomationException("INVALID_ARGUMENT", "禁放区域不能为null。");
            excluded.UnionWith(region.GetCells(map).Select(c => (c.X - border, c.Y - border)));
        }
        var occupied = new List<OrientedFootprintBox>();
        if (args.AvoidExisting && map.GetUnitObjects().Count > 2000)
            throw new AutomationException("LIMIT_EXCEEDED", "已有对象占地检查最多2000个普通对象。");
        if (args.AvoidExisting)
            foreach (var obj in map.GetUnitObjects())
            {
                token.ThrowIfCancellationRequested();
                var p = pieces.FirstOrDefault(p => p.Spec.TypeName == obj.TypeName);
                var measured = _footprints?.Find(obj.TypeName);
                if (p == null && measured == null) throw new AutomationException("FOOTPRINT_MISSING", "已有对象缺少占地测量: " + obj.TypeName + "；可测量后重试，或明确设置 avoidExisting=false。");
                // Explicit length/depth describe the seam-aligned body, so undo the model's angle correction.
                var angle = MapAngles.ToRadians(obj.Angle) - (p?.Spec.AngleOffsetDegrees ?? 0) * Math.PI / 180;
                var originAlong = p?.Spec.OriginOffsetAlongCells ?? 0;
                var originNormal = p?.Spec.OriginOffsetNormalCells ?? 0;
                occupied.Add(new(new FootprintBox { WidthCells = p?.Length ?? measured!.WidthCells,
                    DepthCells = p?.Depth ?? measured!.DepthCells },
                    obj.Position.X / 10d - Math.Cos(angle) * originAlong + Math.Sin(angle) * originNormal,
                    obj.Position.Y / 10d - Math.Sin(angle) * originAlong - Math.Cos(angle) * originNormal, angle));
            }
        uint state = unchecked((uint)args.Seed.Value) ^ 0x9e3779b9;
        int Pick(int count) { state = unchecked(state * 1664525u + 1013904223u); return (int)((ulong)state * (uint)count >> 32); }
        var planned = new List<Placement>(); var skipped = 0; var cornersPlaced = 0;
        CliffLines.Point At(CliffLines.Line line, double distance)
        {
            distance = Math.Clamp(distance, 0, line.LengthCells);
            var i = Math.Min(line.Segments.Length - 1, (int)distance);
            var s = line.Segments[i]; var t = distance - i;
            return new(s.From.X + (s.To.X - s.From.X) * t, s.From.Y + (s.To.Y - s.From.Y) * t);
        }
        bool Fits(OrientedFootprintBox box)
        {
            if (!box.Inside(map.MapPlayableWidth, map.MapPlayableHeight) || occupied.Any(box.Overlaps)) return false;
            for (var y = (int)Math.Floor(box.MinY); y < (int)Math.Ceiling(box.MaxY); y++)
            {
                token.ThrowIfCancellationRequested();
                for (var x = (int)Math.Floor(box.MinX); x < (int)Math.Ceiling(box.MaxX); x++)
                    if ((!selected.Contains((x, y)) || excluded.Contains((x, y)))
                        && box.Overlaps(new(new FootprintBox { WidthCells = 1, DepthCells = 1 }, x + .5, y + .5, 0))) return false;
            }
            return true;
        }
        void Plan(ResolvedPiece piece, double x, double y, double angle, CliffLines.Segment face, int lineId)
        {
            if (planned.Count >= args.MaxObjects) throw new AutomationException("LIMIT_EXCEEDED", "放置数量超过 maxObjects，请缩小区域或增大间距。未修改地图。");
            var ground = TerrainHeight.Sample(map, x, y);
            var z = args.BaseHeight == "terrain" ? piece.Spec.ZOffset
                : (args.BaseHeight == "low" ? face.LowHeight : face.HighHeight) + piece.Spec.ZOffset - ground;
            if (!double.IsFinite(z) || Math.Abs(z) > float.MaxValue) throw new AutomationException("INVALID_ARGUMENT", "放置高度超出范围。");
            planned.Add(new(lineId, piece.Spec.TypeName, x, y, z,
                (angle * 180 / Math.PI + piece.Spec.AngleOffsetDegrees + 720) % 360));
        }
        var spans = new List<Span>();
        var useCorners = pieces.Any(p => p.Spec.Role != "straight");
        foreach (var line in lines)
        {
            if (!useCorners) { spans.Add(new(line, 0, line.LengthCells)); continue; }
            var runs = new List<Span>(); var start = 0;
            for (var i = 1; i < line.Segments.Length; i++)
            {
                var a = line.Segments[i - 1]; var b = line.Segments[i];
                if (a.To.X - a.From.X == b.To.X - b.From.X && a.To.Y - a.From.Y == b.To.Y - b.From.Y) continue;
                runs.Add(new(line, start, i)); start = i;
            }
            runs.Add(new(line, start, line.LengthCells));
            var joins = line.Closed ? runs.Count : runs.Count - 1;
            for (var i = 0; i < joins; i++)
            {
                token.ThrowIfCancellationRequested();
                var incoming = runs[i]; var outgoing = runs[(i + 1) % runs.Count];
                var a = line.Segments[(int)incoming.End - 1]; var b = line.Segments[(int)outgoing.Start];
                var dx = a.To.X - a.From.X; var dy = a.To.Y - a.From.Y;
                var cross = dx * (b.To.Y - b.From.Y) - dy * (b.To.X - b.From.X);
                if (cross == 0) continue;
                var candidates = pieces.Where(p => p.Spec.Role == (cross > 0 ? "outerCorner" : "innerCorner")).ToArray();
                if (candidates.Length == 0) continue;
                var piece = candidates[Pick(candidates.Length)]; var angle = Math.Atan2(dy, dx);
                var nx = -dy; var ny = dx;
                var x = a.To.X + nx * piece.Spec.NormalOffsetCells; var y = a.To.Y + ny * piece.Spec.NormalOffsetCells;
                var centerX = x - dx * piece.Spec.OriginOffsetAlongCells - nx * piece.Spec.OriginOffsetNormalCells;
                var centerY = y - dy * piece.Spec.OriginOffsetAlongCells - ny * piece.Spec.OriginOffsetNormalCells;
                var box = new OrientedFootprintBox(new FootprintBox { WidthCells = piece.Length, DepthCells = piece.Depth }, centerX, centerY, angle);
                var trimIncoming = Math.Max(0, piece.Length / 2 + piece.Spec.OriginOffsetAlongCells);
                var trimOutgoing = Math.Max(0, piece.Depth / 2 + (cross > 0 ? 1 : -1) * (piece.Spec.NormalOffsetCells - piece.Spec.OriginOffsetNormalCells));
                if (incoming.End - trimIncoming < incoming.Start || outgoing.Start + trimOutgoing > outgoing.End
                    || x < 0 || y < 0 || x >= map.MapPlayableWidth || y >= map.MapPlayableHeight || !Fits(box))
                { skipped++; continue; }
                Plan(piece, x, y, angle, a, line.Id); occupied.Add(box); cornersPlaced++;
                incoming.End -= trimIncoming; outgoing.Start += trimOutgoing;
            }
            spans.AddRange(runs);
        }
        foreach (var span in spans)
        {
            var line = span.Line; var cursor = span.Start;
            while (cursor < span.End)
            {
                token.ThrowIfCancellationRequested();
                var available = straightPieces.Where(p => cursor + p.Length <= span.End + 1e-7).ToArray();
                if (available.Length == 0) break;
                var piece = available[Pick(available.Length)]; var end = cursor + piece.Length;
                var from = At(line, cursor); var to = At(line, end); var middle = At(line, cursor + piece.Length / 2);
                var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
                var nx = -Math.Sin(angle); var ny = Math.Cos(angle);
                var x = middle.X + nx * piece.Spec.NormalOffsetCells; var y = middle.Y + ny * piece.Spec.NormalOffsetCells;
                var bend = 0d;
                for (var i = (int)cursor; i < Math.Min(line.Segments.Length, (int)Math.Ceiling(end)); i++)
                {
                    var s = line.Segments[i]; var dot = (s.To.X - s.From.X) * Math.Cos(angle) + (s.To.Y - s.From.Y) * Math.Sin(angle);
                    bend = Math.Max(bend, Math.Acos(Math.Clamp(dot, -1, 1)) * 180 / Math.PI);
                }
                var box = new OrientedFootprintBox(new FootprintBox { WidthCells = piece.Length, DepthCells = piece.Depth }, x, y, angle);
                var objectX = x + Math.Cos(angle) * piece.Spec.OriginOffsetAlongCells + nx * piece.Spec.OriginOffsetNormalCells;
                var objectY = y + Math.Sin(angle) * piece.Spec.OriginOffsetAlongCells + ny * piece.Spec.OriginOffsetNormalCells;
                if (bend > args.MaxBendDegrees || !Fits(box) || objectX < 0 || objectY < 0
                    || objectX >= map.MapPlayableWidth || objectY >= map.MapPlayableHeight) skipped++;
                else
                {
                    var face = line.Segments[Math.Min(line.Segments.Length - 1, (int)(cursor + piece.Length / 2))];
                    // Map object Z is relative to the terrain underneath its origin, as verified
                    // with real WB renders. Writing the absolute height here makes the prop float.
                    Plan(piece, objectX, objectY, angle, face, line.Id);
                    occupied.Add(box);
                }
                cursor = end + args.GapCells;
            }
        }
        var ids = new List<string>();
        foreach (var p in planned)
        {
            token.ThrowIfCancellationRequested();
            var obj = map.AddUnitObject(p.TypeName, (float)(p.X * 10), (float)(p.Y * 10), (float)p.Z);
            obj.Angle = (float)p.AngleDegrees;
            ids.Add(session.Handles.RegisterNewUnit());
        }
        return Task.FromResult<object?>(new { style = args.Style.Name, args.Seed, space = "playableGrid",
            algorithm = "dual-grid-cliff-arc-fit-lcg32-v2", lineCount = lines.Length, placed = ids.Count, skipped, cornersPlaced,
            objectIds = ids, placements = planned, dimensions = pieces.Select(p => new { p.Spec.TypeName, lengthCells = p.Length, depthCells = p.Depth, source = p.Source }),
            catalogHash = _catalog?.ContentHash, assetValidation = _catalog == null ? "unverified" : "editor-declared",
            heightModel = "terrain-relative-z-bilinear-base-conversion", args.BaseHeight,
            args.AvoidExisting, notEvaluated = new[] { "wideSteepSlopes", "engineCollision", "modelPivotAndOrientation", "visualSeamFit" } });
    }

    private sealed record ResolvedPiece(Piece Spec, double Length, double Depth, string Source);
    private sealed class Span
    {
        public CliffLines.Line Line { get; }
        public double Start { get; set; }
        public double End { get; set; }
        public Span(CliffLines.Line line, double start, double end) { Line = line; Start = start; End = end; }
    }
    private sealed record Placement(int LineId, string TypeName, double X, double Y, double Z, double AngleDegrees);
    private sealed class CliffArgs
    {
        public GridRegion? Region { get; set; }
        public double MinDrop { get; set; } = 20;
        public double MinSlopeDegrees { get; set; } = 60;
        public Style? Style { get; set; }
        public int? Seed { get; set; }
        public double GapCells { get; set; }
        public double MaxBendDegrees { get; set; } = 50;
        public int MaxObjects { get; set; } = 2000;
        public bool AvoidExisting { get; set; } = true;
        public string BaseHeight { get; set; } = "terrain";
        public string? CatalogHash { get; set; }
        public GridRegion[] Exclusions { get; set; } = Array.Empty<GridRegion>();
    }
    private sealed class Style
    {
        public string Name { get; set; } = "";
        public Piece[]? Pieces { get; set; }
    }
    private sealed class Piece
    {
        public string TypeName { get; set; } = "";
        public string Role { get; set; } = "straight";
        public double? LengthCells { get; set; }
        public double? DepthCells { get; set; }
        public double AngleOffsetDegrees { get; set; }
        public double NormalOffsetCells { get; set; }
        public double OriginOffsetAlongCells { get; set; }
        public double OriginOffsetNormalCells { get; set; }
        public double ZOffset { get; set; }
    }
}
