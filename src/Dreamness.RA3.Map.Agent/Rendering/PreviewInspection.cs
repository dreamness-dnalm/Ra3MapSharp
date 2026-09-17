using System.Security.Cryptography;
using Dreamness.RA3.Map.Automation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Dreamness.RA3.Map.Agent.Rendering;

public interface IAgentImage
{
    string ImagePath { get; }
    string ImageHash { get; }
}

public sealed record InspectedPreview(string ImagePath, int Width, int Height, int Revision, string MapContentHash,
    string ImageHash, string SourceImageHash, string RendererConfigHash, double[] PixelToPlayableGrid,
    string? PreparedPlanId = null, string? PlanHash = null, string DesignHash = "") : IAgentImage;

public sealed class PixelCrop
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public static class PreviewInspection
{
    public static async Task<InspectedPreview> CreateAsync(OverviewResult source, PixelCrop? crop, int maxEdge = 1024, CancellationToken token = default)
    {
        if (maxEdge < 1 || maxEdge > 2048) throw new AutomationException("INVALID_ARGUMENT", "maxEdge 必须在1–2048之间。");
        var bytes = await ReadVerifiedAsync(source.ImagePath, source.ImageHash, token);
        using var image = Image.Load(bytes);
        crop ??= new PixelCrop { Width = image.Width, Height = image.Height };
        if (crop.X < 0 || crop.Y < 0 || crop.Width <= 0 || crop.Height <= 0
            || (long)crop.X + crop.Width > image.Width || (long)crop.Y + crop.Height > image.Height)
            throw new AutomationException("INVALID_ARGUMENT", "裁剪区域必须完整位于原始预览图内。");
        var scale = Math.Min(1d, (double)maxEdge / Math.Max(crop.Width, crop.Height));
        var width = Math.Max(1, (int)Math.Round(crop.Width * scale));
        var height = Math.Max(1, (int)Math.Round(crop.Height * scale));
        image.Mutate(ctx => ctx.Crop(new Rectangle(crop.X, crop.Y, crop.Width, crop.Height)).Resize(width, height));
        var path = Path.Combine(Path.GetDirectoryName(source.ImagePath)!, "inspect-" + Guid.NewGuid().ToString("N") + ".png");
        await image.SaveAsPngAsync(path, token);
        var matrix = source.PixelToPlayableGrid;
        var sx = (double)crop.Width / width; var sy = (double)crop.Height / height;
        var transformed = new[] { matrix[0] * sx, matrix[1] * sy, matrix[0] * crop.X + matrix[1] * crop.Y + matrix[2],
            matrix[3] * sx, matrix[4] * sy, matrix[3] * crop.X + matrix[4] * crop.Y + matrix[5] };
        return new InspectedPreview(path, width, height, source.Revision, source.MapContentHash,
            Hash(await File.ReadAllBytesAsync(path, token)), source.ImageHash, source.RendererConfigHash, transformed,
            source.PreparedPlanId, source.PlanHash, source.DesignHash);
    }

    public static async Task<byte[]> ReadVerifiedAsync(string path, string hash, CancellationToken token = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, token);
        if (Hash(bytes) != hash) throw new AutomationException("PREVIEW_CHANGED", "预览文件已变化，不能作为该地图修订的可信图像。");
        return bytes;
    }

    private static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
