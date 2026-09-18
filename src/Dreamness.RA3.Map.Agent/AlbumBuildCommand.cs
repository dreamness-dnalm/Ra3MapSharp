using System.Security.Cryptography;
using System.Text.Json;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Agent.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Dreamness.RA3.Map.Agent;

/// <summary>
/// Renders an appearance album for objects the editor ships no screenshot for (--build-album).
/// <para>
/// Objects are laid out on a grid of test maps, the whole grid is raised to land, and one real
/// overview render per batch is sliced into per-object tiles using the renderer's own
/// pixel-to-grid matrix. This gives every object a picture of how it actually looks in game,
/// which no amount of indexing the editor's icon set can provide for the 1206 objects that
/// have no icon.
/// </para>
/// </summary>
internal static class AlbumBuildCommand
{
    internal sealed class Options
    {
        /// <summary>
        /// Playable size of each grid map. 64 cells over the renderer's 2048 px gives 32 px per
        /// cell, which is what separates a legible bench from a 16 px smudge; 128 cells would
        /// quadruple the objects per render but halve the detail.
        /// </summary>
        public int Grid { get; set; } = 64;

        /// <summary>
        /// Kept comfortably wider than the window so a neighbour never enters it; otherwise the
        /// neighbour's shadow merges into the crop and the object looks tiny.
        /// </summary>
        public int Spacing { get; set; } = 14;

        /// <summary>Crop used when the object cannot be told apart from the ground.</summary>
        public int TileCells { get; set; } = 6;

        /// <summary>
        /// Search window, in cells, used to find the object before cropping. Must exceed the
        /// largest prop (roughly six cells) or the object gets clipped.
        /// </summary>
        public int WindowCells { get; set; } = 8;
        public int TileEdge { get; set; } = 128;
        public int? Limit { get; set; }
        public bool All { get; set; }
        public double LandHeight { get; set; } = 300;
    }

    public static async Task<int> RunAsync(string launcher, string artifacts, ObjectCatalog? catalog,
        AssetCatalog assets, string outputDirectory, Options options, CancellationToken token)
    {
        var root = Path.GetFullPath(outputDirectory);
        var mapRoot = Path.Combine(root, "maps");
        var tileRoot = Path.Combine(root, "tiles");
        Directory.CreateDirectory(mapRoot);
        Directory.CreateDirectory(tileRoot);

        var targets = assets.Objects
            .Where(o => options.All || !o.HasEditorScreenshot)
            .Select(o => o.TypeName)
            .ToList();
        if (options.Limit.HasValue) targets = targets.Take(options.Limit.Value).ToList();

        var positions = Positions(options);
        if (positions.Count == 0) throw new AutomationException("INVALID_ARGUMENT", "网格与间距算不出任何摆放位置。");

        Console.WriteLine($"Album: {targets.Count} objects, {positions.Count} per render, "
            + $"{options.Grid} cells at {2048.0 / options.Grid:0.#} px/cell into {root}");
        if (targets.Count == 0) { Console.WriteLine("Nothing to render."); return 0; }

        var album = new AssetAlbum
        {
            BuiltAtUtc = DateTimeOffset.UtcNow,
            GridCells = options.Grid,
            SpacingCells = options.Spacing,
            TileCells = options.TileCells,
            SourceImageEdge = 2048,
            TileEdge = options.TileEdge
        };

        await using var runtime = new AgentRuntime(
            new WorldBuilderRenderer(launcher, Path.Combine(artifacts, "album-render")),
            catalog, Path.Combine(artifacts, "album-diagnostics"), assets);

        var batches = targets.Chunk(positions.Count).ToArray();
        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            token.ThrowIfCancellationRequested();
            var batch = batches[batchIndex];
            var mapName = "Album" + (batchIndex + 1).ToString("D3");
            var parent = Path.Combine(mapRoot, mapName);
            Directory.CreateDirectory(parent);

            var created = await Required(runtime, "map.create", new
            {
                parentPath = parent, mapName,
                playableWidth = options.Grid, playableHeight = options.Grid, border = 8
            }, null, null, token);
            var session = created.SessionId!;
            var revision = created.RevisionAfter;

            var raised = await Required(runtime, "terrain.set_height", new
            {
                region = new { x = 0, y = 0, width = options.Grid, height = options.Grid },
                height = options.LandHeight
            }, session, revision, token);
            revision = raised.RevisionAfter;

            var placed = new List<(string TypeName, int X, int Y)>();
            for (var i = 0; i < batch.Length; i++)
            {
                var (x, y) = positions[i];
                var result = await Call(runtime, "objects.place", new
                {
                    typeName = batch[i], x, y, anchor = "gridPoint",
                    ownerTeam = "PlyrNeutral/teamPlyrNeutral"
                }, session, revision, token);
                if (result.Status == "succeeded")
                {
                    revision = result.RevisionAfter;
                    placed.Add((batch[i], x, y));
                }
                else
                {
                    // Some objects legitimately refuse this terrain; record why instead of
                    // dropping them silently, because the reason is the useful part.
                    album.Failures.Add(new AssetAlbumFailure(batch[i], result.Error?.Code ?? "UNKNOWN",
                        result.Error?.Message ?? ""));
                }
            }

            var saved = await Required(runtime, "map.save", new { compress = true }, session, revision, token);
            revision = saved.RevisionAfter;

            var image = await RenderAsync(runtime, session, revision, token);
            var tiles = await SliceAsync(image, placed, options, tileRoot, batchIndex, token);
            album.Batches.Add(new AssetAlbumBatch(batchIndex, placed.Count, image.MapContentHash,
                image.ImageHash, image.RendererConfigHash, image.ImagePath));
            foreach (var tile in tiles) album.Entries[tile.TypeName] = tile;

            Console.WriteLine($"  batch {batchIndex + 1}/{batches.Length} [{mapName}] "
                + $"placed {placed.Count}/{batch.Length}, failed {batch.Length - placed.Count}, "
                + $"tiles {tiles.Count}, image {image.ImagePath}");
        }

        album.AlbumHash = album.ComputeHash();
        // Pass --build-album <artifacts>/album so the host finds it at its default location.
        var albumPath = Path.Combine(root, "album.json");
        album.Save(albumPath);
        Console.WriteLine($"Album written to {albumPath}");
        Console.WriteLine($"  albumHash {album.AlbumHash}");
        Console.WriteLine($"  images    {album.Entries.Count}");
        Console.WriteLine($"  batches   {album.Batches.Count}");
        Console.WriteLine($"  failures  {album.Failures.Count}");
        if (album.Failures.Count > 0)
        {
            foreach (var group in album.Failures.GroupBy(f => f.Code).OrderByDescending(g => g.Count()))
                Console.WriteLine($"    {group.Key}: {group.Count()}  e.g. {group.First().TypeName} - {group.First().Message}");
        }
        return 0;
    }

    private static List<(int X, int Y)> Positions(Options options)
    {
        var positions = new List<(int X, int Y)>();
        var offset = Math.Max(1, options.Spacing / 2);
        for (var y = offset; y < options.Grid; y += options.Spacing)
            for (var x = offset; x < options.Grid; x += options.Spacing)
                positions.Add((x, y));
        return positions;
    }

    private sealed record RenderedImage(string ImagePath, int Width, int Height, string ImageHash,
        string MapContentHash, string RendererConfigHash, double[] PixelToPlayableGrid);

    private static async Task<RenderedImage> RenderAsync(AgentRuntime runtime, string session, int? revision,
        CancellationToken token)
    {
        var started = await Required(runtime, "preview.start", new { requiredRevision = revision }, session, null, token);
        var jobId = Data(started).GetProperty("jobId").GetString()!;
        while (true)
        {
            await Task.Delay(1000, token);
            var status = await Required(runtime, "jobs.status", new { jobId }, session, null, token);
            var state = Data(status).GetProperty("state").GetString();
            if (state == "running") continue;
            if (state != "succeeded")
            {
                var error = Data(status).TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object
                    ? e.GetProperty("message").GetString() : state;
                throw new AutomationException("RENDER_ERROR", "批次渲染未成功: " + error);
            }
            var result = Data(status).GetProperty("result");
            return new RenderedImage(
                result.GetProperty("imagePath").GetString()!,
                result.GetProperty("width").GetInt32(),
                result.GetProperty("height").GetInt32(),
                result.GetProperty("imageHash").GetString()!,
                result.GetProperty("mapContentHash").GetString()!,
                result.GetProperty("rendererConfigHash").GetString()!,
                result.GetProperty("pixelToPlayableGrid").EnumerateArray().Select(v => v.GetDouble()).ToArray());
        }
    }

    private static async Task<List<AssetAlbumEntry>> SliceAsync(RenderedImage render,
        List<(string TypeName, int X, int Y)> placed, Options options, string tileRoot, int batchIndex,
        CancellationToken token)
    {
        var entries = new List<AssetAlbumEntry>();
        if (placed.Count == 0) return entries;
        var matrix = render.PixelToPlayableGrid;
        var determinant = matrix[0] * matrix[4] - matrix[1] * matrix[3];
        if (Math.Abs(determinant) < 1e-12)
            throw new AutomationException("RENDER_ERROR", "渲染未返回可用的像素到网格矩阵。");
        var pixelsPerCell = 1.0 / Math.Sqrt(Math.Abs(determinant));
        var window = (int)Math.Round(options.WindowCells * pixelsPerCell);
        var fallback = (int)Math.Round(options.TileCells * pixelsPerCell);
        // Let a detected object shrink well below the fallback: framing matters more than
        // sharpness for recognition, and a one-cell prop is only 32 px in the source image.
        var minimum = Math.Max(8, (int)Math.Round(2 * pixelsPerCell));

        using var image = await Image.LoadAsync<Rgba32>(render.ImagePath, token);
        foreach (var (typeName, gridX, gridY) in placed)
        {
            var (pixelX, pixelY) = GridToPixel(matrix, determinant, gridX, gridY);
            var crop = FitToObject(image, pixelX, pixelY, window, fallback, minimum);
            using var tile = image.Clone(context => context
                .Crop(crop)
                .Resize(options.TileEdge, options.TileEdge));
            var file = SafeFileName(typeName) + ".png";
            var path = Path.Combine(tileRoot, file);
            await tile.SaveAsPngAsync(path, token);
            var bytes = await File.ReadAllBytesAsync(path, token);
            entries.Add(new AssetAlbumEntry(typeName, file, bytes.Length,
                "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                gridX, gridY, batchIndex));
        }
        return entries;
    }

    /// <summary>
    /// Finds the object's bounding box inside a window centred on its grid position.
    /// <para>
    /// A fixed crop cannot suit both a bench and a salvage ship, and there is no footprint data
    /// to pick a size from. The test map is deliberately uniform, so the object is simply
    /// whatever differs from the window's dominant colour, which frames small and large objects
    /// alike. Flat decals that never stand out fall back to the centred tile.
    /// </para>
    /// </summary>
    private static Rectangle FitToObject(Image<Rgba32> image, double centreX, double centreY,
        int window, int fallback, int minimum)
    {
        var side = Math.Clamp(window, 1, Math.Min(image.Width, image.Height));
        var left = Math.Clamp((int)Math.Round(centreX - side / 2.0), 0, image.Width - side);
        var top = Math.Clamp((int)Math.Round(centreY - side / 2.0), 0, image.Height - side);

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
        if (histogram.Count == 0) return Centred(left, top, side, fallback);
        var background = histogram.OrderByDescending(pair => pair.Value).First().Key;
        var backR = ((background >> 10) & 31) << 3;
        var backG = ((background >> 5) & 31) << 3;
        var backB = (background & 31) << 3;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, marked = 0;
        for (var y = top; y < top + side; y++)
        {
            for (var x = left; x < left + side; x++)
            {
                var pixel = image[x, y];
                var difference = Math.Max(Math.Abs(pixel.R - backR),
                    Math.Max(Math.Abs(pixel.G - backG), Math.Abs(pixel.B - backB)));
                if (difference <= 24) continue;
                marked++;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        var coverage = (double)marked / (side * (double)side);
        // Nothing stands out (a ground decal) or almost everything does (bad detection).
        if (maxX < 0 || coverage < 0.002 || coverage > 0.9) return Centred(left, top, side, fallback);

        // A bounding box over every marked pixel would swallow whatever neighbour or shadow
        // reaches into the window, which is what made small props look tiny. Take the single
        // blob nearest the centre instead: neighbours form their own components.
        var mask = new bool[side * side];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var pixel = image[left + x, top + y];
                mask[y * side + x] = Math.Max(Math.Abs(pixel.R - backR),
                    Math.Max(Math.Abs(pixel.G - backG), Math.Abs(pixel.B - backB))) > 24;
            }
        }
        var best = NearestComponent(mask, side, side / 2.0, side / 2.0);
        if (best == null) return Centred(left, top, side, fallback);

        var blob = best.Value;
        var footprint = Math.Max(blob.Width, blob.Height);
        var size = Math.Clamp((int)Math.Round(footprint * 1.35), Math.Min(minimum, side), side);
        var objectCentreX = blob.X + blob.Width / 2.0;
        var objectCentreY = blob.Y + blob.Height / 2.0;
        return new Rectangle(
            Math.Clamp(left + (int)Math.Round(objectCentreX - size / 2.0), 0, image.Width - size),
            Math.Clamp(top + (int)Math.Round(objectCentreY - size / 2.0), 0, image.Height - size),
            size, size);
    }

    /// <summary>
    /// Bounding box of the marked component whose centre is closest to (<paramref name="centreX"/>,
    /// <paramref name="centreY"/>), ignoring specks. Returns null when nothing qualifies.
    /// </summary>
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

    private static Rectangle Centred(int left, int top, int side, int fallback)
    {
        var size = Math.Min(fallback, side);
        return new Rectangle(left + (side - size) / 2, top + (side - size) / 2, size, size);
    }

    /// <summary>Inverts the renderer's affine pixel-to-grid matrix.</summary>
    private static (double X, double Y) GridToPixel(double[] matrix, double determinant, double gridX, double gridY)
    {
        var dx = gridX - matrix[2];
        var dy = gridY - matrix[5];
        return ((dx * matrix[4] - matrix[1] * dy) / determinant,
                (matrix[0] * dy - dx * matrix[3]) / determinant);
    }

    private static string SafeFileName(string typeName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var characters = typeName.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(characters);
    }

    private static async Task<CommandResult> Call(AgentRuntime runtime, string command, object arguments,
        string? session, int? revision, CancellationToken token) =>
        await runtime.ExecuteAsync(new CommandRequest
        {
            Command = command,
            SessionId = session,
            ExpectedRevision = revision,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        }, token);

    private static async Task<CommandResult> Required(AgentRuntime runtime, string command, object arguments,
        string? session, int? revision, CancellationToken token)
    {
        var result = await Call(runtime, command, arguments, session, revision, token);
        if (result.Status != "succeeded")
            throw new AutomationException(result.Error?.Code ?? "UNKNOWN",
                $"{command} 失败: {result.Error?.Message}");
        return result;
    }

    private static JsonElement Data(CommandResult result) =>
        JsonSerializer.SerializeToElement(result.Data, AgentJson.Options);
}
