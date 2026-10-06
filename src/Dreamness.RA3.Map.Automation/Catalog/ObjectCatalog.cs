using System.Text.Json;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>Immutable name evidence from an editor category file, not gameplay validation.</summary>
public sealed class ObjectCatalog
{
    private readonly Dictionary<string, ObjectCatalogEntry> _entries;
    public string SourcePath { get; }
    public string ContentHash { get; }
    public int Count => _entries.Count;

    /// <summary>Every declared entry. Used to freeze a complete catalogue rather than a page of it.</summary>
    public IReadOnlyCollection<ObjectCatalogEntry> Entries => _entries.Values;

    public IReadOnlyList<ObjectCatalogSource> Sources { get; }

    private ObjectCatalog(string path, string hash, Dictionary<string, ObjectCatalogEntry> entries, IReadOnlyList<ObjectCatalogSource> sources)
    { SourcePath = path; ContentHash = hash; _entries = entries; Sources = sources; }

    public static ObjectCatalog Load(string path, string? translationsPath = null)
    {
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes.AsMemory().Span.StartsWith(new byte[] { 239, 187, 191 })
            ? bytes.AsMemory(3) : bytes.AsMemory());
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new AutomationException("INVALID_CATALOG", "素材分类文件必须是数组。");
        var entries = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var category in document.RootElement.EnumerateArray())
        {
            if (!category.TryGetProperty("subObjects", out var objects) || objects.ValueKind != JsonValueKind.Array)
                throw new AutomationException("INVALID_CATALOG", "素材分类缺少 subObjects 数组。");
            var labels = new[] { "englishName", "chineseName" }.Where(key => category.TryGetProperty(key, out _))
                .Select(key => category.GetProperty(key).GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>().ToArray();
            foreach (var item in objects.EnumerateArray())
            {
                var name = item.GetString();
                if (string.IsNullOrWhiteSpace(name)) throw new AutomationException("INVALID_CATALOG", "资源名不能为空。");
                if (!entries.TryGetValue(name, out var groups)) entries.Add(name, groups = new HashSet<string>(StringComparer.Ordinal));
                groups.UnionWith(labels);
            }
        }
        var sources = new List<ObjectCatalogSource> { new(Path.GetFullPath(path), ContentHasher.HashBytes(bytes), "editor-category") };
        if (translationsPath != null)
        {
            var translationBytes = File.ReadAllBytes(translationsPath);
            using var translations = JsonDocument.Parse(translationBytes.AsMemory().Span.StartsWith(new byte[] { 239, 187, 191 })
                ? translationBytes.AsMemory(3) : translationBytes.AsMemory());
            if (translations.RootElement.ValueKind != JsonValueKind.Object)
                throw new AutomationException("INVALID_CATALOG", "对象翻译表必须是名称到译名的对象。");
            foreach (var pair in translations.RootElement.EnumerateObject())
            {
                if (pair.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pair.Name))
                    throw new AutomationException("INVALID_CATALOG", "对象翻译条目必须为非空名称和字符串译名。");
                if (!entries.TryGetValue(pair.Name, out var groups)) entries[pair.Name] = groups = new HashSet<string>(StringComparer.Ordinal);
                groups.Add("editor-translation");
                if (!string.IsNullOrWhiteSpace(pair.Value.GetString())) groups.Add(pair.Value.GetString()!);
            }
            sources.Add(new ObjectCatalogSource(Path.GetFullPath(translationsPath), ContentHasher.HashBytes(translationBytes), "editor-translation"));
        }
        var hash = sources.Count == 1 ? sources[0].ContentHash : ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(
            sources.Select(source => new { source.Kind, source.ContentHash }), AutomationJson.Options));
        return new ObjectCatalog(Path.GetFullPath(path), hash, entries.ToDictionary(
            p => p.Key, p => new ObjectCatalogEntry(p.Key, Array.AsReadOnly(p.Value.OrderBy(s => s, StringComparer.Ordinal).ToArray())), StringComparer.Ordinal), sources.AsReadOnly());
    }

    public bool Contains(string name) => _entries.ContainsKey(name);

    public object Search(string? query = null, int offset = 0, int limit = 50)
    {
        if (offset < 0 || limit < 1 || limit > 200)
            throw new AutomationException("INVALID_ARGUMENT", "offset 必须非负，limit 必须为 1–200。");
        var matches = _entries.Values.Where(e => string.IsNullOrWhiteSpace(query)
            || e.TypeName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || e.Categories.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.TypeName, StringComparer.Ordinal).ToArray();
        return new { sourcePath = SourcePath, sources = Sources, catalogHash = ContentHash, validation = "editor-declared",
            total = matches.Length, offset, items = matches.Skip(offset).Take(limit).ToArray() };
    }

    public void Require(string name, string? expectedHash)
    {
        if (expectedHash != null && expectedHash != ContentHash)
            throw new AutomationException("CATALOG_CONFLICT", "素材目录版本已变更，请重新检索。");
        if (!Contains(name)) throw new AutomationException("ASSET_NOT_FOUND", $"当前目录未声明资源: {name}");
    }
}

public sealed record ObjectCatalogEntry(string TypeName, IReadOnlyList<string> Categories);
public sealed record ObjectCatalogSource(string Path, string ContentHash, string Kind);
