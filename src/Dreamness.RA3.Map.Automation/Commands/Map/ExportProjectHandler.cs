using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed class ExportProjectHandler : ICommandHandler
{
    public string Name { get; }
    public ExportProjectHandler(string name) => Name = name;
    public CommandEffect Effect => CommandEffect.Export;
    public async Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<OpenMapArgs>(arguments);
        return await session.ExportProjectAsync(args.ParentPath, args.MapName, Name == "map.export_project", token);
    }
}
