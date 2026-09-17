using System.Security.Cryptography;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation;

namespace Dreamness.RA3.Map.Agent.Inspection;

public static class MapFileInspection
{
    public static async Task<object> ReadAsync(string path, string directory, string[]? typeNames, int offset, int limit,
        bool includeProperties, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path) || offset < 0 || limit is < 1 or > 100 || typeNames?.Length > 32)
            throw new AutomationException("INVALID_ARGUMENT", "path 必填；offset 非负，limit 为1–100，typeNames 最多32项。");
        var sourcePath = Path.GetFullPath(path);
        var bytes = await File.ReadAllBytesAsync(sourcePath, token);
        var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var folder = Path.Combine(Path.GetFullPath(directory), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var snapshotPath = Path.Combine(folder, "scene.map");
        await File.WriteAllBytesAsync(snapshotPath, bytes, token);
        var map = Ra3MapFacade.Open(snapshotPath);
        var objects = map.GetUnitObjects().Select((obj, index) => (obj, index))
            .Where(p => typeNames == null || typeNames.Length == 0 || typeNames.Contains(p.obj.TypeName, StringComparer.Ordinal)).ToArray();
        var result = new
        {
            sourcePath, contentHash = hash, snapshotPath,
            playableWidth = map.MapPlayableWidth, playableHeight = map.MapPlayableHeight, border = map.MapBorderWidth,
            players = map.GetPlayers().Select(p => new { p.Name, p.Faction }).ToArray(),
            teams = map.GetTeams().Select(t => new { t.FullName, t.OwnerPlayerName }).ToArray(),
            starts = map.GetWaypoints().Where(w => w.WaypointName.StartsWith("Player_", StringComparison.OrdinalIgnoreCase)
                && w.WaypointName.EndsWith("_Start", StringComparison.OrdinalIgnoreCase))
                .Select(w => new { name = w.WaypointName, x = w.Position.X, y = w.Position.Y, z = w.Position.Z }).ToArray(),
            total = objects.Length, offset,
            objects = objects.Skip(offset).Take(limit).Select(p => new
            {
                sourceIndex = p.index, typeName = p.obj.TypeName, uniqueId = p.obj.UniqueId, name = p.obj.ObjName,
                originalOwner = p.obj.BelongToTeam, x = p.obj.Position.X, y = p.obj.Position.Y, z = p.obj.Position.Z,
                angleRadians = Automation.Geometry.MapAngles.ToRadians(p.obj.Angle), angleDegrees = p.obj.Angle,
                properties = includeProperties ? p.obj.Properties.PropertiesDict.ToDictionary(v => v.Key, v => v.Value.Value) : null,
                propertyTypes = includeProperties ? p.obj.Properties.PropertiesDict.ToDictionary(v => v.Key, v => v.Value.propertyType.ToString()) : null
            }).ToArray(),
            validation = "file-observed", notEvaluated = new[] { "sourceAuthenticity", "gameplay", "currentModCompatibility" }
        };
        await File.WriteAllTextAsync(Path.Combine(folder, "inspection.json"), System.Text.Json.JsonSerializer.Serialize(result, AgentJson.Options), token);
        return result;
    }
}
