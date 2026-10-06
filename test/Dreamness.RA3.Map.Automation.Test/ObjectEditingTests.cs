using System.Text.Json;
using System.Text.Json.Nodes;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Automation.Test;

public class ObjectEditingTests
{
    private string _root = null!;
    private MapSession _session = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "RA3-Objects-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 16, 12, 4);
    }

    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }

    private Task<CommandResult> Run(string command, object arguments) => _executor.ExecuteAsync(new CommandRequest
    {
        SessionId = _session.SessionId, ExpectedRevision = _session.Revision, Command = command,
        Arguments = JsonSerializer.SerializeToElement(arguments)
    });

    private static JsonElement Data(CommandResult result)
    {
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return JsonSerializer.SerializeToElement(result.Data, Json);
    }

    private async Task<string> Place(float x = 2) => Data(await Run("objects.place",
        new { typeName = "CC_Tree01", x, y = 3, z = 12, angleRadians = 1.5f, name = "Tree" }))
        .GetProperty("objectId").GetString()!;

    [Test]
    public async Task ResourceSettingsSurviveHistoryAndSaveWithoutMovingObject()
    {
        var placed = Data(await Run("objects.place", new { typeName = "OreNode", x = 2, y = 3,
            ownerTeam = "PlyrNeutral/teamPlyrNeutral", settings = new { objectInitialHealth = 75, objectIndestructible = true } }));
        var id = placed.GetProperty("objectId").GetString();
        var changed = Data(await Run("objects.configure", new { objectId = id, name = "MineA",
            settings = new { objectEnabled = false } }));
        Assert.That(changed.GetProperty("x").GetSingle(), Is.EqualTo(25));
        Assert.That(changed.GetProperty("settings").GetProperty("objectInitialHealth").GetInt32(), Is.EqualTo(75));
        Data(await Run("history.undo", new { }));
        Assert.That(_session.Facade.GetUnitObjects().Single().Properties.GetProperty<bool>("objectEnabled"), Is.True);
        Data(await Run("history.redo", new { }));
        Data(await Run("map.save", new { }));
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        var obj = _session.Facade.GetUnitObjects().Single();
        Assert.That(obj.ObjName, Is.EqualTo("MineA"));
        Assert.That(obj.BelongToTeam, Is.EqualTo("PlyrNeutral/teamPlyrNeutral"));
        Assert.That(obj.Properties.GetProperty<bool>("objectEnabled"), Is.False);
        Assert.That(obj.Properties.GetProperty<bool>("objectIndestructible"), Is.True);
        Assert.That(obj.Properties.PropertiesDict["objectInitialHealth"].propertyType.ToString(), Is.EqualTo("intType"));
    }

    [TestCase("{\"ownerTeam\":\"Missing/teamMissing\"}", "INVALID_OWNER")]
    [TestCase("{\"settings\":{\"objectEnabled\":1}}", "INVALID_ARGUMENT")]
    [TestCase("{\"settings\":{\"uniqueID\":\"forged\"}}", "INVALID_ARGUMENT")]
    [TestCase("{\"settings\":{\"objectInitialHealth\":1.5}}", "INVALID_ARGUMENT")]
    public async Task InvalidConfigurationRollsBackEntireTransaction(string patch, string code)
    {
        var id = await Place();
        var before = await _session.CaptureSnapshotAsync();
        var args = JsonNode.Parse(patch)!;
        args["objectId"] = id;
        args["name"] = "MustRollback";
        var result = await Run("objects.configure", args);
        Assert.That(result.Error?.Code, Is.EqualTo(code));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
        Assert.That(_session.Revision, Is.EqualTo(before.Revision));
    }

    [Test]
    public async Task ObjectProtectionCoversAttributeChanges()
    {
        var id = await Place();
        Data(await Run("protections.add", new { id = "keep", region = new { x = 1, y = 1, width = 5, height = 5 }, layers = new[] { "objects" } }));
        var before = await _session.CaptureSnapshotAsync();
        var result = await Run("objects.configure", new { objectId = id, settings = new { objectPowered = false } });
        Assert.That(result.Status, Is.EqualTo("failed"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(before.ContentHash));
    }

    [Test]
    public async Task ProtocolRadiansMatchFacadeDegreesAndSerializedRadians()
    {
        var placed = Data(await Run("objects.place", new { typeName = "CC_Tree01", x = 2, y = 3, angleRadians = MathF.PI / 2 }));
        var id = placed.GetProperty("objectId").GetString();
        Assert.That(_session.Facade.GetUnitObjects().Single().Angle, Is.EqualTo(90).Within(.0001));
        // Data is the parsed binary payload: XYZ floats followed by the rotation float.
        Assert.That(BitConverter.ToSingle(_session.Facade.GetUnitObjects().Single().Obj.Data, 12),
            Is.EqualTo(MathF.PI / 2).Within(.00001));
        Data(await Run("objects.move", new { objectId = id, x = 4, y = 5, angleRadians = -MathF.PI / 4 }));
        Assert.That(_session.Facade.GetUnitObjects().Single().Angle, Is.EqualTo(-45).Within(.0001));
        Data(await Run("map.save", new { }));
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        var item = Data(await Run("objects.query", new { objectId = id })).GetProperty("items")[0];
        Assert.That(item.GetProperty("angleRadians").GetSingle(), Is.EqualTo(-MathF.PI / 4).Within(.00001));
    }

    [Test]
    public async Task ImportedDegreeOrientationIsQueriedInRadians()
    {
        _session.Dispose();
        var map = Ra3MapFacade.Open(Path.Combine(_root, "Test", "Test.map"));
        map.AddUnitObject("CC_Tree01", 20, 20).Angle = 45;
        map.Save();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        var item = Data(await Run("objects.query", new { })).GetProperty("items")[0];
        Assert.That(item.GetProperty("angleRadians").GetSingle(), Is.EqualTo(MathF.PI / 4).Within(.00001));
    }

    [Test]
    public async Task PlaceMoveQueryDeleteAndHistoryPreserveIdentity()
    {
        var id = await Place();
        var item = Data(await Run("objects.query", new { objectId = id })).GetProperty("items")[0];
        Assert.That(item.GetProperty("x").GetSingle(), Is.EqualTo(25));
        Data(await Run("objects.move", new { objectId = id, x = 7, y = 8 }));
        Assert.That(_session.Facade.GetUnitObjects()[0].Position.Z, Is.EqualTo(12));
        Data(await Run("objects.delete", new { objectId = id }));
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        Data(await Run("history.undo", new { }));
        item = Data(await Run("objects.query", new { })).GetProperty("items")[0];
        Assert.That(item.GetProperty("objectId").GetString(), Is.EqualTo(id));
        Assert.That(item.GetProperty("x").GetSingle(), Is.EqualTo(75));
        Data(await Run("history.redo", new { }));
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        Data(await Run("history.undo", new { }));
        Assert.That(await Place(6), Is.Not.EqualTo(id));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CloseReopenPreservesHandlesHistoryAndMonotonicAllocation(bool save)
    {
        var deletedId = await Place();
        Data(await Run("objects.delete", new { objectId = deletedId }));
        var id = await Place(8);
        if (save) Data(await Run("map.save", new { }));
        var revision = _session.Revision;
        _session.Dispose();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Assert.That(_session.Revision, Is.EqualTo(revision));
        Assert.That(Data(await Run("objects.query", new { })).GetProperty("items")[0]
            .GetProperty("objectId").GetString(), Is.EqualTo(id));
        Data(await Run("history.undo", new { }));
        Assert.That(_session.Facade.GetUnitObjects(), Is.Empty);
        var next = await Place(7);
        Assert.That(next, Is.Not.EqualTo(id).And.Not.EqualTo(deletedId));
    }

    [Test]
    public async Task FailedBatchRollsBackObjectsAndHandleAllocation()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var result = await Run("batch.execute", new { commands = new object[] {
            new { command = "objects.place", arguments = new { typeName = "CC_Tree01", x = 2, y = 3 } },
            new { command = "objects.delete", arguments = new { objectId = "obj-missing" } }
        }});
        Assert.That(result.Status, Is.EqualTo("failed"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
        Assert.That(await Place(), Is.EqualTo("obj-1"));
    }

    [Test]
    public async Task DuplicateGameIdsAreIndependentlyAddressable()
    {
        _session.Dispose();
        var map = Ra3MapFacade.Open(Path.Combine(_root, "Test", "Test.map"));
        foreach (var x in new[] { 20, 40 })
        {
            var obj = map.AddUnitObject("CC_Tree01", x, 20);
            obj.Properties.PutProperty("uniqueID", "duplicate");
        }
        map.Save();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        var items = Data(await Run("objects.query", new { })).GetProperty("items");
        var second = items[1].GetProperty("objectId").GetString();
        Data(await Run("objects.delete", new { objectId = second }));
        Assert.That(_session.Facade.GetUnitObjects().Single().Position.X, Is.EqualTo(20));
    }

    [Test]
    public async Task LegacyHistoryMigratesOnlyUnchangedObjectSequences()
    {
        // Seed an imported object so it is present in the baseline and all old snapshots.
        _session.Dispose();
        var path = Path.Combine(_root, "Test", "Test.map");
        var map = Ra3MapFacade.Open(path);
        map.AddUnitObject("CC_Tree01", 20, 20);
        map.Save();
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        Data(await Run("waypoints.place", new { name = "Marker", x = 1, y = 1 }));
        _session.Dispose();
        var indexPath = AutomationLayout.Resolve(_root, "Test").HistoryIndexFilePath;
        var index = JsonNode.Parse(File.ReadAllText(indexPath))!;
        index["schemaVersion"] = 1;
        foreach (var entry in index["entries"]!.AsArray()) entry!.AsObject().Remove("unitObjectIds");
        File.WriteAllText(indexPath, index.ToJsonString());
        _session = await MapSessionManager.OpenAsync(_root, "Test");
        var id = Data(await Run("objects.query", new { })).GetProperty("items")[0].GetProperty("objectId").GetString();
        Data(await Run("history.undo", new { }));
        Assert.That(Data(await Run("objects.query", new { })).GetProperty("items")[0].GetProperty("objectId").GetString(), Is.EqualTo(id));
        Assert.That(JsonNode.Parse(File.ReadAllText(indexPath))!["schemaVersion"]!.GetValue<int>(), Is.EqualTo(9));
    }

    [TestCase("*Waypoints/Waypoint")]
    [TestCase("")]
    [TestCase("bad name")]
    public async Task RejectSpecialOrInvalidObjectTypes(string typeName)
    {
        var result = await Run("objects.place", new { typeName, x = 1, y = 1 });
        Assert.That(result.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(_session.Revision, Is.EqualTo(0));
    }

    [Test]
    public async Task LegacyMigrationRejectsChangedObjectsWithoutWritingHistory()
    {
        await Place();
        _session.Dispose();
        var path = AutomationLayout.Resolve(_root, "Test").HistoryIndexFilePath;
        var index = JsonNode.Parse(File.ReadAllText(path))!;
        index["schemaVersion"] = 1;
        foreach (var entry in index["entries"]!.AsArray()) entry!.AsObject().Remove("unitObjectIds");
        var original = index.ToJsonString();
        File.WriteAllText(path, original);
        var error = Assert.ThrowsAsync<AutomationException>(async () => await MapSessionManager.OpenAsync(_root, "Test"));
        Assert.That(error!.Code, Is.EqualTo("HISTORY_MIGRATION_REQUIRED"));
        Assert.That(File.ReadAllText(path), Is.EqualTo(original));
    }

    [Test]
    public async Task CleanWorkingCopyTamperingIsRejected()
    {
        await Place();
        Data(await Run("map.save", new { }));
        _session.Dispose();
        var path = AutomationLayout.Resolve(_root, "Test").WorkingMapFilePath;
        var map = Ra3MapFacade.Open(path);
        map.AddUnitObject("CC_Tree01", 90, 20);
        map.Save();
        var error = Assert.ThrowsAsync<AutomationException>(async () => await MapSessionManager.OpenAsync(_root, "Test"));
        Assert.That(error!.Code, Is.EqualTo("WORKSPACE_CONFLICT"));
    }
}
