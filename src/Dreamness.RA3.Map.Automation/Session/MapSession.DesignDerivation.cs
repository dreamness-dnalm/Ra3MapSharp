using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.RA3.Map.Automation.Design;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    private static JsonElement ResolveRampHeights(DesignEntitySpec spec, IReadOnlyDictionary<string, DesignEntitySpec> specs)
    {
        var args = spec.Parameters.Deserialize<Dictionary<string, JsonElement>>(AutomationJson.Options)!;
        foreach (var field in new[] { "startHeight", "endHeight" })
        {
            if (!args.Remove(field + "From", out var reference)) continue;
            if (args.ContainsKey(field)) throw new AutomationException("INVALID_ARGUMENT", field + " 与 " + field + "From 不能同时指定。");
            var id = reference.GetString()!;
            // The graph has already rejected reference cycles and missing entities.
            var source = specs[id];
            while (source.Kind == "derived") source = specs[source.Parameters.GetProperty("sourceEntityId").GetString()!];
            if (source.Kind != "platform" || !source.Parameters.TryGetProperty("value", out var height)
                || height.ValueKind != JsonValueKind.Number || !height.TryGetSingle(out var value))
                throw new AutomationException("INVALID_REFERENCE", field + "From 只能引用平台或平台的对称派生实体。");
            args[field] = JsonSerializer.SerializeToElement(TerrainHeight.Normalize(value));
        }
        return JsonSerializer.SerializeToElement(args, AutomationJson.Options);
    }

    private async Task<object?> GenerateDerivedAsync(DesignEntityState entity, HashSet<string> ancestors,
        Func<string, JsonElement, Task<object?>> execute, CancellationToken token)
    {
        var spec = entity.Spec;
        if (!spec.Parameters.TryGetProperty("transform", out var transform) || transform.ValueKind != JsonValueKind.String || transform.GetString() != "rotate180")
            throw new AutomationException("INVALID_ARGUMENT", "derived.transform 当前仅支持 rotate180。");
        var sourceId = spec.Parameters.GetProperty("sourceEntityId").GetString()!;
        var source = DesignEntities[sourceId];
        if (source.Textures?.Count > 0)
        {
            if (spec.Parameters.TryGetProperty("playerSlot", out _))
                throw new AutomationException("INVALID_ARGUMENT", "纹理派生不接受 playerSlot。");
            var textureResult = GenerateTextureEntity(entity, ancestors, token);
            return new DerivedResult(sourceId, "rotate180", 0, new(), new(),
                new[] { "texturePixelRotation", "singleEdgeAndCliffBlendRotation", "wholeMapSymmetry" }) { TextureResult = textureResult };
        }
        var waypoints = source.WaypointFingerprints?.Keys.Select(id => Handles.Resolve(Facade, id)).ToArray()
            ?? Array.Empty<Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject.WaypointWrap>();
        int? playerSlot = null;
        if (waypoints.Length > 0)
        {
            if (waypoints.Length != 1 || !Commands.Waypoints.PlayerStartHandler.Slot(waypoints[0].WaypointName).HasValue
                || !spec.Parameters.TryGetProperty("playerSlot", out var slot) || slot.ValueKind != JsonValueKind.Number || !slot.TryGetInt32(out var value) || value is < 1 or > 6)
                throw new AutomationException("INVALID_ARGUMENT", "派生出生点需要单个来源出生点和新的 playerSlot（1–6）。");
            playerSlot = value;
        }
        else if (spec.Parameters.TryGetProperty("playerSlot", out _))
            throw new AutomationException("INVALID_ARGUMENT", "playerSlot 仅用于派生出生点。");
        var sourceCells = source.Heights.Select(c => (c.X, c.Y)).ToHashSet();
        var cells = source.Heights.Select(c => (X: Facade.MapWidth - 1 - c.X, Y: Facade.MapHeight - 1 - c.Y, Height: c.After)).ToArray();
        if (cells.Any(c => sourceCells.Contains((c.X, c.Y))))
            throw new AutomationException("SYMMETRY_OVERLAP", "对称地形与源实体相交；请将源实体限制在对称轴的一侧。");
        foreach (var cell in cells)
        { token.ThrowIfCancellationRequested(); Facade.SetTerrainHeight(cell.X, cell.Y, cell.Height); }
        var sourceObjects = source.ObjectFingerprints.Keys.Select(id => Handles.ResolveUnit(Facade, id)).ToArray();
        var existingObjects = Facade.GetUnitObjects().ToArray();
        var result = new List<object?>();
        foreach (var obj in sourceObjects)
        {
            token.ThrowIfCancellationRequested();
            var x = Facade.MapPlayableWidth * 10f - obj.Position.X;
            var y = Facade.MapPlayableHeight * 10f - obj.Position.Y;
            if (existingObjects.Any(other => Math.Abs(other.Position.X - x) < .001f && Math.Abs(other.Position.Y - y) < .001f))
                throw new AutomationException("SYMMETRY_OVERLAP", "对称对象中心与已有对象重合；请调整源布局或目标占用。");
            var angle = (MapAngles.ToRadians(obj.Angle) + MathF.PI) % (2 * MathF.PI);
            if (angle < 0) angle += 2 * MathF.PI;
            result.Add(await execute("objects.place", JsonSerializer.SerializeToElement(new
            { typeName = obj.TypeName, x, y, z = obj.Position.Z, angleRadians = angle, space = "world",
                ownerTeam = obj.BelongToTeam, settings = Commands.Objects.ObjectSettings.Read(obj) }, AutomationJson.Options)));
        }
        var starts = new List<object?>();
        foreach (var waypoint in waypoints)
            starts.Add(await execute("starts.place", JsonSerializer.SerializeToElement(new
            { playerSlot, x = Facade.MapPlayableWidth * 10f - waypoint.Position.X,
                y = Facade.MapPlayableHeight * 10f - waypoint.Position.Y, z = waypoint.Position.Z, space = "world" }, AutomationJson.Options)));
        return new DerivedResult(sourceId, "rotate180", cells.Length, result, starts,
            new[] { "textureSymmetry", "objectFootprints", "unsupportedObjectProperties", "gameplayBehavior", "groundAttachment", "wholeMapSymmetry" })
        { Cells = cells.Select(c => (c.X, c.Y)).ToArray() };
    }

    private sealed record DerivedResult(string SourceEntityId, string Transform, int AffectedCells, List<object?> Objects, List<object?> Starts,
        string[] NotEvaluated) : ITerrainFootprint
    {
        [JsonIgnore] public IReadOnlyCollection<(int X, int Y)> Cells { get; init; } = Array.Empty<(int, int)>();
        public object? TextureResult { get; init; }
    }
}
