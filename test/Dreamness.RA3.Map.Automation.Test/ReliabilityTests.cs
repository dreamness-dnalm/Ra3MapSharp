using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Test;

public class ReliabilityTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private CommandExecutor _executor = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Ra3Automation-Reliability-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Map", 16, 12, 2);
        _executor = CommandExecutor.CreateDefault();
    }

    [TearDown]
    public void TearDown()
    {
        _session.Dispose();
        var full = Path.GetFullPath(_root);
        Assert.That(full, Does.StartWith(Path.GetFullPath(Path.GetTempPath())));
        Directory.Delete(full, true);
    }

    private CommandRequest Request(string command, object? args = null) => new()
    {
        SessionId = _session.SessionId, ExpectedRevision = _session.Revision,
        Command = command, Arguments = JsonSerializer.SerializeToElement(args ?? new { })
    };

    private static object Height(float height, int x = 0, string space = "playableGrid", int width = 1) => new
    {
        region = new { kind = "rectangle", space, x, y = 0, width, height = 1 }, height
    };

    private async Task<CommandResult> Success(string command, object? args = null)
    {
        var result = await _executor.ExecuteAsync(Request(command, args));
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return result;
    }

    private Dictionary<string, byte[]> Files() => Directory.GetFiles(_session.AutomationDirectoryPath,
        "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".lock"))
        .ToDictionary(p => p, File.ReadAllBytes);

    private void AssertFiles(Dictionary<string, byte[]> before)
    {
        var after = Files();
        Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
        foreach (var pair in before) Assert.That(after[pair.Key], Is.EqualTo(pair.Value), pair.Key);
    }

    [TestCase("working")]
    [TestCase("snapshot")]
    [TestCase("index")]
    [TestCase("workspace")]
    [TestCase("log")]
    public async Task FailedCommit_PreservesFilesRevisionHandlesAndRedo(string lockedFile)
    {
        var placed = (WaypointData)(await Success("waypoints.place", new {name="Original",x=1,y=1})).Data!;
        await Success("history.undo");
        var before = Files();
        var revision = _session.Revision;
        var dirty = _session.Dirty;
        var nextHandle = _session.Handles.NextHandle;
        var layout = _session.Layout;
        var path = lockedFile switch
        {
            "working" => layout.WorkingMapFilePath,
            "snapshot" => layout.SnapshotFilePath(1),
            "index" => layout.HistoryIndexFilePath,
            "workspace" => layout.WorkspaceFilePath,
            _ => layout.HistoryLogFilePath
        };
        // Reading/backing up is allowed, replacing is not. This exercises rollback
        // after earlier files have already been replaced, not just staging failures.
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = await _executor.ExecuteAsync(Request("waypoints.place", new {name="Failed",x=2,y=2}));
            Assert.That(failed.Error?.Code, Is.EqualTo("IO_ERROR"));
        }
        Assert.That(_session.Revision, Is.EqualTo(revision));
        Assert.That(_session.HistoryCursor, Is.Zero);
        Assert.That(_session.Dirty, Is.EqualTo(dirty));
        Assert.That(_session.CanRedo, Is.True);
        Assert.That(_session.Handles.NextHandle, Is.EqualTo(nextHandle));
        Assert.That(_session.Facade.GetWaypoints(), Is.Empty);
        AssertFiles(before);
        await Success("history.redo");
        Assert.That(_session.Handles.Resolve(_session.Facade, placed.ObjectId).WaypointName, Is.EqualTo("Original"));
    }

    [TestCase("history.undo")]
    [TestCase("history.redo")]
    public async Task FailedHistoryMove_IsAtomic(string command)
    {
        await Success("terrain.set_height", Height(240));
        if (command == "history.redo") await Success("history.undo");
        var before = Files();
        var revision = _session.Revision;
        var height = _session.Facade.GetTerrainHeight(2, 2);
        using (var held = new FileStream(_session.Layout.WorkspaceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = await _executor.ExecuteAsync(Request(command));
            Assert.That(failed.Error?.Code, Is.EqualTo("IO_ERROR"));
        }
        AssertFiles(before);
        Assert.That(_session.Revision, Is.EqualTo(revision));
        Assert.That(_session.Facade.GetTerrainHeight(2, 2), Is.EqualTo(height));
        await Success(command);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RequestConflict_DoesNotOverwriteOriginalReceipt(bool batch)
    {
        object Args(float h) => batch
            ? new {commands = new[] {new {command="terrain.set_height", arguments=Height(h)}}}
            : Height(h);
        var command = batch ? "batch.execute" : "terrain.set_height";
        var original = Request(command, Args(240));
        var receipt = await _executor.ExecuteAsync(original);
        var conflict = Request(command, Args(180));
        conflict.RequestId = original.RequestId;
        Assert.That((await _executor.ExecuteAsync(conflict)).Error?.Code, Is.EqualTo("REQUEST_ID_CONFLICT"));
        var retry = await _executor.ExecuteAsync(original);
        Assert.That(retry.Status, Is.EqualTo("succeeded"));
        Assert.That(retry.RevisionAfter, Is.EqualTo(receipt.RevisionAfter));
        Assert.That(_session.Revision, Is.EqualTo(1));
    }

    [Test]
    public async Task Batch_UndoRedoRestoresWholeBatchAndPreservesEarlierEdit()
    {
        await Success("terrain.set_height", Height(230));
        var beforeCursor = _session.HistoryCursor;
        var batch = await Success("batch.execute", new
        {
            commands = new object[]
            {
                new { command = "terrain.set_height", arguments = Height(240) },
                new { command = "waypoints.place", arguments = new { name = "First", x = 1, y = 1 } },
                new { command = "waypoints.place", arguments = new { name = "Second", x = 2, y = 2 } }
            }
        });
        var results = (BatchResultData)batch.Data!;
        var first = (WaypointData)results.Results[1]!;
        var second = (WaypointData)results.Results[2]!;
        Assert.That(batch.RevisionAfter, Is.EqualTo(2));
        Assert.That(_session.HistoryCursor, Is.EqualTo(beforeCursor + 1));
        Assert.That(_session.Facade.GetTerrainHeight(2, 2), Is.EqualTo(240));
        Assert.That(_session.Facade.GetWaypoints().Count, Is.EqualTo(2));

        // A single undo restores every operation in the batch, but retains the
        // separately committed edit that preceded it.
        await Success("history.undo");
        Assert.That(_session.Revision, Is.EqualTo(3));
        Assert.That(_session.HistoryCursor, Is.EqualTo(beforeCursor));
        Assert.That(_session.Facade.GetTerrainHeight(2, 2), Is.EqualTo(230));
        Assert.That(_session.Facade.GetWaypoints(), Is.Empty);
        Assert.That(_session.CanUndo, Is.True);
        Assert.That(_session.CanRedo, Is.True);

        // The batch boundary and object identities also survive closing/reopening.
        await _session.CloseAsync();
        _session = await MapSessionManager.OpenAsync(_root, "Map");
        await Success("history.redo");
        Assert.That(_session.Revision, Is.EqualTo(4));
        Assert.That(_session.HistoryCursor, Is.EqualTo(beforeCursor + 1));
        Assert.That(_session.Facade.GetTerrainHeight(2, 2), Is.EqualTo(240));
        Assert.That(_session.Facade.GetWaypoints().Count, Is.EqualTo(2));
        Assert.That(_session.Handles.Resolve(_session.Facade, first.ObjectId).WaypointName, Is.EqualTo("First"));
        Assert.That(_session.Handles.Resolve(_session.Facade, second.ObjectId).WaypointName, Is.EqualTo("Second"));
        Assert.That(_session.CanRedo, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Save_RejectsExternallyChangedOrDeletedFile(bool delete)
    {
        await Success("terrain.set_height", Height(240));
        byte[]? changed = null;
        if (delete) File.Delete(_session.UserMapFilePath);
        else
        {
            var map = Ra3MapFacade.Open(_session.UserMapFilePath);
            map.SetTerrainHeight(2, 2, 330);
            map.SaveAs(_session.UserMapFilePath);
            changed = File.ReadAllBytes(_session.UserMapFilePath);
        }
        var failed = await _executor.ExecuteAsync(Request("map.save"));
        Assert.That(failed.Error?.Code, Is.EqualTo("WORKSPACE_CONFLICT"));
        Assert.That(_session.Dirty, Is.True);
        if (delete) Assert.That(File.Exists(_session.UserMapFilePath), Is.False);
        else Assert.That(File.ReadAllBytes(_session.UserMapFilePath), Is.EqualTo(changed));
    }

    [Test]
    public async Task Save_MetadataFailure_RestoresUserFileAndSaveState()
    {
        await Success("terrain.set_height", Height(240));
        var original = File.ReadAllBytes(_session.UserMapFilePath);
        var hash = _session.LastSavedContentHash;
        var before = Files();
        using (var held = new FileStream(_session.Layout.WorkspaceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.That((await _executor.ExecuteAsync(Request("map.save"))).Error?.Code, Is.EqualTo("IO_ERROR"));
        }
        Assert.That(File.ReadAllBytes(_session.UserMapFilePath), Is.EqualTo(original));
        Assert.That(_session.LastSavedContentHash, Is.EqualTo(hash));
        Assert.That(_session.Dirty, Is.True);
        AssertFiles(before);
        await Success("map.save");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StagedSave_RechecksExpectedTargetBeforeReplacing(bool expectedMissing)
    {
        var path = Path.Combine(_root, "export.map");
        var original = new byte[] { 1, 2, 3 };
        if (!expectedMissing) File.WriteAllBytes(path, original);
        var transaction = new FileTransaction();
        transaction.AddChecked(path, new byte[] { 4, 5, 6 },
            expectedMissing ? null : ContentHasher.HashBytes(original));
        var external = new byte[] { 7, 8, 9 };
        File.WriteAllBytes(path, external);
        var error = Assert.ThrowsAsync<AutomationException>(
            async () => await transaction.CommitAsync(CancellationToken.None));
        Assert.That(error!.Code, Is.EqualTo("WORKSPACE_CONFLICT"));
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(external));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Export_RejectsActiveTargetAndKeepsSourceSaveTarget()
    {
        using (var target = await MapSessionManager.CreateAsync(_root, "Copy", 16, 12, 2))
        {
            var before = File.ReadAllBytes(target.UserMapFilePath);
            var failed = await _executor.ExecuteAsync(Request("map.save_as", new {parentPath=_root,mapName="Copy",overwrite=true}));
            Assert.That(failed.Error?.Code, Is.EqualTo("SESSION_ALREADY_OPEN"));
            Assert.That(File.ReadAllBytes(target.UserMapFilePath), Is.EqualTo(before));
        }
        await Success("terrain.set_height", Height(240));
        await Success("map.save_as", new {parentPath=_root,mapName="Copy",overwrite=true});
        Assert.That(_session.MapName, Is.EqualTo("Map"));
        Assert.That(_session.Dirty, Is.True);
    }

    [TestCase(-10f)]
    [TestCase(2560f)]
    [TestCase(float.MaxValue)]
    public async Task InvalidHeight_DoesNotCommit(float value)
    {
        var failed = await _executor.ExecuteAsync(Request("terrain.set_height", Height(value)));
        Assert.That(failed.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.Zero);
        Assert.That(_session.Dirty, Is.False);
    }

    [TestCase(0f)]
    [TestCase(239.98f)]
    [TestCase(240.123f)]
    [TestCase(2559.921f)]
    public async Task HeightResult_EqualsSerializedValue(float value)
    {
        var result = (SetHeightData)(await Success("terrain.set_height", Height(value))).Data!;
        var actual = (HeightQueryData)(await Success("terrain.query", new {x=0,y=0})).Data!;
        Assert.That(result.Height, Is.EqualTo(actual.Height));
        Assert.That(actual.Height, Is.EqualTo(value).Within(0.08f));
    }

    [TestCase(-1, "playableGrid", 1)]
    [TestCase(16, "playableGrid", 1)]
    [TestCase(15, "playableGrid", 2)]
    [TestCase(0, "typo", 1)]
    [TestCase(1, "playableGrid", int.MaxValue)]
    public async Task InvalidRegion_IsRejected(int x, string space, int width)
    {
        var failed = await _executor.ExecuteAsync(Request("terrain.set_height", Height(240,x,space,width)));
        Assert.That(failed.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.Zero);
    }

    [Test]
    public async Task MapGrid_CanExplicitlyEditBorder()
    {
        await Success("terrain.set_height", Height(240,0,"mapGrid"));
        Assert.That(_session.Facade.GetTerrainHeight(0,0), Is.EqualTo(240));
    }

    [TestCase(-1, "playableGrid")]
    [TestCase(16, "playableGrid")]
    [TestCase(0, "typo")]
    [TestCase(10000, "world")]
    public async Task InvalidWaypointPosition_IsRejected(int x, string space)
    {
        var failed = await _executor.ExecuteAsync(Request("waypoints.place", new {x,y=0,space}));
        Assert.That(failed.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.Zero);
    }

    [Test]
    public async Task MalformedArguments_ReturnStructuredErrorIncludingBatchIndex()
    {
        var invalid = await _executor.ExecuteAsync(Request("terrain.query", new {x="oops",y=0}));
        Assert.That(invalid.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(invalid.Error?.Details?["path"], Is.EqualTo("$.x"));
        var batch = await _executor.ExecuteAsync(Request("batch.execute", new { commands = new object[] {
            new {command="terrain.set_height",arguments=Height(240)},
            new {command="terrain.set_height",arguments=new {height="oops"}}
        }}));
        Assert.That(batch.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(batch.Error?.Details?["index"], Is.EqualTo("1"));
        Assert.That(_session.Revision, Is.Zero);
    }

    [Test]
    public async Task Batch_RejectsUnsupportedChildVersionAndRollsBack()
    {
        var result = await _executor.ExecuteAsync(Request("batch.execute", new {commands=new[] {
            new {command="terrain.set_height",commandVersion=1,arguments=Height(240)},
            new {command="terrain.set_height",commandVersion=999,arguments=Height(180)}
        }}));
        Assert.That(result.Error?.Code, Is.EqualTo("UNSUPPORTED_VERSION"));
        Assert.That(result.Error?.Details?["index"], Is.EqualTo("1"));
        Assert.That(_session.Revision, Is.Zero);
        Assert.That(_session.Facade.GetTerrainHeight(2,2), Is.EqualTo(210));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Dirty_UsesMapContentAcrossCompressionAndReopen(bool compress)
    {
        await Success("terrain.set_height", Height(240));
        await Success("map.save", new {compress});
        await Success("history.undo");
        Assert.That(_session.Dirty, Is.True);
        await _session.CloseAsync();
        _session = await MapSessionManager.OpenAsync(_root, "Map");
        await Success("history.redo");
        Assert.That(_session.Dirty, Is.False);
        await Success("terrain.set_height", Height(240));
        Assert.That(_session.Dirty, Is.False, "An edit with identical serialized content must stay clean.");
    }

    [Test]
    public async Task CancellationDuringMutation_RestoresOriginalState()
    {
        using var cancel = new CancellationTokenSource();
        var registry = CommandRegistry.CreateDefault();
        registry.Register(new CancellingHandler(cancel));
        var executor = new CommandExecutor(registry);
        var before = Files();
        var result = await executor.ExecuteAsync(Request("test.cancel"), cancel.Token);
        Assert.That(result.Error?.Code, Is.EqualTo("CANCELLED"));
        Assert.That(_session.Revision, Is.Zero);
        Assert.That(_session.Dirty, Is.False);
        Assert.That(_session.Facade.GetTerrainHeight(2,2), Is.EqualTo(210));
        AssertFiles(before);
    }

    private sealed class CancellingHandler : ICommandHandler
    {
        private readonly CancellationTokenSource _source;
        public CancellingHandler(CancellationTokenSource source) => _source = source;
        public string Name => "test.cancel";
        public CommandEffect Effect => CommandEffect.Mutation;
        public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken cancellationToken)
        {
            context!.Session.Facade.SetTerrainHeight(2,2,330);
            _source.Cancel();
            return Task.FromResult<object?>(null);
        }
    }
}
