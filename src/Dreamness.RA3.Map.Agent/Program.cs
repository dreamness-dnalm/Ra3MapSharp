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
await using var runtime = new AgentRuntime(launcher == null ? null : new WorldBuilderRenderer(launcher, artifacts), catalog, Path.Combine(artifacts, "diagnostics"));
if (mcp)
{
    if (requests != null) { Console.Error.WriteLine("--mcp cannot be combined with --requests."); return 2; }
    var server = new Dreamness.RA3.Map.Agent.Protocol.McpServer(runtime);
    string? rpcLine;
    while ((rpcLine = await Console.In.ReadLineAsync()) != null)
    {
        if (string.IsNullOrWhiteSpace(rpcLine)) continue;
        var response = await server.HandleAsync(rpcLine);
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
        request = JsonSerializer.Deserialize<CommandRequest>(json, AgentJson.Options) ?? throw new JsonException("Expected command object.");
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
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(requests));
    foreach (var command in document.RootElement.EnumerateArray())
        if (!await Execute(command.GetRawText())) return 1;
    return 0;
}
string? line;
while ((line = await Console.In.ReadLineAsync()) != null)
    if (!string.IsNullOrWhiteSpace(line)) await Execute(line);
return 0;
