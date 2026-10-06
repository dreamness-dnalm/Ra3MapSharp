using System.Text.Json;
using Dreamness.RA3.Map.Automation.Catalog;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Commands.Art;

/// <summary>
/// Measures the art-direction profile of the open map. Query only: it reports, it does not judge.
/// <para>
/// The judgement lives with the thresholds it compares against, which are machine data; measuring
/// belongs to the kernel, exactly like the shipped-map analysis uses the same code.
/// </para>
/// </summary>
internal sealed class ArtProfileHandler : ICommandHandler
{
    private readonly Dictionary<string, string>? _categoryLookup;

    public ArtProfileHandler(ObjectCatalog? catalog)
    {
        _categoryLookup = catalog?.Entries
            .GroupBy(entry => entry.TypeName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.First().Categories.FirstOrDefault(label => label != "editor-translation")
                    ?? "unclassified",
                StringComparer.Ordinal);
    }

    public string Name => "art.profile";

    public CommandEffect Effect => CommandEffect.Query;

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken token)
    {
        var map = context?.Session.Facade ?? throw new AutomationException("SESSION_NOT_FOUND", "需要地图会话。");
        var args = AutomationJson.Deserialize<ArtProfileArgs>(arguments);
        if (args.SampleTarget is < 1000 or > 1_000_000)
            throw new AutomationException("INVALID_ARGUMENT", "sampleTarget 需在 1000–1000000 之间。");
        return Task.FromResult<object?>(
            MapArtProfile.Measure(map, _categoryLookup, args.SampleTarget, token));
    }

    private sealed class ArtProfileArgs
    {
        public int SampleTarget { get; set; } = 30000;
    }
}
