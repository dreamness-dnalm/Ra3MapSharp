using System.Text.Json;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Agent.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamness.RA3.Map.Agent;

/// <summary>
/// Measures placement footprints from an album that was already rendered (--build-footprints).
/// <para>
/// No re-rendering is needed: the album records each object's grid position and each batch's
/// overview image, and the overview is orthographic top-down, so re-running the same detection
/// over the stored image yields the object's plan-view extent in cells.
/// </para>
/// </summary>
internal static class FootprintBuildCommand
{
    public static async Task<int> RunAsync(AssetAlbum album, string outputPath, CancellationToken token,
        int threshold = 12)
    {
        if (!Directory.Exists(album.RootDirectory))
            throw new AutomationException("CATALOG_NOT_FOUND", "图册目录不存在: " + album.RootDirectory);

        var catalog = new FootprintCatalog
        {
            BuiltAtUtc = DateTimeOffset.UtcNow,
            SourceAlbumHash = album.AlbumHash
        };

        var byBatch = album.Entries.Values.GroupBy(entry => entry.Batch).OrderBy(group => group.Key).ToArray();
        using var reference = album.ReferenceImagePath != null && File.Exists(album.ReferenceImagePath)
            ? await Image.LoadAsync<Rgba32>(album.ReferenceImagePath, token) : null;
        Console.WriteLine($"Footprints: {album.Entries.Count} objects across {byBatch.Length} renders, "
            + $"window {album.WindowCells} cells, tile {album.TileCells} cells, "
            + (reference == null ? "no ground reference (falling back to window colour)" : "diffing against the empty-grid render"));

        foreach (var group in byBatch)
        {
            token.ThrowIfCancellationRequested();
            var batch = album.Batches.FirstOrDefault(b => b.Index == group.Key);
            if (batch == null || !File.Exists(batch.ImagePath))
            {
                foreach (var entry in group)
                    catalog.Failures.Add(new FootprintFailure(entry.TypeName, "IMAGE_MISSING", batch?.ImagePath ?? ""));
                continue;
            }

            using var image = await Image.LoadAsync<Rgba32>(batch.ImagePath, token);
            var matrix = batch.PixelToPlayableGrid;
            var pixelsPerCell = matrix is { Length: 6 }
                ? 1.0 / Math.Sqrt(Math.Abs(matrix[0] * matrix[4] - matrix[1] * matrix[3]))
                : image.Width / (double)album.GridCells;
            var window = (int)Math.Round(album.WindowCells * pixelsPerCell);
            var fallback = (int)Math.Round(album.TileCells * pixelsPerCell);
            var minimum = Math.Max(8, (int)Math.Round(2 * pixelsPerCell));

            foreach (var entry in group)
            {
                var (pixelX, pixelY) = matrix is { Length: 6 }
                    ? GridToPixel(matrix, entry.GridX, entry.GridY)
                    : (entry.GridX * pixelsPerCell, (album.GridCells - entry.GridY) * pixelsPerCell);
                var (_, found, coverage) = ObjectBlob.Locate(image, pixelX, pixelY, window, fallback, minimum,
                    reference, threshold);
                var blob = found;
                if (blob == null)
                {
                    // Two honest reasons to have no measurement, told apart so a consumer can act:
                    // a flat decal or a pure light leaves the ground pixel-identical, while a cliff
                    // deforms the terrain so the whole window differs from the flat reference.
                    var (code, reason) = coverage <= 0.001
                        ? ("NO_VISUAL_DIFFERENCE", "俯视与地面无视觉差异（贴地贴花或纯灯光）")
                        : coverage > 0.9
                            ? ("NO_FLAT_REFERENCE", "整窗与平地参考不同（改地形的物体，如悬崖）")
                            : ("ONLY_DEBRIS", "只检出碎屑，无成形连通块");
                    catalog.Failures.Add(new FootprintFailure(entry.TypeName, code,
                        $"{reason}，在 ({entry.GridX},{entry.GridY})，覆盖率 {coverage:0.###}"));
                    continue;
                }
                catalog.Entries[entry.TypeName] = new FootprintEntry(entry.TypeName,
                    Math.Round(blob.Value.Width / pixelsPerCell, 2),
                    Math.Round(blob.Value.Height / pixelsPerCell, 2),
                    blob.Value.Width, blob.Value.Height, Math.Round(pixelsPerCell, 4),
                    FootprintCatalog.SourceRendered, batch.MapContentHash, batch.ImageHash);
            }
        }

        catalog.Notes.Add("占地由图册渲染俯视图量得：正交俯视下像素范围即平面尺寸，但包含渲染器绘制的阴影，故为偏保守的上界。");
        catalog.Notes.Add("它是「一个物体占多大地方」的度量，不是引擎的碰撞体；对不阻挡通行的装饰物会偏大。");
        catalog.Notes.Add("编辑器数据中没有任何声明尺寸，所以这是唯一可得的一手来源。");
        catalog.CatalogHash = catalog.ComputeHash();
        catalog.Save(outputPath);

        Console.WriteLine($"Footprints written to {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"  catalogHash {catalog.CatalogHash}");
        Console.WriteLine($"  measured    {catalog.Entries.Count}");
        Console.WriteLine($"  failures    {catalog.Failures.Count}");
        if (catalog.Failures.Count > 0)
        {
            foreach (var failure in catalog.Failures.GroupBy(f => f.Code).OrderByDescending(g => g.Count()))
                Console.WriteLine($"    {failure.Key}: {failure.Count()}  e.g. {failure.First().TypeName}");
        }
        var widths = catalog.Entries.Values.Select(e => e.WidthCells).OrderBy(w => w).ToArray();
        if (widths.Length > 0)
            Console.WriteLine($"  width cells min {widths[0]:0.##}, median {widths[widths.Length / 2]:0.##}, max {widths[^1]:0.##}");
        return 0;
    }

    private static (double X, double Y) GridToPixel(double[] matrix, double gridX, double gridY)
    {
        var determinant = matrix[0] * matrix[4] - matrix[1] * matrix[3];
        var dx = gridX - matrix[2];
        var dy = gridY - matrix[5];
        return ((dx * matrix[4] - matrix[1] * dy) / determinant,
                (matrix[0] * dy - dx * matrix[3]) / determinant);
    }
}
