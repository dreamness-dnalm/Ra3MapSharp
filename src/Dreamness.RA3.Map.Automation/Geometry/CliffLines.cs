using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Geometry;

/// <summary>Height-discontinuity seams on the dual grid. Directed with high ground on the left.</summary>
public static class CliffLines
{
    public sealed record Point(double X, double Y);
    public sealed record Segment(Point From, Point To, double LowHeight, double HighHeight);
    public sealed record Line(int Id, Segment[] Segments, bool Closed)
    {
        public double LengthCells => Segments.Length;
    }

    public static Line[] Detect(Ra3MapFacade map, GridRegion region, double minDrop,
        double minSlopeDegrees, CancellationToken token = default)
    {
        if (!double.IsFinite(minDrop) || minDrop <= 0 || !double.IsFinite(minSlopeDegrees)
            || minSlopeDegrees <= 0 || minSlopeDegrees >= 90)
            throw new AutomationException("INVALID_ARGUMENT", "悬崖识别需要正的 minDrop 和 0–90 度之间的 minSlopeDegrees。");
        var selected = new HashSet<(int X, int Y)>(region.GetCells(map));
        var border = map.MapBorderWidth;
        var segments = new List<Segment>();
        foreach (var (mx, my) in selected.OrderBy(p => p.Y).ThenBy(p => p.X))
        {
            token.ThrowIfCancellationRequested();
            var x = mx - border; var y = my - border;
            if (x < 0 || y < 0 || x >= map.MapPlayableWidth || y >= map.MapPlayableHeight) continue;
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
            {
                if (!selected.Contains((mx + dx, my + dy)) || x + dx >= map.MapPlayableWidth || y + dy >= map.MapPlayableHeight) continue;
                var a = (double)map.GetTerrainHeight(mx, my);
                var b = (double)map.GetTerrainHeight(mx + dx, my + dy);
                var drop = Math.Abs(a - b);
                if (drop < minDrop || Math.Atan(drop / 10) * 180 / Math.PI < minSlopeDegrees) continue;
                // Clip dual edges to the playable boundary. Each accepted edge is exactly one cell.
                Point from, to;
                if (dx == 1)
                {
                    if (y == 0) continue;
                    from = new(x + .5, y - .5); to = new(x + .5, y + .5);
                }
                else
                {
                    if (x == 0) continue;
                    from = new(x + .5, y + .5); to = new(x - .5, y + .5);
                }
                if (a < b) (from, to) = (to, from);
                segments.Add(new(from, to, Math.Min(a, b), Math.Max(a, b)));
                if (segments.Count > 100000) throw new AutomationException("LIMIT_EXCEEDED", "悬崖边段超过100000，请缩小区域。");
            }
        }
        var outgoing = segments.Select((s, i) => (s, i)).GroupBy(p => p.s.From)
            .ToDictionary(g => g.Key, g => g.Select(p => p.i).ToArray());
        var incoming = segments.GroupBy(s => s.To).ToDictionary(g => g.Key, g => g.Count());
        var used = new bool[segments.Count];
        var lines = new List<Line>();
        void Trace(int first)
        {
            var path = new List<Segment>(); var index = first;
            while (!used[index])
            {
                token.ThrowIfCancellationRequested();
                used[index] = true; var edge = segments[index]; path.Add(edge);
                // Ambiguous junctions are split, never joined across different cliff faces.
                if (!outgoing.TryGetValue(edge.To, out var next) || next.Length != 1
                    || incoming[edge.To] != 1 || used[next[0]]) break;
                index = next[0];
            }
            lines.Add(new(lines.Count, path.ToArray(), path[^1].To == path[0].From));
        }
        for (var i = 0; i < segments.Count; i++)
            if (!used[i] && (!incoming.TryGetValue(segments[i].From, out var n) || n != 1
                || outgoing[segments[i].From].Length != 1)) Trace(i);
        for (var i = 0; i < segments.Count; i++) if (!used[i]) Trace(i);
        return lines.ToArray();
    }
}
