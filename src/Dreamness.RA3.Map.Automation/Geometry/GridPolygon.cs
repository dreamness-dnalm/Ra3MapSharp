namespace Dreamness.RA3.Map.Automation.Geometry;

public sealed record PolygonVertex(double X, double Y);

/// <summary>A simple, finite polygon without holes; boundary points are included.</summary>
public sealed class GridPolygon
{
    private readonly PolygonVertex[] _vertices;
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }
    public GridPolygon(PolygonVertex[]? vertices)
    {
        if (vertices == null || vertices.Length is < 3 or > 64 || vertices.Any(p => p == null || !double.IsFinite(p.X) || !double.IsFinite(p.Y)
            || Math.Abs(p.X) > 1000000 || Math.Abs(p.Y) > 1000000))
            throw new AutomationException("INVALID_ARGUMENT", "polygon 需要3–64个有限坐标顶点。");
        _vertices = vertices.ToArray();
        if (_vertices.Distinct().Count() != _vertices.Length) throw new AutomationException("INVALID_ARGUMENT", "多边形顶点不能重复，末尾不重复首点。");
        MinX = vertices.Min(v => v.X); MaxX = vertices.Max(v => v.X);
        MinY = vertices.Min(v => v.Y); MaxY = vertices.Max(v => v.Y);
        double area = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i]; var b = vertices[(i + 1) % vertices.Length];
            var previous = vertices[(i + vertices.Length - 1) % vertices.Length];
            if (Math.Abs(Cross(previous, a, b.X, b.Y)) < 1e-8
                && (a.X - previous.X) * (b.X - a.X) + (a.Y - previous.Y) * (b.Y - a.Y) < 0)
                throw new AutomationException("INVALID_ARGUMENT", "相邻多边形边不能折返重叠。");
            area += a.X * b.Y - b.X * a.Y;
            for (var j = i + 1; j < vertices.Length; j++)
            {
                if (j == i + 1 || (i == 0 && j == vertices.Length - 1)) continue;
                if (Intersects(a, b, vertices[j], vertices[(j + 1) % vertices.Length]))
                    throw new AutomationException("INVALID_ARGUMENT", "多边形不能自交或自接触。");
            }
        }
        if (Math.Abs(area) < 1e-8) throw new AutomationException("INVALID_ARGUMENT", "多边形面积必须为正。");
    }
    private static double Cross(PolygonVertex a, PolygonVertex b, double x, double y) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
    private static bool OnSegment(PolygonVertex a, PolygonVertex b, double x, double y) => Math.Abs(Cross(a, b, x, y)) < 1e-8
        && x >= Math.Min(a.X, b.X) - 1e-8 && x <= Math.Max(a.X, b.X) + 1e-8 && y >= Math.Min(a.Y, b.Y) - 1e-8 && y <= Math.Max(a.Y, b.Y) + 1e-8;
    private static bool Intersects(PolygonVertex a, PolygonVertex b, PolygonVertex c, PolygonVertex d) =>
        OnSegment(a, b, c.X, c.Y) || OnSegment(a, b, d.X, d.Y) || OnSegment(c, d, a.X, a.Y) || OnSegment(c, d, b.X, b.Y)
        || (Math.Sign(Cross(a, b, c.X, c.Y)) != Math.Sign(Cross(a, b, d.X, d.Y))
            && Math.Sign(Cross(c, d, a.X, a.Y)) != Math.Sign(Cross(c, d, b.X, b.Y)));
    public bool Contains(double x, double y)
    {
        var inside = false;
        for (var i = 0; i < _vertices.Length; i++)
        {
            var a = _vertices[i]; var b = _vertices[(i + 1) % _vertices.Length];
            if (OnSegment(a, b, x, y)) return true;
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    public double DistanceToBoundary(double x, double y)
    {
        var minimum = double.PositiveInfinity;
        for (var i = 0; i < _vertices.Length; i++)
        {
            var a = _vertices[i]; var b = _vertices[(i + 1) % _vertices.Length];
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            minimum = Math.Min(minimum, Math.Sqrt(Math.Pow(x - a.X - t * dx, 2) + Math.Pow(y - a.Y - t * dy, 2)));
        }
        return minimum;
    }
}
