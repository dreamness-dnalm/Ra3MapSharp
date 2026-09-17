using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;

namespace Dreamness.RA3.Map.Automation.Commands.Batch;

internal sealed class BatchArguments
{
    public List<BatchSubcommand> Commands { get; set; } = new();
}

internal sealed class BatchSubcommand
{
    public string Command { get; set; } = "";

    public int CommandVersion { get; set; } = 1;

    public JsonElement Arguments { get; set; }
}

internal static class BatchRules
{
    public const int MaxCommands = 100;

    public static bool IsAllowed(CommandEffect effect, string name)
    {
        if (string.Equals(name, "batch.execute", StringComparison.Ordinal) || name.StartsWith("edits.", StringComparison.Ordinal) || name.StartsWith("design.", StringComparison.Ordinal))
        {
            return false;
        }

        return effect == CommandEffect.Mutation;
    }
}
