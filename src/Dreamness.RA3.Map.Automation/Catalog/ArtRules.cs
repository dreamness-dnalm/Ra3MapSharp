using System.Text.Json;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>
/// Art-direction statistics measured from shipped maps, so material and density choices can be
/// justified instead of guessed.
/// <para>
/// The corpus is the highest-quality reference available: these are maps that shipped, and the
/// question "how many textures does a good map use, and how much of it is one material?" has a
/// measurable answer. Every rule below cites the statistic it came from.
/// </para>
/// </summary>
public sealed class ArtRules
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset BuiltAtUtc { get; set; }

    public string CorpusPath { get; set; } = "";

    /// <summary>Cells sampled per map, so a reader knows how much of each map was actually seen.</summary>
    public int SampleTarget { get; set; }

    public string RulesHash { get; set; } = "";

    public List<ArtRuleMap> Maps { get; set; } = new();

    public List<ArtRuleFailure> Failures { get; set; } = new();

    public SortedDictionary<string, ArtRuleDistribution> Distributions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Texture pairs that actually meet at a blend, most frequent first.</summary>
    public List<ArtRulePair> TexturePairs { get; set; } = new();

    /// <summary>Share of placed objects per editor category, median across maps.</summary>
    public SortedDictionary<string, double> CategoryShare { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Sentences derived from the distributions. Each one names its statistic.</summary>
    public List<string> Rules { get; set; } = new();

    public List<string> Notes { get; set; } = new();

    private static readonly JsonSerializerOptions Json = AutomationJson.CompactOptions;

    public string ComputeHash() => ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(new
    {
        SchemaVersion,
        SampleTarget,
        Maps,
        Distributions,
        TexturePairs,
        CategoryShare,
        Rules
    }, Json));

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    public static ArtRules Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) bytes = bytes[3..];
        var rules = JsonSerializer.Deserialize<ArtRules>(bytes, Json)
            ?? throw new AutomationException("INVALID_CATALOG", "美术规则为空。");
        if (rules.SchemaVersion != CurrentSchemaVersion)
            throw new AutomationException("UNSUPPORTED_VERSION", "不支持的美术规则版本。");
        return rules;
    }

    public static string DefaultPath(string artifactsDirectory) =>
        Path.Combine(Path.GetFullPath(artifactsDirectory), "art-rules", "art-rules.json");

    /// <summary>The rules plus the numbers behind them, for a caller that wants to cite them.</summary>
    public object Info() => new
    {
        schemaVersion = SchemaVersion,
        rulesHash = RulesHash,
        builtAtUtc = BuiltAtUtc,
        corpusPath = CorpusPath,
        maps = Maps.Count,
        failures = Failures.Count,
        sampleTarget = SampleTarget,
        distributions = Distributions,
        topTexturePairs = TexturePairs.Take(12).ToArray(),
        categoryShare = CategoryShare,
        rules = Rules,
        notes = Notes
    };
}

public sealed record ArtRuleMap(string Name, int Width, int Height, long Cells, int SampledCells,
    int DistinctTextures, double TopTextureShare, double TopThreeShare, double TransitionShare,
    double BlendedShare, int Objects, double ObjectsPer1000Cells, double ClumpingIndex,
    SortedDictionary<string, int> Categories, List<ArtRulePair> Pairs, MapPatchStats Patches);

public sealed record ArtRuleDistribution(string Key, double Min, double P25, double Median, double P75, double Max);

/// <summary>A pair of textures observed meeting at a blend. Maps is the stronger signal.</summary>
public sealed record ArtRulePair(string Primary, string Secondary, int Maps, int Count);

public sealed record ArtRuleFailure(string Name, string Code, string Message);
