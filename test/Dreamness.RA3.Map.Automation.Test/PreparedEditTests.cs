using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class PreparedEditTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-prepared-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 24, 24, 4);
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private Task<CommandResult> Run(string command, object args) => _executor.ExecuteAsync(new CommandRequest
    { SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
    private async Task<PreparedEditInfo> Prepare(params object[] commands)
    {
        var result = await Run("edits.prepare", new { baseRevision = _session.Revision, commands });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return (PreparedEditInfo)result.Data!;
    }
    private static object Tree(int x = 6) => new { command = "objects.place", arguments = new { typeName = "CC_Tree01", x, y = 8 } };
    private static object ApplyArgs(PreparedEditInfo p) => new { preparedPlanId = p.PreparedPlanId, planHash = p.PlanHash };

    [Test]
    public async Task PreviewDoesNotCommitAndApplyMatchesExactCandidateWithHistory()
    {
        var before = await _session.CaptureSnapshotAsync();
        var plan = await Prepare(Tree(), new { command = "protections.add", arguments = new
        { id = "base", region = new { x = 15, y = 15, width = 4, height = 4 }, layers = new[] { "terrain" } } });
        Assert.That(_session.Revision, Is.Zero);
        Assert.That(_session.CanUndo, Is.False);
        Assert.That(_session.Dirty, Is.False);
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        var candidate = await _session.CapturePreparedSnapshotAsync(plan.PreparedPlanId, plan.PlanHash);
        Assert.That(candidate.ContentHash, Is.Not.EqualTo(before.ContentHash));
        Assert.That(candidate.ProtectionZones.Single().Id, Is.EqualTo("base"));
        candidate.MapBytes[0] ^= 255;
        candidate.ProtectionZones[0].Id = "tampered";
        var applied = await Run("edits.apply", ApplyArgs(plan));
        Assert.That(applied.Status, Is.EqualTo("succeeded"), applied.Error?.Message);
        var current = await _session.CaptureSnapshotAsync();
        Assert.That(current.ContentHash, Is.EqualTo(plan.CandidateContentHash));
        Assert.That(current.ProtectionHash, Is.EqualTo(plan.CandidateProtectionHash));
        Assert.That(_session.Handles.UnitObjectIds.Single(), Is.EqualTo("obj-1"));
        await Run("history.undo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        await Run("history.redo", new { });
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(plan.CandidateContentHash));
        await Run("map.save", new { });
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That((await _session.CaptureSnapshotAsync()).ProtectionHash, Is.EqualTo(plan.CandidateProtectionHash));
        Assert.That((await Run("edits.apply", ApplyArgs(plan))).Error?.Code, Is.EqualTo("PLAN_NOT_FOUND"));
    }

    [Test]
    public async Task CompetingCandidatesAreInvalidatedEvenAfterUndo()
    {
        var first = await Prepare(Tree());
        var second = await Prepare(Tree(10));
        Assert.That(_session.Handles.NextHandle, Is.EqualTo(1));
        Assert.That((await Run("edits.apply", ApplyArgs(first))).Status, Is.EqualTo("succeeded"));
        Assert.That((await Run("edits.apply", ApplyArgs(second))).Error?.Code, Is.EqualTo("PLAN_STALE"));
        await Run("history.undo", new { });
        Assert.That((await Run("edits.apply", ApplyArgs(second))).Error?.Code, Is.EqualTo("PLAN_STALE"));
    }

    [Test]
    public async Task FailedPreparationRestoresObjectsHandlesAndProtection()
    {
        await Run("protections.add", new { id = "base", region = new { x = 2, y = 2, width = 5, height = 5 }, layers = new[] { "terrain" } });
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("edits.prepare", new { baseRevision = 1, commands = new[] { Tree(),
            new { command = "terrain.set_height", arguments = new { region = new { x = 3, y = 3, width = 1, height = 1 }, height = 300 } } as object } });
        Assert.That(result.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
        Assert.That(_session.Handles.NextHandle, Is.EqualTo(1));
        Assert.That(_session.Handles.UnitObjectIds, Is.Empty);
        Assert.That(_session.Revision, Is.EqualTo(1));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That((await _session.CaptureSnapshotAsync()).ProtectionHash, Is.EqualTo(before.ProtectionHash));
    }

    [Test]
    public async Task ApplyReceiptIsIdempotentAndHashMismatchDoesNotEdit()
    {
        var plan = await Prepare(Tree());
        Assert.That((await Run("edits.apply", new { preparedPlanId = plan.PreparedPlanId, planHash = "wrong" })).Error?.Code, Is.EqualTo("PLAN_HASH_CONFLICT"));
        Assert.That(_session.Revision, Is.Zero);
        var request = new CommandRequest { Command = "edits.apply", SessionId = _session.SessionId,
            ExpectedRevision = 0, Arguments = JsonSerializer.SerializeToElement(ApplyArgs(plan)) };
        Assert.That((await _executor.ExecuteAsync(request)).Status, Is.EqualTo("succeeded"));
        Assert.That((await _executor.ExecuteAsync(request)).RevisionAfter, Is.EqualTo(1));
        Assert.That(_session.Revision, Is.EqualTo(1));
        Assert.That(_session.Handles.UnitObjectIds.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task NestedApplyRejectedAndDiscardPreventsUse()
    {
        var plan = await Prepare(Tree());
        Assert.That((await Run("batch.execute", new { commands = new[] { new { command = "edits.apply", arguments = ApplyArgs(plan) } } })).Error?.Code,
            Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.Zero);
        await Run("edits.discard", new { preparedPlanId = plan.PreparedPlanId });
        Assert.That((await Run("edits.apply", ApplyArgs(plan))).Error?.Code, Is.EqualTo("PLAN_NOT_FOUND"));
    }
}
