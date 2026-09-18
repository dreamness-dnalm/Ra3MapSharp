using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Automation.Test;

/// <summary>
/// The rubric is what decides whether work leaves "needs-iteration", so these pin that it fails
/// when it should, passes when it should, and refuses to guess when it has no thresholds.
/// </summary>
public class ArtRubricTests
{
    private static ArtRules Rules()
    {
        var rules = new ArtRules { BuiltAtUtc = DateTimeOffset.UnixEpoch, CorpusPath = "/corpus" };
        for (var index = 0; index < 63; index++)
            rules.Maps.Add(new ArtRuleMap("m" + index, 256, 256, 65536, 30000, 30, 0.28, 0.56, 0.02, 0.19,
                800, 12.0, 3.0, new SortedDictionary<string, int>(StringComparer.Ordinal), new List<ArtRulePair>()));
        void Distribution(string key, double p25, double median, double p75) =>
            rules.Distributions[key] = new ArtRuleDistribution(key, p25 * 0.5, p25, median, p75, p75 * 1.5);
        Distribution("distinctTextures", 21, 30, 37);
        Distribution("topTextureShare", 0.21, 0.28, 0.46);
        Distribution("blendedShare", 0.12, 0.19, 0.24);
        Distribution("objectsPer1000Cells", 9.27, 12.01, 14.82);
        Distribution("clumpingIndex", 2.39, 2.99, 3.81);
        rules.RulesHash = rules.ComputeHash();
        return rules;
    }

    private static MapArtProfile Profile(int textures = 30, double top = 0.28, double blended = 0.19,
        double density = 12, double clumping = 3.0, int landmarkKinds = 4)
    {
        var categories = new SortedDictionary<string, int>(StringComparer.Ordinal) { ["树草"] = 500 };
        foreach (var name in new[] { "雕像和石柱", "阵营特殊建筑", "浮岛要塞装饰", "特殊地形" }.Take(landmarkKinds))
            categories[name] = 5;
        return new MapArtProfile(256, 256, 65536, 30000, textures, top, 0.56, 0.02, blended, 800, density,
            clumping, categories, new List<ArtRulePair>());
    }

    private static string Verdict(ArtRubricResult result, string id) =>
        result.Checks.Single(check => check.Id == id).Verdict;

    [Test]
    public void AProfileMatchingTheCorpusPasses()
    {
        var result = ArtRubric.Evaluate(Profile(), Rules());
        Assert.That(result.Verdict, Is.EqualTo("pass"));
        Assert.That(result.Failed, Is.Zero);
        Assert.That(result.NotEvaluated, Is.EqualTo(1), "combat readability is never decided by a statistic");
        Assert.That(Verdict(result, "combatReadability"), Is.EqualTo(ArtRubric.NotEvaluated));
    }

    [Test]
    public void OneMaterialOverMostOfTheMapFails()
    {
        var result = ArtRubric.Evaluate(Profile(top: 0.8), Rules());
        Assert.That(Verdict(result, "dominantMaterialShare"), Is.EqualTo(ArtRubric.Fail));
        Assert.That(result.Verdict, Is.EqualTo("needs-iteration"));
    }

    [Test]
    public void AnEvenlyPlacedScatterFailsTheClumpingRule()
    {
        // A min-distance scatter lands below 1; the corpus says good maps sit near 3.
        var result = ArtRubric.Evaluate(Profile(clumping: 0.92), Rules());
        Assert.That(Verdict(result, "clumping"), Is.EqualTo(ArtRubric.Fail));
    }

    [Test]
    public void TooFewMaterialsAndTooLittleBlendingFail()
    {
        var result = ArtRubric.Evaluate(Profile(textures: 7, blended: 0.03), Rules());
        Assert.That(Verdict(result, "materialVariety"), Is.EqualTo(ArtRubric.Fail));
        Assert.That(Verdict(result, "transitionCoverage"), Is.EqualTo(ArtRubric.Fail));
    }

    [Test]
    public void DensityOutsideTheBandFailsAndLandmarksAreCounted()
    {
        Assert.That(Verdict(ArtRubric.Evaluate(Profile(density: 4), Rules()), "decorationDensity"),
            Is.EqualTo(ArtRubric.Fail));
        Assert.That(Verdict(ArtRubric.Evaluate(Profile(landmarkKinds: 0), Rules()), "landmarkPresence"),
            Is.EqualTo(ArtRubric.Fail));
        Assert.That(Verdict(ArtRubric.Evaluate(Profile(landmarkKinds: 3), Rules()), "landmarkPresence"),
            Is.EqualTo(ArtRubric.Pass));
    }

    [Test]
    public void WithoutThresholdsNothingIsJudged()
    {
        var result = ArtRubric.Evaluate(Profile(top: 0.95, clumping: 0.2), null);
        Assert.That(result.Verdict, Is.EqualTo("pass"), "no thresholds means no failures, not invented ones");
        Assert.That(result.NotEvaluated, Is.EqualTo(result.Checks.Count - 1),
            "every threshold-driven check is reported as unevaluated except the roadmap landmark rule");
        Assert.That(Verdict(result, "dominantMaterialShare"), Is.EqualTo(ArtRubric.NotEvaluated));
        Assert.That(result.Checks.All(check => check.Source.Length > 0), Is.True,
            "each check names where its threshold came from");
    }

    [Test]
    public void EveryCheckCarriesANoteAndAThreshold()
    {
        var result = ArtRubric.Evaluate(Profile(), Rules());
        Assert.That(result.Checks, Is.Not.Empty);
        Assert.That(result.Checks.All(check => !string.IsNullOrWhiteSpace(check.Note)), Is.True);
        Assert.That(result.Checks.All(check => !string.IsNullOrWhiteSpace(check.Threshold)), Is.True);
        Assert.That(result.Passed + result.Warned + result.Failed + result.NotEvaluated,
            Is.EqualTo(result.Checks.Count));
    }
}
