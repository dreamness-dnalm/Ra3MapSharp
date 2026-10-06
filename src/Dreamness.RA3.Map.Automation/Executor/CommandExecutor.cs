using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Commands.Batch;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Executor;

public sealed class CommandExecutor
{
    public static CommandExecutor Default { get; } = new(CommandRegistry.CreateDefault());

    private readonly CommandRegistry _registry;

    public CommandExecutor(CommandRegistry registry)
    {
        _registry = registry;
    }

    public static CommandExecutor CreateDefault()
    {
        return new CommandExecutor(CommandRegistry.CreateDefault());
    }

    public async Task<CommandResult> ExecuteAsync(
        CommandRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RequestId))
                throw new AutomationException("INVALID_ARGUMENT", "requestId 不能为空。");
            ValidateVersion(request.CommandVersion);

            if (string.Equals(request.Command, "batch.execute", StringComparison.Ordinal))
            {
                return await ExecuteBatchAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var handler = _registry.Get(request.Command);
            if (handler.Effect == CommandEffect.Session)
            {
                var data = await handler.ExecuteAsync(null, request.Arguments, cancellationToken)
                    .ConfigureAwait(false);
                var sessionId = (data as SessionOpenedData)?.SessionId;
                return CommandResult.Succeeded(request, sessionId, null, (data as SessionOpenedData)?.Revision, data);
            }

            return await ExecuteOnSessionAsync(request, handler, cancellationToken).ConfigureAwait(false);
        }
        catch (AutomationException ex)
        {
            return CommandResult.Failed(request, request.SessionId, ex);
        }
        catch (OperationCanceledException ex)
        {
            return CommandResult.Failed(request, request.SessionId,
                new AutomationException("CANCELLED", "命令已取消。", true, innerException: ex));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CommandResult.Failed(request, request.SessionId,
                new AutomationException("IO_ERROR", "文件操作失败。", true,
                    details: new Dictionary<string, string> { ["exception"] = ex.GetType().Name, ["reason"] = ex.Message }, innerException: ex));
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failed(request, request.SessionId,
                new AutomationException("INVALID_ARGUMENT", ex.Message, innerException: ex));
        }
    }

    private async Task<CommandResult> ExecuteOnSessionAsync(
        CommandRequest request,
        ICommandHandler handler,
        CancellationToken cancellationToken)
    {
        var session = RequireSession(request.SessionId);
        await session.ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session.ThrowIfClosed();
            var fingerprint = Fingerprint(request);
            var cacheable = handler.Effect != CommandEffect.Query;
            if (cacheable && session.TryGetDedup(request.RequestId, fingerprint, out var cached) && cached != null)
            {
                return cached;
            }

            var revisionBefore = session.Revision;
            if (handler.Effect is CommandEffect.Mutation or CommandEffect.History or CommandEffect.Export)
            {
                EnsureExpectedRevision(request, session);
            }

            object? data;
            var context = new CommandContext(session);
            if (handler.Effect == CommandEffect.Mutation)
            {
                object? mutationData = null;
                await session.CommitMutationAsync(
                        handler.Name,
                        async () =>
                        {
                            mutationData = await handler.ExecuteAsync(context, request.Arguments, cancellationToken)
                                .ConfigureAwait(false);
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                data = mutationData;
            }
            else
            {
                data = await handler.ExecuteAsync(context, request.Arguments, cancellationToken)
                    .ConfigureAwait(false);
            }

            var result = CommandResult.Succeeded(
                request,
                session.SessionId,
                revisionBefore,
                session.Revision,
                data);
            if (cacheable)
            {
                session.RememberDedup(request.RequestId, fingerprint, result);
            }

            return result;
        }
        catch (AutomationException ex)
        {
            var failed = CommandResult.Failed(request, session.SessionId, ex);
            if (handler.Effect != CommandEffect.Query)
            {
                session.RememberDedup(request.RequestId, Fingerprint(request), failed);
            }

            return failed;
        }
        finally
        {
            session.ExecutionLock.Release();
        }
    }

    private async Task<CommandResult> ExecuteBatchAsync(
        CommandRequest request,
        CancellationToken cancellationToken)
    {
        var session = RequireSession(request.SessionId);
        await session.ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session.ThrowIfClosed();
            var fingerprint = Fingerprint(request);
            if (session.TryGetDedup(request.RequestId, fingerprint, out var cached) && cached != null)
            {
                return cached;
            }

            EnsureExpectedRevision(request, session);
            var args = AutomationJson.Deserialize<BatchArguments>(request.Arguments);
            if (args.Commands == null || args.Commands.Count == 0)
            {
                throw new AutomationException("INVALID_ARGUMENT", "batch.execute 至少需要一条子命令。");
            }

            if (args.Commands.Count > BatchRules.MaxCommands)
            {
                throw new AutomationException(
                    "LIMIT_EXCEEDED",
                    $"批次长度不能超过 {BatchRules.MaxCommands}。");
            }

            var revisionBefore = session.Revision;
            var results = new List<object?>();
            var context = new CommandContext(session);
            await session.CommitMutationAsync(
                    "batch.execute",
                    async () =>
                    {
                        for (var i = 0; i < args.Commands.Count; i++)
                        {
                            var sub = args.Commands[i];
                            ICommandHandler handler;
                            try
                            {
                                if (sub == null)
                                    throw new AutomationException("INVALID_ARGUMENT", "子命令不能为空。");
                                ValidateVersion(sub.CommandVersion);
                                handler = _registry.Get(sub.Command);
                            }
                            catch (AutomationException ex)
                            {
                                throw WrapBatchFailure(i, ex);
                            }

                            if (!BatchRules.IsAllowed(handler.Effect, handler.Name))
                            {
                                throw new AutomationException(
                                    "INVALID_ARGUMENT",
                                    $"子命令不能放入批次: {sub.Command}",
                                    details: new Dictionary<string, string>
                                    {
                                        ["index"] = i.ToString(),
                                        ["command"] = sub.Command
                                    });
                            }

                            try
                            {
                                var data = await handler.ExecuteAsync(context, sub.Arguments, cancellationToken)
                                    .ConfigureAwait(false);
                                results.Add(data);
                            }
                            catch (AutomationException ex)
                            {
                                throw WrapBatchFailure(i, ex);
                            }
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var result = CommandResult.Succeeded(
                request,
                session.SessionId,
                revisionBefore,
                session.Revision,
                new BatchResultData { Count = results.Count, Results = results });
            session.RememberDedup(request.RequestId, fingerprint, result);
            return result;
        }
        catch (AutomationException ex)
        {
            var failed = CommandResult.Failed(request, session.SessionId, ex);
            session.RememberDedup(request.RequestId, Fingerprint(request), failed);
            return failed;
        }
        finally
        {
            session.ExecutionLock.Release();
        }
    }

    private static MapSession RequireSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)
            || !MapSessionManager.TryGetBySessionId(sessionId, out var session))
        {
            throw new AutomationException(
                "SESSION_NOT_FOUND",
                $"未找到工作区: {sessionId}",
                details: new Dictionary<string, string> { ["sessionId"] = sessionId ?? "" });
        }

        return session;
    }

    private static void EnsureExpectedRevision(CommandRequest request, MapSession session)
    {
        if (request.ExpectedRevision == null)
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "写操作必须提供 expectedRevision。");
        }

        if (request.ExpectedRevision.Value != session.Revision)
        {
            throw new AutomationException(
                "REVISION_CONFLICT",
                $"expectedRevision 为 {request.ExpectedRevision.Value}，当前为 {session.Revision}。",
                details: new Dictionary<string, string>
                {
                    ["expectedRevision"] = request.ExpectedRevision.Value.ToString(),
                    ["actualRevision"] = session.Revision.ToString()
                });
        }
    }

    private static void ValidateVersion(int version)
    {
        if (version != 1)
            throw new AutomationException("UNSUPPORTED_VERSION", $"不支持的命令版本: {version}");
    }

    private static string Fingerprint(CommandRequest request)
    {
        var args = request.Arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? ""
            : request.Arguments.GetRawText();
        var text = request.Command + "|" + request.CommandVersion + "|" + args + "|" +
                   (request.ExpectedRevision?.ToString() ?? "");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static AutomationException WrapBatchFailure(int index, AutomationException inner)
    {
        var details = new Dictionary<string, string>(inner.Details ?? new Dictionary<string, string>())
        {
            ["index"] = index.ToString()
        };
        return new AutomationException(
            inner.Code,
            $"批次第 {index} 条子命令失败: {inner.Message}",
            inner.Retryable,
            details,
            inner);
    }
}
