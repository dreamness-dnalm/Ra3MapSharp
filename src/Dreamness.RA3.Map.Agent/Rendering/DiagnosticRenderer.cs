using System.Security.Cryptography;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamness.RA3.Map.Agent.Rendering;

public sealed record DiagnosticLegend(string Label, string Color, int? Cells = null);
public sealed record DiagnosticImage(string ImagePath, string ImageHash, string Layer, int Revision,
    string MapContentHash, string ProtectionHash, int Width, int Height, double[] PixelToPlayableGrid,
    IReadOnlyList<DiagnosticLegend> Legend, string[] NotEvaluated, TerrainMovementProfile? Profile,
    string? PreparedPlanId = null, string? PlanHash = null, string DesignHash = "",
    string? FootprintProfileHash = null, ObjectFootprint[]? Footprints = null,
    IReadOnlyList<DiagnosticRouteOverlay>? Routes = null) : IAgentImage;

public static partial class DiagnosticRenderer
{
    public static async Task<DiagnosticImage> RenderAsync(MapSnapshot snapshot, string directory, string layer,
        TerrainMovementProfile? profile = null, int maxEdge = 1024, CancellationToken token = default, ObjectFootprint[]? footprints = null,
        DiagnosticRouteRequest[]? routes = null)
    {
        if (layer is not ("height" or "textures" or "objects" or "passability" or "constraints") || maxEdge < 1 || maxEdge > 2048)
            throw new AutomationException("INVALID_ARGUMENT", "未知诊断图层或无效 maxEdge（1–2048）。");
        if (layer == "passability" && profile == null) throw new AutomationException("INVALID_ARGUMENT", "通行图层需要显式 profile。");
        if (footprints != null && layer != "passability") throw new AutomationException("INVALID_ARGUMENT", "footprints 当前仅支持 passability 图层。");
        if (routes != null && (layer != "passability" || routes.Length > 16 || routes.Any(r => r == null || r.From == null || r.To == null)))
            throw new AutomationException("INVALID_ARGUMENT", "routes 仅用于 passability 图层，最多16条，每条需要 from/to。");
        snapshot.VerifyIntegrity();
        var output = Path.Combine(Path.GetFullPath(directory), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var mapPath = Path.Combine(output, "scene.map");
        await File.WriteAllBytesAsync(mapPath, snapshot.MapBytes, token);
        var map = Ra3MapFacade.Open(mapPath);
        if (map.MapPlayableWidth != snapshot.PlayableWidth || map.MapPlayableHeight != snapshot.PlayableHeight || map.MapBorderWidth != snapshot.Border)
            throw new AutomationException("SNAPSHOT_CHANGED", "快照尺寸与地图不一致。");
        var width = snapshot.PlayableWidth; var height = snapshot.PlayableHeight; var border = snapshot.Border;
        var grid = new Rgba32[width, height];
        var legend = new List<DiagnosticLegend>();
        var overlays = new List<DiagnosticRouteOverlay>();
        var limitations = new List<string> { "realAppearance", "gameplayValidation" };
        var background = new Rgba32(30, 37, 49);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++) grid[x, y] = background;
        if (layer == "height")
        {
            var low = float.MaxValue; var high = float.MinValue;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            { var z = map.GetTerrainHeight(x + border, y + border); low = Math.Min(low, z); high = Math.Max(high, z); }
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var z = map.GetTerrainHeight(x + border, y + border);
                var intensity = (byte)(high == low ? 128 : 24 + 207 * (z - low) / (high - low));
                grid[x, y] = new Rgba32(intensity, intensity, intensity);
            }
            legend.Add(new DiagnosticLegend($"minHeight={low:R}", high == low ? "#808080" : "#181818"));
            legend.Add(new DiagnosticLegend($"maxHeight={high:R}", high == low ? "#808080" : "#E7E7E7"));
        }
        if (layer == "textures")
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) names.Add(map.GetTileTexture(x + border, y + border));
            var colors = names.Select((name, index) => (name, color: Color(index + 1))).ToDictionary(p => p.name, p => p.color);
            foreach (var pair in colors) legend.Add(new DiagnosticLegend(pair.Key, Hex(pair.Value)));
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) grid[x, y] = colors[map.GetTileTexture(x + border, y + border)];
            limitations.Add("textureBlendsAndMaterialAppearance");
        }
        if (layer == "passability")
        {
            var traversal = new TerrainTraversal(map, profile!, token, footprints);
            var blocked = new Rgba32(207, 60, 75);
            legend.Add(new DiagnosticLegend("blocked", Hex(blocked), traversal.BlockedCells));
            for (var i = 0; i < Math.Min(traversal.ComponentSizes.Count, 200); i++)
                legend.Add(new DiagnosticLegend("component-" + (i + 1), Hex(Color(i + 1)), traversal.ComponentSizes[i]));
            if (traversal.ComponentSizes.Count > 200) limitations.Add("legendTruncatedAfter200Components");
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) grid[x, y] = traversal.Blocked[x, y] ? blocked : Color(traversal.Components[x, y]);
            limitations.AddRange(footprints == null ? new[] { "objectCollision", "bridges", "gameMovementRules" }
                : new[] { "engineCollision", "profileAccuracy", "waypointsAndRoads", "bridges", "gameMovementRules" });
            if (!profile!.WaterLevel.HasValue) limitations.Add("waterDepth");
            foreach (var request in routes ?? Array.Empty<DiagnosticRouteRequest>())
            {
                var route = traversal.FindRoute(request.From!, request.To!, 10000, token);
                var index = overlays.Count;
                overlays.Add(new(index, Hex(RouteColor(index)), request.From!, request.To!, route,
                    traversal.Blocked[request.From!.X, request.From.Y], traversal.Blocked[request.To!.X, request.To.Y]));
                legend.Add(new DiagnosticLegend("route-" + index + ":" + route.Status, Hex(RouteColor(index))));
            }
        }
        if (layer == "constraints")
        {
            var index = 0;
            legend.Add(new DiagnosticLegend("unprotected", Hex(background)));
            foreach (var zone in snapshot.ProtectionZones)
            {
                var color = Color(++index);
                var cells = zone.Region.GetCells(map);
                legend.Add(new DiagnosticLegend(zone.Id + ":" + string.Join(",", zone.Layers), Hex(color), cells.Count));
                foreach (var (x, y) in cells)
                    if (x >= border && y >= border && x < width + border && y < height + border) grid[x - border, y - border] = color;
            }
            limitations.AddRange(new[] { "overlapUsesLastZoneColor", "objectFootprints" });
        }
        var scale = (double)maxEdge / Math.Max(width, height);
        var imageWidth = Math.Max(1, (int)Math.Round(width * scale));
        var imageHeight = Math.Max(1, (int)Math.Round(height * scale));
        using var image = new Image<Rgba32>(imageWidth, imageHeight);
        for (var v = 0; v < imageHeight; v++)
        {
            token.ThrowIfCancellationRequested();
            for (var u = 0; u < imageWidth; u++) image[u, v] = grid[Math.Min(width - 1, (int)((u + .5) * width / imageWidth)),
                height - 1 - Math.Min(height - 1, (int)((v + .5) * height / imageHeight))];
        }
        if (layer == "objects")
        {
            var regular = new Rgba32(249, 200, 74); var waypoint = new Rgba32(66, 184, 250); var road = new Rgba32(227, 112, 170);
            legend.AddRange(new[] { new DiagnosticLegend("regular object center", Hex(regular)), new DiagnosticLegend("waypoint center", Hex(waypoint)), new DiagnosticLegend("road node center", Hex(road)) });
            foreach (var obj in map.ra3Map.Context.ObjectsListAsset.MapObjectList)
            {
                var x = obj.Position.X / 10d; var y = obj.Position.Y / 10d;
                if (x < 0 || y < 0 || x >= width || y >= height) continue;
                var u = (int)(x * imageWidth / width); var v = imageHeight - 1 - (int)(y * imageHeight / height);
                var color = obj.IsWaypoint ? waypoint : obj.IsRoad ? road : regular;
                for (var dy = -3; dy <= 3; dy++)
                for (var dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy <= 9 && u + dx >= 0 && v + dy >= 0 && u + dx < imageWidth && v + dy < imageHeight)
                        image[u + dx, v + dy] = color;
            }
            limitations.AddRange(new[] { "objectFootprints", "occludedMarkers", "roadConnections" });
        }
        if (overlays.Count > 0)
        {
            DrawRoutes(image, width, height, overlays, token);
            legend.AddRange(new[] { new DiagnosticLegend("route start: circle", "#00FF9A"),
                new DiagnosticLegend("route end: square", "#FF66CC"), new DiagnosticLegend("blocked endpoint: X", "#FFFFFF") });
            limitations.AddRange(new[] { "routeOverlayOccludesUnderlyingCells", "routeStrokeIsDisplayWidthNotUnitClearance" });
            if (overlays.Any(r => r.Route.Truncated)) limitations.Add("routeDisplayTruncatedAfter10000Cells");
        }
        var imagePath = Path.Combine(output, layer + ".png");
        await image.SaveAsPngAsync(imagePath, token);
        var result = new DiagnosticImage(imagePath, Hash(await File.ReadAllBytesAsync(imagePath, token)), layer, snapshot.Revision,
            snapshot.ContentHash, snapshot.ProtectionHash, imageWidth, imageHeight,
            new[] { (double)width / imageWidth, 0, 0, 0, -(double)height / imageHeight, (double)height }, legend, limitations.ToArray(), profile,
            snapshot.PreparedPlanId, snapshot.PlanHash, snapshot.DesignHash,
            footprints == null ? null : ObjectFootprintSet.ContentHash(footprints), footprints, routes == null ? null : overlays);
        await File.WriteAllTextAsync(Path.Combine(output, "diagnostic.json"), System.Text.Json.JsonSerializer.Serialize(result, AgentJson.Options), token);
        return result;
    }

    private static Rgba32 Color(int id)
    {
        var bits = unchecked((uint)id * 2654435761u);
        return new Rgba32((byte)(64 + (bits & 127)), (byte)(64 + ((bits >> 8) & 127)), (byte)(64 + ((bits >> 16) & 127)));
    }
    private static string Hex(Rgba32 color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
