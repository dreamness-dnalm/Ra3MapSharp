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
                800, 12.0, 3.0, new SortedDictionary<string, int>(StringComparer.Ordinal), new List<ArtRulePair>(),
                new MapPatchStats(40, 900, 0.85, 0.6, 1200, 14)));
        void Distribution(string key, double p25, double median, double p75) =>
            rules.Distributions[key] = new ArtRuleDistribution(key, p25 * 0.5, p25, median, p75, p75 * 1.5);
        Distribution("distinctTextures", 21, 30, 37);
        Distribution("topTextureShare", 0.21, 0.28, 0.46);
        Distribution("blendedShare", 0.12, 0.19, 0.24);
        Distribution("objectsPer1000Cells", 9.27, 12.01, 14.82);
        Distribution("clumpingIndex", 2.39, 2.99, 3.81);
        Distribution("largestPatchShare", 0.72, 0.85, 0.93);
        Distribution("meanCompactness", 320, 560, 960);
        rules.RulesHash = rules.ComputeHash();
        return rules;
    }

    private static MapArtProfile Profile(int textures = 30, double top = 0.28, double blended = 0.19,
        double density = 12, double clumping = 3.0, int landmarkKinds = 4, double cohesion = 0.85,
        double compactness = 560)
    {
        var categories = new SortedDictionary<string, int>(StringComparer.Ordinal) { ["树草"] = 500 };
        foreach (var name in new[] { "雕像和石柱", "阵营特殊建筑", "浮岛要塞装饰", "特殊地形" }.Take(landmarkKinds))
            categories[name] = 5;
        return new MapArtProfile(256, 256, 65536, 30000, textures, top, 0.56, 0.02, blended, 800, density,
            clumping, categories, new List<ArtRulePair>(), new MapPatchStats(40, 900, cohesion, 0.6, 1200, compactness));
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
        // A min-distance scatter lands below 1 (regular/even); that remains a fail.
        var result = ArtRubric.Evaluate(Profile(clumping: 0.92), Rules());
        Assert.That(Verdict(result, "clumping"), Is.EqualTo(ArtRubric.Fail));
    }

    [Test]
    public void ShapeIsMeasuredButDoesNotDecideTheVerdict()
    {
        // Both shape metrics failed to validate against the data: shipped maps are far more
        // fragmented than assumed (median patch 4 cells), and a map whose materials follow the
        // terrain contours scored 34 where a map stamped with discs scored 159. Compactness is
        // therefore measuring boundary ragginess rather than composition, and a check that
        // rewards adding noise is worse than no check. They stay visible as measurements.
        var stamped = ArtRubric.Evaluate(Profile(cohesion: 0.18, compactness: 158.73), Rules());
        Assert.That(Verdict(stamped, "materialCohesion"), Is.EqualTo(ArtRubric.Descriptive));
        Assert.That(Verdict(stamped, "patchNaturalness"), Is.EqualTo(ArtRubric.Descriptive));
        Assert.That(stamped.Descriptive, Is.EqualTo(3),
            "shape checks and landmarks are reported, none of them decide");
        Assert.That(stamped.Verdict, Is.EqualTo("pass"), "no amount is out of range, and shape does not decide");
        Assert.That(stamped.Checks.Single(c => c.Id == "patchNaturalness").Measured, Is.EqualTo(158.73),
            "the number is still reported so a human can look at it");
    }

    [Test]
    public void TooFewMaterialsAndTooLittleBlendingFail()
    {
        var result = ArtRubric.Evaluate(Profile(textures: 7, blended: 0.03), Rules());
        Assert.That(Verdict(result, "materialVariety"), Is.EqualTo(ArtRubric.Fail));
        Assert.That(Verdict(result, "transitionCoverage"), Is.EqualTo(ArtRubric.Fail));
    }

    [Test]
    public void DensityOutsideTheBandFails()
    {
        Assert.That(Verdict(ArtRubric.Evaluate(Profile(density: 4), Rules()), "decorationDensity"),
            Is.EqualTo(ArtRubric.Fail));
    }

    [Test]
    public void ClumpingBetweenOneAndCorpusP25IsReportedOnly()
    {
        var result = ArtRubric.Evaluate(Profile(clumping: 1.96), Rules());
        Assert.That(Verdict(result, "clumping"), Is.EqualTo(ArtRubric.Descriptive));
        Assert.That(result.Verdict, Is.EqualTo("pass"),
            "clustered-but-below-P25 must not force iteration");
    }

    [Test]
    public void LandmarkCountIsAdvisoryAndDoesNotDecide()
    {
        var none = ArtRubric.Evaluate(Profile(landmarkKinds: 0), Rules());
        var some = ArtRubric.Evaluate(Profile(landmarkKinds: 1), Rules());
        var many = ArtRubric.Evaluate(Profile(landmarkKinds: 3), Rules());
        Assert.That(Verdict(none, "landmarkPresence"), Is.EqualTo(ArtRubric.Descriptive));
        Assert.That(Verdict(some, "landmarkPresence"), Is.EqualTo(ArtRubric.Descriptive));
        Assert.That(Verdict(many, "landmarkPresence"), Is.EqualTo(ArtRubric.Descriptive));
        Assert.That(none.Verdict, Is.EqualTo("pass"));
        Assert.That(some.Checks.Single(c => c.Id == "landmarkPresence").Measured, Is.EqualTo(1));
    }

    [Test]
    public void WithoutThresholdsNothingIsJudged()
    {
        var result = ArtRubric.Evaluate(Profile(top: 0.95, clumping: 0.2), null);
        Assert.That(result.Verdict, Is.EqualTo("pass"), "no thresholds means no failures, not invented ones");
        Assert.That(result.NotEvaluated + result.Descriptive, Is.EqualTo(result.Checks.Count),
            "without thresholds everything is either unevaluated or descriptive");
        Assert.That(result.Descriptive, Is.EqualTo(3),
            "shape checks and landmarks are descriptive regardless of rules");
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
        Assert.That(result.Passed + result.Warned + result.Failed + result.NotEvaluated + result.Descriptive,
            Is.EqualTo(result.Checks.Count));
    }
}
