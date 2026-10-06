using System.Text.Json;

namespace Dreamness.RA3.Map.Automation.Design;

public sealed class DesignEntitySpec
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? Label { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DependsOn { get; set; }
    public JsonElement Parameters { get; set; }
}

public sealed class DesignPatch
{
    public int SchemaVersion { get; set; } = 1;
    public List<DesignEntitySpec> Upsert { get; set; } = new();
    public List<string> Remove { get; set; } = new();
}

public sealed class DesignEntityState
{
    public const string CurrentAlgorithmVersion = "entity-compiler-v8";
    public DesignEntitySpec Spec { get; set; } = new();
    public string AlgorithmVersion { get; set; } = CurrentAlgorithmVersion;
    public Dictionary<string, string> ObjectFingerprints { get; set; } = new(StringComparer.Ordinal);
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? WaypointFingerprints { get; set; }
    public List<OwnedHeightCell> Heights { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<OwnedTextureCell>? Textures { get; set; }
}

public sealed record OwnedHeightCell(int X, int Y, float Before, float After);
public sealed record TextureCellValue(ushort Tile, ushort Blend, ushort SingleEdgeBlend, ushort CliffBlend, string DefinitionHash);
public sealed record OwnedTextureCell(int X, int Y, TextureCellValue Before, TextureCellValue After);
