using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class CommandLoopTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string _tempDir = null!;
    private readonly CommandExecutor _executor = CommandExecutor.CreateDefault();

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "Ra3MapSharp-Automation-Cmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var mapName in new[] { "Alpha", "Beta", "Exported" })
        {
            if (MapSessionManager.TryGetOpen(_tempDir, mapName, out var session))
            {
                session.Dispose();
            }
        }

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public async Task CreateSetHeightSaveReopen_PersistsNormalizedHeight()
    {
        var created = await SucceedAsync(new CommandRequest
        {
            Command = "map.create",
            Arguments = Args(new
            {
                parentPath = _tempDir,
                mapName = "Alpha",
                playableWidth = 16,
                playableHeight = 12,
                border = 0
            })
        });
        var opened = (SessionOpenedData)created.Data!;
        Assert.That(opened.Revision, Is.EqualTo(0));

        var height = await SucceedAsync(Mutate(opened.SessionId, 0, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 1, y = 2, width = 3, height = 2 },
            height = 240f
        }));
        Assert.That(((SetHeightData)height.Data!).AffectedCells, Is.EqualTo(6));
        Assert.That(height.RevisionAfter, Is.EqualTo(1));

        var query = await SucceedAsync(new CommandRequest
        {
            SessionId = opened.SessionId,
            Command = "terrain.query",
            Arguments = Args(new { space = "playableGrid", x = 2, y = 3 })
        });
        Assert.That(((HeightQueryData)query.Data!).Height, Is.EqualTo(240f).Within(0.05f));

        await SucceedAsync(Mutate(opened.SessionId, 1, "map.save", new { compress = true }));

        MapSessionManager.TryGetBySessionId(opened.SessionId, out var live);
        await live!.CloseAsync();

        var reopened = await SucceedAsync(new CommandRequest
        {
            Command = "map.open",
            Arguments = Args(new { parentPath = _tempDir, mapName = "Alpha" })
        });
        var reopenedData = (SessionOpenedData)reopened.Data!;
        var queryAgain = await SucceedAsync(new CommandRequest
        {
            SessionId = reopenedData.SessionId,
            Command = "terrain.query",
            Arguments = Args(new { space = "playableGrid", x = 2, y = 3 })
        });
        Assert.That(((HeightQueryData)queryAgain.Data!).Height, Is.EqualTo(240f).Within(0.05f));
        var info = await SucceedAsync(new CommandRequest
        {
            SessionId = reopenedData.SessionId,
            Command = "map.info"
        });
        Assert.That(((MapInfoData)info.Data!).Dirty, Is.False);
        Assert.That(((MapInfoData)info.Data!).PlayableWidth, Is.EqualTo(16));
    }

    [Test]
    public async Task PlaceMoveDeleteWaypoint_UsesStableHandles()
    {
        var sessionId = await CreateAlphaAsync();
        var placed = await SucceedAsync(Mutate(sessionId, 0, "waypoints.place", new
        {
            name = "WP_A",
            space = "playableGrid",
            anchor = "center",
            x = 2,
            y = 3,
            z = 0
        }));
        var waypoint = (WaypointData)placed.Data!;
        Assert.That(waypoint.ObjectId, Is.EqualTo("wp-1"));
        Assert.That(waypoint.Name, Is.EqualTo("WP_A"));
        Assert.That(waypoint.X, Is.EqualTo(25f).Within(0.01f));
        Assert.That(waypoint.Y, Is.EqualTo(35f).Within(0.01f));

        var moved = await SucceedAsync(Mutate(sessionId, 1, "waypoints.move", new
        {
            objectId = waypoint.ObjectId,
            space = "playableGrid",
            x = 4,
            y = 1,
            z = 10
        }));
        var movedData = (WaypointData)moved.Data!;
        Assert.That(movedData.ObjectId, Is.EqualTo("wp-1"));
        Assert.That(movedData.X, Is.EqualTo(45f).Within(0.01f));
        Assert.That(movedData.Y, Is.EqualTo(15f).Within(0.01f));
        Assert.That(movedData.Z, Is.EqualTo(10f).Within(0.01f));

        await SucceedAsync(Mutate(sessionId, 2, "waypoints.delete", new { objectId = waypoint.ObjectId }));
        var info = await SucceedAsync(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        Assert.That(((MapInfoData)info.Data!).WaypointCount, Is.EqualTo(0));
    }

    [Test]
    public async Task BatchExecute_IsAtomicAndSingleRevision()
    {
        var sessionId = await CreateAlphaAsync();
        var batch = await SucceedAsync(Mutate(sessionId, 0, "batch.execute", new
        {
            commands = new object[]
            {
                new
                {
                    command = "terrain.set_height",
                    arguments = new
                    {
                        region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 2, height = 2 },
                        height = 240f
                    }
                },
                new
                {
                    command = "waypoints.place",
                    arguments = new { name = "WP_B", x = 1, y = 1, z = 0 }
                }
            }
        }));
        Assert.That(batch.RevisionAfter, Is.EqualTo(1));
        Assert.That(((BatchResultData)batch.Data!).Count, Is.EqualTo(2));

        var failed = await ExecuteAsync(Mutate(sessionId, 1, "batch.execute", new
        {
            commands = new object[]
            {
                new
                {
                    command = "terrain.set_height",
                    arguments = new
                    {
                        region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
                        height = 100f
                    }
                },
                new
                {
                    command = "waypoints.move",
                    arguments = new { objectId = "wp-missing", x = 0, y = 0 }
                }
            }
        }));
        Assert.That(failed.Status, Is.EqualTo("failed"));
        Assert.That(failed.Error!.Code, Is.EqualTo("OBJECT_NOT_FOUND"));

        var query = await SucceedAsync(new CommandRequest
        {
            SessionId = sessionId,
            Command = "terrain.query",
            Arguments = Args(new { x = 0, y = 0 })
        });
        Assert.That(((HeightQueryData)query.Data!).Height, Is.EqualTo(240f).Within(0.05f));
        var info = await SucceedAsync(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        Assert.That(((MapInfoData)info.Data!).Revision, Is.EqualTo(1));
        Assert.That(((MapInfoData)info.Data!).WaypointCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UndoRedo_RestoresHeightAndClearsRedoOnNewEdit()
    {
        var sessionId = await CreateAlphaAsync();
        var original = await QueryHeightAsync(sessionId, 0, 0);

        await SucceedAsync(Mutate(sessionId, 0, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
            height = 240f
        }));
        Assert.That(await QueryHeightAsync(sessionId, 0, 0), Is.EqualTo(240f).Within(0.05f));

        var undo = await SucceedAsync(Mutate(sessionId, 1, "history.undo", new { }));
        Assert.That(undo.RevisionAfter, Is.EqualTo(2));
        Assert.That(((HistoryData)undo.Data!).CanRedo, Is.True);
        Assert.That(await QueryHeightAsync(sessionId, 0, 0), Is.EqualTo(original).Within(0.05f));

        await SucceedAsync(Mutate(sessionId, 2, "history.redo", new { }));
        Assert.That(await QueryHeightAsync(sessionId, 0, 0), Is.EqualTo(240f).Within(0.05f));

        await SucceedAsync(Mutate(sessionId, 3, "history.undo", new { }));
        await SucceedAsync(Mutate(sessionId, 4, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
            height = 180f
        }));

        var redo = await ExecuteAsync(Mutate(sessionId, 5, "history.redo", new { }));
        Assert.That(redo.Status, Is.EqualTo("failed"));
        Assert.That(await QueryHeightAsync(sessionId, 0, 0), Is.EqualTo(180f).Within(0.05f));
    }

    [Test]
    public async Task ExpectedRevisionAndRequestIdDedup()
    {
        var sessionId = await CreateAlphaAsync();
        var first = Mutate(sessionId, 0, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
            height = 240f
        });
        first.RequestId = "req-1";
        await SucceedAsync(first);

        var retry = Mutate(sessionId, 0, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
            height = 240f
        });
        retry.RequestId = "req-1";
        var retried = await SucceedAsync(retry);
        Assert.That(retried.RevisionAfter, Is.EqualTo(1));

        var conflict = await ExecuteAsync(Mutate(sessionId, 0, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
            height = 200f
        }));
        Assert.That(conflict.Error!.Code, Is.EqualTo("REVISION_CONFLICT"));

        var sameIdDifferentArgs = Mutate(sessionId, 1, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 0, y = 0, width = 1, height = 1 },
            height = 100f
        });
        sameIdDifferentArgs.RequestId = "req-1";
        var idConflict = await ExecuteAsync(sameIdDifferentArgs);
        Assert.That(idConflict.Error!.Code, Is.EqualTo("REQUEST_ID_CONFLICT"));
    }

    [Test]
    public async Task OutOfRangeHeight_DoesNotChangeRevision()
    {
        var sessionId = await CreateAlphaAsync();
        var failed = await ExecuteAsync(Mutate(sessionId, 0, "terrain.set_height", new
        {
            region = new { kind = "rectangle", space = "playableGrid", x = 80, y = 0, width = 1, height = 1 },
            height = 240f
        }));
        Assert.That(failed.Status, Is.EqualTo("failed"));
        Assert.That(failed.Error!.Code, Is.EqualTo("INVALID_ARGUMENT"));
        var info = await SucceedAsync(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        Assert.That(((MapInfoData)info.Data!).Revision, Is.EqualTo(0));
        Assert.That(((MapInfoData)info.Data!).Dirty, Is.False);
    }

    private async Task<string> CreateAlphaAsync()
    {
        var created = await SucceedAsync(new CommandRequest
        {
            Command = "map.create",
            Arguments = Args(new
            {
                parentPath = _tempDir,
                mapName = "Alpha",
                playableWidth = 16,
                playableHeight = 16,
                border = 0
            })
        });
        return ((SessionOpenedData)created.Data!).SessionId;
    }

    private async Task<float> QueryHeightAsync(string sessionId, int x, int y)
    {
        var query = await SucceedAsync(new CommandRequest
        {
            SessionId = sessionId,
            Command = "terrain.query",
            Arguments = Args(new { space = "playableGrid", x, y })
        });
        return ((HeightQueryData)query.Data!).Height;
    }

    private static CommandRequest Mutate(string sessionId, int expectedRevision, string command, object arguments)
    {
        return new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = expectedRevision,
            Command = command,
            Arguments = Args(arguments)
        };
    }

    private async Task<CommandResult> SucceedAsync(CommandRequest request)
    {
        var result = await _executor.ExecuteAsync(request);
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
        return result;
    }

    private Task<CommandResult> ExecuteAsync(CommandRequest request)
    {
        return _executor.ExecuteAsync(request);
    }

    private static JsonElement Args(object value)
    {
        return JsonSerializer.SerializeToElement(value, JsonOptions);
    }
}
