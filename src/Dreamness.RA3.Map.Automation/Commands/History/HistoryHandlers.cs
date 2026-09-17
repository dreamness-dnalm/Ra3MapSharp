using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.History;

internal sealed class UndoHandler : ICommandHandler
{
    public string Name => "history.undo";

    public CommandEffect Effect => CommandEffect.History;

    public async Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "history.undo 需要已打开的工作区。");
        }

        await context.Session.UndoAsync(cancellationToken).ConfigureAwait(false);
        return new HistoryData
        {
            HistoryCursor = context.Session.HistoryCursor,
            CanUndo = context.Session.CanUndo,
            CanRedo = context.Session.CanRedo
        };
    }
}

internal sealed class RedoHandler : ICommandHandler
{
    public string Name => "history.redo";

    public CommandEffect Effect => CommandEffect.History;

    public async Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (context == null)
        {
            throw new AutomationException("SESSION_NOT_FOUND", "history.redo 需要已打开的工作区。");
        }

        await context.Session.RedoAsync(cancellationToken).ConfigureAwait(false);
        return new HistoryData
        {
            HistoryCursor = context.Session.HistoryCursor,
            CanUndo = context.Session.CanUndo,
            CanRedo = context.Session.CanRedo
        };
    }
}
