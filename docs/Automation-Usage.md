# Automation 库使用说明

本文面向在 C# 程序中集成 `Dreamness.RA3.Map.Automation` 的开发者，按当前源码说明如何创建地图、调用命令、管理修订和保存工程。架构背景见 [Automation-Design.md](Automation-Design.md)；通过 JSONL、MCP 或 `AgentRuntime` 调用时，另见 [Agent-Usage.md](Agent-Usage.md) 和 [MCP-Usage.md](MCP-Usage.md)。设计文档中的规划接口不代表已经实现。

## 1. 定位与环境

Automation 在 Facade 上提供带会话、事务和历史的地图编辑命令。通常使用 `CommandExecutor` 调用；`CommandRegistry` 注册处理器；`MapSessionManager` 创建、打开和查找会话。`AgentRuntime` 位于上层 Agent 项目，负责协议、作业与渲染装配。

项目目标框架为 `net6.0`，仓库使用 Windows、PowerShell 和 .NET 6 SDK。基本编辑不需要启动 WorldBuilder；真实鸟瞰渲染需要 Agent 宿主和外部 `WbLauncher.exe`。

在调用方项目中引用：

```xml
<ItemGroup>
  <!-- 按调用方项目位置调整相对路径 -->
  <ProjectReference Include="..\src\Dreamness.RA3.Map.Automation\Dreamness.RA3.Map.Automation.csproj" />
</ItemGroup>
```

在仓库根目录构建：

```powershell
dotnet build src/Dreamness.RA3.Map.Automation/Dreamness.RA3.Map.Automation.csproj
```

## 2. 最小完整示例

下面可作为 .NET 6 控制台项目的 `Program.cs`。示例创建独立临时目录，生成地图、批量抬高地形并放置路径点，读回高度，撤销、重做并保存，最后释放会话。临时目录保留，便于检查产物。

```csharp
using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

var executor = CommandExecutor.CreateDefault();
var mapsRoot = Path.Combine(Path.GetTempPath(), "Ra3Automation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(mapsRoot);

JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

async Task<CommandResult> Run(CommandRequest request)
{
    var result = await executor.ExecuteAsync(request);
    if (result.Status != "succeeded")
        throw new InvalidOperationException($"{result.Error?.Code}: {result.Error?.Message}");
    return result;
}

var created = await Run(new CommandRequest
{
    Command = "map.create",
    Arguments = Json(new
    {
        parentPath = mapsRoot,
        mapName = "DemoIsland",
        playableWidth = 32,
        playableHeight = 24,
        border = 8
    })
});
var opened = (SessionOpenedData)created.Data!;
var sessionId = opened.SessionId;
var revision = opened.Revision;

if (!MapSessionManager.TryGetBySessionId(sessionId, out var session))
    throw new InvalidOperationException("创建后未找到会话。");

try
{
    // 一批编辑作为一个历史步骤提交。
    var batch = await Run(new CommandRequest
    {
        SessionId = sessionId,
        ExpectedRevision = revision,
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
                        region = new { kind = "rectangle", space = "playableGrid",
                            x = 4, y = 8, width = 2, height = 3 },
                        height = 240f
                    }
                },
                new
                {
                    command = "waypoints.place",
                    arguments = new { name = "Lookout", space = "playableGrid",
                        anchor = "center", x = 5, y = 9, z = 0 }
                }
            }
        })
    });
    revision = batch.RevisionAfter!.Value;

    var queried = await Run(new CommandRequest
    {
        SessionId = sessionId,
        Command = "terrain.query",
        Arguments = Json(new { space = "playableGrid", x = 4, y = 8 })
    });
    var cell = (HeightQueryData)queried.Data!;
    Console.WriteLine($"高度：{cell.Height}；mapGrid：({cell.MapX}, {cell.MapY})");

    foreach (var command in new[] { "history.undo", "history.redo" })
    {
        var result = await Run(new CommandRequest
        {
            SessionId = sessionId,
            ExpectedRevision = revision,
            Command = command
        });
        revision = result.RevisionAfter!.Value;
    }

    var saved = await Run(new CommandRequest
    {
        SessionId = sessionId,
        ExpectedRevision = revision,
        Command = "map.save",
        Arguments = Json(new { compress = true })
    });
    Console.WriteLine(((SaveMapData)saved.Data!).UserMapFilePath);
}
finally
{
    await session.CloseAsync();
}
```

新建地图不会自动生成出生点。示例中的 `Lookout` 是普通路径点；对战出生点应使用 `starts.place`。更多可运行示例见 [UsageExamplesTests.cs](../test/Dreamness.RA3.Map.Automation.Test/UsageExamplesTests.cs)。

## 3. 请求、结果与修订

### 请求字段

| C# 字段 | 用途 |
| --- | --- |
| `RequestId` | 默认自动生成 GUID；重发同一请求时保留原值 |
| `SessionId` | `map.create/open` 不需要；会话内命令必须提供真实会话 ID |
| `ExpectedRevision` | Mutation、History、Export 命令必须等于当前修订 |
| `Command` | 精确命令名，区分大小写 |
| `CommandVersion` | 默认 1；当前仅支持 1 |
| `Arguments` | `JsonElement`；示例使用小驼峰字段名 |

直接调用库不支持 Agent 的 `sessionId="$current"` 别名。它也不经过 MCP 参数 schema 校验，而由命令处理器检查参数。

### 结果处理

先检查 `Status`，成功为 `succeeded`，失败为 `failed`。成功时读取 `Data`、`RevisionBefore` 和 `RevisionAfter`；失败时读取 `Error.Code/Message/Details/Retryable`。失败结果的修订字段可能为空，不要据此更新本地修订。

部分 `Data` 是公开 DTO，可直接转换，例如 `SessionOpenedData`、`MapInfoData`、`HeightQueryData`、`WaypointData`、`BatchResultData`、`SaveMapData`。部分命令返回匿名对象，可先用 `JsonSerializer.SerializeToElement(result.Data)` 转成 JSON 再读取。默认序列化保留 C# 属性大小写；需要 JSON 协议的小驼峰输出时指定 `JsonNamingPolicy.CamelCase`。

`ExecuteAsync` 接受 `CancellationToken`，会将已捕获的取消转换为 `CANCELLED`。库会转换常见 Automation、IO 和参数异常，但并非所有异常都会变成失败结果；应用边界仍应处理意外异常。

### 修订规则

- 新地图从修订 0 开始；重新打开已有工程应以返回值为准。
- 一次成功 Mutation 增加一个修订；一个批次只增加一次。
- 查询和保存/导出不增加修订，保存/导出仍需 `ExpectedRevision`。
- 撤销与重做产生新修订。例如编辑后为 1，撤销后为 2，重做后为 3。
- 失败的编辑事务回滚，不推进修订。
- 同一会话的命令通过执行锁串行化；多个调用者仍需用修订处理竞争。

发生 `REVISION_CONFLICT` 后，用 `map.info` 重新读取状态、重新判断编辑意图，再创建新请求。不要简单循环替换版本并强行重试。

### 请求去重

会话内非 Query 命令按 `RequestId` 和请求指纹缓存结果。相同 ID、相同请求返回缓存结果；相同 ID、不同命令/参数/修订返回 `REQUEST_ID_CONFLICT`。指纹包含参数的原始 JSON 文本，因此空白或属性顺序改变也可能被视为不同请求。

部分失败结果同样会缓存。修正参数或修订后必须换新的 `RequestId`；原样重传才复用旧 ID。Query 和创建/打开会话不使用此机制，去重也不能作为跨进程重启的幂等保证。

## 4. 会话、工作副本和保存

`map.open` 的参数是 `parentPath` 与 `mapName`，目标文件按以下结构定位：

```text
<parentPath>/<mapName>/
  <mapName>.map
  .automation/
    MapInfo.json
    Workspace.json
    Working/current.map
    Snapshots/<历史索引>.map
    History/history.jsonl
    History/index.json
    workspace.lock
```

`MapId` 标识工程，`SessionId` 标识本次打开。关闭再开会得到新的 `SessionId`。同一地图只允许一个写工作区，重复打开或跨进程锁冲突会失败；创建不同 executor 不会绕开全局会话管理。

编辑提交到工作副本和历史，用户 `.map` 由 `map.save` 更新。`map.info` 的 `Dirty` 表示地图/保护信息变化，`DesignDirty` 表示设计变化，`HasUnexportedChanges` 汇总两者；同时查看 `CanUndo/CanRedo`。只检查 `Dirty` 可能漏掉设计规格变更。

| 命令 | 保存内容与用途 |
| --- | --- |
| `map.save` | 保存到原用户地图，同步工程保存状态；`compress=true` 默认 |
| `map.save_as` | `parentPath/mapName` 导出地图文件；`overwrite=false` 默认，不切换当前会话，不携带完整工程元数据 |
| `map.export_project` | 导出当前地图、附属文件、句柄、保护区、设计与完整可达历史，生成独立 MapId |
| `map.export_package` | 导出当前地图与附属文件，排除 `.automation`，适合交付文件 |

工程/包导出要求目标目录不存在且不在源工程内部；导出不替源工程执行保存。附属脚本内容不会因地图改名而自动重写。

命令注册表没有 `map.close`。直接集成时用 `MapSessionManager.TryGetBySessionId` 找到会话并 `await session.CloseAsync()`，或调用 `Dispose()`。默认关闭保留未保存工作副本，不自动保存到用户地图；`CloseAsync(discard: true)` 删除工作副本并清空历史、重置修订，不适合只想释放锁的情况。保存后重开且源文件未变时，历史、修订和句柄可恢复。

外部修改干净用户地图后重开会建立新基线，应重新查询句柄；未保存工作区与外部改图冲突时会拒绝打开。不要手工修改 `.automation` 来解决冲突，先备份工程并明确要保留哪份状态。

## 5. 坐标与区域

| `space` | 原点与单位 |
| --- | --- |
| `playableGrid`（默认） | 可玩区左下角，单位为格 |
| `mapGrid` | 包含边界的完整地图左下角，单位为格 |
| `world` | 可玩区原点的世界坐标，1 格 = 10 世界单位 |

边界宽度为 `border` 时，地形格满足 `mapX=playableX+border`、`mapY=playableY+border`。对象/路径点使用 `center` 默认锚点：`worldX=(playableX+0.5)*10`；`gridPoint` 为 `worldX=playableX*10`，Y 同理。`world` 输入直接使用原值。

例如 border=8 时，playableGrid `(5,9)` 对应 mapGrid `(13,17)`；center 锚点的世界位置为 `(55,95)`。对象结果位置通常为世界坐标，不要当格坐标再次传入默认空间。朝向字段 `angleRadians` 使用弧度；Z 为对象原始参数，不自动贴地。

矩形：

```json
{"kind":"rectangle","space":"playableGrid","x":4,"y":8,"width":2,"height":3}
```

选中半开区间 `[4,6) × [8,11)`，共 6 格。圆形：

```json
{"kind":"circle","space":"playableGrid","centerX":16,"centerY":12,"radius":6}
```

圆形按格心选中。区域需完整落在指定空间内，不静默裁剪；地形操作使用网格空间，不支持 world。具体命令支持的区域类型见下表。

## 6. 常用编辑命令

以下为参数速查，复杂结构以处理器源码和 [command-schemas.json](../src/Dreamness.RA3.Map.Agent/Protocol/command-schemas.json) 对照确认。schema 还包含宿主命令，不能全部传给 Automation executor。

| 命令 | 主要 arguments |
| --- | --- |
| `map.create` | parentPath、mapName、playableWidth、playableHeight；border=8、defaultTexture=Dirt_Yucatan03、compress=true |
| `map.open` / `map.info` | open：parentPath、mapName；info：无 |
| `terrain.query` | x、y、space=playableGrid |
| `terrain.set_height` | region 矩形、height |
| `terrain.sculpt` | region 矩形/圆、mode=set/raise、value、falloff=0（0–1） |
| `terrain.smooth` | region 矩形/圆、iterations=1（1–32）、strength=1（0–1） |
| `terrain.ramp` | polyline（2–64 个连续可玩格坐标）、startHeight、endHeight；widthCells=4、transitionCells=2、maxSlopeDegrees=35 |
| `terrain.analyze` | profile 必填；points 可选，进行静态地形连通性分析 |
| `terrain.rebuild_passability` | maxSlopeDegrees=45、expandCardinalHalo=true |
| `textures.list` | query、offset=0、limit=50（1–200），需要会话 |
| `texture.paint` | region 矩形/圆、texture 精确名称、autoBlend=true |
| `texture.paint_by_height` | region、bands（1–16 条，maxHeight 严格递增）、autoBlend=true；每带 texture 或 textureNames |
| `waypoints.place` | name 可选、x/y、z=0、space=playableGrid、anchor=center |
| `waypoints.move` / `waypoints.delete` | move：objectId、x/y、z=0、space/anchor；delete：objectId |
| `starts.list` / `starts.place` | list：无；place：playerSlot（1–6）、x/y，可选 z/space/anchor |
| `objects.query` | objectId/typeName 可选精确过滤、offset=0、limit=100（1–200） |
| `objects.place` | typeName、x/y；name、z=0、angleRadians=0、space/anchor、ownerTeam、settings 可选 |
| `objects.move` | objectId、x/y；z/angleRadians 省略保留原值，space/anchor 可选 |
| `objects.delete` / `objects.configure` | delete：objectId；configure：objectId、ownerTeam/settings |
| `objects.scatter` | region、profile、seed、count（1–2000）、typeNames（1–64）；minDistanceCells=2、exclusions、footprints、catalogHash 可选 |
| `objects.analyze_space` | footprints、limit=100，可用 preparedPlanId/planHash 检查候选 |
| `protections.list/add/remove` | add：id、region、layers；remove：id；layers 为 terrain/textures/passability/objects |
| `art.profile` | 测量当前地图美术特征，具体参数见处理器 |
| `map.validate` | expectedPlayers、profile、footprints、resources、resourceAccess、routes、buildAreas；可检查候选 |

对象与路径点操作使用返回的 `obj-N` / `wp-N` 句柄，不用名称或游戏 UniqueId 作为唯一主键。普通对象查询不包含路径点与道路。设计实体重建可能替换对象并分配新句柄，应重新查询。

`objects.configure` 的 settings 支持布尔字段 `objectEnabled`、`objectIndestructible`、`objectUnsellable`、`objectPowered`、`objectRecruitableAI`、`objectTargetable`、`objectSleeping`，以及整数 `objectInitialHealth`、`objectBasePriority`、`objectBasePhase`。`ownerTeam` 必须唯一匹配已有队伍，且队伍所属玩家存在。

高度编辑不会自动重建通行标记。`terrain.rebuild_passability` 会重写全图普通 Passable/Impassable 标记，保留特殊限制标记；对有手工阻塞的旧地图应先明确这一影响。`terrain.analyze` 使用静态模型，不能替代游戏中的物体碰撞、建造与单位移动验证。

纹理自动混合可能写入选区外邻域。保护区按选中格执行，对象按中心格检查。解除保护应先独立提交，再编辑；同批移除保护并改该区域仍会被事务前后保护检查拒绝。

## 7. 批次与历史

`batch.execute` 的 commands 数组有 1–100 项，每项为 `command`、`commandVersion=1`、`arguments`。外层携带 SessionId 和 ExpectedRevision，子项不携带独立修订。仅允许普通 Mutation；查询、保存/导出、撤销重做、嵌套 batch、edits.* 和 design.* 都不能放入。

全部子命令共享一次事务，失败时整批回滚，成功时只增加一个修订并成为一个撤销步骤。批次错误的 `Details.index` 为从 0 开始的子命令索引。子命令间不能使用 JSON 变量引用前一项返回值；需要返回句柄再移动对象时可拆成两次请求。

`history.undo` / `history.redo` 无 arguments，需要当前 ExpectedRevision。撤销后执行新编辑会形成新的历史路径，调用方应以 `map.info` 返回的可撤销/重做状态为准。

## 8. 先准备候选，再应用

复杂编辑可先用 `edits.prepare` 执行模拟，检查结果后再提交。下例接续第 2 节，置于会话关闭前：

```csharp
var prepared = await Run(new CommandRequest
{
    SessionId = sessionId,
    Command = "edits.prepare",
    Arguments = Json(new
    {
        baseRevision = revision,
        commands = new[]
        {
            new
            {
                command = "terrain.set_height",
                arguments = new
                {
                    region = new { kind = "rectangle", space = "playableGrid",
                        x = 10, y = 10, width = 4, height = 4 },
                    height = 300f
                }
            }
        }
    })
});
var plan = (PreparedEditInfo)prepared.Data!;
// 检查 plan.Results；也可调用 session.CapturePreparedSnapshotAsync 导出独立快照。
var applied = await Run(new CommandRequest
{
    SessionId = sessionId,
    ExpectedRevision = revision,
    Command = "edits.apply",
    Arguments = Json(new { preparedPlanId = plan.PreparedPlanId, planHash = plan.PlanHash })
});
revision = applied.RevisionAfter!.Value;
```

prepare 是 Query，不需要 ExpectedRevision，但 arguments.baseRevision 必须匹配当前修订。它不发布工作副本、历史或新修订；apply 校验来源修订、地图/保护/设计哈希和目录身份，提交已准备内容，不重新运行散布算法。

候选仅在当前会话内存中保存 30 分钟，最多 32 份。关闭后无法恢复；`edits.discard` 传 preparedPlanId 释放候选。准备后发生编辑、撤销或重做，应用会因过期来源失败，应重新 prepare。准备阶段持有执行锁，其他会话内命令等待。

需要图像检查时，通过 Agent 的 `diagnostics.render` 或 `preview.start` 传同一组 preparedPlanId/planHash；这些渲染命令不是 Automation 注册命令。

## 9. 设计实体

希望保留“平台/树林/坡道”的生成意图，而不仅是编辑后的二进制地图时，使用 `design.prepare` → 检查候选 → `design.apply`。`design.query` 查询规格与生成内容绑定。

准备参数示例：

```json
{
  "baseRevision": 0,
  "patch": {
    "schemaVersion": 1,
    "upsert": [
      {
        "id": "base-a",
        "kind": "platform",
        "label": "玩家平台",
        "parameters": {
          "region": {"kind":"rectangle","space":"playableGrid","x":8,"y":8,"width":12,"height":12},
          "value": 300,
          "falloff": 0.4
        }
      }
    ],
    "remove": []
  }
}
```

将实际当前修订替换示例中的 0。patch 每次修改 1–100 个实体，总量最多 1000；upsert 中未出现的旧实体保留，remove 显式删除。当前 kind 包括 platform、ramp、objects、scatter、derived 和 playerStart，参数说明见 [Agent-Usage.md 的设计实体章节](Agent-Usage.md#设计实体与候选)。

design.apply 与 edits.apply 参数相同，但候选类型不能混用。design.query 支持 entityId、offset、limit、includeHeightCells；高度明细使用 mapGrid。

实体更新会核对拥有的对象指纹和高度，恢复旧生成内容后重建，保留未绑定内容。人工修改实体拥有的内容可能导致 `ENTITY_CONFLICT`。依赖实体删除需同步删除后代或更新依赖，不自动级联；循环依赖返回 `ENTITY_DEPENDENCY_CYCLE`。设计状态随事务、历史和工程导出保存。

## 10. 目录注入、扩展与能力发现

默认 executor 未注入物体目录，放置结果可能为 `unverified`。需要目录检查、占地数据与对应分析时，自行加载公开的目录类型并注入注册表：

```csharp
// objectCatalog、footprintCatalog 是调用方已加载的
// Catalog.ObjectCatalog 与 Catalog.FootprintCatalog 实例。
var registry = CommandRegistry.CreateDefault(objectCatalog, footprintCatalog);
var executorWithCatalog = new CommandExecutor(registry);

foreach (var description in registry.Describe())
    Console.WriteLine($"{description.Name}: {description.Effect}, v{description.CommandVersion}");
```

载入目录后，放置会检查名称与可选 catalogHash；目录声明本身不证明当前 Mod 行为。所有相关类型有量测占地时，scatter 未显式传 footprints 可取注入目录的数据。随机散布在相同地图状态、参数和种子下可复现；已有对象变化会影响结果，无法达到 count 时失败回滚。

`registry.Describe()` 返回注册命令，不包含 executor 特殊处理的 `batch.execute`。Agent 的 `system.capabilities/system.schema`、assets.*、jobs.*、preview.*、review.* 等属于宿主扩展，直接传给默认 Automation executor 会返回 UNKNOWN_COMMAND。

可实现公开 `ICommandHandler`，用 `registry.Register(handler)` 添加命令。普通改图处理器应声明 Mutation，让 executor 统一管理锁、修订和事务；不要绕过 executor 直接修改 `session.Facade`，否则历史、句柄与保护检查可能失去一致性。给 Agent 增加或修改命令时必须同步 command-schemas.json。

## 11. 常见错误与验证

| 错误码 | 处理方式 |
| --- | --- |
| `INVALID_ARGUMENT` / `UNKNOWN_COMMAND` / `UNSUPPORTED_VERSION` | 核对参数、命令名和版本，修正后换新 requestId |
| `SESSION_NOT_FOUND` | 确认当前进程中的会话仍打开，使用真实 sessionId |
| `REVISION_CONFLICT` | map.info 读回状态，重新判断操作后提交新请求 |
| `REQUEST_ID_CONFLICT` | 不同请求使用不同 ID；原样重传保留 ID |
| `MAP_EXISTS` / `MAP_NOT_FOUND` | 核对 parentPath/mapName 与地图目录结构 |
| `WORKSPACE_CONFLICT` | 备份并检查外部用户地图与未保存工作副本的冲突 |
| `PLAN_NOT_FOUND` / `PLAN_EXPIRED` / `PLAN_STALE` / `PLAN_HASH_CONFLICT` | 在当前会话重新 prepare，使用对应 ID 与哈希 |
| `CATALOG_CONFLICT` | 核对目录来源，重新查询素材或准备候选 |
| `ENTITY_CONFLICT` | 检查实体拥有内容是否被人工修改，重新协调设计规格 |
| `SLOPE_LIMIT_EXCEEDED` / `PLACEMENT_FAILED` | 调整坡道或散布约束，失败事务不会部分提交 |
| `IO_ERROR` / `CANCELLED` | 检查文件权限、占用或取消原因，再查询状态决定重试 |

功能改动后可运行稳定测试：

```powershell
dotnet test test/Dreamness.RA3.Map.Automation.Test/Dreamness.RA3.Map.Automation.Test.csproj --no-restore --filter "TestCategory!=UsageExamples"
```

单独验证本文所参考的使用示例：

```powershell
dotnet test test/Dreamness.RA3.Map.Automation.Test/Dreamness.RA3.Map.Automation.Test.csproj --no-restore --filter "TestCategory=UsageExamples"
```

这些示例会在临时目录保留地图产物。通过 CLI 验证改动前必须重新构建 Agent 项目，让宿主获得最新 Automation 副本。编辑、结构检查、静态通行性分析与美术测量不能替代在目标 Mod 中加载地图并验证实际玩法。
