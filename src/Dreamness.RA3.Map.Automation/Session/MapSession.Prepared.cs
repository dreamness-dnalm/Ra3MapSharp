using System.Text.Json;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Commands.Batch;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed record PreparedEditInfo(string PreparedPlanId, string PlanHash, int BaseRevision,
    string InputContentHash, string InputProtectionHash, string? CatalogHash, string CandidateContentHash,
    string CandidateProtectionHash, DateTimeOffset ExpiresAt, JsonElement Commands, JsonElement Results,
    string InputDesignHash, string CandidateDesignHash, string PlanKind);

public sealed partial class MapSession
{
    private sealed record PreparedEdit(PreparedEditInfo Info, byte[] Bytes, List<string> Waypoints,
        List<string> Objects, int NextHandle, List<ProtectionZone> Zones, SortedDictionary<string, Design.DesignEntityState> Design);
    private readonly Dictionary<string, PreparedEdit> _prepared = new(StringComparer.Ordinal);

    // Called by the executor under ExecutionLock. Preparation restores all in-memory
    // editing state and never publishes a working file, history entry or revision.
    internal async Task<PreparedEditInfo> PrepareEditsAsync(int? baseRevision, BatchArguments args,
        CommandRegistry registry, string? catalogHash, CancellationToken token, Func<Task<object?>>? compileDesign = null)
    {
        if (baseRevision != Revision) throw new AutomationException("REVISION_CONFLICT", "准备方案需要匹配当前 baseRevision。");
        if (args.Commands == null || args.Commands.Count > BatchRules.MaxCommands || (args.Commands.Count < 1 && compileDesign == null))
            throw new AutomationException("INVALID_ARGUMENT", "候选需包含1–100条编辑命令。");
        foreach (var key in _prepared.Where(p => p.Value.Info.ExpiresAt <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) _prepared.Remove(key);
        if (_prepared.Count >= 32) throw new AutomationException("LIMIT_EXCEEDED", "最多保留32个候选，请先 edits.discard。");
        var previous = Facade;
        var oldWaypoints = Handles.WaypointObjectIds.ToArray();
        var oldObjects = Handles.UnitObjectIds.ToArray();
        var oldNext = Handles.NextHandle;
        var oldZones = ProtectionZones;
        var oldDesign = DesignEntities;
        var inputHash = await ContentHasher.HashFileAsync(WorkingMapFilePath, token);
        var inputProtection = ProtectionHash();
        var inputDesign = DesignHash();
        var results = new List<object?>();
        try
        {
            Facade = Ra3MapFacade.Open(WorkingMapFilePath);
            ProtectionZones = Clone(oldZones);
            DesignEntities = Clone(oldDesign);
            if (compileDesign != null) results.Add(await compileDesign());
            for (var i = 0; i < args.Commands.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var sub = args.Commands[i];
                if (sub == null || sub.CommandVersion != 1)
                    throw new AutomationException("INVALID_ARGUMENT", "候选子命令或版本无效。");
                var handler = registry.Get(sub.Command);
                if (!BatchRules.IsAllowed(handler.Effect, handler.Name))
                    throw new AutomationException("INVALID_ARGUMENT", "候选仅允许普通编辑命令: " + sub.Command);
                try { results.Add(await handler.ExecuteAsync(new CommandContext(this), sub.Arguments, token)); }
                catch (AutomationException ex)
                { throw new AutomationException(ex.Code, $"候选第 {i} 条命令失败: {ex.Message}", ex.Retryable, ex.Details, ex); }
            }
            Handles.EnsureMatches(Facade);
            var bytes = SerializeMap(true, out var normalized);
            Facade = normalized;
            foreach (var zone in oldZones.Concat(ProtectionZones)) zone.EnsureUnchanged(previous, normalized);
            var commands = JsonSerializer.SerializeToElement(args.Commands, AutomationJson.Options);
            var frozenResults = JsonSerializer.SerializeToElement(results, AutomationJson.Options);
            var candidateHash = ContentHasher.HashBytes(bytes);
            var candidateProtection = ProtectionHash();
            var candidateDesign = DesignHash();
            var planKind = compileDesign == null ? "edits" : "design";
            var hash = ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(new
            {
                MapId, SessionId, baseRevision, inputHash, inputProtection, catalogHash, candidateHash,
                candidateProtection, commands, waypoints = Handles.WaypointObjectIds, objects = Handles.UnitObjectIds,
                nextHandle = Handles.NextHandle, inputDesign, candidateDesign, planKind
            }, AutomationJson.Options));
            var info = new PreparedEditInfo(Guid.NewGuid().ToString("N"), hash, Revision, inputHash,
                inputProtection, catalogHash, candidateHash, candidateProtection, DateTimeOffset.UtcNow.AddMinutes(30), commands, frozenResults,
                inputDesign, candidateDesign, planKind);
            _prepared.Add(info.PreparedPlanId, new PreparedEdit(info, bytes, Handles.WaypointObjectIds.ToList(),
                Handles.UnitObjectIds.ToList(), Handles.NextHandle, Clone(ProtectionZones), Clone(DesignEntities)));
            return info;
        }
        finally
        {
            Facade = previous;
            Handles.Import(oldWaypoints, oldNext, oldObjects);
            ProtectionZones = oldZones;
            DesignEntities = oldDesign;
        }
    }

    private PreparedEdit RequirePrepared(string? id, string? hash)
    {
        if (id == null || !_prepared.TryGetValue(id, out var plan))
            throw new AutomationException("PLAN_NOT_FOUND", "候选不存在；候选只在当前会话存活。");
        if (plan.Info.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new AutomationException("PLAN_EXPIRED", "候选已过期，请重新准备。");
        if (hash != plan.Info.PlanHash) throw new AutomationException("PLAN_HASH_CONFLICT", "planHash 与候选不一致。");
        return plan;
    }

    internal async Task<PreparedEditInfo> ApplyPreparedAsync(string? id, string? hash, string? catalogHash, CancellationToken token, string planKind = "edits")
    {
        var plan = RequirePrepared(id, hash);
        if (plan.Info.PlanKind != planKind) throw new AutomationException("INVALID_ARGUMENT", "候选类型与应用命令不匹配。");
        if (plan.Info.BaseRevision != Revision) throw new AutomationException("PLAN_STALE", "地图修订已改变，请重新准备候选。");
        if (plan.Info.CatalogHash != catalogHash) throw new AutomationException("CATALOG_CONFLICT", "素材目录与候选来源不一致。");
        if (plan.Info.InputContentHash != await ContentHasher.HashFileAsync(WorkingMapFilePath, token)
            || plan.Info.InputProtectionHash != ProtectionHash() || plan.Info.InputDesignHash != DesignHash())
            throw new AutomationException("PLAN_STALE", "候选来源地图或保护信息发生变化。");
        var path = Path.Combine(_layout.WorkingDirectoryPath, Guid.NewGuid().ToString("N") + ".map");
        try
        {
            await File.WriteAllBytesAsync(path, plan.Bytes, token);
            Facade = Ra3MapFacade.Open(path);
            Handles.Import(plan.Waypoints, plan.NextHandle, plan.Objects);
            ProtectionZones = Clone(plan.Zones);
            DesignEntities = Clone(plan.Design);
        }
        finally { FileTransaction.TryDelete(path); }
        return plan.Info;
    }

    internal object DiscardPrepared(string? id)
    {
        if (id == null || !_prepared.Remove(id)) throw new AutomationException("PLAN_NOT_FOUND", "候选不存在。");
        return new { preparedPlanId = id, discarded = true };
    }

    public async Task<MapSnapshot> CapturePreparedSnapshotAsync(string id, string hash, CancellationToken token = default)
    {
        await ExecutionLock.WaitAsync(token);
        try
        {
            ThrowIfClosed();
            var plan = RequirePrepared(id, hash);
            return new MapSnapshot(MapId, SessionId, MapName, plan.Info.BaseRevision, plan.Info.CandidateContentHash,
                Facade.MapPlayableWidth, Facade.MapPlayableHeight, Facade.MapBorderWidth, plan.Bytes.ToArray())
            { ProtectionZones = Clone(plan.Zones).AsReadOnly(), ProtectionHash = plan.Info.CandidateProtectionHash,
                PreparedPlanId = id, PlanHash = hash, DesignHash = plan.Info.CandidateDesignHash };
        }
        finally { ExecutionLock.Release(); }
    }
}
