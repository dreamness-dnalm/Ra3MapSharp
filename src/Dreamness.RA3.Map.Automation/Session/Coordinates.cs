using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Session;

internal static class Coordinates
{
    public const string PlayableGrid = "playableGrid";
    public const string MapGrid = "mapGrid";
    public const string World = "world";

    public static void ToMapCell(
        Ra3MapFacade map,
        string? space,
        int x,
        int y,
        out int mapX,
        out int mapY)
    {
        space = NormalizeSpace(space);
        if (space == World)
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "地形操作不支持 world 坐标，请使用 playableGrid 或 mapGrid。");
        }

        if (space == MapGrid)
        {
            mapX = x;
            mapY = y;
        }
        else
        {
            EnsureRange(x, y, map.MapPlayableWidth, map.MapPlayableHeight);
            mapX = x + map.MapBorderWidth;
            mapY = y + map.MapBorderWidth;
        }

        EnsureMapCell(map, mapX, mapY);
    }

    public static void ToWorld(
        Ra3MapFacade map,
        string? space,
        float x,
        float y,
        string? anchor,
        out float worldX,
        out float worldY)
    {
        space = NormalizeSpace(space);
        anchor = string.IsNullOrWhiteSpace(anchor) ? "center" : anchor;
        EnsureFinite("x", x);
        EnsureFinite("y", y);
        if (space == PlayableGrid) EnsureRange(x, y, map.MapPlayableWidth, map.MapPlayableHeight);
        else if (space == MapGrid) EnsureRange(x, y, map.MapWidth, map.MapHeight);
        else
        {
            var border = map.MapBorderWidth * 10f;
            EnsureRange(x + border, y + border, map.MapWidth * 10f, map.MapHeight * 10f);
        }

        switch (space)
        {
            case World:
                worldX = x;
                worldY = y;
                return;
            case MapGrid:
            {
                var playableX = x - map.MapBorderWidth;
                var playableY = y - map.MapBorderWidth;
                ApplyGridAnchor(playableX, playableY, anchor, out worldX, out worldY);
                return;
            }
            default:
                ApplyGridAnchor(x, y, anchor, out worldX, out worldY);
                return;
        }
    }

    public static void ForEachRectangleCell(
        Ra3MapFacade map,
        string? space,
        int x,
        int y,
        int width,
        int height,
        Action<int, int> visit)
    {
        if (width <= 0 || height <= 0)
        {
            throw new AutomationException("INVALID_ARGUMENT", "区域宽高必须为正数。");
        }

        ToMapCell(map, space, x, y, out var minX, out var minY);
        var endX = (long)x + width - 1;
        var endY = (long)y + height - 1;
        if (endX > int.MaxValue || endY > int.MaxValue)
            throw new AutomationException("INVALID_ARGUMENT", "区域范围溢出。");
        ToMapCell(map, space, (int)endX, (int)endY, out var maxX, out var maxY);
        if (minX > maxX || minY > maxY)
        {
            throw new AutomationException("INVALID_ARGUMENT", "区域范围无效。");
        }

        for (var mapY = minY; mapY <= maxY; mapY++)
        {
            for (var mapX = minX; mapX <= maxX; mapX++)
            {
                visit(mapX, mapY);
            }
        }
    }

    public static void EnsureFinite(string name, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                $"{name} 不能为 NaN 或 Infinity。",
                details: new Dictionary<string, string> { ["name"] = name });
        }
    }

    private static void ApplyGridAnchor(
        float playableX,
        float playableY,
        string anchor,
        out float worldX,
        out float worldY)
    {
        if (string.Equals(anchor, "gridPoint", StringComparison.OrdinalIgnoreCase))
        {
            worldX = playableX * 10f;
            worldY = playableY * 10f;
            return;
        }

        if (!string.Equals(anchor, "center", StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "anchor 仅支持 center 或 gridPoint。");
        }

        worldX = (playableX + 0.5f) * 10f;
        worldY = (playableY + 0.5f) * 10f;
    }

    private static void EnsureMapCell(Ra3MapFacade map, int mapX, int mapY)
    {
        if (mapX < 0 || mapY < 0 || mapX >= map.MapWidth || mapY >= map.MapHeight)
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                $"网格坐标越界: ({mapX}, {mapY})。",
                details: new Dictionary<string, string>
                {
                    ["mapX"] = mapX.ToString(),
                    ["mapY"] = mapY.ToString(),
                    ["mapWidth"] = map.MapWidth.ToString(),
                    ["mapHeight"] = map.MapHeight.ToString()
                });
        }
    }

    private static string NormalizeSpace(string? space)
    {
        if (string.IsNullOrWhiteSpace(space)) return PlayableGrid;
        if (space is PlayableGrid or MapGrid or World) return space;
        throw new AutomationException("INVALID_ARGUMENT", $"未知坐标空间: {space}");
    }

    private static void EnsureRange(double x, double y, double width, double height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            throw new AutomationException("INVALID_ARGUMENT", $"坐标超出指定空间范围: ({x}, {y})。");
    }
}
