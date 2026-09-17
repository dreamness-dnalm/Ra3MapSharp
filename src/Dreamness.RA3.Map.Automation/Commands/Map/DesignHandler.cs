using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Design;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed class DesignHandler : ICommandHandler
{
    public string Name { get; }
    private readonly CommandRegistry _registry;
    private readonly string? _catalogHash;
    public CommandEffect Effect => Name == "design.apply" ? CommandEffect.Mutation : CommandEffect.Query;
    public DesignHandler(string name, CommandRegistry registry, string? catalogHash)
    { Name = name; _registry = registry; _catalogHash = catalogHash; }
    public async Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<Arguments>(arguments);
        if (Name == "design.query") return session.QueryDesign(args.EntityId, args.Offset, args.Limit, args.IncludeHeightCells);
        if (Name == "design.apply") return await session.ApplyPreparedAsync(args.PreparedPlanId, args.PlanHash, _catalogHash, token, "design");
        if (args.Patch == null) throw new AutomationException("INVALID_ARGUMENT", "patch 必填。");
        return await session.PrepareDesignAsync(args.BaseRevision, args.Patch, _registry, _catalogHash, token);
    }
    private sealed class Arguments
    {
        public int? BaseRevision { get; set; }
        public DesignPatch? Patch { get; set; }
        public string? PreparedPlanId { get; set; }
        public string? PlanHash { get; set; }
        public string? EntityId { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; } = 50;
        public bool IncludeHeightCells { get; set; }
    }
}
