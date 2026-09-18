using Dreamness.RA3.Map.Automation.Storage;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>
/// How the primary materials are laid out, as opposed to how much of each there is.
/// <para>
/// Aggregate counts cannot tell a map of coherent regions from one covered in stamps:
/// both can hit the same material counts and shares. These measure the shape of the regions.
/// </para>
/// </summary>
public sealed record MapPatchStats(int Patches, double MedianPatchCells, double LargestPatchShare,
    double PatchCountPer1000Cells, double MeanPatchCells, double MeanCompactness);

/// <summary>
/// The art-direction measurements for one map.
/// <para>
/// The same code measures a shipped corpus map and a work in progress on purpose: a review is
/// only meaningful if the number being judged was produced the same way as the number being
/// compared against.
/// </para>
/// </summary>
public sealed record MapArtProfile(int Width, int Height, long Cells, int SampledCells,
    int DistinctTextures, double TopTextureShare, double TopThreeShare, double TransitionShare,
    double BlendedShare, int Objects, double ObjectsPer1000Cells, double ClumpingIndex,
    SortedDictionary<string, int> Categories, List<ArtRulePair> Pairs, MapPatchStats Patches)
{
    private const int QuadratCells = 8;

    /// <summary>Samples the map on a stride so a 1000x1000 map costs about as much as a 64x64 one.</summary>
    public static MapArtProfile Measure(Ra3MapFacade map, IReadOnlyDictionary<string, string>? categoryLookup,
        int sampleTarget, CancellationToken token)
    {
        var width = map.MapPlayableWidth;
        var height = map.MapPlayableHeight;
        var cells = (long)width * height;
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(cells / (double)Math.Max(1, sampleTarget))));

        var textureCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var pairCounts = new Dictionary<(string Primary, string Secondary), int>();
        int sampled = 0, transitions = 0, blended = 0;
        for (var y = 0; y < height; y += stride)
        {
            for (var x = 0; x < width; x += stride)
            {
                token.ThrowIfCancellationRequested();
                var (primary, secondary) = map.GetTexturesInvolved(x, y);
                if (string.IsNullOrEmpty(primary)) continue;
                sampled++;
                textureCounts[primary] = textureCounts.TryGetValue(primary, out var seen) ? seen + 1 : 1;
                // Transition materials show up as the blended-in secondary, not as the primary.
                if (TextureSemantics.IsTransition(primary)
                    || (secondary != null && TextureSemantics.IsTransition(secondary))) transitions++;
                if (secondary == null) continue;
                blended++;
                var pair = (primary, secondary);
                pairCounts[pair] = pairCounts.TryGetValue(pair, out var pairs) ? pairs + 1 : 1;
            }
        }

        var ranked = textureCounts.OrderByDescending(pair => pair.Value).ToArray();
        var total = Math.Max(1, sampled);
        var objects = map.GetUnitObjects();
        var categories = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var positions = new List<(double X, double Y)>(objects.Count);
        foreach (var placed in objects)
        {
            var label = categoryLookup != null && categoryLookup.TryGetValue(placed.TypeName, out var known)
                ? known : "unclassified";
            categories[label] = categories.TryGetValue(label, out var count) ? count + 1 : 1;
            positions.Add((placed.Position.X / 10d, placed.Position.Y / 10d));
        }

        return new MapArtProfile(width, height, cells, sampled,
            textureCounts.Count,
            ranked.Length > 0 ? ranked[0].Value / (double)total : 0,
            ranked.Take(3).Sum(pair => pair.Value) / (double)total,
            transitions / (double)total,
            blended / (double)total,
            objects.Count,
            objects.Count * 1000.0 / Math.Max(1, cells),
            MeasureClumping(positions, width, height),
            categories,
            pairCounts.OrderByDescending(pair => pair.Value).Take(40)
                .Select(pair => new ArtRulePair(pair.Key.Primary, pair.Key.Secondary, 1, pair.Value)).ToList(),
            MeasurePatches(map, token));
    }

    /// <summary>
    /// Variance-to-mean ratio of object counts in 8-cell quadrats. About 1 means independent
    /// scatter, well above 1 means groves or bases, well below 1 means an even grid.
    /// </summary>
    public static double MeasureClumping(IReadOnlyList<(double X, double Y)> points, int width, int height)
    {
        var columns = width / QuadratCells;
        var rows = height / QuadratCells;
        if (columns < 2 || rows < 2 || points.Count < 50) return 0;
        var counts = new int[columns * rows];
        foreach (var (x, y) in points)
        {
            var column = (int)(x / QuadratCells);
            var row = (int)(y / QuadratCells);
            if (column < 0 || row < 0 || column >= columns || row >= rows) continue;
            counts[row * columns + column]++;
        }
        var mean = counts.Average();
        if (mean <= 0) return 0;
        var variance = counts.Sum(count => (count - mean) * (count - mean)) / counts.Length;
        return variance / mean;
    }

    /// <summary>
    /// Labels the connected regions of equal primary texture and summarises their shape.
    /// <para>
    /// Full resolution on purpose: a stride destroys adjacency, and adjacency is the whole point.
    /// Compactness is perimeter squared over area, which is about 4*pi for a disc and rises as a
    /// region gets ragged — a map stamped with discs is conspicuous because its regions are
    /// maximally compact.
    /// </para>
    /// </summary>
    public static MapPatchStats MeasurePatches(Ra3MapFacade map, CancellationToken token)
    {
        var width = map.MapPlayableWidth;
        var height = map.MapPlayableHeight;
        if (width <= 0 || height <= 0) return new MapPatchStats(0, 0, 0, 0, 0, 0);

        var ids = new int[width * height];
        var labels = new int[width * height];
        var textureIds = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                token.ThrowIfCancellationRequested();
                var name = map.GetTileTexture(x, y);
                if (!textureIds.TryGetValue(name, out var id)) textureIds[name] = id = textureIds.Count + 1;
                ids[y * width + x] = id;
                labels[y * width + x] = -1;
            }
        }

        var areas = new List<int>();
        var perimeters = new List<int>();
        var componentTexture = new List<int>();
        var queue = new Queue<int>();
        for (var start = 0; start < ids.Length; start++)
        {
            if (labels[start] >= 0) continue;
            var id = ids[start];
            labels[start] = 1;
            queue.Enqueue(start);
            int area = 0, perimeter = 0;
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                area++;
                var x = index % width;
                var y = index / width;
                if (x == 0 || ids[index - 1] != id) perimeter++; else Visit(index - 1, id, labels, ids, queue);
                if (x == width - 1 || ids[index + 1] != id) perimeter++; else Visit(index + 1, id, labels, ids, queue);
                if (y == 0 || ids[index - width] != id) perimeter++; else Visit(index - width, id, labels, ids, queue);
                if (y == height - 1 || ids[index + width] != id) perimeter++; else Visit(index + width, id, labels, ids, queue);
            }
            areas.Add(area);
            perimeters.Add(perimeter);
            componentTexture.Add(id);
        }

        var cells = (double)Math.Max(1, width * height);
        var byTexture = new Dictionary<int, (int Total, int Largest)>();
        for (var index = 0; index < areas.Count; index++)
        {
            var id = componentTexture[index];
            var running = byTexture.TryGetValue(id, out var seen) ? seen : (Total: 0, Largest: 0);
            byTexture[id] = (running.Total + areas[index], Math.Max(running.Largest, areas[index]));
        }
        var largestShare = 0d;
        foreach (var entry in byTexture.Values)
            largestShare += entry.Largest / (double)entry.Total * (entry.Total / cells);

        var sizes = areas.OrderBy(area => area).ToArray();
        var totalCells = Math.Max(1, areas.Sum());
        var weightedCompactness = 0d;
        for (var index = 0; index < areas.Count; index++)
            weightedCompactness += perimeters[index] * (double)perimeters[index] / areas[index] * areas[index];
        weightedCompactness /= totalCells;

        return new MapPatchStats(areas.Count,
            sizes.Length == 0 ? 0 : sizes[sizes.Length / 2],
            Math.Round(largestShare, 4),
            Math.Round(areas.Count * 1000.0 / cells, 3),
            sizes.Length == 0 ? 0 : Math.Round(sizes.Average(), 2),
            Math.Round(weightedCompactness, 2));
    }

    private static void Visit(int index, int id, int[] labels, int[] ids, Queue<int> queue)
    {
        if (labels[index] >= 0 || ids[index] != id) return;
        labels[index] = 1;
        queue.Enqueue(index);
    }
}

/// <summary>One rubric line: what was measured, against which threshold, and where that came from.</summary>
public sealed record ArtRubricCheck(string Id, string Verdict, double? Measured, string Threshold,
    string Source, string Note);

public sealed record ArtRubricResult(string Verdict, int Passed, int Warned, int Failed,
    int NotEvaluated, string RulesHash, List<ArtRubricCheck> Checks, int Descriptive);

/// <summary>
/// Judges a map against the thresholds measured from shipped maps.
/// <para>
/// Every threshold names where it came from, and a rubric item that cannot be decided from
/// statistics says so instead of being approximated into a pass. In particular combat
/// readability needs eyes on the render, not a number.
/// </para>
/// </summary>
public static class ArtRubric
{
    public const string Pass = "pass";
    public const string Warn = "warn";
    public const string Fail = "fail";
    public const string NotEvaluated = "not-evaluated";

    /// <summary>
    /// Measured and reported, deliberately not judged. Used where the number is real but
    /// whether high or low is better has not been established, so letting it decide the verdict
    /// would push work in a direction the evidence does not support.
    /// </summary>
    public const string Descriptive = "descriptive";

    /// <summary>Categories that read as deliberate landmarks rather than filler.</summary>
    private static readonly string[] LandmarkCategories =
    {
        "雕像和石柱", "阵营特殊建筑", "盟军战役地标建筑物", "浮岛要塞装饰",
        "特殊地形", "桥", "沉船", "冰块"
    };

    public static ArtRubricResult Evaluate(MapArtProfile profile, ArtRules? rules)
    {
        var checks = new List<ArtRubricCheck>();
        double? Threshold(string key, double fraction) => rules != null && rules.Distributions.TryGetValue(key, out var d)
            ? d.Get(fraction) : null;
        string Source(string key) => rules != null && rules.Distributions.ContainsKey(key)
            ? $"语料实测 P25–P75（{rules.Maps.Count} 张 shipped 地图）" : "未加载美术规则，无法比对";

        var varietyFloor = Threshold("distinctTextures", 0.25);
        checks.Add(Judge("materialVariety", profile.DistinctTextures,
            varietyFloor, varietyFloor * 0.4,
            $"至少 {varietyFloor:0} 种（语料 P25）", Source("distinctTextures"),
            "地形材质种类"));

        var dominantCeiling = Threshold("topTextureShare", 0.75);
        checks.Add(new ArtRubricCheck("dominantMaterialShare",
            dominantCeiling == null ? NotEvaluated
                : profile.TopTextureShare <= dominantCeiling ? Pass
                : profile.TopTextureShare <= dominantCeiling * 1.3 ? Warn : Fail,
            Math.Round(profile.TopTextureShare, 4),
            dominantCeiling == null ? "-" : $"不超过 {dominantCeiling:P0}（语料 P75）",
            Source("topTextureShare"),
            "单一材质占比；铺满整图是最常见的失败方式"));

        var blendFloor = Threshold("blendedShare", 0.25);
        checks.Add(Judge("transitionCoverage", profile.BlendedShare,
            blendFloor, blendFloor * 0.4,
            $"至少 {blendFloor:P0}（语料 P25）", Source("blendedShare"),
            "发生混合的格占比；过渡靠两种普通材质互相混合实现"));

        var densityLow = Threshold("objectsPer1000Cells", 0.25);
        var densityHigh = Threshold("objectsPer1000Cells", 0.75);
        checks.Add(new ArtRubricCheck("decorationDensity",
            densityLow == null || densityHigh == null ? NotEvaluated
                : profile.ObjectsPer1000Cells >= densityLow && profile.ObjectsPer1000Cells <= densityHigh ? Pass
                : profile.ObjectsPer1000Cells >= densityLow * 0.5 && profile.ObjectsPer1000Cells <= densityHigh * 1.5 ? Warn : Fail,
            Math.Round(profile.ObjectsPer1000Cells, 2),
            densityLow == null ? "-" : $"{densityLow:0.#}–{densityHigh:0.#} /千格（语料 P25–P75）",
            Source("objectsPer1000Cells"),
            "装饰密度；对 500x500 图约 " + (densityLow == null ? "?" : $"{densityLow * 250:0}-{densityHigh * 250:0}") + " 个物体"));

        var clumpFloor = Threshold("clumpingIndex", 0.25);
        checks.Add(Judge("clumping", profile.ClumpingIndex,
            clumpFloor, 1.0,
            $"至少 {clumpFloor:0.##}（语料 P25；约 1 为随机，小于 1 为规则排布）", Source("clumpingIndex"),
            "聚簇程度；规则网格散布会明显低于 1"));

        checks.Add(new ArtRubricCheck("materialCohesion", Descriptive,
            profile.Patches.LargestPatchShare, "仅报告", Source("largestPatchShare"),
            "最大连通块占该材质面积的比例。实测 shipped 地图本身就偏碎（斑块中位仅 4 格），"
                + "且拼贴图落在此指标的语料带内，故只报告不判分"));

        // Measured against the corpus this is the check that sees a stamped map: shipped
        // boundaries are ragged (median 560, P25 320), while material stamped as discs lands
        // near the theoretical disc value. The first two shape metrics I tried did not
        // discriminate at all — shipped maps are more fragmented than assumed, so a patch-size
        // or largest-patch check passed a map covered in circles. Only the check enforces a
        // floor; an implausibly ragged map would pass it.
        checks.Add(new ArtRubricCheck("patchNaturalness", Descriptive,
            profile.Patches.MeanCompactness, "仅报告", Source("meanCompactness"),
            "斑块紧凑度（周长^2/面积，正圆约 12.6）。**不再判分**：实测跟着地形等高线走的图得 34，"
                + "比圆形印章图的 159 还低，说明它度量的是边界锯齿度（接近逐格噪声），不是构成质量；"
                + "拿它当判据会给「往图上加噪声」正向激励"));

        var landmarkKinds = LandmarkCategories.Count(category =>
            profile.Categories.TryGetValue(category, out var count) && count >= 3);
        checks.Add(new ArtRubricCheck("landmarkPresence", landmarkKinds >= 3 ? Pass : landmarkKinds == 2 ? Warn : Fail,
            landmarkKinds,
            "至少 3 类、每类至少 3 个（Roadmap S2 验收条件，非语料统计）",
            "Roadmap S2 验收条件", "可辨识地标数量"));

        // Not decidable from statistics, and saying so beats inventing a proxy.
        checks.Add(new ArtRubricCheck("combatReadability", NotEvaluated, null, "-",
            "需要人眼看渲染图", "战斗可读性无法由统计量判定，须看 review 大图"));

        var failed = checks.Count(c => c.Verdict == Fail);
        var warned = checks.Count(c => c.Verdict == Warn);
        var passed = checks.Count(c => c.Verdict == Pass);
        var notEvaluated = checks.Count(c => c.Verdict == NotEvaluated);
        // Descriptive checks carry a measurement but no direction, so they never decide.
        var descriptive = checks.Count(c => c.Verdict == Descriptive);
        return new ArtRubricResult(failed > 0 ? "needs-iteration" : warned > 0 ? "acceptable" : "pass",
            passed, warned, failed, notEvaluated, rules?.RulesHash ?? "", checks, descriptive);
    }

    private static ArtRubricCheck Judge(string id, double measured, double? floor, double? hardFloor,
        string threshold, string source, string note) => new(id,
        floor == null ? NotEvaluated
            : measured >= floor ? Pass
            : measured >= (hardFloor ?? floor) ? Warn : Fail,
        Math.Round(measured, 4), floor == null ? "-" : threshold, source, note);
}

internal static class ArtRuleDistributionExtensions
{
    /// <summary>Reads a quantile from a measured distribution, interpolating between recorded points.</summary>
    public static double Get(this ArtRuleDistribution distribution, double fraction) => fraction switch
    {
        <= 0.25 => distribution.P25,
        <= 0.5 => distribution.P25 + (distribution.Median - distribution.P25) * (fraction - 0.25) / 0.25,
        <= 0.75 => distribution.Median + (distribution.P75 - distribution.Median) * (fraction - 0.5) / 0.25,
        _ => distribution.P75
    };
}
