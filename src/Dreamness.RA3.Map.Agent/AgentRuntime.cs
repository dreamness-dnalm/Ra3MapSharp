using System.Collections.Concurrent;
using System.Text.Json;
using Dreamness.RA3.Map.Agent.Rendering;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Agent;

/// <summary>Shared agent entry point for JSONL and future MCP adapters.</summary>
public sealed class AgentRuntime : IAsyncDisposable
{
    private readonly CommandRegistry _registry;
    private readonly ObjectCatalog? _catalog;
    private readonly AssetCatalog? _assets;
    private readonly FootprintCatalog? _footprints;
    private readonly string? _footprintOverridesPath;
    private readonly CommandExecutor _executor;
    private readonly WorldBuilderRenderer? _renderer;
    private readonly ConcurrentDictionary<string, RenderJob> _jobs = new();
    private readonly HashSet<string> _ownedSessions = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly Dictionary<string, (string Fingerprint, CommandResult Result)> _previewRequests = new();
    private string? _currentSession;
    private readonly string _diagnosticsRoot;

    public AgentRuntime(WorldBuilderRenderer? renderer = null, ObjectCatalog? catalog = null,
        string? diagnosticsRoot = null, AssetCatalog? assets = null,
        FootprintCatalog? footprints = null, string? footprintOverridesPath = null)
    {
        _diagnosticsRoot = diagnosticsRoot ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "agent-diagnostics");
        _catalog = catalog;
        _assets = assets;
        _footprints = footprints;
        _footprintOverridesPath = footprintOverridesPath;
        _registry = CommandRegistry.CreateDefault(catalog, footprints);
        _renderer = renderer;
        _executor = new CommandExecutor(_registry);
    }

    // Commands are dispatched serially; render jobs operate on detached snapshots.
    public async Task<CommandResult> ExecuteAsync(CommandRequest request, CancellationToken token = default)
    {
        if (request.SessionId == "$current") request.SessionId = _currentSession;
        try
        {
            if (string.IsNullOrWhiteSpace(request.RequestId))
                throw new AutomationException("INVALID_ARGUMENT", "requestId 不能为空。");
            if (request.CommandVersion != 1)
                throw new AutomationException("UNSUPPORTED_VERSION", "仅支持 commandVersion=1。");
            if (request.Command == "system.capabilities")
                return Success(request, new
                {
                    protocol = "ra3-agent-jsonl-v1", transports = new[] { "jsonl", "mcp-stdio-2025-06-18" }, commands = _registry.Describe(),
                    hostCommands = new[] { "system.capabilities", "system.schema", "assets.objects", "assets.catalog_info", "assets.search", "assets.album", "footprints.get", "footprints.list", "footprints.set", "map.inspect_file", "map.close", "preview.start", "preview.inspect", "diagnostics.render", "jobs.status", "jobs.cancel" },
                    objectCatalog = _catalog == null ? null : new { _catalog.SourcePath, _catalog.ContentHash, _catalog.Count, _catalog.Sources },
                    footprints = _footprints == null ? null : new
                    {
                        _footprints.CatalogHash, measured = _footprints.Entries.Count,
                        failures = _footprints.Failures.Count, _footprints.SourceAlbumHash
                    },
                    assetCatalog = _assets == null ? null : new
                    {
                        _assets.CatalogHash, _assets.BuiltAtUtc,
                        textures = _assets.Textures.Count, objects = _assets.Objects.Count,
                        categories = _assets.Categories.Count, editorScreenshots = _assets.Thumbnails.Files,
                        _assets.Notes
                    },
                    batch = new { command = "batch.execute", maxCommands = 100, mutationsOnly = true },
                    renderer = _renderer?.GetAvailability(),
                    limitations = new[] { "MCP 当前提供本地 stdio 入口；未接入地编伴侣 HTTP。", "预览只检查渲染成功，不能证明游戏可玩。", "新设计功能将增量加入，命令清单以这里为准。" }
                });
            if (request.Command == "system.schema")
            {
                var args = Arguments<SchemaArguments>(request);
                if (args.Command == null) return Success(request, Protocol.CommandSchemas.Definitions);
                if (!Protocol.CommandSchemas.Definitions.TryGetValue(args.Command, out var schema))
                    throw new AutomationException("UNKNOWN_COMMAND", "没有该命令的 schema。");
                return Success(request, schema);
            }
            if (request.Command == "assets.objects")
            {
                if (_catalog == null) throw new AutomationException("CATALOG_UNAVAILABLE", "使用 --object-catalog 或 --launcher 加载素材分类文件。");
                var args = Arguments<CatalogArguments>(request);
                return Success(request, _catalog.Search(args.Query, args.Offset, args.Limit));
            }
            if (request.Command == "assets.catalog_info")
            {
                if (_assets == null) throw MissingCatalog();
                return Success(request, _assets.Info());
            }
            if (request.Command == "assets.search")
            {
                if (_assets == null) throw MissingCatalog();
                return Success(request, _assets.Search(Arguments<AssetSearchQuery>(request)));
            }
            if (request.Command is "footprints.get" or "footprints.list" or "footprints.set")
            {
                if (_footprints == null) throw new AutomationException("FOOTPRINT_UNAVAILABLE",
                    "未加载占地目录。用 --build-footprints 生成后宿主会自动加载，或用 --footprints 指定。");
                if (request.Command == "footprints.get")
                {
                    var get = Arguments<FootprintGetArguments>(request);
                    if (string.IsNullOrWhiteSpace(get.TypeName))
                        throw new AutomationException("INVALID_ARGUMENT", "typeName 必填。");
                    var entry = _footprints.Find(get.TypeName!)
                        ?? throw new AutomationException("FOOTPRINT_MISSING", "没有该物体的占地测量: " + get.TypeName);
                    return Success(request, entry);
                }
                if (request.Command == "footprints.list")
                    return Success(request, _footprints.List(Arguments<FootprintListQuery>(request)));
                var set = Arguments<FootprintSetArguments>(request);
                _footprints.SetOverride(set.TypeName ?? "", set.WidthCells, set.DepthCells);
                PersistFootprintOverrides();
                return Success(request, _footprints.Find(set.TypeName!)!);
            }
            if (request.Command == "assets.album")
            {
                if (_assets?.Album == null) throw new AutomationException("ALBUM_UNAVAILABLE",
                    "未加载物体图册。用 --build-album 生成到 <artifacts>/album 后宿主会自动加载，或用 --album 指定。");
                var args = Arguments<AlbumArguments>(request);
                if (string.IsNullOrWhiteSpace(args.TypeName))
                    throw new AutomationException("INVALID_ARGUMENT", "typeName 必填。");
                var entry = _assets.FindAlbumEntry(args.TypeName!)
                    ?? throw new AutomationException("ASSET_NOT_FOUND", "图册中没有该物体的渲染图: " + args.TypeName);
                var album = _assets.Album;
                return Success(request, new Rendering.AlbumImage(album.TilePath(entry.File), entry.ImageHash,
                    entry.TypeName, album.AlbumHash, entry.GridX, entry.GridY, entry.Batch, entry.Bytes));
            }
            if (request.Command == "map.inspect_file")
            {
                var args = Arguments<FileInspectionArguments>(request);
                return Success(request, await Inspection.MapFileInspection.ReadAsync(args.Path, Path.Combine(_diagnosticsRoot, "inspections"),
                    args.TypeNames, args.Offset, args.Limit, args.IncludeProperties, token));
            }
            if (request.Command is "jobs.status" or "jobs.cancel")
            {
                var args = Arguments<JobArguments>(request);
                if (args.JobId == null || !_jobs.TryGetValue(args.JobId, out var job))
                    throw new AutomationException("JOB_NOT_FOUND", "渲染任务不存在。");
                if (request.Command == "jobs.cancel") job.Cancel();
                return Success(request, job.Status());
            }
            if (request.Command == "preview.inspect")
            {
                var args = Arguments<InspectArguments>(request);
                if (args.JobId == null || !_jobs.TryGetValue(args.JobId, out var job))
                    throw new AutomationException("JOB_NOT_FOUND", "渲染任务不存在。");
                return Success(request, await PreviewInspection.CreateAsync(job.Overview(), args.Crop, args.MaxEdge, token));
            }
            if (request.Command == "diagnostics.render")
            {
                var args = Arguments<DiagnosticArguments>(request);
                var snapshot = await CaptureViewAsync(request, args.RequiredRevision, args.PreparedPlanId, args.PlanHash, token);
                return Success(request, await DiagnosticRenderer.RenderAsync(snapshot, _diagnosticsRoot, args.Layer, args.Profile, args.MaxEdge, token, args.Footprints, args.Routes));
            }
            if (request.Command == "map.close")
            {
                var session = RequireSession(request);
                await session.CloseAsync(cancellationToken: token);
                _ownedSessions.Remove(session.SessionId);
                if (_currentSession == session.SessionId) _currentSession = null;
                return Success(request, new { closed = true });
            }
            if (request.Command == "preview.start")
            {
                var fingerprint = request.SessionId + "|" + (request.Arguments.ValueKind == JsonValueKind.Undefined ? "{}" : request.Arguments.GetRawText());
                if (_previewRequests.TryGetValue(request.RequestId, out var receipt))
                {
                    if (receipt.Fingerprint != fingerprint)
                        throw new AutomationException("REQUEST_ID_CONFLICT", "requestId 已用于不同的预览参数。");
                    return receipt.Result;
                }
                if (_renderer == null) throw new AutomationException("RENDER_NOT_CONFIGURED", "宿主未配置 --launcher。");
                var args = Arguments<PreviewArguments>(request);
                var snapshot = await CaptureViewAsync(request, args.RequiredRevision, args.PreparedPlanId, args.PlanHash, token);
                var job = new RenderJob(snapshot.Revision, snapshot.ContentHash);
                _jobs[job.Id] = job;
                job.Completion = RunRenderAsync(job, snapshot);
                var result = Success(request, job.Status());
                result.RevisionBefore = result.RevisionAfter = snapshot.Revision;
                _previewRequests[request.RequestId] = (fingerprint, result);
                return result;
            }
            var executed = await _executor.ExecuteAsync(request, token);
            if (executed.Data is SessionOpenedData opened)
            {
                _currentSession = opened.SessionId;
                _ownedSessions.Add(opened.SessionId);
            }
            return executed;
        }
        catch (AutomationException ex) { return CommandResult.Failed(request, request.SessionId, ex); }
        catch (OperationCanceledException)
        { return CommandResult.Failed(request, request.SessionId, new AutomationException("CANCELLED", "操作取消。", true)); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        { return CommandResult.Failed(request, request.SessionId, new AutomationException("INVALID_ARGUMENT", ex.Message)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return CommandResult.Failed(request, request.SessionId, new AutomationException("IO_ERROR", ex.Message, true)); }
    }

    private async Task RunRenderAsync(RenderJob job, MapSnapshot snapshot)
    {
        var entered = false;
        try
        {
            await _renderGate.WaitAsync(job.Cancellation.Token);
            entered = true;
            job.Set("running");
            var result = await _renderer!.RenderAsync(snapshot, job.Cancellation.Token);
            job.Set("succeeded", result);
        }
        catch (OperationCanceledException) { job.Set("cancelled"); }
        catch (AutomationException ex) { job.Set("failed", error: new CommandError { Code = ex.Code, Message = ex.Message, Details = ex.Details, Retryable = ex.Retryable }); }
        catch (Exception ex) { job.Set("failed", error: new CommandError { Code = "RENDER_ERROR", Message = ex.Message }); }
        finally { if (entered) _renderGate.Release(); }
    }

    /// <summary>Hand corrections live outside the generated catalogue so a rebuild cannot erase them.</summary>
    private void PersistFootprintOverrides()
    {
        if (_footprintOverridesPath == null)
            throw new AutomationException("INVALID_ARGUMENT", "宿主未配置占地覆盖文件，无法持久化。");
        var overrides = new FootprintOverrides();
        foreach (var entry in _footprints!.Entries.Values
            .Where(e => e.Source == FootprintCatalog.SourceDeclared))
            overrides.Entries[entry.TypeName] = new FootprintOverride(entry.WidthCells, entry.DepthCells);
        var directory = Path.GetDirectoryName(_footprintOverridesPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = _footprintOverridesPath + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(overrides, AgentJson.Options));
        File.Move(temporary, _footprintOverridesPath, overwrite: true);
    }

    private static AutomationException MissingCatalog() => new("CATALOG_UNAVAILABLE",
        "未加载素材目录。先运行 --build-catalog 生成 catalog.json（Agent 会按 --artifacts 下的 catalog/catalog.json 自动加载），或用 --asset-catalog 指定。");

    private static T Arguments<T>(CommandRequest request) where T : new() =>
        request.Arguments.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? new T() : request.Arguments.Deserialize<T>(AgentJson.Options) ?? throw new JsonException("参数不能为 null。");

    private sealed class CatalogArguments
    {
        public string? Query { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; } = 50;
    }
    private sealed class SchemaArguments { public string? Command { get; set; } }
    private sealed class AlbumArguments { public string? TypeName { get; set; } }
    private sealed class FootprintGetArguments { public string? TypeName { get; set; } }
    private sealed class FootprintSetArguments
    {
        public string? TypeName { get; set; }
        public double WidthCells { get; set; }
        public double DepthCells { get; set; }
    }
    private sealed class FileInspectionArguments
    {
        public string Path { get; set; } = "";
        public string[]? TypeNames { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; } = 50;
        public bool IncludeProperties { get; set; }
    }

    private static MapSession RequireSession(CommandRequest request)
    {
        if (request.SessionId == null || !MapSessionManager.TryGetBySessionId(request.SessionId, out var session))
            throw new AutomationException("SESSION_NOT_FOUND", "需要有效 sessionId 或先打开地图再使用 $current。");
        return session;
    }

    private static async Task<MapSnapshot> CaptureViewAsync(CommandRequest request, int? revision, string? planId, string? hash, CancellationToken token)
    {
        var session = RequireSession(request);
        if (planId == null && hash == null) return await session.CaptureSnapshotAsync(revision, token);
        if (planId == null || hash == null) throw new AutomationException("INVALID_ARGUMENT", "候选预览需要 preparedPlanId 和 planHash。");
        var snapshot = await session.CapturePreparedSnapshotAsync(planId, hash, token);
        if (revision.HasValue && revision != snapshot.Revision) throw new AutomationException("REVISION_CONFLICT", "requiredRevision 与候选来源不一致。");
        return snapshot;
    }

    private static CommandResult Success(CommandRequest request, object data) =>
        CommandResult.Succeeded(request, request.SessionId, null, null, data);

    public async ValueTask DisposeAsync()
    {
        foreach (var job in _jobs.Values) job.Cancel();
        await Task.WhenAll(_jobs.Values.Select(j => j.Completion));
        foreach (var id in _ownedSessions)
            if (MapSessionManager.TryGetBySessionId(id, out var session)) await session.CloseAsync();
        foreach (var job in _jobs.Values) job.Cancellation.Dispose();
        _renderGate.Dispose();
    }

    private sealed class PreviewArguments
    {
        public int? RequiredRevision { get; set; }
        public string? PreparedPlanId { get; set; }
        public string? PlanHash { get; set; }
    }
    private sealed class JobArguments { public string? JobId { get; set; } }
    private sealed class InspectArguments
    {
        public string? JobId { get; set; }
        public PixelCrop? Crop { get; set; }
        public int MaxEdge { get; set; } = 1024;
    }
    private sealed class DiagnosticArguments
    {
        public string Layer { get; set; } = "height";
        public int? RequiredRevision { get; set; }
        public int MaxEdge { get; set; } = 1024;
        public Automation.Geometry.TerrainMovementProfile? Profile { get; set; }
        public Automation.Geometry.ObjectFootprint[]? Footprints { get; set; }
        public DiagnosticRouteRequest[]? Routes { get; set; }
        public string? PreparedPlanId { get; set; }
        public string? PlanHash { get; set; }
    }
    private sealed class RenderJob
    {
        private readonly object _gate = new();
        private string _state = "queued";
        private OverviewResult? _result;
        private CommandError? _error;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public int Revision { get; }
        public string ContentHash { get; }
        public CancellationTokenSource Cancellation { get; } = new();
        public Task Completion { get; set; } = Task.CompletedTask;
        public RenderJob(int revision, string hash) { Revision = revision; ContentHash = hash; }
        public void Cancel() { lock (_gate) { if (_state is "queued" or "running") Cancellation.Cancel(); } }
        public void Set(string state, OverviewResult? result = null, CommandError? error = null)
        { lock (_gate) { _state = state; _result = result; _error = error; } }
        public OverviewResult Overview()
        {
            lock (_gate) return _state == "succeeded" && _result != null ? _result
                : throw new AutomationException("PREVIEW_NOT_READY", "只有成功的渲染任务可提供图像。");
        }
        public object Status()
        {
            lock (_gate) return new { jobId = Id, state = _state, revision = Revision, contentHash = ContentHash, result = _result, error = _error };
        }
    }
}
