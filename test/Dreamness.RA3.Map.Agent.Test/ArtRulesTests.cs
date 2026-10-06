using System.Text.Json;
using Dreamness.RA3.Map.Agent;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.Ra3.Map.Facade.Core;
using NUnit.Framework;

namespace Dreamness.RA3.Map.Agent.Test;

public class ArtRulesTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-artrules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void Cleanup() => Directory.Delete(_root, true);

    private static ArtRules Sample()
    {
        var rules = new ArtRules { BuiltAtUtc = DateTimeOffset.UnixEpoch, CorpusPath = "/corpus", SampleTarget = 30000 };
        rules.Maps.Add(new ArtRuleMap("a.map", 64, 64, 4096, 4096, 3, 0.9, 1.0, 0, 0.1, 5, 1.22, 0,
            new SortedDictionary<string, int>(StringComparer.Ordinal) { ["树草"] = 5 },
            new List<ArtRulePair> { new("Dirt_Yucatan03", "Grass_Yucatan01", 1, 10) },
            new MapPatchStats(8, 512, 0.8, 0.5, 700, 14)));
        rules.Distributions["distinctTextures"] = new ArtRuleDistribution("distinctTextures", 3, 3, 3, 3, 3);
        rules.TexturePairs.Add(new ArtRulePair("Dirt_Yucatan03", "Grass_Yucatan01", 1, 10));
        rules.CategoryShare["树草"] = 1.0;
        rules.Rules.Add("单图地形纹理种类：中位数 3");
        rules.RulesHash = rules.ComputeHash();
        return rules;
    }

    [Test]
    public void RoundTripsAndHashMovesWithContent()
    {
        var path = Path.Combine(_root, "art-rules.json");
        var rules = Sample();
        rules.Save(path);
        var loaded = ArtRules.Load(path);

        Assert.That(loaded.Maps, Has.Count.EqualTo(1));
        Assert.That(loaded.TexturePairs[0].Primary, Is.EqualTo("Dirt_Yucatan03"));
        Assert.That(loaded.RulesHash, Is.EqualTo(rules.RulesHash));
        loaded.Rules.Add("changed");
        Assert.That(loaded.ComputeHash(), Is.Not.EqualTo(rules.RulesHash), "the derived rules are part of the identity");
    }

    [Test]
    public void InfoIsSerializableForTheArtRulesTool()
    {
        var payload = JsonSerializer.Serialize(Sample().Info(), AgentJson.Options);
        Assert.That(payload, Does.Contain("rulesHash"));
        Assert.That(payload, Does.Contain("texturePairs").Or.Contain("topTexturePairs"));
    }

    [Test]
    public async Task AnalysisMeasuresTexturesObjectsAndReportsRules()
    {
        var corpus = Path.Combine(_root, "corpus");
        var sample = Path.Combine(corpus, "Sample");
        Directory.CreateDirectory(sample);
        var map = Ra3MapFacade.NewMap(playableWidth: 64, playableHeight: 64, border: 8,
            initPlayerStartWaypointCnt: 0, defaultTexture: "Dirt_Yucatan03");
        map.SetTileTexture(10, 10, "Grass_Yucatan01");
        map.SetTileTexture(11, 10, "Rock_Yucatan01");
        map.AddUnitObject("CC_Tree01", 200, 200);
        map.SaveAs(Path.Combine(sample, "Sample.map"), true);

        var output = Path.Combine(_root, "art-rules.json");
        Assert.That(await CorpusAnalysisCommand.RunAsync(corpus, output, null, 30000, null, CancellationToken.None),
            Is.EqualTo(0));

        var rules = ArtRules.Load(output);
        Assert.That(rules.Maps, Has.Count.EqualTo(1));
        var measured = rules.Maps[0];
        Assert.That(measured.DistinctTextures, Is.GreaterThanOrEqualTo(3));
        Assert.That(measured.SampledCells, Is.GreaterThan(1000));
        Assert.That(measured.Objects, Is.EqualTo(1));
        Assert.That(measured.TopTextureShare, Is.GreaterThan(0.9), "one texture covers almost the whole map");
        Assert.That(rules.Rules, Is.Not.Empty);
        Assert.That(rules.Rules.Any(rule => rule.Contains("主导纹理占比")), Is.True);
        Assert.That(rules.Distributions.ContainsKey("topTextureShare"), Is.True);
    }

    [Test]
    public async Task AnUnreadableMapIsRecordedInsteadOfAbortingTheRun()
    {
        var corpus = Path.Combine(_root, "corpus");
        var sample = Path.Combine(corpus, "Broken");
        Directory.CreateDirectory(sample);
        // The corpus is external input; a map the parser cannot read must not kill the analysis.
        await File.WriteAllBytesAsync(Path.Combine(sample, "Broken.map"), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var output = Path.Combine(_root, "art-rules.json");
        Assert.That(await CorpusAnalysisCommand.RunAsync(corpus, output, null, 30000, null, CancellationToken.None),
            Is.EqualTo(0));
        var rules = ArtRules.Load(output);
        Assert.That(rules.Maps, Is.Empty);
        Assert.That(rules.Failures, Has.Count.EqualTo(1));
        Assert.That(rules.Failures[0].Name, Is.EqualTo("Broken.map"));
    }

    [Test]
    public async Task ArtRulesToolServesTheMeasuredRules()
    {
        var rules = Sample();
        await using var runtime = new AgentRuntime(artRules: rules);
        var result = await runtime.ExecuteAsync(new CommandRequest { Command = "art.rules" });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        var data = JsonSerializer.SerializeToElement(result.Data, AgentJson.Options);
        Assert.That(data.GetProperty("rulesHash").GetString(), Is.EqualTo(rules.RulesHash));
        Assert.That(data.GetProperty("maps").GetInt32(), Is.EqualTo(1));
        Assert.That(data.GetProperty("rules").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task ReviewRefusesWithoutARenderer()
    {
        // review.render_set composes images, so a host with no launcher must say so rather than
        // return a verdict with no pictures behind it.
        await using var runtime = new AgentRuntime(artRules: Sample());
        var result = await runtime.ExecuteAsync(new CommandRequest { Command = "review.render_set" });
        Assert.That(result.Error?.Code, Is.EqualTo("RENDER_NOT_CONFIGURED"));
    }

    [Test]
    public async Task ArtRulesToolWithoutRulesSaysSo()
    {
        await using var runtime = new AgentRuntime();
        Assert.That((await runtime.ExecuteAsync(new CommandRequest { Command = "art.rules" })).Error?.Code,
            Is.EqualTo("RULES_UNAVAILABLE"));
    }
}
