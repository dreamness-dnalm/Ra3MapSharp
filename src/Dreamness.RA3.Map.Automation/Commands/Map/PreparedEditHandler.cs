using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Commands.Batch;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed class PreparedEditHandler : ICommandHandler
{
    private readonly CommandRegistry _registry;
    private readonly string? _catalogHash;
    public string Name { get; }
    public CommandEffect Effect => Name == "edits.apply" ? CommandEffect.Mutation : CommandEffect.Query;
    public PreparedEditHandler(string name, CommandRegistry registry, string? catalogHash)
    { Name = name; _registry = registry; _catalogHash = catalogHash; }
    public async Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<Arguments>(arguments);
        if (Name == "edits.prepare") return await session.PrepareEditsAsync(args.BaseRevision,
            new BatchArguments { Commands = args.Commands }, _registry, _catalogHash, token);
        if (Name == "edits.discard") return session.DiscardPrepared(args.PreparedPlanId);
        return await session.ApplyPreparedAsync(args.PreparedPlanId, args.PlanHash, _catalogHash, token);
    }
    private sealed class Arguments
    {
        public int? BaseRevision { get; set; }
        public List<BatchSubcommand> Commands { get; set; } = new();
        public string? PreparedPlanId { get; set; }
        public string? PlanHash { get; set; }
    }
}
