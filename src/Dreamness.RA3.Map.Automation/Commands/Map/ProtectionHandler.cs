using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed class ProtectionHandler : ICommandHandler
{
    public string Name { get; }
    public ProtectionHandler(string name) => Name = name;
    public CommandEffect Effect => Name == "protections.list" ? CommandEffect.Query : CommandEffect.Mutation;
    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        if (Name == "protections.list") return Task.FromResult<object?>(new { zones = session.ProtectionZones.ToArray() });
        var zone = AutomationJson.Deserialize<ProtectionZone>(arguments);
        if (string.IsNullOrWhiteSpace(zone.Id)) throw new AutomationException("INVALID_ARGUMENT", "id 必填。");
        if (Name == "protections.add")
        {
            zone.Validate(session.Facade);
            if (session.ProtectionZones.Count >= 100 || session.ProtectionZones.Any(p => p.Id == zone.Id))
                throw new AutomationException("INVALID_ARGUMENT", "保护区 id 重复或已达到100区上限。");
            session.ProtectionZones.Add(zone);
            return Task.FromResult<object?>(zone);
        }
        if (session.ProtectionZones.RemoveAll(p => p.Id == zone.Id) == 0)
            throw new AutomationException("OBJECT_NOT_FOUND", "未找到保护区。");
        return Task.FromResult<object?>(new { id = zone.Id, deleted = true });
    }
}
