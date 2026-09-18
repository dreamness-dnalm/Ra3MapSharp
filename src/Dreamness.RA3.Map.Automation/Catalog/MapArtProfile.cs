using Dreamness.RA3.Map.Automation.Storage;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Catalog;

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
    SortedDictionary<string, int> Categories, List<ArtRulePair> Pairs)
{
    private const int QuadratCells = 8;

    /// <summary>
    /// Samples the map on a stride so a 1000x1000 map costs about as much as a 64x64 one.
    /// </summary>
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
                .Select(pair => new ArtRulePair(pair.Key.Primary, pair.Key.Secondary, 1, pair.Value)).ToList());
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
}

/// <summary>One rubric line: what was measured, against which threshold, and where that came from.</summary>
public sealed record ArtRubricCheck(string Id, string Verdict, double? Measured, string Threshold,
    string Source, string Note);

public sealed record ArtRubricResult(string Verdict, int Passed, int Warned, int Failed,
    int NotEvaluated, string RulesHash, List<ArtRubricCheck> Checks);

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

        // Material variety and dominance are the two halves of "do not paint it in one colour".
        var varietyFloor = Threshold("distinctTextures", 0.25);
        checks.Add(Judge("materialVariety", profile.DistinctTextures,
            varietyFloor, varietyFloor * 0.4,
            $"至少 {varietyFloor:0} 种（语料 P25）", Source("distinctTextures"),
            "地形材质种类")) ;

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
        return new ArtRubricResult(failed > 0 ? "needs-iteration" : warned > 0 ? "acceptable" : "pass",
            passed, warned, failed, notEvaluated, rules?.RulesHash ?? "", checks);
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
    /// <summary>Reads a quantile from a measured distribution, interpolating between the recorded points.</summary>
    public static double Get(this ArtRuleDistribution distribution, double fraction) => fraction switch
    {
        <= 0.25 => distribution.P25,
        <= 0.5 => distribution.P25 + (distribution.Median - distribution.P25) * (fraction - 0.25) / 0.25,
        <= 0.75 => distribution.Median + (distribution.P75 - distribution.Median) * (fraction - 0.5) / 0.25,
        _ => distribution.P75
    };
}
