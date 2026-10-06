using System.Text.Json;
using Dreamness.Ra3.Map.Facade.enums;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Terrain;

internal sealed class QueryTexturesHandler : ICommandHandler
{
    public string Name => "textures.list";
    public CommandEffect Effect => CommandEffect.Query;
    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var args = AutomationJson.Deserialize<TextureQueryArgs>(arguments);
        if (args.Offset < 0 || args.Limit is < 1 or > 200)
            throw new AutomationException("INVALID_ARGUMENT", "offset 必须非负；limit 为 1–200。");
        var textures = Enum.GetNames<TextureEnum>().Where(n => string.IsNullOrEmpty(args.Query) || n.Contains(args.Query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult<object?>(new { total = textures.Length, textures = textures.Skip(args.Offset).Take(args.Limit).ToArray(),
            validationLevel = "library-declared", note = "枚举声明不代表当前 Mod 已逐项验证。" });
    }
}

internal sealed class PaintTextureHandler : ICommandHandler
{
    public string Name => "texture.paint";
    public CommandEffect Effect => CommandEffect.Mutation;
    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<PaintTextureArgs>(arguments);
        if (args.Region == null || args.Texture == null || !Enum.GetNames<TextureEnum>().Contains(args.Texture, StringComparer.Ordinal))
            throw new AutomationException("INVALID_ARGUMENT", "region 必填，texture 必须是 textures.list 的准确名称。");
        var cells = args.Region.Cells(map);
        return Task.FromResult<object?>(PaintCells(map, cells, args.Texture, args.AutoBlend, token));
    }

    internal static object PaintCells(Dreamness.Ra3.Map.Facade.Core.Ra3MapFacade map,
        IReadOnlyCollection<(int X, int Y)> cells, string texture, bool autoBlend, CancellationToken token)
    {
        if (!Enum.GetNames<TextureEnum>().Contains(texture, StringComparer.Ordinal))
            throw new AutomationException("INVALID_ARGUMENT", "texture 必须是 textures.list 的准确名称。");
        foreach (var (x, y) in cells)
        {
            token.ThrowIfCancellationRequested();
            map.SetTileTexture(x, y, texture);
        }
        var halo = new HashSet<(int X, int Y)>();
        if (autoBlend)
        {
            foreach (var (x, y) in cells)
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    if (x + dx >= 0 && y + dy >= 0 && x + dx < map.MapWidth && y + dy < map.MapHeight)
                        halo.Add((x + dx, y + dy));
            foreach (var cell in halo.OrderBy(c => c.Y).ThenBy(c => c.X))
            {
                token.ThrowIfCancellationRequested();
                map.RemoveBlend(cell.X, cell.Y);
                map.AutoDetectBlend(cell.X, cell.Y);
            }
        }
        return new { affectedCells = cells.Count, blendCells = halo.Count,
            blendBounds = halo.Count == 0 ? null : new { space = "mapGrid", x = halo.Min(c => c.X), y = halo.Min(c => c.Y),
                width = halo.Max(c => c.X) - halo.Min(c => c.X) + 1, height = halo.Max(c => c.Y) - halo.Min(c => c.Y) + 1 } };
    }
}

internal sealed class TextureQueryArgs
{
    public string? Query { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; } = 50;
}
internal sealed class PaintTextureArgs
{
    public GridRegion? Region { get; set; }
    public string? Texture { get; set; }
    public bool AutoBlend { get; set; } = true;
}
