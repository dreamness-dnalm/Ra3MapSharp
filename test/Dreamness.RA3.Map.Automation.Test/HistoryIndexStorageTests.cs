using System.Text.Json;
using System.Text.Json.Nodes;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

/// <summary>
/// Guards the compact history-index format (schemaVersion 9). Each revision stores only the
/// design entities whose payload changed, so these tests pin both directions: repeated
/// payloads must not be re-serialized, and reload/undo/redo must still resolve every
/// revision back to a complete state.
/// </summary>
public class HistoryIndexStorageTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-history-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 32, 24, 4);
    }

    [TearDown]
    public void Cleanup()
    {
        _session.Dispose();
        Directory.Delete(_root, true);
    }

    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    {
        SessionId = _session.SessionId,
        ExpectedRevision = _session.Revision,
        Command = command,
        Arguments = JsonSerializer.SerializeToElement(args)
    });

    private async Task Apply(object patch)
    {
        var prepared = await Run("design.prepare", new { baseRevision = _session.Revision, patch });
        Assert.That(prepared.Status, Is.EqualTo("succeeded"), prepared.Error?.Message);
        var plan = (PreparedEditInfo)prepared.Data!;
        var applied = await Run("design.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash });
        Assert.That(applied.Status, Is.EqualTo("succeeded"), applied.Error?.Message);
    }

    /// <summary>A 16x16 platform owns 256 height cells, enough for the payload to dominate.</summary>
    private static object Platform(string id = "base", float value = 300) => new
    { id, kind = "platform", parameters = new { region = new { x = 2, y = 2, width = 16, height = 16 }, value, falloff = .4 } };

    private JsonNode Index()
    {
        var layout = AutomationLayout.Resolve(_root, "Test");
        return JsonNode.Parse(File.ReadAllText(layout.HistoryIndexFilePath))!;
    }

    private static IEnumerable<JsonObject> Entries(JsonNode index) =>
        index["entries"]!.AsArray().Select(node => node!.AsObject());

    /// <summary>Revisions whose delta carries the "base" payload.</summary>
    private int Carrying() => Entries(Index()).Count(entry => entry["designEntityChanges"]?["base"] != null);

    [Test]
    public async Task UnchangedDesignEntityIsSerializedOnceAcrossRevisions()
    {
        await Apply(new { upsert = new[] { Platform() } });
        // Unrelated mutations, so the entity payload above is identical in every revision.
        await Run("waypoints.place", new { name = "M1", x = 1, y = 1 });
        await Run("waypoints.place", new { name = "M2", x = 3, y = 3 });
        await Run("waypoints.place", new { name = "M3", x = 5, y = 5 });

        var index = Index();
        var entries = Entries(index).ToList();
        Assert.That(entries.Count, Is.EqualTo(5), "baseline + 4 mutations");

        var carrying = entries.Count(entry => entry["designEntityChanges"]?["base"] != null);
        Assert.That(carrying, Is.EqualTo(1),
            "a payload that never changed must be stored once, not once per revision");
        Assert.That(entries.Any(entry => entry["designEntities"] != null), Is.False,
            "schemaVersion 9 must not fall back to complete per-revision snapshots");
        Assert.That(index["schemaVersion"]!.GetValue<int>(), Is.EqualTo(9));
    }

    [Test]
    public async Task OnlyRevisionsThatChangeAPayloadStoreIt()
    {
        // Re-applying an identical patch is idempotent, so the payload is unchanged and
        // must not be stored again.
        await Apply(new { upsert = new[] { Platform() } });
        await Apply(new { upsert = new[] { Platform() } });
        Assert.That(Carrying(), Is.EqualTo(1), "an idempotent re-apply must not duplicate the payload");

        // A genuinely different payload is stored for each revision that produced it.
        await Apply(new { upsert = new[] { Platform(value: 320) } });
        await Apply(new { upsert = new[] { Platform(value: 340) } });
        Assert.That(Carrying(), Is.EqualTo(3), "three distinct payloads");
    }

    [Test]
    public async Task ReloadRestoresEveryRevisionAndUndoRedoResolvesDeltas()
    {
        await Apply(new { upsert = new[] { Platform() } });
        await Run("waypoints.place", new { name = "M1", x = 1, y = 1 });
        var heights = _session.DesignEntities["base"].Heights.ToList();
        Assert.That(heights, Is.Not.Empty, "a platform owns the height cells it touched");

        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");

        Assert.That(_session.DesignEntities.Keys, Is.EqualTo(new[] { "base" }));
        Assert.That(_session.DesignEntities["base"].Heights, Is.EqualTo(heights),
            "every owned cell must round-trip exactly");

        // Walking back must resolve the delta chains in both directions.
        await Run("history.undo", new { });
        await Run("history.undo", new { });
        Assert.That(_session.DesignEntities, Is.Empty, "the revision before the entity owns nothing");
        await Run("history.redo", new { });
        Assert.That(_session.DesignEntities["base"].Heights, Is.EqualTo(heights));
        await Run("history.redo", new { });
        Assert.That(_session.DesignEntities["base"].Heights, Is.EqualTo(heights));
    }

    [Test]
    public async Task RemovedEntityStaysRemovedAfterReloadAndReturnsOnUndo()
    {
        await Apply(new { upsert = new[] { Platform() } });
        await Apply(new { remove = new[] { "base" } });
        await Run("waypoints.place", new { name = "M1", x = 1, y = 1 });

        var removals = Entries(Index())
            .Count(entry => entry["removedDesignEntityIds"]?.AsArray().Any(id => id!.GetValue<string>() == "base") == true);
        Assert.That(removals, Is.EqualTo(1), "only the revision that removed the entity records it");

        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That(_session.DesignEntities, Is.Empty, "a removed entity must not reappear");

        await Run("history.undo", new { });
        await Run("history.undo", new { });
        Assert.That(_session.DesignEntities.Keys, Is.EqualTo(new[] { "base" }),
            "undo past the removal must reconstruct the entity");
    }
}
