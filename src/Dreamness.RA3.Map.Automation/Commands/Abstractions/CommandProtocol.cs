using System.Text.Json;

namespace Dreamness.RA3.Map.Automation.Commands.Abstractions;

public sealed class CommandRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("D");

    public string? SessionId { get; set; }

    public int? ExpectedRevision { get; set; }

    public string Command { get; set; } = "";

    public int CommandVersion { get; set; } = 1;

    public JsonElement Arguments { get; set; }
}

public sealed class CommandResult
{
    public string RequestId { get; set; } = "";

    public string? SessionId { get; set; }

    public string Status { get; set; } = "succeeded";

    public int? RevisionBefore { get; set; }

    public int? RevisionAfter { get; set; }

    public object? Data { get; set; }

    public List<string> Warnings { get; set; } = new();

    public CommandError? Error { get; set; }

    public static CommandResult Succeeded(
        CommandRequest request,
        string? sessionId,
        int? revisionBefore,
        int? revisionAfter,
        object? data)
    {
        return new CommandResult
        {
            RequestId = request.RequestId,
            SessionId = sessionId,
            Status = "succeeded",
            RevisionBefore = revisionBefore,
            RevisionAfter = revisionAfter,
            Data = data
        };
    }

    public static CommandResult Failed(CommandRequest request, string? sessionId, AutomationException exception)
    {
        return new CommandResult
        {
            RequestId = request.RequestId,
            SessionId = sessionId,
            Status = "failed",
            Error = new CommandError
            {
                Code = exception.Code,
                Message = exception.Message,
                Details = exception.Details,
                Retryable = exception.Retryable
            }
        };
    }
}

public sealed class CommandError
{
    public string Code { get; set; } = "";

    public string Message { get; set; } = "";

    public IReadOnlyDictionary<string, string>? Details { get; set; }

    public bool Retryable { get; set; }
}

public enum CommandEffect
{
    Query,
    Mutation,
    Session,
    History,
    Export
}

public interface ICommandHandler
{
    string Name { get; }

    CommandEffect Effect { get; }

    Task<object?> ExecuteAsync(
        CommandContext? context,
        JsonElement arguments,
        CancellationToken cancellationToken);
}

public sealed class CommandContext
{
    public CommandContext(Session.MapSession session)
    {
        Session = session;
    }

    public Session.MapSession Session { get; }
}
