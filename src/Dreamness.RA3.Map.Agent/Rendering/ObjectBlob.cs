using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamness.RA3.Map.Agent.Rendering;

/// <summary>
/// Locates an object against the deliberately uniform ground of a grid test render.
/// <para>
/// The render is an orthographic top-down view (its pixel-to-grid matrix carries no shear), so
/// the blob's pixel extent is the object's plan-view extent. That makes this the only place a
/// real footprint can come from: no declared sizes exist anywhere in the editor data.
/// </para>
/// <para>
/// A global bounding box over every pixel that differs from the ground would swallow whatever
/// neighbour or shadow reaches into the window, so the component nearest the centre wins.
/// </para>
/// </summary>
internal static class ObjectBlob
{
    /// <param name="image">A grid render whose ground is one uniform texture.</param>
    /// <param name="centreX">Object centre in image pixels.</param>
    /// <param name="centreY">Object centre in image pixels.</param>
    /// <param name="window">Search window side, in pixels.</param>
    /// <param name="fallback">Crop used when no object can be told apart from the ground.</param>
    /// <param name="minimum">Smallest crop allowed, so a tiny prop is still framed usefully.</param>
    /// <param name="reference">
    /// The same view with no objects. When supplied the mask is the difference from it, which is
    /// the only reliable rule: inferring the ground from the window's dominant colour inverts
    /// whenever the object covers most of the window, and then a large prop measures as a speck
    /// (or as the whole window).
    /// </param>
    /// <param name="threshold">Per-channel difference that counts as "not ground".</param>
    /// <returns>
    /// The crop to cut and, when an object was found, its bounding box in image pixels.
    /// </returns>
    public static (Rectangle Crop, Rectangle? Blob, double Coverage) Locate(Image<Rgba32> image, double centreX,
        double centreY, int window, int fallback, int minimum, Image<Rgba32>? reference = null, int threshold = 24)
    {
        var side = Math.Clamp(window, 1, Math.Min(image.Width, image.Height));
        var left = Math.Clamp((int)Math.Round(centreX - side / 2.0), 0, image.Width - side);
        var top = Math.Clamp((int)Math.Round(centreY - side / 2.0), 0, image.Height - side);

        var useReference = reference != null
            && reference.Width == image.Width && reference.Height == image.Height;
        int backR = 0, backG = 0, backB = 0;
        if (!useReference)
        {
            var histogram = new Dictionary<int, int>();
            for (var y = top; y < top + side; y += 2)
            {
                for (var x = left; x < left + side; x += 2)
                {
                    var pixel = image[x, y];
                    var key = (pixel.R >> 3 << 10) | (pixel.G >> 3 << 5) | (pixel.B >> 3);
                    histogram[key] = histogram.TryGetValue(key, out var count) ? count + 1 : 1;
                }
            }
            if (histogram.Count == 0) return (Centred(left, top, side, fallback, image), null, 0);
            var background = histogram.OrderByDescending(pair => pair.Value).First().Key;
            backR = ((background >> 10) & 31) << 3;
            backG = ((background >> 5) & 31) << 3;
            backB = (background & 31) << 3;
        }

        var mask = new bool[side * side];
        var marked = 0;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var pixel = image[left + x, top + y];
                int difference;
                if (useReference)
                {
                    var ground = reference![left + x, top + y];
                    difference = Math.Max(Math.Abs(pixel.R - ground.R),
                        Math.Max(Math.Abs(pixel.G - ground.G), Math.Abs(pixel.B - ground.B)));
                }
                else
                {
                    difference = Math.Max(Math.Abs(pixel.R - backR),
                        Math.Max(Math.Abs(pixel.G - backG), Math.Abs(pixel.B - backB)));
                }
                if (difference <= threshold) continue;
                mask[y * side + x] = true;
                marked++;
            }
        }

        var coverage = (double)marked / (side * (double)side);
        if (marked == 0 || coverage > 0.9) return (Centred(left, top, side, fallback, image), null, coverage);

        var blob = NearestComponent(mask, side, side / 2.0, side / 2.0);
        if (blob == null) return (Centred(left, top, side, fallback, image), null, coverage);

        var local = blob.Value;
        var footprint = Math.Max(local.Width, local.Height);
        var size = Math.Clamp((int)Math.Round(footprint * 1.35), Math.Min(minimum, side), side);
        var crop = new Rectangle(
            Math.Clamp(left + (int)Math.Round(local.X + local.Width / 2.0 - size / 2.0), 0, image.Width - size),
            Math.Clamp(top + (int)Math.Round(local.Y + local.Height / 2.0 - size / 2.0), 0, image.Height - size),
            size, size);
        return (crop, new Rectangle(left + local.X, top + local.Y, local.Width, local.Height), coverage);
    }

    private static Rectangle Centred(int left, int top, int side, int fallback, Image<Rgba32> image)
    {
        var size = Math.Min(fallback, side);
        return new Rectangle(left + (side - size) / 2, top + (side - size) / 2, size, size);
    }

    private static Rectangle? NearestComponent(bool[] mask, int side, double centreX, double centreY)
    {
        var visited = new bool[mask.Length];
        var stack = new Stack<int>();
        var minimumSize = Math.Max(4, (int)(mask.Length * 0.0004));
        Rectangle? best = null;
        var bestDistance = double.MaxValue;
        for (var start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            visited[start] = true;
            stack.Push(start);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, count = 0;
            while (stack.Count > 0)
            {
                var index = stack.Pop();
                var x = index % side;
                var y = index / side;
                count++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (x > 0) Push(mask, visited, stack, index - 1);
                if (x < side - 1) Push(mask, visited, stack, index + 1);
                if (y > 0) Push(mask, visited, stack, index - side);
                if (y < side - 1) Push(mask, visited, stack, index + side);
            }
            if (count < minimumSize) continue;
            var distance = Math.Abs((minX + maxX) / 2.0 - centreX) + Math.Abs((minY + maxY) / 2.0 - centreY);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        return best;
    }

    private static void Push(bool[] mask, bool[] visited, Stack<int> stack, int index)
    {
        if (!mask[index] || visited[index]) return;
        visited[index] = true;
        stack.Push(index);
    }
}
