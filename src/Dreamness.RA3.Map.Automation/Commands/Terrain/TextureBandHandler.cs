using System.Text.Json;
using Dreamness.Ra3.Map.Facade.enums;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

/// <summary>
/// Paints each cell by the elevation band it falls in.
/// <para>
/// Placing materials as shapes draws the shape: the boundary is whatever the brush was, which is
/// why a map built from circular stamps reads as stamps. Deriving the material from the height
/// field instead makes the boundaries where the terrain actually changes, so they come out
/// ragged and legible, and the material follows the land rather than covering it.
/// </para>
/// </summary>
internal sealed class PaintTextureByHeightHandler : ICommandHandler
{
    public string Name => "texture.paint_by_height";

    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<BandPaintArgs>(arguments);
        if (args.Region == null || args.Bands == null || args.Bands.Count is < 1 or > 16)
            throw new AutomationException("INVALID_ARGUMENT", "region 必填；bands 需要 1–16 条。");
        var known = Enum.GetNames<TextureEnum>();
        double previous = double.NegativeInfinity;
        foreach (var band in args.Bands)
        {
            var names = band?.Names();
            if (band == null || names == null || names.Count == 0
                || names.Any(name => !known.Contains(name, StringComparer.Ordinal)))
                throw new AutomationException("INVALID_ARGUMENT",
                    "每条 band 需要 texture，或 1–8 个 textureNames，且必须是 textures.list 的准确名称。");
            Coordinates.EnsureFinite("maxHeight", (float)band.MaxHeight);
            if (band.VariantBlockCells is < 4 or > 256)
                throw new AutomationException("INVALID_ARGUMENT", "variantBlockCells 需在 4–256 之间。");
            // Ascending bands are what make "first band that contains this height" well defined.
            if (band.MaxHeight <= previous)
                throw new AutomationException("INVALID_ARGUMENT", "bands 的 maxHeight 必须严格递增。");
            previous = band.MaxHeight;
        }

        var byTexture = new Dictionary<string, List<(int X, int Y)>>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var heights = new List<float>();
        foreach (var (x, y) in args.Region.Cells(map))
        {
            token.ThrowIfCancellationRequested();
            var height = map.GetTerrainHeight(x, y);
            var band = args.Bands.FirstOrDefault(candidate => height <= candidate.MaxHeight) ?? args.Bands[^1];
            // One species per band would read as a contour map; shipped maps vary the material
            // across a band as well, so a band may carry several and pick per coarse block.
            var texture = band.Pick(x, y);
            if (!byTexture.TryGetValue(texture, out var cells)) byTexture[texture] = cells = new();
            cells.Add((x, y));
            counts[texture] = counts.TryGetValue(texture, out var seen) ? seen + 1 : 1;
            heights.Add(height);
        }

        foreach (var pair in byTexture)
            PaintTextureHandler.PaintCells(map, pair.Value, pair.Key, args.AutoBlend, token);

        return Task.FromResult<object?>(new
        {
            painted = counts.Values.Sum(),
            byTexture = counts.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value),
            minHeight = heights.Count == 0 ? 0 : heights.Min(),
            maxHeight = heights.Count == 0 ? 0 : heights.Max(),
            model = "elevation-bands-v1",
            note = "材质由高度带宽决定，因此边界落在真正的地形变化处，而不是画笔形状处。"
        });
    }

    private sealed class BandPaintArgs
    {
        public GridRegion? Region { get; set; }
        public List<HeightBand>? Bands { get; set; }
        public bool AutoBlend { get; set; } = true;
    }

    private sealed class HeightBand
    {
        public double MaxHeight { get; set; }

        public string? Texture { get; set; }

        /// <summary>Several materials for this band, chosen per coarse block.</summary>
        public List<string>? TextureNames { get; set; }

        public int VariantBlockCells { get; set; } = 24;

        public List<string>? Names()
        {
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(Texture)) names.Add(Texture!);
            if (TextureNames != null) names.AddRange(TextureNames.Where(name => !string.IsNullOrWhiteSpace(name)));
            return names.Count == 0 ? null : names.Distinct(StringComparer.Ordinal).Take(8).ToList();
        }

        /// <summary>Deterministic per-block choice, so the same inputs always paint the same map.</summary>
        public string Pick(int x, int y)
        {
            var names = Names()!;
            if (names.Count == 1) return names[0];
            var blockX = (int)Math.Floor(x / (double)VariantBlockCells);
            var blockY = (int)Math.Floor(y / (double)VariantBlockCells);
            // Multiplicative xor alone relates to the block parity and paints a checkerboard
            // when the variant count is even; the final mix is what breaks that correlation.
            var hash = unchecked((uint)blockX * 0x9E3779B1u) ^ unchecked((uint)blockY * 0x85EBCA77u);
            hash ^= hash >> 15;
            hash = unchecked(hash * 0x2545F491u);
            hash ^= hash >> 13;
            return names[(int)(hash % (uint)names.Count)];
        }
    }
}
