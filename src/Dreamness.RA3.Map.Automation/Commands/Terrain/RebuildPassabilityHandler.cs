using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

internal sealed class RebuildPassabilityHandler : ICommandHandler
{
    public string Name => "terrain.rebuild_passability";
    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<RebuildArgs>(arguments);
        token.ThrowIfCancellationRequested();
        map.UpdatePassabilityMap(args.MaxSlopeDegrees, args.ExpandCardinalHalo);
        var counts = new Dictionary<string, int>();
        for (var y = 0; y < map.MapHeight; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < map.MapWidth; x++)
            {
                var flag = map.GetPassability(x, y);
                counts[flag] = counts.GetValueOrDefault(flag) + 1;
            }
        }
        return Task.FromResult<object?>(new { space = "mapGrid", counts, args.MaxSlopeDegrees, args.ExpandCardinalHalo,
            specialFlagsPreserved = true, ordinaryFlagsReplaced = true,
            notEvaluated = new[] { "water", "objectCollision", "gameMovementRules", "buildability" } });
    }

    private sealed class RebuildArgs
    {
        public float MaxSlopeDegrees { get; set; } = 45;
        public bool ExpandCardinalHalo { get; set; } = true;
    }
}
