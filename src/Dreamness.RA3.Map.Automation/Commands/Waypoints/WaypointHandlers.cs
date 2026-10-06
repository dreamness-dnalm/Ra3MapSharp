using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Waypoints;

internal sealed class PlaceWaypointHandler : ICommandHandler
{
    public string Name => "waypoints.place";

    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var session = Require(context);
        var args = AutomationJson.Deserialize<PlaceWaypointArgs>(arguments);
        Coordinates.EnsureFinite("x", args.X);
        Coordinates.EnsureFinite("y", args.Y);
        Coordinates.EnsureFinite("z", args.Z);
        Coordinates.ToWorld(
            session.Facade,
            args.Space,
            args.X,
            args.Y,
            args.Anchor,
            out var worldX,
            out var worldY);

        var waypoint = string.IsNullOrWhiteSpace(args.Name)
            ? session.Facade.AddWaypoint(worldX, worldY, args.Z)
            : session.Facade.AddWaypoint(args.Name, worldX, worldY, args.Z);
        var objectId = session.Handles.RegisterNewWaypoint();
        return Task.FromResult<object?>(ToData(objectId, waypoint));
    }

    internal static WaypointData ToData(string objectId, WaypointWrap waypoint)
    {
        return new WaypointData
        {
            ObjectId = objectId,
            Name = waypoint.WaypointName,
            UniqueId = waypoint.UniqueId,
            X = waypoint.Position.X,
            Y = waypoint.Position.Y,
            Z = waypoint.Position.Z
        };
    }

    private static MapSession Require(CommandContext? context)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "路径点命令需要已打开的工作区。");
        }

        return context.Session;
    }
}

internal sealed class MoveWaypointHandler : ICommandHandler
{
    public string Name => "waypoints.move";

    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "waypoints.move 需要已打开的工作区。");
        }

        var args = AutomationJson.Deserialize<MoveWaypointArgs>(arguments);
        if (string.IsNullOrWhiteSpace(args.ObjectId))
        {
            throw new AutomationException("INVALID_ARGUMENT", "objectId 不能为空。");
        }

        Coordinates.EnsureFinite("x", args.X);
        Coordinates.EnsureFinite("y", args.Y);
        Coordinates.EnsureFinite("z", args.Z);
        Coordinates.ToWorld(
            context.Session.Facade,
            args.Space,
            args.X,
            args.Y,
            args.Anchor,
            out var worldX,
            out var worldY);

        var waypoint = context.Session.Handles.Resolve(context.Session.Facade, args.ObjectId);
        waypoint.Position = new Dreamness.Ra3.Map.Parser.Util.Vec3D(worldX, worldY, args.Z);
        return Task.FromResult<object?>(PlaceWaypointHandler.ToData(args.ObjectId, waypoint));
    }
}

internal sealed class DeleteWaypointHandler : ICommandHandler
{
    public string Name => "waypoints.delete";

    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "waypoints.delete 需要已打开的工作区。");
        }

        var args = AutomationJson.Deserialize<DeleteWaypointArgs>(arguments);
        if (string.IsNullOrWhiteSpace(args.ObjectId))
        {
            throw new AutomationException("INVALID_ARGUMENT", "objectId 不能为空。");
        }

        var waypoint = context.Session.Handles.Resolve(context.Session.Facade, args.ObjectId);
        context.Session.Facade.Remove(waypoint);
        context.Session.Handles.Remove(args.ObjectId);
        return Task.FromResult<object?>(new { objectId = args.ObjectId, deleted = true });
    }
}

internal sealed class PlaceWaypointArgs
{
    public string? Name { get; set; }

    public string Space { get; set; } = Coordinates.PlayableGrid;

    public string Anchor { get; set; } = "center";

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }
}

internal sealed class MoveWaypointArgs
{
    public string ObjectId { get; set; } = "";

    public string Space { get; set; } = Coordinates.PlayableGrid;

    public string Anchor { get; set; } = "center";

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }
}

internal sealed class DeleteWaypointArgs
{
    public string ObjectId { get; set; } = "";
}
