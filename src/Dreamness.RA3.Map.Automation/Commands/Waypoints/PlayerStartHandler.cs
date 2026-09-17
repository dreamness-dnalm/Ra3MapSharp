using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Waypoints;

internal sealed class PlayerStartHandler : ICommandHandler
{
    public string Name { get; }
    public PlayerStartHandler(string name) => Name = name;
    public CommandEffect Effect => Name == "starts.list" ? CommandEffect.Query : CommandEffect.Mutation;
    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var map = session.Facade;
        var waypoints = map.GetWaypoints();
        if (Name == "starts.list")
        {
            var items = waypoints.Select((w, i) => (waypoint: w, id: session.Handles.WaypointObjectIds[i]))
                .Where(p => p.waypoint.WaypointName.StartsWith("Player_", StringComparison.OrdinalIgnoreCase)
                    && p.waypoint.WaypointName.EndsWith("_Start", StringComparison.OrdinalIgnoreCase))
                .Select(p => new { playerSlot = Slot(p.waypoint.WaypointName), waypoint = PlaceWaypointHandler.ToData(p.id, p.waypoint) }).ToArray();
            return Task.FromResult<object?>(new { items, total = items.Length, invalidNames = items.Count(i => i.playerSlot == null),
                duplicateSlots = items.Where(i => i.playerSlot.HasValue).GroupBy(i => i.playerSlot).Where(g => g.Count() > 1).Select(g => g.Key).ToArray(),
                notEvaluated = new[] { "spawnClearance", "gameInitialization", "playability" } });
        }
        var args = AutomationJson.Deserialize<StartArguments>(arguments);
        if (args.PlayerSlot is < 1 or > 6 || !args.X.HasValue || !args.Y.HasValue)
            throw new AutomationException("INVALID_ARGUMENT", "playerSlot 为1–6，x/y 必填。");
        Coordinates.EnsureFinite("z", args.Z);
        Coordinates.ToWorld(map, args.Space, args.X.Value, args.Y.Value, args.Anchor, out var x, out var y);
        if (x < 0 || y < 0 || x >= map.MapPlayableWidth * 10f || y >= map.MapPlayableHeight * 10f)
            throw new AutomationException("INVALID_ARGUMENT", "出生点必须位于可玩区域。");
        var name = $"Player_{args.PlayerSlot}_Start";
        if (waypoints.Any(w => string.Equals(w.WaypointName, name, StringComparison.OrdinalIgnoreCase)))
            throw new AutomationException("START_EXISTS", "该玩家已有出生点；先查询句柄，再移动或显式删除。");
        if (waypoints.Any(w => Slot(w.WaypointName).HasValue && Math.Abs(w.Position.X - x) < .001 && Math.Abs(w.Position.Y - y) < .001))
            throw new AutomationException("START_POSITION_CONFLICT", "出生点不能与另一个玩家的出生点中心重合。");
        var waypoint = map.AddPlayerStartWaypoint(args.PlayerSlot, x, y, args.Z);
        var id = session.Handles.RegisterNewWaypoint();
        return Task.FromResult<object?>(new { playerSlot = args.PlayerSlot, waypoint = PlaceWaypointHandler.ToData(id, waypoint),
            notEvaluated = new[] { "spawnClearance", "gameInitialization", "playability" } });
    }
    internal static int? Slot(string name)
    {
        for (var slot = 1; slot <= 6; slot++) if (name == $"Player_{slot}_Start") return slot;
        return null;
    }
    private sealed class StartArguments
    {
        public int PlayerSlot { get; set; }
        public float? X { get; set; }
        public float? Y { get; set; }
        public float Z { get; set; }
        public string Space { get; set; } = "playableGrid";
        public string Anchor { get; set; } = "center";
    }
}
