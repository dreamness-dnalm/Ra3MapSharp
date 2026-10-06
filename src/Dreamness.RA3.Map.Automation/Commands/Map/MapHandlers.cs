using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Map;

internal sealed class CreateMapHandler : ICommandHandler
{
    public string Name => "map.create";

    public CommandEffect Effect => CommandEffect.Session;

    public async Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var args = AutomationJson.Deserialize<CreateMapArgs>(arguments);
        Require(args.ParentPath, nameof(args.ParentPath));
        Require(args.MapName, nameof(args.MapName));

        var session = await MapSessionManager.CreateAsync(
                args.ParentPath,
                args.MapName,
                args.PlayableWidth,
                args.PlayableHeight,
                args.Border,
                string.IsNullOrWhiteSpace(args.DefaultTexture) ? "Dirt_Yucatan03" : args.DefaultTexture,
                args.Compress,
                cancellationToken)
            .ConfigureAwait(false);

        return session.ToOpenedData();
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AutomationException("INVALID_ARGUMENT", $"{name} 不能为空。");
        }
    }
}

internal sealed class OpenMapHandler : ICommandHandler
{
    public string Name => "map.open";

    public CommandEffect Effect => CommandEffect.Session;

    public async Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var args = AutomationJson.Deserialize<OpenMapArgs>(arguments);
        if (string.IsNullOrWhiteSpace(args.ParentPath) || string.IsNullOrWhiteSpace(args.MapName))
        {
            throw new AutomationException("INVALID_ARGUMENT", "parentPath 与 mapName 不能为空。");
        }

        var session = await MapSessionManager.OpenAsync(args.ParentPath, args.MapName, cancellationToken)
            .ConfigureAwait(false);
        return session.ToOpenedData();
    }
}

internal sealed class MapInfoHandler : ICommandHandler
{
    public string Name => "map.info";

    public CommandEffect Effect => CommandEffect.Query;

    public Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "map.info 需要已打开的工作区。");
        }

        return Task.FromResult<object?>(context.Session.ToMapInfoData());
    }
}

internal sealed class SaveMapHandler : ICommandHandler
{
    public string Name => "map.save";

    public CommandEffect Effect => CommandEffect.Export;

    public async Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "map.save 需要已打开的工作区。");
        }

        var args = AutomationJson.Deserialize<SaveMapArgs>(arguments);
        await context.Session.SaveUserMapAsync(args.Compress, cancellationToken).ConfigureAwait(false);
        return new SaveMapData
        {
            UserMapFilePath = context.Session.UserMapFilePath,
            ContentHash = context.Session.LastSavedContentHash
        };
    }
}

internal sealed class SaveMapAsHandler : ICommandHandler
{
    public string Name => "map.save_as";

    public CommandEffect Effect => CommandEffect.Export;

    public async Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "map.save_as 需要已打开的工作区。");
        }

        var args = AutomationJson.Deserialize<SaveMapAsArgs>(arguments);
        if (string.IsNullOrWhiteSpace(args.ParentPath) || string.IsNullOrWhiteSpace(args.MapName))
        {
            throw new AutomationException("INVALID_ARGUMENT", "parentPath 与 mapName 不能为空。");
        }

        await context.Session.SaveUserMapAsAsync(
                args.ParentPath,
                args.MapName,
                args.Compress,
                args.Overwrite,
                cancellationToken)
            .ConfigureAwait(false);

        var layout = AutomationLayout.Resolve(args.ParentPath, args.MapName);
        return new SaveMapData { UserMapFilePath = layout.UserMapFilePath };
    }
}

internal sealed class CreateMapArgs
{
    public string ParentPath { get; set; } = "";

    public string MapName { get; set; } = "";

    public int PlayableWidth { get; set; }

    public int PlayableHeight { get; set; }

    public int Border { get; set; } = 8;

    public string DefaultTexture { get; set; } = "Dirt_Yucatan03";

    public bool Compress { get; set; } = true;
}

internal sealed class OpenMapArgs
{
    public string ParentPath { get; set; } = "";

    public string MapName { get; set; } = "";
}

internal sealed class SaveMapArgs
{
    public bool Compress { get; set; } = true;
}

internal sealed class SaveMapAsArgs
{
    public string ParentPath { get; set; } = "";

    public string MapName { get; set; } = "";

    public bool Compress { get; set; } = true;

    public bool Overwrite { get; set; }
}
