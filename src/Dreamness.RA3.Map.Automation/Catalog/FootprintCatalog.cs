using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>
/// Measured placement footprints, keyed by object type.
/// <para>
/// Nothing in the editor data declares how much room an object occupies, so every command that
/// cares about overlap had to be handed footprints by the caller or skip the check entirely
/// (they report the omission as "notEvaluated"). These come from the album's renders: the
/// overview is orthographic top-down, so an object's pixel extent on uniform ground is its
/// plan-view extent, which is what a footprint is.
/// </para>
/// <para>
/// The measurement includes whatever shadow the renderer drew, so a value is an upper bound
/// rather than an exact blocking shape. The source string says so instead of hiding it, and
/// hand corrections live in a separate overrides file so a rebuild cannot erase them.
/// </para>
/// </summary>
public sealed class FootprintCatalog
{
    public const int CurrentSchemaVersion = 1;

    public const string SourceRendered = "rendered-extent";
    public const string SourceDeclared = "declared";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset BuiltAtUtc { get; set; }

    public string SourceAlbumHash { get; set; } = "";

    public string CatalogHash { get; set; } = "";

    public SortedDictionary<string, FootprintEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Objects the render could not measure. Recorded rather than assumed to be 1x1.</summary>
    public List<FootprintFailure> Failures { get; set; } = new();

    public List<string> Notes { get; set; } = new();

    private static readonly JsonSerializerOptions Json = AutomationJson.CompactOptions;

    public string ComputeHash() => ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(new
    {
        SchemaVersion,
        SourceAlbumHash,
        Entries,
        Failures
    }, Json));

    public FootprintEntry? Find(string typeName) =>
        Entries.TryGetValue(typeName, out var entry) ? entry : null;

    /// <summary>
    /// Footprints for the requested types, in the shape the geometric commands accept.
    /// Throws for a type with no measurement rather than inventing a default, because a guessed
    /// footprint silently changes collision results.
    /// </summary>
    public ObjectFootprint[] Profiles(IEnumerable<string> typeNames)
    {
        var profiles = new List<ObjectFootprint>();
        foreach (var typeName in typeNames.Distinct(StringComparer.Ordinal))
        {
            if (!Entries.TryGetValue(typeName, out var entry))
                throw new AutomationException("FOOTPRINT_MISSING", "没有该物体的占地测量: " + typeName);
            profiles.Add(entry.ToProfile());
        }
        return profiles.ToArray();
    }

    /// <summary>Pages the measured footprints. Items are the entries themselves so a caller can read provenance.</summary>
    /// <summary>
    /// Profiles for every requested type, or false when even one has no measurement. Callers use
    /// this to decide whether the catalogue can stand in for an explicit argument.
    /// </summary>
    public bool TryProfiles(IEnumerable<string> typeNames, out ObjectFootprint[] profiles)
    {
        var resolved = new List<ObjectFootprint>();
        foreach (var typeName in typeNames.Distinct(StringComparer.Ordinal))
        {
            if (!Entries.TryGetValue(typeName, out var entry))
            {
                profiles = Array.Empty<ObjectFootprint>();
                return false;
            }
            resolved.Add(entry.ToProfile());
        }
        profiles = resolved.ToArray();
        return profiles.Length > 0;
    }

    public object List(FootprintListQuery query)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 200)
            throw new AutomationException("INVALID_ARGUMENT", "offset 必须非负；limit 为 1–200。");
        var matches = Entries.Values
            .Where(entry => string.IsNullOrWhiteSpace(query.Query)
                || entry.TypeName.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(query.Source)
                || entry.Source.Equals(query.Source, StringComparison.OrdinalIgnoreCase))
            .Where(entry => query.MinCells == null
                || Math.Max(entry.WidthCells, entry.DepthCells) >= query.MinCells)
            .Where(entry => query.MaxCells == null
                || Math.Min(entry.WidthCells, entry.DepthCells) <= query.MaxCells)
            .OrderBy(entry => entry.TypeName, StringComparer.Ordinal)
            .ToArray();
        return new
        {
            catalogHash = CatalogHash,
            sourceAlbumHash = SourceAlbumHash,
            total = matches.Length,
            offset = query.Offset,
            items = matches.Skip(query.Offset).Take(query.Limit).ToArray()
        };
    }

    public object Info() => new
    {
        schemaVersion = SchemaVersion,
        catalogHash = CatalogHash,
        builtAtUtc = BuiltAtUtc,
        sourceAlbumHash = SourceAlbumHash,
        objects = Entries.Count,
        failures = Failures.Count,
        largest = Entries.Values.OrderByDescending(e => e.WidthCells * e.DepthCells)
            .Take(5).Select(e => new { e.TypeName, e.WidthCells, e.DepthCells }).ToArray(),
        notes = Notes
    };

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    public static FootprintCatalog Load(string path, string? overridesPath = null)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) bytes = bytes[3..];
        var catalog = JsonSerializer.Deserialize<FootprintCatalog>(bytes, Json)
            ?? throw new AutomationException("INVALID_CATALOG", "占地目录为空。");
        if (catalog.SchemaVersion != CurrentSchemaVersion)
            throw new AutomationException("UNSUPPORTED_VERSION", "不支持的占地目录版本。");
        if (overridesPath != null && File.Exists(overridesPath)) catalog.ApplyOverrides(overridesPath);
        return catalog;
    }

    private void ApplyOverrides(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) bytes = bytes[3..];
        var overrides = JsonSerializer.Deserialize<FootprintOverrides>(bytes, Json)
            ?? throw new AutomationException("INVALID_CATALOG", "占地覆盖文件为空。");
        foreach (var pair in overrides.Entries)
        {
            var existing = Find(pair.Key);
            Entries[pair.Key] = new FootprintEntry(pair.Key, pair.Value.WidthCells, pair.Value.DepthCells,
                existing?.WidthPixels ?? 0, existing?.HeightPixels ?? 0, existing?.PixelsPerCell ?? 0,
                SourceDeclared, existing?.BatchMapContentHash ?? "", existing?.BatchImageHash ?? "");
        }
    }

    public void SetOverride(string typeName, double widthCells, double depthCells)
    {
        if (string.IsNullOrWhiteSpace(typeName) || widthCells <= 0 || depthCells <= 0
            || !double.IsFinite(widthCells) || !double.IsFinite(depthCells)
            || widthCells > 4096 || depthCells > 4096)
            throw new AutomationException("INVALID_ARGUMENT", "占地需要合法的 typeName 与正的宽深（最多4096格）。");
        var existing = Find(typeName);
        Entries[typeName] = new FootprintEntry(typeName, widthCells, depthCells,
            existing?.WidthPixels ?? 0, existing?.HeightPixels ?? 0, existing?.PixelsPerCell ?? 0,
            SourceDeclared, existing?.BatchMapContentHash ?? "", existing?.BatchImageHash ?? "");
    }

    /// <summary>Where the agent looks for footprints when none are given explicitly.</summary>
    public static string DefaultPath(string artifactsDirectory) =>
        Path.Combine(Path.GetFullPath(artifactsDirectory), "footprints", "footprints.json");

    public static string DefaultOverridesPath(string artifactsDirectory) =>
        Path.Combine(Path.GetFullPath(artifactsDirectory), "footprints", "footprints.overrides.json");
}

/// <summary>One measured footprint, with the render it was measured from.</summary>
public sealed record FootprintEntry(string TypeName, double WidthCells, double DepthCells,
    int WidthPixels, int HeightPixels, double PixelsPerCell, string Source,
    string BatchMapContentHash, string BatchImageHash)
{
    /// <summary>The same rectangle in the shape the geometric commands accept.</summary>
    public ObjectFootprint ToProfile() => new()
    {
        TypeName = TypeName,
        Boxes = new[]
        {
            new FootprintBox
            {
                Label = "body",
                WidthCells = WidthCells,
                DepthCells = DepthCells,
                OffsetXCells = 0,
                OffsetYCells = 0,
                BlocksMovement = true
            }
        }
    };
}

public sealed record FootprintFailure(string TypeName, string Code, string Message);

public sealed class FootprintListQuery
{
    public string? Query { get; set; }
    public string? Source { get; set; }

    /// <summary>Keep entries whose larger dimension is at least this many cells.</summary>
    public double? MinCells { get; set; }

    /// <summary>Keep entries whose smaller dimension is at most this many cells.</summary>
    public double? MaxCells { get; set; }

    public int Offset { get; set; }
    public int Limit { get; set; } = 50;
}

/// <summary>Hand corrections, kept out of the generated catalogue so a rebuild cannot erase them.</summary>
public sealed class FootprintOverrides
{
    public int SchemaVersion { get; set; } = 1;

    public SortedDictionary<string, FootprintOverride> Entries { get; set; } = new(StringComparer.Ordinal);
}

public sealed record FootprintOverride(double WidthCells, double DepthCells, string? Note = null);
