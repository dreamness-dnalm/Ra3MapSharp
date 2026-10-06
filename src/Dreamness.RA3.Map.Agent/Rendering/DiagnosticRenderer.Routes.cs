using Dreamness.RA3.Map.Automation.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamness.RA3.Map.Agent.Rendering;

public sealed class DiagnosticRouteRequest
{
    public RoutePoint? From { get; set; }
    public RoutePoint? To { get; set; }
}
public sealed record DiagnosticRouteOverlay(int Index, string Color, RoutePoint From, RoutePoint To,
    GridRoute Route, bool FromBlocked, bool ToBlocked);

public static partial class DiagnosticRenderer
{
    private static Rgba32 RouteColor(int index) => new[] {
        new Rgba32(0, 245, 255), new Rgba32(255, 230, 0), new Rgba32(255, 125, 0), new Rgba32(170, 100, 255),
        new Rgba32(255, 255, 255), new Rgba32(0, 255, 110), new Rgba32(255, 60, 190), new Rgba32(90, 160, 255)
    }[index % 8];

    private static void DrawRoutes(Image<Rgba32> image, int width, int height, IReadOnlyList<DiagnosticRouteOverlay> routes,
        CancellationToken token)
    {
        (int X, int Y) Pixel(RoutePoint point) => (
            Math.Clamp((int)((point.X + .5) * image.Width / width), 0, image.Width - 1),
            Math.Clamp(image.Height - 1 - (int)((point.Y + .5) * image.Height / height), 0, image.Height - 1));
        void Dot(int x, int y, Rgba32 color)
        {
            if (x >= 0 && y >= 0 && x < image.Width && y < image.Height) image[x, y] = color;
        }
        void Line((int X, int Y) from, (int X, int Y) to, Rgba32 color)
        {
            var x = from.X; var y = from.Y;
            var dx = Math.Abs(to.X - x); var dy = -Math.Abs(to.Y - y);
            var sx = x < to.X ? 1 : -1; var sy = y < to.Y ? 1 : -1; var error = dx + dy;
            while (true)
            {
                Dot(x, y, color);
                if (x == to.X && y == to.Y) break;
                var twice = 2 * error;
                if (twice >= dy) { error += dy; x += sx; }
                if (twice <= dx) { error += dx; y += sy; }
            }
        }
        // Draw segments only from returned path cells. Never connect disconnected endpoints or a truncated tail.
        foreach (var overlay in routes)
            for (var i = 1; i < overlay.Route.Cells.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                Line(Pixel(overlay.Route.Cells[i - 1]), Pixel(overlay.Route.Cells[i]), RouteColor(overlay.Index));
            }
        foreach (var overlay in routes)
        {
            token.ThrowIfCancellationRequested();
            Marker(Pixel(overlay.From), false, overlay.FromBlocked);
            Marker(Pixel(overlay.To), true, overlay.ToBlocked);
        }
        void Marker((int X, int Y) pixel, bool end, bool blocked)
        {
            var color = blocked ? new Rgba32(255, 255, 255) : end ? new Rgba32(255, 102, 204) : new Rgba32(0, 255, 154);
            for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
                if (blocked ? Math.Abs(dx) == Math.Abs(dy) : end || dx * dx + dy * dy <= 9)
                    Dot(pixel.X + dx, pixel.Y + dy, color);
        }
    }
}
