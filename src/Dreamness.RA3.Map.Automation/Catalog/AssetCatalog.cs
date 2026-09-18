using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.RA3.Map.Automation.Storage;
using Dreamness.Ra3.Map.Facade.enums;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>A terrain texture with the semantics derivable from its name.</summary>
public sealed record TextureAsset(string Name, string Surface, string Theme, int? Variant, string Kind,
    bool IsTransition, bool IsStructure);

/// <summary>An editor-declared object plus whether the editor ships a screenshot for it.</summary>
public sealed record ObjectAsset(string TypeName, IReadOnlyList<string> Categories, bool HasEditorScreenshot);

/// <summary>One editor category ("树草") and the objects it groups.</summary>
public sealed record AssetCategory(string ChineseName, string EnglishName, IReadOnlyList<string> Members);

public sealed record AssetCatalogThumbnail(string Name, string FileName, long Bytes);

public sealed record AssetCatalogSource(string Path, string ContentHash, string Kind, int Count);

/// <summary>How far the editor screenshots actually cover the object catalogue.</summary>
public sealed record ThumbnailCoverage(int Files, int ObjectsWithScreenshot, int Objects, int UnmatchedFiles);

/// <summary>
/// A frozen, hash-stamped inventory of the authoring material on this machine.
/// <para>
/// The point is to give an author something to decide <em>with</em>. The raw engine surface is
/// two name lists (textures and objects) with no attributes, which is why material choice was
/// previously guesswork. This catalog adds the semantics that can be derived honestly from the
/// sources, and records for every part what it is and what it is not.
/// </para>
/// </summary>
public sealed class AssetCatalog
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Content hash of everything below, so a suggestion can name the catalogue it came from.</summary>
    public string CatalogHash { get; set; } = "";

    public DateTimeOffset BuiltAtUtc { get; set; }

    public List<AssetCatalogSource> Sources { get; set; } = new();

    public List<TextureAsset> Textures { get; set; } = new();

    public List<ObjectAsset> Objects { get; set; } = new();

    public List<AssetCategory> Categories { get; set; } = new();

    public ThumbnailCoverage Thumbnails { get; set; } = new(0, 0, 0, 0);

    /// <summary>Explicit statements about evidence quality, surfaced by assets.catalog_info.</summary>
    public List<string> Notes { get; set; } = new();

    private static readonly JsonSerializerOptions Json = AutomationJson.CompactOptions;

    public static AssetCatalog Build(ObjectCatalog objects, IReadOnlyList<AssetCategory> categories,
        ThumbnailIndex? thumbnails, DateTimeOffset builtAtUtc)
    {
        var screenshotNames = thumbnails == null
            ? new HashSet<string>(StringComparer.Ordinal) : thumbnails.Names;
        var objectNames = new HashSet<string>(objects.Entries.Select(e => e.TypeName), StringComparer.Ordinal);
        var unmatched = thumbnails == null ? 0 : thumbnails.Entries.Count(e => !objectNames.Contains(e.Name));

        var catalog = new AssetCatalog
        {
            BuiltAtUtc = builtAtUtc,
            Textures = TextureSemantics.DescribeAll(Enum.GetNames<TextureEnum>()).ToList(),
            Categories = categories.ToList(),
            Objects = objects.Entries
                .OrderBy(e => e.TypeName, StringComparer.Ordinal)
                .Select(e => new ObjectAsset(e.TypeName, e.Categories, screenshotNames.Contains(e.TypeName)))
                .ToList(),
            Thumbnails = new ThumbnailCoverage(thumbnails?.Entries.Count ?? 0,
                objects.Entries.Count(e => screenshotNames.Contains(e.TypeName)), objects.Count, unmatched)
        };

        catalog.Sources.AddRange(objects.Sources.Select(s => new AssetCatalogSource(s.Path, s.ContentHash, s.Kind, objects.Count)));
        if (thumbnails != null)
            catalog.Sources.Add(new AssetCatalogSource(thumbnails.SourcePath, thumbnails.ManifestHash,
                "editor-screenshots-manifest", thumbnails.Entries.Count));
        catalog.Sources.Add(new AssetCatalogSource("Dreamness.Ra3.Map.Facade.Enums.TextureEnum",
            ContentHasher.HashBytes(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", catalog.Textures.Select(t => t.Name)))),
            "engine-texture-enum", catalog.Textures.Count));

        catalog.Notes.Add("纹理来自引擎 TextureEnum 枚举：列出的是引擎认识的名称，不代表当前 Mod 已逐项验证贴图存在。");
        catalog.Notes.Add("物体来自编辑器 ObjectCategory.json + ObjWndTrans.json：编辑器声明，未逐项验证可放置性。");
        catalog.Notes.Add("半透明/水面等主题纹理的适用高度与水陆归属未经验证，catalog 只给名称与命名派生的语义。");
        if (thumbnails != null)
            catalog.Notes.Add("编辑器截图为按文件名匹配的清单（哈希只覆盖文件名与大小），未做图像内容校验；未匹配的截图属于单位/建筑等其他名单。");

        catalog.CatalogHash = catalog.ComputeHash();
        return catalog;
    }

    /// <summary>Hashes the content, excluding the hash field itself so the value is reproducible.</summary>
    public string ComputeHash() => ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(new
    {
        SchemaVersion,
        Sources,
        Textures,
        Objects,
        Categories,
        Thumbnails
    }, Json));

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Json));
        File.Move(temporary, path, overwrite: true);
    }

    public static AssetCatalog Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) bytes = bytes[3..];
        var catalog = JsonSerializer.Deserialize<AssetCatalog>(bytes, Json)
            ?? throw new AutomationException("INVALID_CATALOG", "素材目录为空。");
        if (catalog.SchemaVersion != CurrentSchemaVersion)
            throw new AutomationException("UNSUPPORTED_VERSION", "不支持的素材目录版本。");
        return catalog;
    }

    public object Info() => new
    {
        schemaVersion = SchemaVersion,
        catalogHash = CatalogHash,
        builtAtUtc = BuiltAtUtc,
        counts = new
        {
            textures = Textures.Count,
            objects = Objects.Count,
            categories = Categories.Count,
            editorScreenshots = Thumbnails.Files
        },
        textureSurfaces = Textures.GroupBy(t => t.Surface, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
        textureThemes = Textures.Where(t => t.Theme != TextureSemantics.UnknownTheme)
            .GroupBy(t => t.Theme, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
        objectCategories = Categories.OrderByDescending(c => c.Members.Count).ThenBy(c => c.ChineseName, StringComparer.Ordinal)
            .ToDictionary(c => c.ChineseName, c => c.Members.Count, StringComparer.Ordinal),
        thumbnails = Thumbnails,
        sources = Sources,
        notes = Notes
    };

    public object Search(AssetSearchQuery query)
    {
        if (query.Offset < 0 || query.Limit is < 1 or > 200)
            throw new AutomationException("INVALID_ARGUMENT", "offset 必须非负；limit 为 1–200。");
        var kind = string.IsNullOrWhiteSpace(query.Kind) ? "any" : query.Kind.Trim().ToLowerInvariant();
        if (kind is not ("any" or "texture" or "object"))
            throw new AutomationException("INVALID_ARGUMENT", "kind 必须是 any、texture 或 object。");
        if (kind == "any")
        {
            // A texture-only filter must not drag every object into the result: surface and
            // theme do not describe objects, so a caller setting one is asking about textures.
            var textures = query.Surface != null || query.Theme != null || query.OnlyTransition;
            var objects = query.Category != null || query.OnlyMissingScreenshot;
            kind = textures && !objects ? "texture" : objects && !textures ? "object" : "any";
        }

        var items = new List<AssetSearchItem>();
        if (kind is not "object")
        {
            items.AddRange(Textures
                .Where(t => string.IsNullOrWhiteSpace(query.Query) || t.Name.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
                .Where(t => string.IsNullOrWhiteSpace(query.Surface) || t.Surface.Equals(query.Surface, StringComparison.OrdinalIgnoreCase))
                .Where(t => string.IsNullOrWhiteSpace(query.Theme) || t.Theme.Equals(query.Theme, StringComparison.OrdinalIgnoreCase))
                .Where(t => !query.OnlyTransition || t.IsTransition)
                .Select(t => new AssetSearchItem("texture", t.Name, null, t.Surface, t.Theme, t.Variant, t.Kind,
                    t.IsTransition, t.IsStructure, false, null)));
        }
        if (kind is not "texture")
        {
            items.AddRange(Objects
                .Where(o => string.IsNullOrWhiteSpace(query.Query)
                    || o.TypeName.Contains(query.Query, StringComparison.OrdinalIgnoreCase)
                    || o.Categories.Any(c => c.Contains(query.Query, StringComparison.OrdinalIgnoreCase)))
                .Where(o => string.IsNullOrWhiteSpace(query.Category)
                    || o.Categories.Any(c => c.Equals(query.Category, StringComparison.OrdinalIgnoreCase)))
                .Where(o => !query.OnlyMissingScreenshot || !o.HasEditorScreenshot)
                .Select(o => new AssetSearchItem("object", o.TypeName, o.Categories, null, null, null, null,
                    null, null, o.HasEditorScreenshot, o.HasEditorScreenshot ? o.TypeName + ".jpg" : null)));
        }

        var ordered = items.OrderBy(i => i.Kind, StringComparer.Ordinal).ThenBy(i => i.Name, StringComparer.Ordinal).ToArray();
        return new
        {
            catalogHash = CatalogHash,
            kind,
            total = ordered.Length,
            textureMatches = ordered.Count(i => i.Kind == "texture"),
            objectMatches = ordered.Count(i => i.Kind == "object"),
            offset = query.Offset,
            items = ordered.Skip(query.Offset).Take(query.Limit).ToArray()
        };
    }

    /// <summary>Where the agent looks for a catalogue when none is given explicitly.</summary>
    public static string DefaultPath(string artifactsDirectory) =>
        Path.Combine(Path.GetFullPath(artifactsDirectory), "catalog", "catalog.json");
}

/// <summary>One search hit. A single shape keeps the contract stable across kinds.</summary>
public sealed record AssetSearchItem(string Kind, string Name, IReadOnlyList<string>? Categories,
    string? Surface, string? Theme, int? Variant, string? SurfaceKind, bool? IsTransition, bool? IsStructure,
    bool HasEditorScreenshot, string? Thumbnail);

public sealed class AssetSearchQuery
{
    [JsonPropertyName("query")] public string? Query { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("surface")] public string? Surface { get; set; }
    [JsonPropertyName("theme")] public string? Theme { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("onlyMissingScreenshot")] public bool OnlyMissingScreenshot { get; set; }
    [JsonPropertyName("onlyTransition")] public bool OnlyTransition { get; set; }
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("limit")] public int Limit { get; set; } = 50;
}

/// <summary>
/// The editor's per-object screenshots, indexed by file name.
/// <para>
/// These are <em>not</em> an illustration set for the object catalogue: measured on this
/// install, 3041 files name only 362 of the 1494 categorised objects, and the rest portrait
/// gameplay units and structures. Coverage is therefore reported rather than assumed.
/// </para>
/// </summary>
public sealed class ThumbnailIndex
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png" };

    public string SourcePath { get; }
    public string ManifestHash { get; }
    public IReadOnlyList<AssetCatalogThumbnail> Entries { get; }
    public HashSet<string> Names { get; }

    private ThumbnailIndex(string sourcePath, string manifestHash, IReadOnlyList<AssetCatalogThumbnail> entries)
    {
        SourcePath = sourcePath;
        ManifestHash = manifestHash;
        Entries = entries;
        Names = entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
    }

    public static ThumbnailIndex Scan(string directory)
    {
        if (!Directory.Exists(directory))
            throw new AutomationException("CATALOG_NOT_FOUND", "找不到编辑器截图目录: " + directory);
        var entries = Directory.EnumerateFiles(directory)
            .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(file => new FileInfo(file))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .Select(file => new AssetCatalogThumbnail(Path.GetFileNameWithoutExtension(file.Name), file.Name, file.Length))
            .ToArray();
        // Hashing 34 MB of JPEGs on every build buys little over a name/size manifest, so the
        // hash is explicitly a manifest hash and says so in its source kind.
        var manifest = ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(
            entries.Select(e => new { e.FileName, e.Bytes }), AutomationJson.CompactOptions));
        return new ThumbnailIndex(Path.GetFullPath(directory), manifest, entries);
    }

    public static IReadOnlyList<AssetCategory> ReadCategories(string objectCategoryJsonPath)
    {
        var bytes = File.ReadAllBytes(objectCategoryJsonPath);
        if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) bytes = bytes[3..];
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new AutomationException("INVALID_CATALOG", "素材分类文件必须是数组。");
        var categories = new List<AssetCategory>();
        foreach (var category in document.RootElement.EnumerateArray())
        {
            var chinese = category.TryGetProperty("chineseName", out var cn) ? cn.GetString() ?? "" : "";
            var english = category.TryGetProperty("englishName", out var en) ? en.GetString() ?? "" : "";
            var members = category.TryGetProperty("subObjects", out var subs) && subs.ValueKind == JsonValueKind.Array
                ? subs.EnumerateArray().Select(s => s.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>()
                    .OrderBy(s => s, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            if (chinese.Length + english.Length > 0) categories.Add(new AssetCategory(chinese, english, members));
        }
        return categories;
    }
}
