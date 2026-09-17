using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

internal sealed class SetHeightHandler : ICommandHandler
{
    public string Name => "terrain.set_height";

    public CommandEffect Effect => CommandEffect.Mutation;

    public Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "terrain.set_height 需要已打开的工作区。");
        }

        var args = AutomationJson.Deserialize<SetHeightArgs>(arguments);
        Coordinates.EnsureFinite("height", args.Height);
        var maxHeight = Dreamness.Ra3.Map.Parser.Util.StreamExtension.FromSageFloat16(ushort.MaxValue);
        if (args.Height < 0 || args.Height > maxHeight)
            throw new AutomationException("INVALID_ARGUMENT", $"height 必须在 0 到 {maxHeight} 之间。");
        // The format's fractional byte tops out below 10. Clamp the small gap rather
        // than letting the underlying byte conversion wrap back to zero.
        var maxFraction = Dreamness.Ra3.Map.Parser.Util.StreamExtension.FromSageFloat16(255);
        var storageHeight = Math.Min(args.Height, MathF.Floor(args.Height / 10f) * 10f + maxFraction);
        var actualHeight = Dreamness.Ra3.Map.Parser.Util.StreamExtension.FromSageFloat16(
            Dreamness.Ra3.Map.Parser.Util.StreamExtension.ToSageFloat16(storageHeight));
        if (args.Region == null)
        {
            throw new AutomationException("INVALID_ARGUMENT", "region 不能为空。");
        }

        if (!string.Equals(args.Region.Kind, "rectangle", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(args.Region.Kind))
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "当前仅支持 kind=rectangle 的区域。");
        }

        var affected = 0;
        Coordinates.ForEachRectangleCell(
            context.Session.Facade,
            args.Region.Space,
            args.Region.X,
            args.Region.Y,
            args.Region.Width,
            args.Region.Height,
            (mapX, mapY) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Session.Facade.SetTerrainHeight(mapX, mapY, storageHeight);
                affected++;
            });

        return Task.FromResult<object?>(new SetHeightData
        {
            AffectedCells = affected,
            Height = actualHeight
        });
    }
}

internal sealed class QueryHeightHandler : ICommandHandler
{
    public string Name => "terrain.query";

    public CommandEffect Effect => CommandEffect.Query;

    public Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "terrain.query 需要已打开的工作区。");
        }

        var args = AutomationJson.Deserialize<QueryHeightArgs>(arguments);
        Coordinates.ToMapCell(context.Session.Facade, args.Space, args.X, args.Y, out var mapX, out var mapY);
        return Task.FromResult<object?>(new HeightQueryData
        {
            MapX = mapX,
            MapY = mapY,
            Height = context.Session.Facade.GetTerrainHeight(mapX, mapY)
        });
    }
}

internal sealed class SetHeightArgs
{
    public RegionArgs? Region { get; set; }

    public float Height { get; set; }
}

internal sealed class RegionArgs
{
    public string Kind { get; set; } = "rectangle";

    public string Space { get; set; } = Coordinates.PlayableGrid;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}

internal sealed class QueryHeightArgs
{
    public string Space { get; set; } = Coordinates.PlayableGrid;

    public int X { get; set; }

    public int Y { get; set; }
}
