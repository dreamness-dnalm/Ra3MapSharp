using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Commands.Batch;
using Dreamness.RA3.Map.Automation.Design;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    internal object QueryDesign(string? entityId, int offset, int limit, bool includeHeightCells)
    {
        if (offset < 0 || limit is < 1 or > 200) throw new AutomationException("INVALID_ARGUMENT", "offset 非负；limit 为1–200。");
        var all = DesignEntities.Values.Where(e => entityId == null || e.Spec.Id == entityId).ToArray();
        return new { schemaVersion = 1, designHash = DesignHash(), designDirty = DesignDirty, total = all.Length, offset,
            entities = all.Skip(offset).Take(limit).Select(e => new { spec = Clone(e.Spec), algorithmVersion = e.AlgorithmVersion,
                objectIds = e.ObjectFingerprints.Keys.ToArray(), ownedHeightCells = e.Heights.Count,
                ownedTextureCells = e.Textures?.Count ?? 0,
                waypointIds = e.WaypointFingerprints?.Keys.ToArray() ?? Array.Empty<string>(),
                heights = includeHeightCells ? e.Heights.ToArray() : null }).ToArray() };
    }

    internal Task<PreparedEditInfo> PrepareDesignAsync(int? revision, DesignPatch patch, CommandRegistry registry,
        string? catalogHash, CancellationToken token) => PrepareEditsAsync(revision, new BatchArguments(), registry,
            catalogHash, token, () => CompileDesignAsync(patch, registry, token));

    private async Task<object?> CompileDesignAsync(DesignPatch patch, CommandRegistry registry, CancellationToken token)
    {
        if (patch.SchemaVersion != 1) throw new AutomationException("UNSUPPORTED_VERSION", "仅支持 design patch schemaVersion=1。");
        if (patch.Upsert == null || patch.Remove == null || patch.Upsert.Count + patch.Remove.Count is < 1 or > 100)
            throw new AutomationException("INVALID_ARGUMENT", "每次修改1–100个设计实体。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in patch.Remove.Concat(patch.Upsert.Select(s => s?.Id)))
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id))
                throw new AutomationException("INVALID_ARGUMENT", "实体 ID 必须唯一且长度为1–128；不能同时更新与删除。");
        foreach (var spec in patch.Upsert)
            if (spec.Kind is not ("platform" or "ramp" or "texture" or "objects" or "scatter" or "derived" or "playerStart") || spec.Parameters.ValueKind != JsonValueKind.Object)
                throw new AutomationException("INVALID_ARGUMENT", "实体 kind 或 parameters 无效。");
        if (DesignEntities.Count + patch.Upsert.Count(s => !DesignEntities.ContainsKey(s.Id)) - patch.Remove.Count > 1000)
            throw new AutomationException("LIMIT_EXCEEDED", "设计最多1000个实体。");
        foreach (var id in patch.Remove)
            if (!DesignEntities.ContainsKey(id)) throw new AutomationException("ENTITY_NOT_FOUND", "未找到实体: " + id);
        var oldSpecs = DesignEntities.ToDictionary(p => p.Key, p => p.Value.Spec, StringComparer.Ordinal);
        var oldGraph = new DesignGraph(oldSpecs);
        var desired = new Dictionary<string, DesignEntitySpec>(oldSpecs, StringComparer.Ordinal);
        foreach (var id in patch.Remove) desired.Remove(id);
        foreach (var spec in patch.Upsert) desired[spec.Id] = spec;
        var graph = new DesignGraph(desired);
        var rebuild = new HashSet<string>(patch.Remove, StringComparer.Ordinal);
        foreach (var spec in patch.Upsert)
            if (!oldSpecs.TryGetValue(spec.Id, out var old) || old.Kind != spec.Kind
                || DesignEntities[spec.Id].AlgorithmVersion != DesignEntityState.CurrentAlgorithmVersion
                || CanonicalParameters(old.Parameters) != CanonicalParameters(spec.Parameters)
                || !(old.DependsOn ?? new List<string>()).ToHashSet(StringComparer.Ordinal).SetEquals(spec.DependsOn ?? new List<string>()))
                rebuild.Add(spec.Id);
        // Edges can change in the same patch. Propagate through both old and new
        // graphs until all entities affected by either layering order are included.
        while (oldGraph.IncludeDescendants(rebuild) | graph.IncludeDescendants(rebuild)) { }
        var visibleHeights = new Dictionary<(int X, int Y), float>();
        var visibleTextures = new Dictionary<(int X, int Y), TextureCellValue>();
        foreach (var id in oldGraph.Order)
        {
            foreach (var cell in DesignEntities[id].Heights) visibleHeights[(cell.X, cell.Y)] = cell.After;
            foreach (var cell in DesignEntities[id].Textures ?? new()) visibleTextures[(cell.X, cell.Y)] = cell.After;
        }
        var verify = ids.Concat(rebuild).ToHashSet(StringComparer.Ordinal);
        foreach (var id in verify.ToArray())
        {
            if (oldGraph.Ancestors.TryGetValue(id, out var oldAncestors)) verify.UnionWith(oldAncestors);
            if (graph.Ancestors.TryGetValue(id, out var ancestors)) verify.UnionWith(ancestors);
        }
        foreach (var id in verify)
            if (DesignEntities.TryGetValue(id, out var state)) VerifyEntity(state, visibleHeights, visibleTextures);
        // Unwind overlays before their foundations, then rebuild in dependency order.
        foreach (var id in oldGraph.Order.Reverse().Where(rebuild.Contains))
        {
            if (!DesignEntities.TryGetValue(id, out var previous)) continue;
            foreach (var waypointId in previous.WaypointFingerprints?.Keys.ToArray() ?? Array.Empty<string>())
            {
                Facade.Remove(Handles.Resolve(Facade, waypointId));
                Handles.Remove(waypointId);
            }
            foreach (var objectId in previous.ObjectFingerprints.Keys)
            {
                var obj = Handles.ResolveUnit(Facade, objectId);
                Facade.Remove(obj);
                Handles.RemoveUnit(objectId);
            }
            foreach (var cell in previous.Heights) Facade.SetTerrainHeight(cell.X, cell.Y, cell.Before);
            foreach (var cell in previous.Textures ?? new()) RestoreTextureCell(cell.X, cell.Y, cell.Before);
            DesignEntities.Remove(id);
        }

        var results = new List<object>();
        foreach (var id in graph.Order.Where(id => rebuild.Contains(id) || ids.Contains(id)))
        {
            var spec = desired[id];
            token.ThrowIfCancellationRequested();
            if (!rebuild.Contains(id))
            {
                DesignEntities[id].Spec = Clone(spec);
                results.Add(new { id, regenerated = false });
                continue;
            }
            var entity = new DesignEntityState { Spec = Clone(spec) };
            var oldIds = Handles.UnitObjectIds.ToHashSet(StringComparer.Ordinal);
            var oldWaypoints = Handles.WaypointObjectIds.ToHashSet(StringComparer.Ordinal);
            float[,]? heights = null;
            if (spec.Kind is "platform" or "ramp" or "derived")
            {
                heights = new float[Facade.MapWidth, Facade.MapHeight];
                for (var y = 0; y < Facade.MapHeight; y++)
                for (var x = 0; x < Facade.MapWidth; x++) heights[x, y] = Facade.GetTerrainHeight(x, y);
            }
            async Task<object?> Execute(string command, JsonElement args) =>
                await registry.Get(command).ExecuteAsync(new CommandContext(this), args, token);
            object? generated;
            if (spec.Kind == "derived") generated = await GenerateDerivedAsync(entity, graph.Ancestors[id], Execute, token);
            else if (spec.Kind == "texture") generated = GenerateTextureEntity(entity, graph.Ancestors[id], token);
            else if (spec.Kind == "playerStart") generated = await Execute("starts.place", spec.Parameters);
            else if (spec.Kind == "objects")
            {
                if (!spec.Parameters.TryGetProperty("placements", out var placements) || placements.ValueKind != JsonValueKind.Array
                    || placements.GetArrayLength() is < 1 or > 2000)
                    throw new AutomationException("INVALID_ARGUMENT", "objects 实体需要1–2000个 placements。");
                var placed = new List<object?>();
                foreach (var placement in placements.EnumerateArray())
                { token.ThrowIfCancellationRequested(); placed.Add(await Execute("objects.place", placement)); }
                generated = placed;
            }
            else if (spec.Kind == "platform")
            {
                var args = spec.Parameters.Deserialize<Dictionary<string, JsonElement>>(AutomationJson.Options)!;
                if (!args.ContainsKey("value")) throw new AutomationException("INVALID_ARGUMENT", "platform.value 必填。");
                if (args.ContainsKey("mode")) throw new AutomationException("INVALID_ARGUMENT", "platform 使用固定 set 模式，不接受 mode。");
                args["mode"] = JsonSerializer.SerializeToElement("set");
                generated = await Execute("terrain.sculpt", JsonSerializer.SerializeToElement(args));
            }
            else generated = await Execute(spec.Kind == "ramp" ? "terrain.ramp" : "objects.scatter",
                spec.Kind == "ramp" ? ResolveRampHeights(spec, desired) : spec.Parameters);
            foreach (var objectId in Handles.UnitObjectIds.Where(objectId => !oldIds.Contains(objectId))) entity.ObjectFingerprints[objectId] = ObjectFingerprint(objectId);
            var newWaypoints = Handles.WaypointObjectIds.Where(waypointId => !oldWaypoints.Contains(waypointId)).ToArray();
            if (newWaypoints.Length > 0) entity.WaypointFingerprints = newWaypoints.ToDictionary(waypointId => waypointId, WaypointFingerprint, StringComparer.Ordinal);
            if (heights != null)
            {
                var claimed = DesignEntities.Where(e => !graph.Ancestors[id].Contains(e.Key))
                    .SelectMany(e => e.Value.Heights).Select(c => (c.X, c.Y)).ToHashSet();
                var footprint = ((Geometry.ITerrainFootprint)generated!).Cells;
                foreach (var (x, y) in footprint)
                {
                    var after = Facade.GetTerrainHeight(x, y);
                    if (claimed.Contains((x, y))) throw new AutomationException("ENTITY_OVERLAP", $"实体 {spec.Id} 修改了其他实体拥有的高度格 ({x},{y})。");
                    entity.Heights.Add(new OwnedHeightCell(x, y, heights[x, y], after));
                }
            }
            DesignEntities.Add(spec.Id, entity);
            results.Add(new { id = spec.Id, regenerated = true, objectIds = entity.ObjectFingerprints.Keys.ToArray(),
                waypointIds = newWaypoints,
                ownedHeightCells = entity.Heights.Count, changedHeightCells = entity.Heights.Count(c => c.Before != c.After),
                ownedTextureCells = entity.Textures?.Count ?? 0, generated });
        }
        return new { algorithm = DesignEntityState.CurrentAlgorithmVersion, removed = patch.Remove, updated = results,
            automaticallyRebuilt = graph.Order.Where(id => rebuild.Contains(id) && !ids.Contains(id)).ToArray(),
            designHash = DesignHash(), notEvaluated = new[] { "gameplay", "objectFootprints", "automaticGroundAttachment", "semanticRoutes", "wholeMapSymmetry" } };
    }

    private void VerifyEntity(DesignEntityState entity, IReadOnlyDictionary<(int X, int Y), float> visibleHeights,
        IReadOnlyDictionary<(int X, int Y), TextureCellValue> visibleTextures)
    {
        foreach (var pair in entity.WaypointFingerprints ?? new Dictionary<string, string>())
            if (!Handles.WaypointObjectIds.Contains(pair.Key) || WaypointFingerprint(pair.Key) != pair.Value)
                throw new AutomationException("ENTITY_CONFLICT", $"实体 {entity.Spec.Id} 的路径点 {pair.Key} 已被其他编辑改变。");
        foreach (var pair in entity.ObjectFingerprints)
        {
            if (!Handles.UnitObjectIds.Contains(pair.Key) || ObjectFingerprint(pair.Key) != pair.Value)
                throw new AutomationException("ENTITY_CONFLICT", $"实体 {entity.Spec.Id} 的对象 {pair.Key} 已被其他编辑改变。");
        }
        foreach (var cell in entity.Heights)
            if (Facade.GetTerrainHeight(cell.X, cell.Y) != visibleHeights[(cell.X, cell.Y)])
                throw new AutomationException("ENTITY_CONFLICT", $"实体 {entity.Spec.Id} 的高度格 ({cell.X},{cell.Y}) 已被其他编辑改变。");
        foreach (var cell in entity.Textures ?? new())
            if (ReadTextureCell(cell.X, cell.Y) != visibleTextures[(cell.X, cell.Y)])
                throw new AutomationException("ENTITY_CONFLICT", $"实体 {entity.Spec.Id} 的纹理格 ({cell.X},{cell.Y}) 已被其他编辑改变。");
    }

    private string ObjectFingerprint(string id) => ContentHasher.HashBytes(Handles.ResolveUnit(Facade, id).Obj.ToBytes(Facade.ra3Map.Context));
    private string WaypointFingerprint(string id) => ContentHasher.HashBytes(Handles.Resolve(Facade, id).Obj.ToBytes(Facade.ra3Map.Context));

    private static string CanonicalParameters(JsonElement value)
    {
        // Ignore JSON object property order while preserving array order and numeric spelling.
        object? Normalize(JsonElement item) => item.ValueKind switch
        {
            JsonValueKind.Object => item.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToDictionary(p => p.Name, p => Normalize(p.Value)),
            JsonValueKind.Array => item.EnumerateArray().Select(Normalize).ToArray(),
            _ => item
        };
        return JsonSerializer.Serialize(Normalize(value), AutomationJson.Options);
    }
}
