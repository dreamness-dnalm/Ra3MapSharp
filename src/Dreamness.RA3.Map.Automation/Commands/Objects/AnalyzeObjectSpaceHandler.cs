using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Objects;

internal sealed class AnalyzeObjectSpaceHandler : ICommandHandler
{
    public string Name => "objects.analyze_space";
    public CommandEffect Effect => CommandEffect.Query;
    public async Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<AnalyzeObjectSpaceArgs>(arguments);
        return await session.AnalyzeObjectSpaceAsync(args, token);
    }
}

internal sealed class AnalyzeObjectSpaceArgs
{
    public ObjectFootprint[] Footprints { get; set; } = Array.Empty<ObjectFootprint>();
    public string? PreparedPlanId { get; set; }
    public string? PlanHash { get; set; }
    public int Limit { get; set; } = 100;
}
