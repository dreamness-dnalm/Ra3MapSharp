using System.Text.Json;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>
/// Images the agent rendered itself for objects the editor ships no screenshot for.
/// <para>
/// The editor's 3041 screenshots only name 482 of the 1688 declared objects; the remaining
/// 1206 would otherwise be names with no appearance attached. This album places objects on a
/// grid of test maps and renders them through the real game renderer, so each image is what
/// the object actually looks like in game rather than an editor icon.
/// </para>
/// <para>
/// Every entry keeps the map content hash and render hash it came from, so an image can be
/// traced back to the exact render that produced it.
/// </para>
/// </summary>
public sealed class AssetAlbum
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset BuiltAtUtc { get; set; }

    /// <summary>Playable size in cells of each grid test map.</summary>
    public int GridCells { get; set; }

    /// <summary>Distance in cells between object positions.</summary>
    public int SpacingCells { get; set; }

    /// <summary>Side of the square crop, in grid cells, taken around each object.</summary>
    public int TileCells { get; set; }

    /// <summary>Overview pixel width the crops were taken from.</summary>
    public int SourceImageEdge { get; set; }

    /// <summary>Edge length of the written tile images.</summary>
    public int TileEdge { get; set; }

    public string AlbumHash { get; set; } = "";

    public List<AssetAlbumBatch> Batches { get; set; } = new();

    public SortedDictionary<string, AssetAlbumEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Objects that could not be placed. Recorded rather than silently dropped.</summary>
    public List<AssetAlbumFailure> Failures { get; set; } = new();

    private static readonly JsonSerializerOptions Json = AutomationJson.CompactOptions;

    public string ComputeHash() => ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(new
    {
        SchemaVersion,
        GridCells,
        SpacingCells,
        TileCells,
        SourceImageEdge,
        TileEdge,
        Batches,
        Entries,
        Failures
    }, Json));

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Directory holding album.json; tiles live in its "tiles" subdirectory.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RootDirectory { get; set; } = "";

    public string TilePath(string fileName) => Path.Combine(RootDirectory, "tiles", fileName);

    public static AssetAlbum Load(string path)
    {
        var full = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(full);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) bytes = bytes[3..];
        var album = JsonSerializer.Deserialize<AssetAlbum>(bytes, Json)
            ?? throw new AutomationException("INVALID_CATALOG", "物体图册为空。");
        if (album.SchemaVersion != CurrentSchemaVersion)
            throw new AutomationException("UNSUPPORTED_VERSION", "不支持的物体图册版本。");
        album.RootDirectory = Path.GetDirectoryName(full)!;
        return album;
    }

    /// <summary>Where the agent looks for a rendered album when none is given explicitly.</summary>
    public static string DefaultPath(string artifactsDirectory) =>
        Path.Combine(Path.GetFullPath(artifactsDirectory), "album", "album.json");

    public object Info() => new
    {
        schemaVersion = SchemaVersion,
        albumHash = AlbumHash,
        builtAtUtc = BuiltAtUtc,
        gridCells = GridCells,
        spacingCells = SpacingCells,
        tileCells = TileCells,
        sourceImageEdge = SourceImageEdge,
        tileEdge = TileEdge,
        images = Entries.Count,
        batches = Batches.Count,
        failures = Failures.Count
    };
}

/// <summary>One grid test map and the render taken from it.</summary>
public sealed record AssetAlbumBatch(int Index, int Objects, string MapContentHash, string ImageHash,
    string RendererConfigHash, string ImagePath);

/// <summary>One rendered object tile, with the grid position it was placed at.</summary>
public sealed record AssetAlbumEntry(string TypeName, string File, long Bytes, string ImageHash,
    int GridX, int GridY, int Batch);

public sealed record AssetAlbumFailure(string TypeName, string Code, string Message);
