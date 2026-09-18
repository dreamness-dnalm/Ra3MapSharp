using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Agent;

/// <summary>
/// Measures art-direction statistics across the shipped-map corpus (--analyze-corpus).
/// <para>
/// This is the evidence half of the art loop: it answers "how many materials does a good map use,
/// how much of it is one material, how much is blended, how densely are props placed, and are
/// they clumped or laid out on a grid?" from maps that actually shipped. The output is a set of
/// thresholds a later reviewer can hold work against.
/// </para>
/// </summary>
internal static class CorpusAnalysisCommand
{
    public static Task<int> RunAsync(string corpusPath, string outputPath, ObjectCatalog? catalog,
        int sampleTarget, int? limit, CancellationToken token)
    {
        var root = new DirectoryInfo(Path.GetFullPath(corpusPath));
        if (!root.Exists) throw new AutomationException("CATALOG_NOT_FOUND", "语料目录不存在: " + root.FullName);

        var maps = root.EnumerateDirectories()
            .Select(directory => directory.EnumerateFiles("*.map").FirstOrDefault())
            .Where(file => file != null).Cast<FileInfo>()
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToList();
        if (limit.HasValue) maps = maps.Take(limit.Value).ToList();
        if (maps.Count == 0) throw new AutomationException("CATALOG_NOT_FOUND", "语料目录里没有 .map。");

        Console.WriteLine($"Corpus: {maps.Count} maps from {root.FullName}, sampling ~{sampleTarget} cells each");
        var rules = new ArtRules
        {
            BuiltAtUtc = DateTimeOffset.UtcNow,
            CorpusPath = root.FullName,
            SampleTarget = sampleTarget
        };

        // Built once: resolving a category per object with a linear scan would be 1688 comparisons
        // for every one of tens of thousands of objects.
        var categoryLookup = catalog?.Entries
            .GroupBy(entry => entry.TypeName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.First().Categories.FirstOrDefault(label => label != "editor-translation")
                    ?? "unclassified",
                StringComparer.Ordinal);

        foreach (var file in maps)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                rules.Maps.Add(Analyze(file, categoryLookup, sampleTarget, token));
                var analysed = rules.Maps[^1];
                Console.WriteLine($"  {file.Name}: {analysed.Width}x{analysed.Height}, "
                    + $"{analysed.DistinctTextures} textures, top {analysed.TopTextureShare:P0}, "
                    + $"blended {analysed.BlendedShare:P0}, {analysed.Objects} objects, "
                    + $"clumping {analysed.ClumpingIndex:0.0}");
            }
            // The corpus is external input: a map this parser cannot read must not abort the run.
            // The failure is recorded with its type so the gap is visible rather than silent.
            catch (Exception ex)
            {
                rules.Failures.Add(new ArtRuleFailure(file.Name, "ANALYSIS_FAILED",
                    ex.GetType().Name + ": " + ex.Message));
                Console.WriteLine($"  {file.Name}: FAILED {ex.GetType().Name}: {ex.Message}");
            }
        }

        Aggregate(rules);
        rules.RulesHash = rules.ComputeHash();
        rules.Save(outputPath);
        Console.WriteLine($"Art rules written to {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"  rulesHash {rules.RulesHash}   maps {rules.Maps.Count}   failures {rules.Failures.Count}");
        foreach (var rule in rules.Rules) Console.WriteLine("  - " + rule);
        return Task.FromResult(0);
    }

    private static ArtRuleMap Analyze(FileInfo file, IReadOnlyDictionary<string, string>? categoryLookup,
        int sampleTarget, CancellationToken token)
    {
        // The same measurement the review command applies to work in progress: a rubric is only
        // meaningful when the judged number and the threshold were produced the same way.
        var profile = MapArtProfile.Measure(Ra3MapFacade.Open(file.FullName), categoryLookup, sampleTarget, token);
        return new ArtRuleMap(file.Name, profile.Width, profile.Height, profile.Cells, profile.SampledCells,
            profile.DistinctTextures, profile.TopTextureShare, profile.TopThreeShare, profile.TransitionShare,
            profile.BlendedShare, profile.Objects, profile.ObjectsPer1000Cells, profile.ClumpingIndex,
            profile.Categories, profile.Pairs);
    }

    private static void Aggregate(ArtRules rules)
    {
        var maps = rules.Maps;
        if (maps.Count == 0) return;
        void Distribution(string key, Func<ArtRuleMap, double> selector, bool positiveOnly = false)
        {
            var values = maps.Select(selector).Where(value => double.IsFinite(value) && (!positiveOnly || value > 0))
                .OrderBy(value => value).ToArray();
            if (values.Length == 0) return;
            double At(double fraction) => values[Math.Min(values.Length - 1, (int)(fraction * values.Length))];
            rules.Distributions[key] = new ArtRuleDistribution(key,
                values[0], At(0.25), At(0.5), At(0.75), values[^1]);
        }
        Distribution("distinctTextures", m => m.DistinctTextures);
        Distribution("topTextureShare", m => m.TopTextureShare);
        Distribution("topThreeShare", m => m.TopThreeShare);
        Distribution("transitionShare", m => m.TransitionShare);
        Distribution("blendedShare", m => m.BlendedShare);
        Distribution("objectsPer1000Cells", m => m.ObjectsPer1000Cells);
        Distribution("clumpingIndex", m => m.ClumpingIndex, positiveOnly: true);

        // A pair is interesting when many maps blend those two textures, not when one map does it
        // a lot, so count maps first and samples second.
        var pairMaps = new Dictionary<(string Primary, string Secondary), (int Maps, int Count)>();
        foreach (var map in maps)
        {
            foreach (var pair in map.Pairs)
            {
                var key = (pair.Primary, pair.Secondary);
                var current = pairMaps.TryGetValue(key, out var seen) ? seen : (Maps: 0, Count: 0);
                pairMaps[key] = (current.Maps + 1, current.Count + pair.Count);
            }
        }
        rules.TexturePairs.AddRange(pairMaps
            .OrderByDescending(pair => pair.Value.Maps).ThenByDescending(pair => pair.Value.Count)
            .Take(40)
            .Select(pair => new ArtRulePair(pair.Key.Primary, pair.Key.Secondary, pair.Value.Maps, pair.Value.Count)));

        // Category share: median share of placed objects per category across maps.
        var shareByCategory = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        foreach (var map in maps)
        {
            var total = Math.Max(1, map.Categories.Values.Sum());
            foreach (var pair in map.Categories)
            {
                if (!shareByCategory.TryGetValue(pair.Key, out var list)) shareByCategory[pair.Key] = list = new List<double>();
                list.Add(pair.Value / (double)total);
            }
        }
        foreach (var pair in shareByCategory.OrderByDescending(pair => Median(pair.Value)).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            rules.CategoryShare[pair.Key] = Math.Round(Median(pair.Value), 4);

        rules.Rules.AddRange(BuildRules(rules));
        rules.Notes.Add("统计来自 shipped 地图语料，按格采样（每图约 " + rules.SampleTarget + " 格），不是逐格普查。");
        rules.Notes.Add("纹理配对只统计真正发生混合（存在次纹理）的相邻关系，因此可直接当作过渡搭配的依据。");
        rules.Notes.Add("聚簇指数为 8 格样方的方差/均值：约 1 为独立散布，明显大于 1 为成群，明显小于 1 为规则排布。");
        rules.Notes.Add("这些是 shipped 地图的既有惯例，不是引擎限制；偏离它们需要理由，但不是错误。");
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
    }

    private static IEnumerable<string> BuildRules(ArtRules rules)
    {
        string Describe(string key) => rules.Distributions.TryGetValue(key, out var d)
            ? $"中位数 {Format(key, d.Median)}（P25 {Format(key, d.P25)} – P75 {Format(key, d.P75)}）" : "未测得";
        yield return $"单图地形纹理种类：{Describe("distinctTextures")}——不要只用一两种材质。";
        yield return $"主导纹理占比：{Describe("topTextureShare")}——单一材质铺满会明显偏离惯例。";
        yield return $"前三纹理合计占比：{Describe("topThreeShare")}——说明大部分面积由少数材质承担，其余是点缀。";
        yield return $"发生混合的格占比：{Describe("blendedShare")}——过渡是常规做法，不是可选项。";
        var transitionMaps = rules.Maps.Count(map => map.TransitionShare > 0);
        yield return $"Transition_* 材质：{transitionMaps}/{rules.Maps.Count} 张图用到，占比{Describe("transitionShare")}"
            + "——真正的过渡手段是两种普通表面材质互相混合（见下一行），不是这个家族。";
        yield return $"物体密度：{Describe("objectsPer1000Cells")}（每千格）——按此推算装饰物数量。";
        yield return $"聚簇指数：{Describe("clumpingIndex")}——明显大于 1，即成群分布；规则网格排布会得到小于 1。";
        var top = rules.CategoryShare.Where(pair => pair.Value >= 0.005)
            .OrderByDescending(pair => pair.Value)
            .Select(pair => $"{pair.Key} {pair.Value:P0}");
        yield return "主要分类占已放置物体的比例（中位数，>=0.5%）：" + string.Join("、", top) + "。";
        if (rules.TexturePairs.Count > 0)
        {
            var pairs = rules.TexturePairs.Take(4)
                .Select(pair => $"{pair.Primary} + {pair.Secondary}（{pair.Maps} 张图）");
            yield return "实测最常一起出现（真正发生混合）的纹理配对：" + string.Join("、", pairs)
                + "——选材时优先复用这些组合。";
        }
    }

    private static string Format(string key, double value) => key.Contains("Share")
        ? value.ToString("P0") : value.ToString("0.##");
}
