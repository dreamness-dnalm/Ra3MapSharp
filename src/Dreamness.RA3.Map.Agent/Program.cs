using System.Text;
using System.Text.Json;
using Dreamness.RA3.Map.Agent;
using Dreamness.RA3.Map.Agent.Rendering;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
string? launcher = Environment.GetEnvironmentVariable("RA3_WB_LAUNCHER");
string artifacts = Path.Combine(Environment.CurrentDirectory, "artifacts", "agent-previews");
string? requests = null;
string? catalogPath = null;
string? translationsPath = null;
string? assetCatalogPath = null;
string? buildCatalogPath = null;
string? screenshotsDirectory = null;
string? buildAlbumPath = null;
string? albumCatalogPath = null;
string? buildFootprintsPath = null;
string? footprintsCatalogPath = null;
var footprintThreshold = 12;
string? analyzeCorpusPath = null;
string? corpusPath = null;
int? corpusLimit = null;
var corpusSample = 30000;
string? artRulesPath = null;
int? albumLimit = null;
var albumOptions = new AlbumBuildCommand.Options();
var albumAll = false;
bool mcp = false;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--stdio") continue;
    if (args[i] == "--mcp") { mcp = true; continue; }
    if (args[i] == "--help")
    {
        Console.WriteLine("RA3 agent host: --stdio (JSONL) or --mcp (MCP stdio) [--launcher WbLauncher.exe] [--artifacts directory] [--object-catalog ObjectCategory.json]\n--requests file.json executes an array of commands, stopping on first failure.\nUse sessionId=$current after map.create/open. expectedRevision is always explicit.\npreview.start returns a jobId; keep stdin open and query jobs.status. EOF cancels outstanding jobs.");
        Console.WriteLine("--object-translations ObjWndTrans.json supplements the category catalogue with editor name translations. Automatic launcher discovery loads both files when present.");
        return 0;
    }
    if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing option value."); return 2; }
    switch (args[i])
    {
        case "--launcher": launcher = args[++i]; break;
        case "--artifacts": artifacts = args[++i]; break;
        case "--requests": requests = args[++i]; break;
        case "--object-catalog": catalogPath = args[++i]; break;
        case "--object-translations": translationsPath = args[++i]; break;
        case "--asset-catalog": assetCatalogPath = args[++i]; break;
        case "--build-catalog": buildCatalogPath = args[++i]; break;
        case "--screenshots": screenshotsDirectory = args[++i]; break;
        case "--build-album": buildAlbumPath = args[++i]; break;
        case "--album": albumCatalogPath = args[++i]; break;
        case "--build-footprints": buildFootprintsPath = args[++i]; break;
        case "--footprints": footprintsCatalogPath = args[++i]; break;
        case "--footprint-threshold": footprintThreshold = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--analyze-corpus": analyzeCorpusPath = args[++i]; break;
        case "--corpus": corpusPath = args[++i]; break;
        case "--corpus-limit": corpusLimit = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--corpus-sample": corpusSample = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--art-rules": artRulesPath = args[++i]; break;
        case "--album-all": albumAll = true; break;
        case "--album-limit": albumLimit = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--album-grid": albumOptions.Grid = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--album-spacing": albumOptions.Spacing = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--album-tile": albumOptions.TileCells = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--album-tile-edge": albumOptions.TileEdge = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        default: Console.Error.WriteLine("Unknown option: " + args[i]); return 2;
    }
}
Dreamness.RA3.Map.Automation.Catalog.ObjectCatalog? catalog = null;
try
{
    if (catalogPath == null && launcher != null)
    {
        var candidate = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(launcher))!, "data", "config", "ObjectCategory.json");
        if (File.Exists(candidate)) catalogPath = candidate;
        var translationCandidate = Path.Combine(Path.GetDirectoryName(candidate)!, "ObjWndTrans.json");
        if (translationsPath == null && File.Exists(translationCandidate)) translationsPath = translationCandidate;
    }
    if (translationsPath != null && catalogPath == null) throw new AutomationException("INVALID_CATALOG", "--object-translations 需要素材分类目录。");
    if (catalogPath != null) catalog = Dreamness.RA3.Map.Automation.Catalog.ObjectCatalog.Load(catalogPath, translationsPath);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
{ Console.Error.WriteLine("Cannot load object catalogue: " + ex.Message); return 2; }

if (buildCatalogPath != null)
{
    try
    {
        if (catalog == null || catalogPath == null)
        { Console.Error.WriteLine("--build-catalog needs --object-catalog ObjectCategory.json (or --launcher)."); return 2; }
        var screenshots = screenshotsDirectory;
        if (screenshots == null && launcher != null)
        {
            var candidate = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(launcher))!, "data", "objectScreenShot");
            if (Directory.Exists(candidate)) screenshots = candidate;
        }
        return CatalogBuildCommand.Run(catalog, catalogPath, screenshots, buildCatalogPath);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
    { Console.Error.WriteLine("Cannot build asset catalogue: " + ex.Message); return 2; }
}

Dreamness.RA3.Map.Automation.Catalog.AssetCatalog? assets = null;
try
{
    var assetPath = assetCatalogPath ?? Dreamness.RA3.Map.Automation.Catalog.AssetCatalog.DefaultPath(artifacts);
    if (File.Exists(assetPath)) assets = Dreamness.RA3.Map.Automation.Catalog.AssetCatalog.Load(assetPath);
    else if (assetCatalogPath != null) { Console.Error.WriteLine("Asset catalogue not found: " + assetPath); return 2; }
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
{ Console.Error.WriteLine("Cannot load asset catalogue: " + ex.Message); return 2; }

// The album and the footprints are loaded independently of the catalogue: measuring footprints
// only needs the renders, so it must work even without a catalogue.
Dreamness.RA3.Map.Automation.Catalog.AssetAlbum? album = null;
try
{
    var albumPath = albumCatalogPath ?? Dreamness.RA3.Map.Automation.Catalog.AssetAlbum.DefaultPath(artifacts);
    if (File.Exists(albumPath)) album = Dreamness.RA3.Map.Automation.Catalog.AssetAlbum.Load(albumPath);
    else if (albumCatalogPath != null) { Console.Error.WriteLine("Album not found: " + albumPath); return 2; }
    if (assets != null) assets.Album = album;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
{ Console.Error.WriteLine("Cannot load album: " + ex.Message); return 2; }

if (analyzeCorpusPath != null)
{
    if (corpusPath == null) { Console.Error.WriteLine("--analyze-corpus needs --corpus <directory>."); return 2; }
    try { return await CorpusAnalysisCommand.RunAsync(corpusPath, analyzeCorpusPath, catalog, corpusSample, corpusLimit, CancellationToken.None); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException or ArgumentException)
    { Console.Error.WriteLine("Cannot analyze corpus: " + ex.Message); return 2; }
}

if (buildFootprintsPath != null)
{
    if (album == null) { Console.Error.WriteLine("--build-footprints needs an album; run --build-album first."); return 2; }
    try { return await FootprintBuildCommand.RunAsync(album, buildFootprintsPath, CancellationToken.None, footprintThreshold); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
    { Console.Error.WriteLine("Cannot build footprints: " + ex.Message); return 2; }
}

if (buildAlbumPath != null)
{
    if (assets == null) { Console.Error.WriteLine("--build-album needs an asset catalogue; run --build-catalog first."); return 2; }
    if (launcher == null) { Console.Error.WriteLine("--build-album needs --launcher to render."); return 2; }
    try
    {
        albumOptions.All = albumAll;
        albumOptions.Limit = albumLimit;
        return await AlbumBuildCommand.RunAsync(launcher, artifacts, catalog, assets, buildAlbumPath, albumOptions, CancellationToken.None);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
    { Console.Error.WriteLine("Cannot build album: " + ex.Message); return 2; }
}

Dreamness.RA3.Map.Automation.Catalog.FootprintCatalog? footprints = null;
try
{
    var footprintsPath = footprintsCatalogPath
        ?? Dreamness.RA3.Map.Automation.Catalog.FootprintCatalog.DefaultPath(artifacts);
    if (File.Exists(footprintsPath))
    {
        footprints = Dreamness.RA3.Map.Automation.Catalog.FootprintCatalog.Load(footprintsPath,
            Dreamness.RA3.Map.Automation.Catalog.FootprintCatalog.DefaultOverridesPath(artifacts));
    }
    else if (footprintsCatalogPath != null) { Console.Error.WriteLine("Footprints not found: " + footprintsPath); return 2; }
    if (assets != null) assets.Footprints = footprints;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
{ Console.Error.WriteLine("Cannot load footprints: " + ex.Message); return 2; }

Dreamness.RA3.Map.Automation.Catalog.ArtRules? artRules = null;
try
{
    var rulesPath = artRulesPath ?? Dreamness.RA3.Map.Automation.Catalog.ArtRules.DefaultPath(artifacts);
    if (File.Exists(rulesPath)) artRules = Dreamness.RA3.Map.Automation.Catalog.ArtRules.Load(rulesPath);
    else if (artRulesPath != null) { Console.Error.WriteLine("Art rules not found: " + rulesPath); return 2; }
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or AutomationException or InvalidOperationException)
{ Console.Error.WriteLine("Cannot load art rules: " + ex.Message); return 2; }

await using var runtime = new AgentRuntime(
    launcher == null ? null : new WorldBuilderRenderer(launcher, artifacts), catalog,
    Path.Combine(artifacts, "diagnostics"), assets, footprints,
    Dreamness.RA3.Map.Automation.Catalog.FootprintCatalog.DefaultOverridesPath(artifacts), artRules);
if (mcp)
{
    if (requests != null) { Console.Error.WriteLine("--mcp cannot be combined with --requests."); return 2; }
    var server = new Dreamness.RA3.Map.Agent.Protocol.McpServer(runtime);
    string? rpcLine;
    while ((rpcLine = await Console.In.ReadLineAsync()) != null)
    {
        if (string.IsNullOrWhiteSpace(rpcLine)) continue;
        var response = await server.HandleAsync(AgentJson.WithoutBom(rpcLine));
        if (response != null) Console.WriteLine(JsonSerializer.Serialize(response, AgentJson.Options));
    }
    return 0;
}
async Task<bool> Execute(string json)
{
    CommandResult result;
    CommandRequest request = new();
    try
    {
        request = JsonSerializer.Deserialize<CommandRequest>(AgentJson.WithoutBom(json), AgentJson.Options) ?? throw new JsonException("Expected command object.");
        result = await runtime.ExecuteAsync(request);
    }
    catch (JsonException ex)
    {
        result = CommandResult.Failed(request, null, new AutomationException("INVALID_ARGUMENT", ex.Message));
    }
    Console.WriteLine(JsonSerializer.Serialize(result, AgentJson.Options));
    return result.Status == "succeeded";
}
if (requests != null)
{
    using var document = JsonDocument.Parse(AgentJson.WithoutBom(await File.ReadAllTextAsync(requests)));
    foreach (var command in document.RootElement.EnumerateArray())
        if (!await Execute(command.GetRawText())) return 1;
    return 0;
}
string? line;
while ((line = await Console.In.ReadLineAsync()) != null)
    if (!string.IsNullOrWhiteSpace(line)) await Execute(line);
return 0;
