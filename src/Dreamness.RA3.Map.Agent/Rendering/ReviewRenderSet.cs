using System.Security.Cryptography;
using Dreamness.RA3.Map.Automation.Catalog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Dreamness.RA3.Map.Agent.Rendering;

/// <summary>The review set: one contact sheet plus the rubric that judges the same revision.</summary>
public sealed record ReviewResult(string ImagePath, string ImageHash, string MapContentHash,
    string RendererConfigHash, string RulesHash, MapArtProfile Profile, ArtRubricResult Rubric,
    List<ReviewShot> Shots, int Width, int Height) : IAgentImage;

/// <summary>One image in a review set, with the pixel rectangle it was cut from.</summary>
public sealed record ReviewShot(string Kind, string Label, string Path, string Hash,
    int X, int Y, int Width, int Height);

/// <summary>
/// Builds the review image set: one overview plus a local shot per player start, composed into a
/// single contact sheet so a reviewer (or an agent) sees the whole judgement in one image.
/// <para>
/// The camera is not controllable — the launcher only exports a north-up overview — so the local
/// shots are crops of that same render. That is a feature for comparison: the corpus maps can be
/// cropped at the same grid offsets and compared like for like.
/// </para>
/// </summary>
internal static class ReviewRenderSet
{
    private const int SheetWidth = 1024;
    private const int LocalEdge = 256;
    private const int MaxLocals = 4;

    public static async Task<(string Path, string Hash, int Width, int Height, List<ReviewShot> Shots)>
        ComposeAsync(OverviewResult overview, IReadOnlyList<(string Label, double GridX, double GridY)> locals,
            int localCells, string outputDirectory, CancellationToken token)
    {
        Directory.CreateDirectory(outputDirectory);
        using var source = await Image.LoadAsync<Rgba32>(overview.ImagePath, token);
        var pixelsPerCell = overview.Width / (double)Math.Max(1, CellsFromMatrix(overview.PixelToPlayableGrid, overview.Width));
        var localPixels = Math.Clamp((int)Math.Round(localCells * pixelsPerCell), 16, Math.Min(source.Width, source.Height));

        var shots = new List<ReviewShot>();
        var overviewEdge = Math.Min(SheetWidth, source.Width);
        var overviewHeight = Math.Max(1, (int)Math.Round(source.Height * (overviewEdge / (double)source.Width)));
        var chosen = locals.Take(MaxLocals).ToArray();
        var sheetHeight = overviewHeight + (chosen.Length > 0 ? LocalEdge : 0);

        using var sheet = new Image<Rgba32>(SheetWidth, sheetHeight, new Rgba32(24, 24, 24));
        using (var scaled = source.Clone(context => context.Resize(overviewEdge, overviewHeight)))
            sheet.Mutate(context => context.DrawImage(scaled, new Point(0, 0), 1f));
        var overviewPath = Path.Combine(outputDirectory, "overview.png");
        await scaledSaveAsync(source, overviewEdge, overviewHeight, overviewPath, token);
        shots.Add(new ReviewShot("overview", "north-up overview", overviewPath, await HashAsync(overviewPath, token),
            0, 0, overviewEdge, overviewHeight));

        for (var index = 0; index < chosen.Length; index++)
        {
            var (label, gridX, gridY) = chosen[index];
            // The renderer maps grid to pixels through its own matrix; reproduce that mapping.
            var pixelX = gridX / CellsFromMatrix(overview.PixelToPlayableGrid, overview.Width) * overview.Width;
            var pixelY = (1 - gridY / CellsFromMatrix(overview.PixelToPlayableGrid, overview.Width)) * overview.Height;
            var x = Math.Clamp((int)Math.Round(pixelX - localPixels / 2.0), 0, Math.Max(0, source.Width - localPixels));
            var y = Math.Clamp((int)Math.Round(pixelY - localPixels / 2.0), 0, Math.Max(0, source.Height - localPixels));
            var path = Path.Combine(outputDirectory, $"local-{index + 1}.png");
            using (var crop = source.Clone(context => context
                .Crop(new Rectangle(x, y, localPixels, localPixels))
                .Resize(LocalEdge, LocalEdge)))
            {
                await crop.SaveAsPngAsync(path, token);
                var destination = new Point(index * LocalEdge, overviewHeight);
                sheet.Mutate(context => context.DrawImage(crop, destination, 1f));
            }
            shots.Add(new ReviewShot("local", label, path, await HashAsync(path, token),
                x, y, LocalEdge, LocalEdge));
        }

        var sheetPath = Path.Combine(outputDirectory, "review-sheet.png");
        await sheet.SaveAsPngAsync(sheetPath, token);
        return (sheetPath, await HashAsync(sheetPath, token), SheetWidth, sheetHeight, shots);
    }

    private static async Task scaledSaveAsync(Image<Rgba32> source, int width, int height, string path,
        CancellationToken token)
    {
        using var scaled = source.Clone(context => context.Resize(width, height));
        await scaled.SaveAsPngAsync(path, token);
    }

    /// <summary>Playable cells covered by the render, read back from the renderer's own matrix.</summary>
    private static double CellsFromMatrix(double[] matrix, int fallbackWidth) =>
        matrix is { Length: 6 } && matrix[0] != 0 ? 1.0 / Math.Abs(matrix[0]) : fallbackWidth;

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        var bytes = await File.ReadAllBytesAsync(path, token);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
