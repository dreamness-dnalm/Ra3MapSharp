using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Geometry;

/// <summary>Continuous grid bounds; select cells by their centers. Rectangle bounds are half-open.</summary>
public sealed class GridRegion
{
    public string Kind { get; set; } = "rectangle";
    public string Space { get; set; } = "playableGrid";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Radius { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PolygonVertex[]? Vertices { get; set; }
    public IReadOnlyList<(int X, int Y)> GetCells(Ra3MapFacade map) => Cells(map).AsReadOnly();

    internal List<(int X, int Y)> Cells(Ra3MapFacade map)
    {
        var cells = new List<(int X, int Y)>();
        if (Kind == "rectangle")
        {
            Coordinates.ForEachRectangleCell(map, Space, X, Y, Width, Height, (x, y) => cells.Add((x, y)));
            return cells;
        }
        if (Kind == "polygon")
        {
            if (Space != Coordinates.PlayableGrid && Space != Coordinates.MapGrid) throw new AutomationException("INVALID_ARGUMENT", "多边形坐标必须为 playableGrid 或 mapGrid。");
            var polygon = new GridPolygon(Vertices);
            var regionWidth = Space == Coordinates.PlayableGrid ? map.MapPlayableWidth : map.MapWidth;
            var regionHeight = Space == Coordinates.PlayableGrid ? map.MapPlayableHeight : map.MapHeight;
            if (polygon.MinX < 0 || polygon.MinY < 0 || polygon.MaxX > regionWidth || polygon.MaxY > regionHeight)
                throw new AutomationException("INVALID_ARGUMENT", "多边形超出地图范围。");
            var border = Space == Coordinates.PlayableGrid ? map.MapBorderWidth : 0;
            for (var y = (int)Math.Floor(polygon.MinY); y < (int)Math.Ceiling(polygon.MaxY); y++)
            for (var x = (int)Math.Floor(polygon.MinX); x < (int)Math.Ceiling(polygon.MaxX); x++)
                if (polygon.Contains(x + .5, y + .5)) cells.Add((x + border, y + border));
            if (cells.Count == 0) throw new AutomationException("INVALID_ARGUMENT", "多边形未包含任何格心。");
            return cells;
        }
        if (Kind != "circle") throw new AutomationException("INVALID_ARGUMENT", "区域仅支持 rectangle、circle 或 polygon。");
        Coordinates.EnsureFinite("centerX", CenterX);
        Coordinates.EnsureFinite("centerY", CenterY);
        Coordinates.EnsureFinite("radius", Radius);
        if (Radius <= 0) throw new AutomationException("INVALID_ARGUMENT", "radius 必须为正。");
        if (Space != Coordinates.PlayableGrid && Space != Coordinates.MapGrid)
            throw new AutomationException("INVALID_ARGUMENT", "区域坐标必须为 playableGrid 或 mapGrid。");
        var width = Space == Coordinates.PlayableGrid ? map.MapPlayableWidth : map.MapWidth;
        var height = Space == Coordinates.PlayableGrid ? map.MapPlayableHeight : map.MapHeight;
        if (CenterX - Radius < 0 || CenterY - Radius < 0 || CenterX + Radius > width || CenterY + Radius > height)
            throw new AutomationException("INVALID_ARGUMENT", "圆形区域超出地图范围。");
        var offset = Space == Coordinates.PlayableGrid ? map.MapBorderWidth : 0;
        for (var y = (int)Math.Floor(CenterY - Radius); y < (int)Math.Ceiling(CenterY + Radius); y++)
        for (var x = (int)Math.Floor(CenterX - Radius); x < (int)Math.Ceiling(CenterX + Radius); x++)
            if (Math.Pow(x + .5 - CenterX, 2) + Math.Pow(y + .5 - CenterY, 2) <= Radius * Radius)
                cells.Add((x + offset, y + offset));
        if (cells.Count == 0) throw new AutomationException("INVALID_ARGUMENT", "区域未包含任何格心。");
        return cells;
    }
}
