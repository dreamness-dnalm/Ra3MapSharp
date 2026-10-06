using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

/// <summary>
/// Automation 命令使用范例。可直接对照复制到调用方；断言只用来保证范例本身可运行。
///
/// 约定：
/// - 写操作（改图、保存、撤销）必须带 expectedRevision，且等于当前 revision。
/// - 查询（map.info、terrain.query）不需要 expectedRevision。
/// - 默认坐标空间是 playableGrid：可玩区左下角为 (0,0)。
/// - 路径点默认 anchor=center，世界坐标 = (格子 + 0.5) * 10。
/// </summary>
[Category("UsageExamples")]
public class UsageExamplesTests
{
    private string _mapsRoot = null!;
    private CommandExecutor _executor = null!;

    [SetUp]
    public void SetUp()
    {
        _mapsRoot = Path.Combine(Path.GetTempPath(), "Ra3Automation-Usage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mapsRoot);
        _executor = CommandExecutor.CreateDefault();
    }

    // [TearDown]
    // public void TearDown()
    // {
    //     foreach (var name in new[] { "DemoIsland", "DemoIslandCopy" })
    //     {
    //         if (MapSessionManager.TryGetOpen(_mapsRoot, name, out var session))
    //         {
    //             session.Dispose();
    //         }
    //     }
    //
    //     if (Directory.Exists(_mapsRoot))
    //     {
    //         Directory.Delete(_mapsRoot, true);
    //     }
    // }

    [Test]
    public async Task Example_CreateOpenSaveAndQuery()
    {
        // 1. 新建一张空地图（不随机放置出生点）。
        var create = await Run(new CommandRequest
        {
            Command = "map.create",
            Arguments = Json(
                new
                {
                    parentPath = _mapsRoot,
                    mapName = "DemoIsland",
                    playableWidth = 32,
                    playableHeight = 24,
                    border = 8
                })
        });
        var session = (SessionOpenedData)create.Data!;
        // session.SessionId 用于后续命令；Revision 从 0 开始。
        Assert.That(session.Revision, Is.EqualTo(0));
        Assert.That(File.Exists(session.UserMapFilePath), Is.True);

        // 2. 查看地图摘要。
        var info = await Run(new CommandRequest
        {
            SessionId = session.SessionId,
            Command = "map.info"
        });
        var map = (MapInfoData)info.Data!;
        Assert.That(map.PlayableWidth, Is.EqualTo(32));
        Assert.That(map.PlayableHeight, Is.EqualTo(24));
        Assert.That(map.Border, Is.EqualTo(8));
        Assert.That(map.MapWidth, Is.EqualTo(48)); // 可玩区 + 两侧边界
        Assert.That(map.Dirty, Is.False);

        // 3. 保存当前工作副本到用户 .map（此时还没有未保存编辑）。
        var saved = await Run(new CommandRequest
        {
            SessionId = session.SessionId,
            ExpectedRevision = map.Revision, // 0
            Command = "map.save",
            Arguments = Json(new { compress = true })
        });
        Assert.That(((SaveMapData)saved.Data!).UserMapFilePath, Is.EqualTo(session.UserMapFilePath));

        // 4. 关闭后再打开：得到新的 sessionId，mapId 不变。
        MapSessionManager.TryGetBySessionId(session.SessionId, out var live);
        await live!.CloseAsync();

        var reopen = await Run(new CommandRequest
        {
            Command = "map.open",
            Arguments = Json(new { parentPath = _mapsRoot, mapName = "DemoIsland" })
        });
        var reopened = (SessionOpenedData)reopen.Data!;
        Assert.That(reopened.MapId, Is.EqualTo(session.MapId));
        Assert.That(reopened.SessionId, Is.Not.EqualTo(session.SessionId));
    }

    [Test]
    public async Task Example_SetHeightPlaceMoveDeleteWaypoint()
    {
        var sessionId = await CreateDemoIsland();
        var revision = 0;

        // 在可玩区 [4,6) x [8,11) 抬高一块台地。半开区间，共 2 * 3 = 6 格。
        var setHeight = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = revision,
            Command = "terrain.set_height",
            Arguments = Json(new
            {
                region = new
                {
                    kind = "rectangle",
                    space = "playableGrid",
                    x = 4,
                    y = 8,
                    width = 2,
                    height = 3
                },
                height = 240f
            })
        });
        revision = setHeight.RevisionAfter!.Value;
        Assert.That(((SetHeightData)setHeight.Data!).AffectedCells, Is.EqualTo(6));

        // 读回一个格子。playableGrid (4,8) 对应含边界的 mapGrid (12,16)。
        var query = await Run(new CommandRequest
        {
            SessionId = sessionId,
            Command = "terrain.query",
            Arguments = Json(new { space = "playableGrid", x = 4, y = 8 })
        });
        var cell = (HeightQueryData)query.Data!;
        Assert.That(cell.MapX, Is.EqualTo(12));
        Assert.That(cell.MapY, Is.EqualTo(16));
        Assert.That(cell.Height, Is.EqualTo(240f).Within(0.05f));

        // 放置路径点。center 锚点：格子 (5, 9) -> 世界坐标 (55, 95)。
        var placed = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = revision,
            Command = "waypoints.place",
            Arguments = Json(new
            {
                name = "Lookout",
                space = "playableGrid",
                anchor = "center",
                x = 5,
                y = 9,
                z = 0
            })
        });
        revision = placed.RevisionAfter!.Value;
        var waypoint = (WaypointData)placed.Data!;
        Assert.That(waypoint.ObjectId, Is.EqualTo("wp-1"));
        Assert.That(waypoint.X, Is.EqualTo(55f).Within(0.01f));
        Assert.That(waypoint.Y, Is.EqualTo(95f).Within(0.01f));

        // 用返回的 objectId 移动。不要用可能重复的 UniqueId / 名称当主键。
        var moved = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = revision,
            Command = "waypoints.move",
            Arguments = Json(new
            {
                objectId = waypoint.ObjectId,
                space = "playableGrid",
                x = 10,
                y = 10,
                z = 0
            })
        });
        revision = moved.RevisionAfter!.Value;
        Assert.That(((WaypointData)moved.Data!).X, Is.EqualTo(105f).Within(0.01f));

        var deleted = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = revision,
            Command = "waypoints.delete",
            Arguments = Json(new { objectId = waypoint.ObjectId })
        });
        Assert.That(deleted.RevisionAfter, Is.EqualTo(revision + 1));

        var info = await Run(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        Assert.That(((MapInfoData)info.Data!).WaypointCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Example_BatchThenUndoThenSave()
    {
        var sessionId = await CreateDemoIsland();

        // 一批命令共用一次快照、只增加 1 个 Revision。
        // 不允许放入 save / undo / 另一个 batch。
        var batch = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = 0,
            Command = "batch.execute",
            Arguments = Json(new
            {
                commands = new object[]
                {
                    new
                    {
                        command = "terrain.set_height",
                        arguments = new
                        {
                            region = new
                            {
                                kind = "rectangle",
                                space = "playableGrid",
                                x = 0,
                                y = 0,
                                width = 4,
                                height = 4
                            },
                            height = 240f
                        }
                    },
                    new
                    {
                        command = "waypoints.place",
                        arguments = new
                        {
                            name = "Player_1_Start",
                            x = 2,
                            y = 2,
                            z = 0
                        }
                    },
                    new
                    {
                        command = "waypoints.place",
                        arguments = new
                        {
                            name = "Player_2_Start",
                            x = 20,
                            y = 18,
                            z = 0
                        }
                    }
                }
            })
        });
        Assert.That(batch.RevisionBefore, Is.EqualTo(0));
        Assert.That(batch.RevisionAfter, Is.EqualTo(1));
        Assert.That(((BatchResultData)batch.Data!).Count, Is.EqualTo(3));

        var afterBatch = await Run(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        var info = (MapInfoData)afterBatch.Data!;
        Assert.That(info.Dirty, Is.True);
        Assert.That(info.CanUndo, Is.True);
        Assert.That(info.WaypointCount, Is.EqualTo(2));

        // 回滚整批编辑。Revision 继续递增（1 -> 2），不会回到 0。
        var undo = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = 1,
            Command = "history.undo"
        });
        Assert.That(undo.RevisionAfter, Is.EqualTo(2));
        var undone = (HistoryData)undo.Data!;
        Assert.That(undone.CanUndo, Is.False);
        Assert.That(undone.CanRedo, Is.True);

        var afterUndo = await Run(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        Assert.That(((MapInfoData)afterUndo.Data!).WaypointCount, Is.EqualTo(0));

        // 需要的话可以 history.redo；这里演示重做后再保存。
        var redo = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = 2,
            Command = "history.redo"
        });
        Assert.That(redo.RevisionAfter, Is.EqualTo(3));

        await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = 3,
            Command = "map.save",
            Arguments = Json(new { compress = true })
        });

        // 另存一份拷贝，不切换当前工作区。
        var saveAs = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = 3,
            Command = "map.save_as",
            Arguments = Json(new
            {
                parentPath = _mapsRoot,
                mapName = "DemoIslandCopy",
                compress = true,
                overwrite = false
            })
        });
        Assert.That(File.Exists(((SaveMapData)saveAs.Data!).UserMapFilePath), Is.True);
    }

    [Test]
    public async Task Example_FailedCommandDoesNotAdvanceRevision()
    {
        var sessionId = await CreateDemoIsland();

        // 越界区域会失败，地图与 revision 都不变，可以稍后用同一个 expectedRevision 重试。
        var failed = await _executor.ExecuteAsync(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = 0,
            Command = "terrain.set_height",
            Arguments = Json(new
            {
                region = new
                {
                    kind = "rectangle",
                    space = "playableGrid",
                    x = 999,
                    y = 0,
                    width = 1,
                    height = 1
                },
                height = 240f
            })
        });
        Assert.That(failed.Status, Is.EqualTo("failed"));
        Assert.That(failed.Error!.Code, Is.EqualTo("INVALID_ARGUMENT"));

        var info = await Run(new CommandRequest { SessionId = sessionId, Command = "map.info" });
        Assert.That(((MapInfoData)info.Data!).Revision, Is.EqualTo(0));
        Assert.That(((MapInfoData)info.Data!).Dirty, Is.False);
    }

    private async Task<string> CreateDemoIsland()
    {
        var created = await Run(new CommandRequest
        {
            Command = "map.create",
            Arguments = Json(new
            {
                parentPath = _mapsRoot,
                mapName = "DemoIsland",
                playableWidth = 32,
                playableHeight = 24,
                border = 8
            })
        });
        return ((SessionOpenedData)created.Data!).SessionId;
    }

    private async Task<CommandResult> Run(CommandRequest request)
    {
        var result = await _executor.ExecuteAsync(request);
        Assert.That(
            result.Status,
            Is.EqualTo("succeeded"),
            result.Error == null ? "" : $"{result.Error.Code}: {result.Error.Message}");
        return result;
    }

    private static JsonElement Json(object value)
    {
        return JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
