# Agent 地图制作系统详细设计

日期：2026-09-16。状态：待实施设计。本文基于当前代码，不表示接口已可调用。

实施已开始。当前已交付内容与待办以 [实施状态](Agent-Implementation-Status.md) 为准，实际命令用法见 [Agent-Usage.md](Agent-Usage.md)。编辑器已提供 `--export-overview` 真实渲染接口并完成首轮联调；下文的理想接口和里程碑仍需逐项实现。

## 1. 产品边界与确定决策

2026-09-17 用户范围更新：不需要真实游戏加载验证。后续不启动游戏进行加载/试玩，也不将真实采矿、建造、胜负或引擎寻路验证作为交付前置。验收采用静态结构与空间检查、事务/历史回归、保存重开、完整导出和 WorldBuilder 真实预览。下文早期里程碑中的游戏试玩要求由此更新取代；报告保留“未在游戏中验证”，不把静态结果说成实际玩法通过。

系统让现有 agent 通过工具完成“设计、编辑、观察、检查、修正、导出”。不另造聊天机器人，也不把训练模型作为开工条件。

### 1.1 默认水域和灯光

- 新地图沿用 `Ra3Map.NewMap()` 的默认环境资产；水面高度采用当前默认值 200，固化到环境 profile 并以样本验证。
- 不提供水域、海平面、灯光或后处理编辑命令；不补这些资产的结构化解析分发。
- 可以降低/抬高地形形成海岸、岛屿和高地，水域资产本身不变。第一张验收图优先使用简单陆地主战场。
- 打开已有地图时保留其水域、灯光及未知资产，不用默认资产覆盖旧图。
- 默认环境由创建来源及资产指纹识别。不能识别的旧地图仍可编辑，但水陆及海军分析标为 `unknown`，不得按 200 假装精确分析。
- 生成器输出只导入批准的地形/纹理数据；最终环境继续沿用目标会话中的默认或原有资产。

### 1.2 首版交付范围

一个游戏/Mod profile、一个已验证主题、双人遭遇战：有出生点、基地可建区、资源组合件、主路/侧路、地形层次和装饰；可由自然语言局部修改，并查看结果和验证报告。

原版/日冕的具体选择不硬编码到内核。项目第一次创建时选择已安装且验证过的 profile；初始素材包只需支持一个。地图不是只输出 `.map`，还包含所需附属文件和交付报告。

延期：水灯编辑、任意旧图整体旋转/缩放、完整游戏渲染复刻、任意复杂战役生成、新模型资产生产、完全自动多人平衡评测。

### 1.3 三项核心约束

1. **所有地图写入经过同一 Session 事务。** UI、MCP、生成器不分别维护可写地图。
2. **所有图像和分析绑定同一 Revision。** Agent 必须知道自己看到的是哪个版本。
3. **硬约束由代码执行，创作选择交给 agent。** 资源类型、边界、保护区、占地等不能只写在提示词里。

## 2. 代码组织和依赖

先在现有项目增加目录，不一次拆成大量新程序集。

| 位置 | 新增/扩展内容 | 依赖边界 |
| --- | --- | --- |
| Automation/Commands | 对象、区域地形、纹理、布局、查询、验证等 handler | 延续现有 `ICommandHandler` |
| Automation/Geometry | 区域栅格化、坐标换算、距离场、路线走廊 | 不依赖 UI/模型运行时 |
| Automation/Catalog | profile、素材、组合件、配色方案加载与检索 | 本地文件起步，可替换数据源 |
| Automation/Design | DesignSpec、实体标识、规划编译、保护规则 | 输出确定性编辑计划 |
| Automation/Algorithms | 坡道、平滑、纹理规则、散布、组合件布置 | 输入快照，输出变更 |
| Automation/Analysis | 结构、空间、玩法分析，统一问题 DTO | 不依赖图像生成 |
| Automation/Session、History、Storage | 对象句柄扩展、设计元数据快照、提交恢复 | 唯一写入口 |
| Visualization | 分层 PNG、图例、坐标映射、截图合成 | 消费只读 DTO，不依赖 Automation |
| Ra3MapUtils/src/Core | 生成任务/资源接口、应用服务协议 | 不依赖 WPF 控件 |
| Ra3MapUtils/src/UI/Services | 注册 Automation、渲染器、生成 worker 适配 | 实现编排，不实现地形算法 |
| Ra3MapUtils/src/UI/MCP | 工具 schema、图片返回、错误转换 | 薄适配，共用上述服务 |

Automation 定义 `MapObservation` 和 `IMapPreviewRenderer` 接口；宿主适配器把 DTO 转给 Visualization。避免 Automation 与 Visualization 循环依赖。首版无需启动浏览器或三维前端才能调用工具。

Ra3MapSharp 保持现有 net6.0 内核；新 UI 的 net10.0 宿主做兼容集成。构建时固定 Parser/Facade/Automation DLL 来自同一产物集，启动返回版本清单；不假设当前 `lib` 中的 DLL 已同步源码。

## 3. 真相来源、状态和文件布局

### 3.1 状态模型

`MapSession` 的可序列化地图与编辑元数据构成一次完整编辑状态：

```text
EditState
  MapBytes                 地图实际结果
  DesignSpec               已提交的设计意图
  EntityBindings           设计实体 → 对象句柄/区域/组合件
  ProtectionMasks          用户锁定、路线保留、玩法对象保护
  ProfileIdentity          游戏资源版本、默认环境指纹
  AlgorithmVersions        已使用算法版本
```

设计草稿可以不进入 Session；一旦 `design.apply` 提交，它及其生成的地图变更一起进入历史。设计内容变化但地图字节没变，也推进 Revision，并单独标记 `designDirty`；保留当前 `Dirty` 的地图内容语义，增加 `hasUnexportedChanges` 汇总两类变更。

`Revision` 是会话编辑顺序，Undo/Redo 后仍递增；内容哈希标识结果。`historyCursor` 是历史位置，三者不能混用。验证结果不进入编辑历史，不改变 Revision。

### 3.2 在现有布局上增加

```text
<MapName>/
  <MapName>.map
  map.str / 其他原有附属文件
  .automation/
    MapInfo.json、Workspace.json、workspace.lock      已有
    Working/current.map                             已有
    Working/design.json、entities.json、protection.bin 新增
    Snapshots/<index>.map                           保留现有布局
    Snapshots/<index>.state.json                     新增元数据快照清单
    Snapshots/blobs/<hash>                           不可变元数据/掩码
    History/index.json、history.jsonl                扩展 schema
    Drafts/<draftId>.json                            未提交设计草稿
    Artifacts/<contentHash>/<optionsHash>/            可重建预览/报告
    Transactions/<transactionId>/                    暂存与恢复记录
```

生成模型、缩略图原库、候选历史放在共享资源目录或现有 `ai-data`，不复制进每个快照。已导入候选只记录来源和哈希，Redo 使用快照。

### 3.3 提交和迁移

延续当前隔离 Facade → 修改 → 序列化 → 重开规范化 → 写快照的流程，增加元数据事务参与者。失败恢复必须同时覆盖地图、句柄、设计规格和掩码。

当前历史只存路径点句柄，升级增加普通对象绑定。旧历史只在验证后迁移为新 schema；不能证明绑定正确时保留旧历史备份并明确要求建立新基线，禁止按名称猜测。

现有“干净会话重开清理历史”行为需要调整：项目设计和实体身份持久保留，历史清理必须保留当前基线状态。旧工作区没有设计元数据时按导入图处理。

多文件事务不声称具有文件系统级断电原子性。增加写前暂存和恢复记录：打开 Session 前检查未完成事务；可验证的完成/回滚自动恢复，不能验证则返回 `WORKSPACE_RECOVERY_REQUIRED`，不继续编辑。

## 4. 地图设计数据

### 4.1 GameProfile

| 字段 | 含义 |
| --- | --- |
| id/version/contentHash | 原版或 Mod 资源配置身份 |
| environmentPresetId | 已验证默认环境；只读 |
| unitsPerCell | 当前为 10 |
| coordinateConvention | 格心、格角、朝向转换版本 |
| allowedAssets/palettes/prefabs | 此配置可使用的素材集 |
| movementProfiles | 陆地等移动半径、坡度/水深规则及验证来源 |
| gameplayTemplate | 玩家/队伍/胜负基础配置与所需附属文件 |

首版可以用一个经游戏验证的平坦模板作为初始化来源；使用 `NewMap` 的路径需要达到相同的游戏验收标准。默认水灯不需要用户额外配置。

### 4.2 DesignSpec 示例

下列 ID 是设计占位符，不代表游戏中存在同名资源。实际提交前必须解析为目录中的有效 ID。

```json
{
  "schemaVersion": 1,
  "profileId": "installed-profile",
  "themeId": "verified-coastal-theme",
  "seed": 7319,
  "map": { "playableWidth": 256, "playableHeight": 256, "border": 8 },
  "symmetry": { "kind": "rotate180", "scope": "gameplay" },
  "zones": [
    { "id": "base-a", "role": "base", "shape": { "kind": "circle", "space": "playableGrid", "center": [48, 48], "radius": 18 }, "constraints": { "buildable": true, "targetHeight": 260 } },
    { "id": "base-b", "role": "base", "deriveFrom": "base-a", "transform": "symmetry" }
  ],
  "routes": [
    { "id": "main", "from": "base-a", "to": "base-b", "via": [[128, 128]], "clearWidthCells": 10, "movement": "ground-default" }
  ],
  "placements": [
    { "id": "start-a", "kind": "playerStart", "playerSlot": 1, "zoneId": "base-a" },
    { "id": "start-b", "kind": "playerStart", "playerSlot": 2, "zoneId": "base-b" }
  ],
  "decoration": { "themeId": "verified-coastal-theme", "excludeRouteBufferCells": 3 },
  "constraints": { "preserveUnknownAssets": true, "preserveEnvironment": true }
}
```

该示例只是数据形状，完整双人模板还必须提供资源组、侧路和验收参数。数值是初始设计值，需通过目标游戏配置验证，不是 RA3 的通用平衡标准。

### 4.3 几何契约

- 地形点操作沿用整数 `playableGrid/mapGrid`。区域边界采用连续格坐标，矩形为半开区间 `[x,x+width)`。
- 区域选格按格心 `(i+0.5,j+0.5)` 是否在图形内；多边形边界点算内部，拒绝自交多边形。圆、多边形、走廊共用一套栅格化实现。
- 对象 `anchor=center` 时世界坐标为 `(i+0.5)*10`，`anchor=corner` 时为 `i*10`；mapGrid 先扣边界。
- 对连续区域做 180 度旋转是 `(W-x,H-y)`；对格索引是 `(W-1-i,H-1-j)`，不能混用。
- API 朝向统一为度；精确零度方向和正向旋转通过一组已知朝向对象校准，profile 记录到文件字段的转换。
- 地形采样用于贴地：首版采用明确的插值策略并与编辑器样本比对；预览和放置使用同一策略。地形落盘量化后再次检查坡度与贴地结果。

### 4.4 稳定实体和局部修改

设计层用 `base-a/main/forest-east` 等实体 ID；对象层沿用 `objectId` 概念，但把当前仅路径点的表扩展为所有对象记录。

每个绑定保存句柄、类别、资产类型、快照内对象序号、属性指纹和所属设计实体。对象序号只在受控快照内使用；每次增删重排同步更新，不能把名称或 UniqueId 当唯一键。

对于复制粘贴导致完全相同的对象，也应通过快照内记录和独立句柄区分。外部编辑器重写后的导入创建新映射，返回旧句柄失效范围；不能承诺跨任意外部编辑仍自动保持身份。

同一设计实体重复应用默认更新它拥有的对象，不能叠加重复生成。未被系统认领的对象默认受保护。首版对称只作用于设计生成的区域/对象，不调用整图旋转替换原图。

## 5. 命令协议与 MCP

### 5.1 保持现有请求信封

保留 `requestId/sessionId/expectedRevision/command/commandVersion/arguments` 和 `CommandResult`；新增命令通过注册表加入。MCP 工具名采用 `ra3_terrain_ramp` 等固定名称，内部映射到 `terrain.ramp`，由同一命令元数据生成 schema，避免两份参数规则漂移。

给注册项增加参数 schema、效果类型、支持 profile、示例和限制。MCP 适配器需要这些元数据；当前只有 Name/Effect/ExecuteAsync，应增量扩展。

普通写操作沿用 expectedRevision。读操作返回观察到的 Revision；可选请求 `requiredRevision`，版本不匹配拒绝读取，避免把多次查询误拼成同一个状态。

### 5.2 首版工具与参数

| 内部命令 | 关键输入 | 返回/说明 |
| --- | --- | --- |
| `system.capabilities` | 无 | 实际支持命令、版本、profile、限制、生成器/预览状态 |
| `assets.search` | kind、query、tags、profileId、cursor、limit | 分页候选和图册引用 |
| `assets.get` | assetId | typeName、缩略图、占地、属性及验证状态 |
| `map.create/open/info/save/save_as` | 延续当前参数 | 已有；create 后绑定 profile/template |
| `map.inspect` | region、layers、统计粒度 | 区域高度/纹理/对象/约束摘要 |
| `map.preview` | region、layers、pixelSize、requiredRevision | PNG 和坐标映射 |
| `terrain.set_height/query` | 延续现有参数 | 保持兼容，矩形先不改语义 |
| `terrain.smooth` | region、radiusCells、iterations、strength | 掩码限制的平滑 |
| `terrain.ramp` | polyline、widthCells、端点高度、maxSlope | 创建连续通行坡道 |
| `texture.paint` | region、textureId、blendPolicy | 批量绘制并处理邻接混合 |
| `objects.query` | region、kind、tags、cursor、limit | 句柄和空间信息 |
| `objects.place/update/delete` | assetId/objectId、位置/朝向/受支持属性 | 返回实际句柄、世界位置和占地 |
| `waypoint.*` | 沿用当前命令 | 增加显式 playerStart 模板接口 |
| `prefab.place` | prefabId、position、rotation、entityId | 原子创建对象集合和绑定 |
| `objects.scatter` | region、assetSetId、spacing、seed、exclusions | 受约束分布；返回实际数量及未满足原因 |
| `design.prepare/apply` | DesignSpec 或 preparedPlanId | 预计算、预览/检查、原子提交 |
| `map.validate` | ruleSet、region、requiredRevision | 分级问题和证据 |
| `batch.execute/history.undo/history.redo` | 沿用当前协议 | 已有；扩展为完整编辑状态 |

后续工具：`generation.start/status/cancel/results/import`、`map.export_package`、`editor.open/capture`。只暴露已经实现的命令，不用占位工具虚构能力。

普通对象 update 只允许已知字段，如位置、朝向、名称、所属队伍和验证过的属性；不开放任意属性字典写入来绕过校验。资源对象常有配套语义，优先使用验证过的组合件。

### 5.3 示例：装饰性散布

```json
{
  "requestId": "forest-east-001",
  "sessionId": "session-id",
  "expectedRevision": 12,
  "command": "objects.scatter",
  "commandVersion": 1,
  "arguments": {
    "entityId": "forest-east",
    "region": { "kind": "rectangle", "space": "playableGrid", "x": 160, "y": 60, "width": 30, "height": 80 },
    "assetSetId": "verified-theme-trees",
    "seed": 42,
    "minSpacingCells": 3,
    "targetCount": 40,
    "excludeTags": ["route-reserved", "base-buildable", "resource-access"],
    "countPolicy": "up-to"
  }
}
```

`up-to` 是明确允许稀疏结果的装饰策略；资源/出生等玩法对象使用 `exact`，未满足数量或位置约束整笔失败。返回实际数量、拒绝原因统计、changedBounds、生成句柄和 Revision，不只返回“成功”。

### 5.4 prepare/apply、批次与重试

- 简单命令可以直接事务执行；复杂布局用 prepare。prepare 在只读快照上运行完整算法、规范化并检查，产生不可变候选与哈希，不改变地图 Revision。
- apply 按 baseRevision、profileHash、inputContentHash、planHash 检查后提交准备好的结果，不能重新随机运行一遍算法。准备结果过期或缺失返回明确错误。
- 保留现有 batch 上限 100 条、只接受 Mutation、禁止嵌套；复杂高层操作在服务内部展开，不拆成数千次 RPC。
- 新增 batch `clientRef` 与对象引用解析，可在同批创建后修改；引用只能指向前面已经成功的子命令。失败整批回滚，clientRef 不泄漏成持久句柄。
- 当前去重只在内存中。首个可用版明确重启后应先查询结果；稳定版将 requestId、规范化参数哈希和提交回执随事务持久化。失败的瞬时 IO 可用新 requestId 重试，不能让同 ID 改参数。
- 新增结构化错误：`ASSET_NOT_FOUND`、`PROFILE_MISMATCH`、`PROTECTED_REGION`、`CONSTRAINT_UNSATISFIED`、`STALE_PLAN`、`STALE_CANDIDATE`。错误含实体/坐标/字段，沿用 existing code 的 `REVISION_CONFLICT` 等。

## 6. 编辑算法与约束执行

### 6.1 固定编译阶段

```text
资源和设计校验 → 区域/路线栅格化 → 基地平台/坡道
→ 地形细节 → 通行/禁建数据 → 地表纹理与混合
→ 玩法组合件 → 装饰 → 序列化规范化 → 检查与预览
```

每阶段输出实际修改掩码、摘要和诊断。依赖图决定局部重算：调色不重布资源，移动树林不重新生成地形，调整主路只重算它的影响范围及关联对象。

### 6.2 地形

- 平台：平台核心设定高度，外围过渡带插值；保护区保持原值。
- 坡道：沿折线弧长插值目标高度，横向生成平缓核心和过渡带；检查量化后的最大坡度及宽度。端点落在不可编辑区且不匹配时返回约束失败。
- 平滑：双缓冲迭代，固定读取上轮高度，避免遍历顺序改变结果；掩码外样本可作为边界条件，但不能写入。
- 细节：固定 seed 的噪声仅作用于装饰地形，不扰动基地平台和关键通路。
- 降低地形形成水面可见区域，但不修改水域配置。没有真实悬崖专用支持时，不将陡坡命名为已完成的悬崖系统。

### 6.3 材质

Palette 定义基础地表、坡面、岸边、道路和点缀纹理，以及可混合组合。先按高度/坡度/区域选材，再加入受控低频变化，最后更新邻接混合。

修改范围包含必要的混合邻域 halo；prepare 必须列出实际 halo。若 halo 与硬保护区冲突则拒绝或要求缩小笔刷，不悄悄修改受保护区域。

首版复用 `AutoDetectBlendsInRegion`，用对角边界、三种纹理交汇等样本验证其限制；无法表达的情况输出明确诊断。已有工作台的纹理预算限制属于生成器配置，不直接当成 RA3 文件格式上限。

### 6.4 对象和组合件

贴地位置 = 规范化地形采样高度 + 资产配置 offset。游戏对象的 Z 语义、道路节点连接和朝向通过样本验收；未验证的素材不参与自动贴地承诺。

散布使用固定 seed 的最小距离采样，加空间索引筛选：边界、坡度、默认水陆条件、对象占地和禁放区域。初始版本可以使用简化包围形状，但其精度和来源必须在报告里可见。

组合件声明对象局部坐标、朝向、队伍/玩家绑定、关联名称、占地、交互通道、地形要求。实例化时命名去重并修正内部引用；资源组合件必须通过实际采矿验证。

### 6.5 路线与可玩性

路线图先定义连接关系和净宽；地形走廊与道路视觉对象分开处理。绘制道路不会自动证明路线可通行。

验证器从高度、通行标记、障碍物和移动 profile 派生分析网格：按单位半径膨胀障碍，使用不穿墙角的邻接规则做连通性及路径分析。坡度阈值、碰撞精度来自 profile，不硬编码成所有单位一样。

修正当前 `UpdatePassabilityMap` 的角度单位和写邻格覆盖问题；计算阶段与写回阶段分离。AI 提示、静态可达和引擎实际寻路分别报告。

## 7. 素材目录与范例系统

初始实现采用 versioned JSON + 本地图片，不先建设新的数据库服务。

```text
Catalog/<profile>/<version>/
  profile.json
  objects.json
  textures.json
  palettes/*.json
  prefabs/*.json
  templates/<id>/manifest.json + 地图及附属文件
  thumbnails/*
  examples/*
```

每个素材包含 ID、真实 typeName、tags、displayName、profile、thumbnail、footprint、placementRules、validationLevel、source。足迹未知时 `validationLevel=unverified`，默认不用于关键玩法布局。

建议首包规模：约 10–20 个纹理、20–40 个装饰对象、3–5 个已验证组合件、1 个玩法模板、3 个制作范例。规模是工作量目标，不是产品硬限制。

检索先用名称/标签匹配和人工同义词；确有需求再接现有知识库。对象图册必须显示 ID，让 agent 能把视觉选择映射回准确资源。

制作范例记录“需求 → 设计规格 → 关键编辑 → 预览 → 问题 → 修正”，重点是教学流程与局部组合，不只收集最终地图截图。

## 8. 观察、预览与评价

### 8.1 快照观察

宿主在 Session 锁内取只读快照后释放锁，后台渲染不会长期阻塞编辑。缓存键包含地图内容哈希、元数据哈希、profile、图层/视口选项和渲染器版本。

`MapObservation` 至少包含：dimensions/border、gridToWorld、区域高度、纹理索引、对象摘要与占地、设计区/路线、保护掩码、environmentConfidence、Revision。

### 8.2 二维预览协议

首版图层：`terrain`、`textures`、`objects`、`gameplay`、`passability`、`constraints`。单图不默认叠加全部图层。默认全图 1024 像素长边，最大 2048；这是起始服务限制，可配置。

响应包含 PNG、legend、viewport、Revision 和 pixelToPlayableGrid 仿射变换。像素坐标以左上为原点，网格以可玩区左下为原点，裁剪/缩放都反映在变换里；另给格心定位示例，避免截图坐标直接被当世界坐标。

地图像素渲染先提供主题色/纹理缩略图平铺和对象符号；精细混合预览逐步补充。每张图明确 `schematic` 或 `approximate`，未提供真实材质时不能称为游戏效果。

MCP 层返回模型可读取的图像内容与结构化摘要；大图另返回资源引用。必须做一次客户端端到端验收，确认模型收到图像而非只看到路径。

### 8.3 三维和真实截图

后续三维预览消费同一 Observation：高度网格、材质、对象代理、固定相机。内部可使用 Three.js，但代码中统一坐标轴转换，不允许它成为另一套地图状态。

真实截图携带导出的 contentHash、镜头 ID、时间和工具版本。WorldBuilder 打开的是导出副本，若编辑器保存了修改，作为新的外部候选导入；不能自动覆盖正在编辑的 Session。

### 8.4 ValidationReport

```text
reportId, revision, contentHash, profileHash, ruleSetVersion
status: passed | failed | incomplete
issues[]: code, severity, entityId, objectId, region, message,
          measuredValue, expectedRange, confidence, suggestedFix
metrics: connectivity, buildableArea, resourceDistance, clearance, ...
notEvaluated[]: 原因与所需条件
```

结构错误、缺出生点、硬保护被修改、关键路线不连通为 error；资源距离差、装饰过密可为 warning；未知碰撞和环境信息必须进入 notEvaluated。没有检查的规则不得按通过计分。

双人对称模板先以资源组一致、主/侧路可达、基地可建面积一致为硬规则；路径距离偏差等阈值在模板内配置，并用人工地图校准。视觉评审使用固定总览和 4 个局部镜头，评价主题、过渡、地标、疏密和战斗可读性。

## 9. 可选生成器适配

`IGenerationBackend` 暴露 Capabilities、Start、Status、Cancel、GetResults。适配既有 ONNX 和区域 PyTorch 路径；能力包括可用尺寸、输入图层、续画支持、纹理输出和运行时身份。

任务状态：`queued → running → succeeded/failed/cancelled`；成功只是产生候选。导入是独立命令，需要当前 expectedRevision 匹配任务 baseRevision。

输入冻结：地图快照、DesignSpec、语义/保护掩码、seed、profile、模型/运行时版本。已冻结后用户继续编辑不会改变后台任务输入。

输出候选通过以下步骤导入：校验哈希与尺寸 → 提取高度/纹理逻辑值 → 核对保护区 → 验证未知资源 → 规范化 → 检查 → Session 提交。纹理按名称/目录身份重新注册，不能直接复制候选地图内部的纹理索引。

不直接替换整张候选地图，避免把默认水灯、玩家、脚本和原对象覆盖。候选带有源图保留报告也仍需在导入侧复查实际差异。

过期候选首版拒绝自动合并；用户/agent 可根据新快照重新生成。模型不可用时，平台、坡道、噪声、模板和素材工具仍能完成地图制作。

## 10. Agent 工作流程

1. 读取 capabilities/profile，了解已实现命令和素材限制。
2. 将用户目标转为 DesignSpec 草稿，明确可验证的玩法约束。
3. 查询主题素材/组合件，用图册选择资源；不猜 typeName。
4. prepare 地形与玩法布局，查看总览、坡度和路线；修正重大空间问题。
5. apply 后放置资源/基地组合件；检查关键通路和建造空间。
6. 增加纹理细节和装饰，查看局部图；修复异常位置。
7. 导出副本，在 WorldBuilder/游戏检查；保存同版本截图和试玩记录。
8. 交付地图包、说明和仍未验证项。

工具返回建议的后续观察区域，但不强制 agent 运行固定剧本。初始每阶段最多 3 轮修正是默认预算，可调整；达到预算仍有 error 时交付可诊断的草稿状态，不标成可玩成品。

示例：“东侧树林减少一半，不改变道路和矿区” → 查 forest-east 及 Revision → 按实体所有权选择装饰子集 → 使用固定 seed 保留一半 → 一次事务删除 → 查看东侧局部图 → 重查主路净宽。整个过程不重新生成整张地图。

## 11. 导出与游戏验收

`map.save/save_as` 保持现有语义；另加 `map.export_package` 负责完整交付，在指定 Revision 冻结地图、模板附属文件、map.str 和声明的脚本。

先在暂存目录生成并检查清单/哈希，再发布到独立目标目录。更新已有目录时只替换清单拥有的文件，保留未知附属文件；外部变化返回冲突。不会把 `.automation`、模型或内部日志装入游戏地图包。

报告区分：静态验证完成、WorldBuilder 打开完成、游戏加载完成、玩法检查完成。真实测试覆盖出生、基地建造、资源使用、地面关键路线；海军/两栖相关规则只在目标地图需要且 profile 有验证能力时加入。

首版允许人工完成这些步骤并录入结果，避免桌面自动化成为所有功能的前置阻塞。后续 Computer use 适配再自动执行可重复部分。

## 12. 实施拆分与验收测试

下列每项可作为一个独立 PR；时间估算应在 A/B 落地后按实际代码更新。

| PR | 工作范围 | 依赖 | 必须验证 |
| --- | --- | --- | --- |
| A | 命令元数据、profile、坐标/区域公共契约 | 无 | 注册 schema 与实际 handler 一致；边界/格心换算 |
| B | 通用句柄、EditState、元数据历史迁移 | A | 重复名称对象独立操作；失败/Undo/Redo 同步恢复；重开不丢设计 |
| C | 只读 Observation、二维预览、MCP 图片 | A | 图像位置对应数据；Revision 标注；客户端实际看图 |
| D | 素材目录、普通对象命令、最小组合件 | B | 不存在素材拒绝；占地/属性检查；玩法组合件游戏验证 |
| E | 区域平滑/坡道、材质绘制、通行性修正 | A/B | 量化后坡度；保护区；混合 halo；遍历顺序无关 |
| F | DesignSpec 编译、prepare/apply、散布与局部更新 | D/E | 同输入同结果；重复应用不累加；只修改目标实体 |
| G | ValidationReport、完整导出、双人模板验收 | C/F | 关键通路/建造/资源；默认水灯及未知资产保留；真实试玩 |
| H | 现有生成器后台适配与候选导入 | G 后增强 | 过期候选拒绝；纹理索引重映射；源图/环境不变 |
| I | 编辑器桥接、三维预览、更多主题 | G 后增强 | 图像绑定地图版本；对真实效果和坐标做对照 |

第一个可用演示在 A–E：agent 新建地图、造平台/坡道、涂材质、放对象、看图、改图、撤销和导出。A–G 达到“完整双人地图闭环”。生成模型接入 H 能增强创作，但不会阻塞最小系统。

### 12.1 必测故障场景

- 对象属性非法/材质不存在：零提交，地图和元数据都不变。
- 批次后段失败：前段创建的对象/句柄/实体都不存在。
- 同版本并发写：最多一个成功；apply 过期计划拒绝。
- 平滑/材质 halo 越过保护区：按契约拒绝或只写合法范围，不静默破坏锁定区。
- Undo 后编辑：Redo 分支失效；旧截图仍标原 Revision。
- 序列化规范化：检查实际落盘高度和对象位置，不能只验证内存数据。
- 人工改图或更换 profile：报告冲突/能力不匹配，不沿用旧分析报告。
- 生成取消或进程退出：成功候选与活动 Session 都可解释，未完成候选不能导入。
- 水灯资产：新图默认值不变；导入旧图的环境资产 payload 保留。

### 12.2 性能与交付门槛

以 256、500、800 格样本分别测量：打开、一次区域编辑、保存、预览、验证、历史磁盘增长及峰值内存；500/800 是负载样本，不要求基础演示必须生成这么大。

拟定交互目标：缓存预览 1 秒内、普通局部编辑与预览 3 秒内；耗时任务返回 jobId 并支持取消。这些是待实测的目标，不是当前性能结论。

初始仍用完整地图快照，批量编辑只序列化一次；先通过结果再根据实测决定是否做分块快照。所有缓存可重建，清理缓存不影响设计和地图历史。

## 13. Agent Skill 层

地图制作主 Skill 位于用户指定工作区：`N:\Program Files (x86)\Red Alert 3(Incomplete)\ra3_map_workspace\.agents\skills\ra3-map-authoring\SKILL.md`。

它负责布局与美术方法、能力发现、观察/局部修正和验收证据；MCP/Automation 负责 schema、资源合法性、事务和保护约束。架构入口为 `Agent + ra3-map-authoring → 实际可用工具 → 地图与反馈`。

首版入口附带 `references/execution.md` 和 `references/design-and-review.md`；按需复用同目录已有 C#、HTTP 和 Lua Skill。它明确区分当前命令与拟议接口，随 A–G 实施增加经过实际运行的例子；未验收的流程示例不能标为成功案例。

该目录是地图工作区的本地 Skill 位置，不自动等同于 Ra3MapSharp 等其他项目的全局安装；跨项目任务可显式读取该路径，需要自动发现时另行配置作用域。

## 14. 本次设计交付

本节记录最初设计交付：当时只新增设计文档并同步水域/灯光边界。后续已进入源码实施，当前能力、测试证据及待办以 Agent-Implementation-Status.md 为准。实现按仓库 AGENTS.md 的变更矩阵执行构建/测试；按用户最新要求，真实游戏加载验证不属于验收范围。
