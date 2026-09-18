# Agent 地图制作系统：可行性结论与建设路线图

日期：2026-09-18
扫描范围：`Ra3MapSharp`、`Ra3MapUtils`、`ra3_map_workspace`、NewWorldBuilder（新地编）安装目录、`origin_maps` 精品图语料。

> **范围决定（2026-09-18，用户指示）**：`ra3_map_diffusion` / TerrainForge 的生成模型效果不理想，**本次开发不纳入**。本文只把它目录下的 `dataset/origin_maps`（65 个精品地图包）当作**只读参考语料**使用，不规划任何模型训练、推理接入或数据管线依赖。移除该层后，地形生成能力退回**模板 + 确定性工具**保底路径。

本文是**结论与路线图**。既有详细设计不重复，见 [Agent-Map-Creation-Design.md](Agent-Map-Creation-Design.md)、[Agent-Map-Creation-Plan.md](Agent-Map-Creation-Plan.md)、[Agent-Implementation-Status.md](Agent-Implementation-Status.md)、[Automation-Design.md](Automation-Design.md)、[Agent-Usage.md](Agent-Usage.md)、[MCP-Usage.md](MCP-Usage.md)。

## 1. 结论

**可行；编辑内核已经打通并有真实证据，瓶颈已经从"能不能编辑"转移到三件事：素材与内容库、美术质量闭环、以及各部件之间从未接线。**

本次实际执行（不是引用文档）：

| 验证 | 命令 / 证据 | 结果 |
| --- | --- | --- |
| 内核构建 | `dotnet build src/Dreamness.RA3.Map.Agent` | 0 错误，7.5 秒 |
| 自动化内核测试 | `Automation.Test`（排除 UsageExamples） | **159 通过 / 0 失败** |
| Agent 宿主测试 | `Agent.Test` | **23 通过 / 0 失败** |
| 真实渲染 | `artifacts/duel-example/20260917-094140-735251/.../scene.overview.png` | 2048² 真实 D3D9 鸟瞰图，已人工查看 |
| MCP 真实链路 | `artifacts/mcp-smoke/20260917-093638-626319/` | 五层诊断 PNG + 路线叠加 + 官方 SDK 握手 |
| 历史索引膨胀 | `.automation/History/index.json` | 256 格图 + 4 次修订 = **30.1 MB**，单条修订 `designEntities` 达 **6.7 MB** |
| 依赖服务状态 | 探测 30033 / 30034 | **均未监听** |

真渲染图的人工判断：结构正确（180° 对称、8 矿、2 油井、2 处高地、出生点、静态路线通过），但**美术仍是草稿**——单色草地铺满、树呈同心圆"玫瑰花结"、高地是单材质团块、无水域/悬崖/道路/建筑/地标。这与 `art-review.json` 的 `needs-iteration` 一致。

**三块资产齐备但彼此未接线**：编辑内核（Ra3MapSharp）、伴侣与服务（Ra3MapUtils）、技能与 Lua 工程（ra3_map_workspace）。目前**没有任何一个 MCP 客户端配置指向 Automation**，两个本地服务也都没运行——能力是"文档承诺"，不是"已安装工具"。

需要特别提醒的是：各库之间**成熟度差异很大**。Ra3MapSharp 的 Automation/Agent 有 51 个工具 + 159/23 项测试 + 真实渲染证据；而伴侣侧仍有空壳（KnowledgeBase 零数据、`Ra3MapFacade.NewMap` 无调用点）。规划时**一律以代码事实为准**（见 §2 与 §3 的 G11）。

## 2. 已核实的资产

### 2.1 Ra3MapSharp —— 编辑内核（最成熟）

| 项 | 事实 |
| --- | --- |
| 命令面 | **51 个命令**（`Protocol/command-schemas.json`）：地图创建/打开/保存/另存/导出工程与包/检查文件、`terrain.*`（query/set_height/sculpt/smooth/ramp/analyze/rebuild_passability）、`textures.list`/`texture.paint`、`waypoints.*`、`starts.list/place`、`objects.query/place/move/delete/configure/scatter/analyze_space`、`assets.objects`、`protections.*`、`edits.prepare/apply/discard`、`design.query/prepare/apply`、`history.undo/redo`、`batch.execute`、`map.validate`、`preview.start/inspect`、`diagnostics.render`、`jobs.*`、`system.capabilities/schema` |
| 一致性内核 | 会话 + 修订号 + 事务 + 撤销重做 + 请求去重 + 对象句柄 + 外部修改检测 |
| 设计实体 | platform / ramp / objects / scatter / **texture** / playerStart / derived(180° 对称)，含依赖图、层叠高度所有权、参数派生、幂等重复应用、手工冲突拒绝，`entity-compiler-v8` |
| 静态验收 | `map.validate`：出生槽位/边界/重合、玩家定义、对象所属、显式资源数量、占地、连通性、`buildAreas`、`resourceAccess` |
| 反馈 | 五层诊断 PNG、路线叠加（≤16 条）、候选看图、真实 WB 鸟瞰图、`preview.inspect` 裁剪缩放 + 哈希/坐标矩阵 |
| 协议 | MCP stdio（2025-06-18）+ JSONL，工具名 `ra3_*` |
| 状态 | **全部在未提交分支 `feat/automation`**；`AGENTS.md`/`CLAUDE.md` 未收录 Automation 与 Agent |

### 2.2 NewWorldBuilder 安装目录 —— 现成的内容与扩展点（本次新发现）

| 发现 | 路径 / 事实 | 对系统的意义 |
| --- | --- | --- |
| **3041 张对象缩略图** | `data/objectScreenShot/*.jpg`，按 typeName 命名 | 直接补上"素材目录缺缩略图" |
| **美化模板机制与存储** | `data/objectsTemplate/*.object.bin`（本机 7 个）；地编 v14 起 `Shift+C` 把选中物体（含路径点、道路）存为模板 | 现成的 prefab 机制，库需自行积累 |
| **编辑器插件 = 进程内 C# API** | `data/scripts/<Name>/Source/*.cs`，实现 `ScriptInterface.Apply(MapDataContext)`；可用 `HeightMapData.elevations`、`ObjectsList.AddObject`、`Script/ScriptCondition/ScriptAction`。**9 个官方示例**：CircleTerrain、ImportXYZ、loadObjFromFiles、RandomAddTrees、TerrainLevelUp、TerrainNoiseOver、Mix、OnlyInfantry、RA3MapUtil_LuaImporter | **编辑器桥接最现实的抓手** |
| **每 Mod 自定义分类** | `ObjectCategory_<mod>.json` 随 `*.skudef`/`*.skudefWB` 选择 | 素材目录必须 Mod 维度 |
| 脚本元数据 | `data/config/ScriptActionNew.json`(321 KB)、`ScriptConditonNew.json`(90 KB)、`scriptTrans.json`(134 KB)、`scriptContent.xsd`(675 KB) | 玩法脚本合法性校验的数据源 |
| 命令行任务 | `MAP_TASKS.md`：`WbLauncher.exe --export-overview <map>`（真 D3D9 渲染，北朝上，2048 长边，退出码 0–8 分级）、`--open-map`（保留编辑器交互） | 真实视觉反馈的主力 |
| 游戏调试器 | `game/WuRa3GameDebug.exe`；菜单"启动游戏调试" | 见 2.3 的 30034 |
| 参考图 | `maps/` 3 张官方战役图；`日冕地图lua例子/` 5 个 Lua 库示例；`data/Art/` DDS/TGA 与 `FXOceanRA3.w3x`、`posteffect*.json` | 范例与美术资源 |

### 2.3 Ra3MapUtils —— 伴侣、服务与游戏内桥接

| 项 | 事实 |
| --- | --- |
| 形态 | **仓库内两套并存**（分支 `v2`）：`src/UI` = v2 伴侣（VERSION 2.0.9，**net10.0-windows**，AssemblyName 仍为 `Ra3MapUtils`）；`Ra3MapUtils/` = v1 遗留全功能版（VERSION 1.10.11，net8.0-windows）。**插件宿主目录只存在于 v1**，v2 由 `WBLegacy/` 承载旧插件路线 |
| HTTP | `http://127.0.0.1:30033`（仅环回），Swagger `/swagger/v1/swagger.json`；**6 控制器 / 13 端点，两树路由一致**：`api/status/{ping,companion}`、`api/csharpscript/run/{code,file}`、`api/lua4/syntax/{code,file}`、`api/lua-import/scheme/{export,import}`、`api/nanoprogram/{list,wb_visible_list,run/{id}}`、`api/image-encoding/{preview,encode}`。统一响应 `ApiResponse<T>{Code,Message,Data}`（1000 成功 / 1001 未知 / 1002 执行失败 / 1003 参数非法） |
| 认证 | **无**。无 `AddAuthentication` / `[Authorize]` / API Key，安全仅靠"环回绑定 + 单实例 Mutex" |
| MCP | `AddMcpServer().WithHttpTransport()` → **Streamable HTTP**（非 stdio）挂在 `/mcp`：**13 个工具**（`RunRa3CSharpScript`、`GetLibStructure`、`GetLoadedAssemblies`、`GetMethodSignature`、`GetTypeInfo`、`GetEnumValues`、`CheckLua4Syntax`、`ExportLuaImportScheme`、`ImportLuaBySchemeJson`、`GetMapList`、`CopyRa3Map`、`RenameRa3Map`、`DeleteRa3Map`）。**仓库内无 MCP 配置文件** |
| **游戏内运行桥接** | 端口 **30034**（插件 `TinyNative`）：`GET /api/status`、`POST /api/lua/execute`、`POST /api/lua/execute-file`、`GET /api/objects/{id}`、`GET /api/objects/selected`、`GET /api/time/status`、`POST /api/time/{pause,fast,single-step,step,fast-to,reset}`（RA3 每秒 15 逻辑帧） |
| C# 脚本底座 | `Dreamness.ScriptExecutor` + Roslyn + `lib/Dreamness.RA3.Map.*.dll`（AssemblyVersion 全为 1.0.0.0）；依赖图 `UI → Services → Business → DAO → SQLite` |
| 生成链路 | **本次不纳入**（见 §2.4）。需澄清的是：`src/AiMapAdapter` 本来就不是生成适配器，而是约 20 行的只读导入外壳；真正生成是外部 `TerrainForge.Inference.Host.exe` worker 进程。伴侣侧的 `src/Core/AiMaps` / `eng/ai-map` 在本计划中视为不存在 |
| **未完成项（会误导规划）** | ①**KnowledgeBase 是空壳**：`KnowledgeBasePage.xaml` 只有"开发中, 敬请期待"，DAO 有 FTS5+jieba 代码但**零数据、零 DB 文件**，CLI 只打印 Hello World，v2 完全没有它；②`Ra3MapFacade.NewMap` 在伴侣侧**无任何调用点**（"从零建图"未端到端验证） |
| 规格 | `openspec/specs` 20 个 capability（含 `mcp-tools`、`http-api`、`ai-map-generation`、`ai-map-guided-editing`、`ai-map-components`）；归档 15 个 change + **1 个未归档在办** `redesign-ai-map-workbench` |
| 规格 | `openspec/specs` 有 20 个 capability，含 `mcp-tools`、`http-api`、`ai-map-generation`、`ai-map-guided-editing`、`ai-map-components` |

### 2.4 ra3_map_diffusion / TerrainForge —— **本次不纳入**

该仓库提供数据集、ONNX 推理与质量评测，但模型效果不理想，用户已决定**本次开发不纳入**。此处只留事实记录，便于日后重新评估：

- 已知限制：语料仅 60 张母图 / 441 个 256×256 窗口（按母图 48/6/6 划分）；自述"strong prototype, not a production-ready unconditional generator"；最新模式仍需从"结构丰富的训练裁剪"起步。
- 交付障碍（供日后评估）：`src/UI/data/ai-map/components.json` 的组件 `download_url` 全为 `null`；`eng/ai-map/README.md` 自述 not a qualified public AI release，且未找到上游 license。
- **对本次计划的影响**：伴侣侧 `src/Core/AiMaps`、`src/AiMapAdapter`、`eng/ai-map` 一律**视为不存在的依赖**；不规划 `generation.*` 命令族、不接 TerrainForge 宿主、不依赖其数据管线。

### 2.5 精品图语料 `origin_maps`（本次新发现，价值最高）

位于 `E:\ai_workspace\ra3_map_diffusion\dataset\origin_maps`。**只作为只读参考语料使用**，与已被排除的模型开发无关。

**65 个完整地图包**，构成"优秀"的对照基准：

| 类别 | 数量 | 例子 |
| --- | --- | --- |
| 官方战役（苏/盟/日） | 27 | `CAMP_A01_BrightonBeach_Smith` … `CAMP_S09_NewYork_Rao`，每包含 `.map`、`AIP_*/AIS_*.xml`、`map.xml`、`overrides.xml`、`_Art.tga` |
| 日冕 Samsara 战役 | 14 | `[cor_samsara]ep01A_poland_demo1` … `ep14S_Romanian_laboratory_site_demo1` |
| **官方遭遇战图** | 10 | `官方地图_无限岛`、`_混乱火山湖`、`_雪犁`、`_神庙传奇`… —— **与 agent 要做的遭遇战图最可比** |
| 战网官方图 | 3 | `神庙传奇`、`绿石花园`、`蔚蓝深海` |
| 玩家精品图 | 11 | `33超美丽图16油井`、`[对决]天际能源站2.0`、`GYyaosai`、`magmatropolis_mp6`、`Verdansk1.1`、`World_Battle`、`（33大图）双城之战`、`（COR）皇城PK`… |

这批语料同时是：**范例与美术规则来源**、**素材/prefab 抽取来源**、**评测对照集**。（不再作为训练集——模型开发已移出本次范围。）

### 2.6 ra3_map_workspace —— 技能、Lua 工程与 API 知识库

| 项 | 事实 |
| --- | --- |
| Skills | 9 个：`ra3-map-authoring`（统筹，含 20.5 KB 的 `references/execution.md`）、`ra3-map-lua4`、`ra3-csharp-map-editing`、`ra3-maputils-companion-http`、`ra3-lua4-syntax-http`、4 个 `openspec-*` |
| Rules | `.agents/rules/{ra3-lua-rule, melee-train-map-str-version}.mdc` |
| API 知识库 | `RA3CoronaMapLuaLib` v1.5.2：`lib/` **117 个 Lua 模块**、`example/` 25 个示例、`origin_funcs/` 18 个引擎函数对照、`doc/` **503 条脚本动作 + 149 条条件** dump |
| Lua 部署链 | `/api/lua-import/scheme/import`；`tools/import_lua.ps1`（151 行：身份校验 → 备份 `.map`/`map.str` → 导入 → 覆盖 map.str → SHA256 → 失败回滚） |
| 地图工程 | `special_workspace/` 9 个（`melee_train`、`sakura_battle` 最完整，带 `docs/` 与 `openspec/`）；`workspace/` 4 个 |
| **致命缺口** | **全工作区没有任何 MCP 客户端配置**（`reasonix.toml` 的 `[[plugins]]` 全注释、`.codex/config.toml` 只有 LM Studio provider、无 `*mcp*.json`）。`execution.md` 描述的 Automation MCP 在客户端侧**从未接线** |
| 其他不一致 | `emmyrc.json` 写 `Lua5.4` 且 `workspace.library` 为空（与 Lua 4.0 约束冲突）；`RA3CoronaMapLuaLib/.lib_meta.json` 只覆盖 91/172 个 .lua（`lib/.lib_meta.json` 才同步）；`example/*/.lib_meta.json` 指向旧机器路径 `H:\workspace\...`；SDD 指南推荐的 `docs/{project,technical_constraints,decisions,features}` **零落地** |

## 3. 缺口清单（按对"优秀地图"的阻碍强度排序）

| 编号 | 缺口 | 证据 | 性质 |
| --- | --- | --- | --- |
| **G1** | **美术与内容密度**：单色铺满、树成规则图案、材质过渡无依据、缺地标与细节 | 真渲染图 + `art-review.json`=`needs-iteration` | 方法与内容问题 |
| **G2** | **素材体系未成型**：只有编辑器声明名称表（`editor-declared`），无持久占地、无 prefab、无 Mod 维度 | 占地靠每次调用显式传参。**缩略图并不像原先设想的那样可用**：实测 1688 个物体只有 482 个有同名编辑器截图，其余 2559 张属于单位/建筑名单 | 工程量中等，收益最大 |
| **G3** | **各部件从未接线**：Automation MCP 无任何客户端配置；30033/30034 未运行 | 全工作区无 `*mcp*.json`；端口探测为 false | 最低成本、最高杠杆 |
| **G4** | **未提交 / 未交付**：全部在 `feat/automation`；无安装器、无客户端配置写入、无版本发布；`AGENTS.md`/`CLAUDE.md` 未收录新项目 | `git status` 全部未跟踪 | 低风险，必须先做 |
| **G5** | **两套系统未分工**：A=Ra3MapSharp（net6.0，编辑内核）；B=Ra3MapUtils v2（net10.0-windows，外壳与服务） | TFM、MCP 宿主、素材格式、地图模型都不同 | 架构决策，越晚越贵 |
| **G6** | **编辑器桥接未做**：只能靠 CLI 任务与隐藏窗口；不能在运行中的编辑器里设相机/截图/选区/增量操作 | 插件 API 已确认存在，未做探针 | 有可行性风险，先探针 |
| **G7** | **玩法层未进内核**：道路身份与编辑、队伍/玩家配置、触发器脚本、资源经济模板、基地 prefab、水域/海军拓扑均无命令 | 状态文档第 2、5 项；`the_train` 里用外部 C# 脚本兜底铁路 | 工程量中等 |
| **G8** | **性能与健壮性未测**：256 格图 4 次修订即 `index.json` **30.1 MB**（单条 `designEntities` 6.7 MB，无去重）；无 500/800 实测；对象上限 2000；`edits.prepare` 持会话锁 | 实测文件大小 | 规模化前必做 |
| **G9** | **工作区与文档不自洽**：skills 写死绝对路径不可移植；`emmyrc.json` 配成 Lua5.4（与 Lua4.0 冲突）；`RA3CoronaMapLuaLib/.lib_meta.json` 只覆盖 91/172 个 .lua；SDD 指南的 `docs/*` 结构**零落地** | 2.6 表末行 + 子代理逐项核对 | 卫生问题，影响可复现性 |
| **G10** | **水域与灯光被排除**：但"优秀美术"通常需要水面与光照 | 多份文档的"范围更新" | 待决策 |
| **G11** | **伴侣侧未完成项会误导规划**：KnowledgeBase 是空壳（零数据）；`Ra3MapFacade.NewMap` 在伴侣侧无调用点（"从零建图"未端到端验证）；v1/v2 双树并存、插件宿主只在 v1 | 2.3 表"未完成项"行 | 需先澄清交付目标树 |
| **G12** | **地形生成只有保底路径**：模型层已移出本次范围，地形塑造只能靠模板 + 确定性工具（雕刻/平滑/坡道/噪声）+ `origin_maps` 范例反推 | 本文件 §2.4 的范围决定 | 需要在 S2 用方法与内容补足，不能等模型 |

## 4. 目标架构与 computer use 的定位

```text
   ┌────────── 用户目标 / DesignSpec / 精品图范例（origin_maps 65 张）──────────┐
   ▼                                                                          │
   Agent（模型 + ra3-map-authoring skill）                                     │
   │                                                                          │
   ├─ [内容层] 素材目录/缩略图3041/prefab(.object.bin)/模板/范例  ── 待建(S1)    │
   ├─ [评价层] 固定镜头评审 rubric + 静态验收 + 质量评测  ────────── 部分已有     │
   │                                                                          │
   ▼  MCP（唯一稳定工具面，当前未接线）                                        │
   ┌──────────────── Ra3MapSharp Automation 内核（51 命令 / 事务 / 修订 / 保护）┐ │
   │ 地形 纹理 对象 道路 路径点 玩家 队伍 脚本 设计实体                          │ │
   └───┬───────────────┬────────────────┬──────────────────────┬─────────────┘ │
       ▼               ▼                ▼                      ▼               │
  Facade/Parser   反馈：诊断图/真鸟瞰图  地形：模板+确定性工具   编辑器/游戏桥接     │
       ▼               ▼                ▼                      ▼               │
   .map 工程     WbLauncher 渲染    ONNX 推理 + 数据集      插件(30033)/游戏(30034)│
       └───────────────┴────────────────┴──────────────────────┴──────────────►┘
```

**computer use 的定位：不要以"屏幕鼠标自动化"为主线。** 按可靠性分级：

1. **官方 CLI 任务（已可用）**：`--export-overview` 拿真实鸟瞰图；`--open-map` 交人工。真实反馈主力。
2. **进程内桥接（推荐主攻，已有两块拼图）**
   - **编辑器侧**：插件是进程内 C#，能直接读写 `MapDataContext`（有 9 个官方示例）。目标是补 CLI 给不了的能力：`reload_map`、`set_camera`、`capture_view`、`select_region`、编辑器内执行、`get_status`。
   - **游戏侧（已存在）**：30034 的 `TinyNative` 已提供 `pause/fast/single-step/fast-to`、`/api/lua/execute`、`/api/objects/{id}`、`/api/objects/selected`。**"试玩观察"不需要屏幕自动化**，采样成本极低。
3. **UI 自动化（最后手段）**：仅用于以上都不可行、且低频无 API 的操作。

**写入纪律不变**：同一时刻只允许一方写地图文件。内核导出指定 Revision 的副本 → 编辑器/游戏打开副本 → 采集同版本截图。

## 5. 路线图

估算为熟悉本仓库的一名开发者的有效人日，非承诺工期。

### S0 收口与接线（3–5 天）——直击 G3 / G4

1. 提交 `feat/automation`（含 `docs/`、`scripts/`、两个测试项目）。
2. 把 Automation MCP **接进真实客户端配置**：写工作区的 MCP 配置（stdio：`dotnet <Agent.dll> --mcp --launcher <WbLauncher.exe>`），并提供一个探测脚本确认工具清单可见。这是当前**最高杠杆**的一件事。
3. `AGENTS.md` / `CLAUDE.md` 补 Automation 与 Agent 两层的结构、命令与"变更-验证矩阵"。
4. 写 ADR：**三部件分工**与 **TFM/版本矩阵**；清理 `nul`。
5. 修正工作区卫生项：`emmyrc.json` 的 Lua 版本、`.lib_meta.json` 同步、skill 里的绝对路径改为可重定位说明。

**验收**：全新克隆按文档可构建；Parser / Automation / Agent 三套测试全绿；任一 MCP 客户端能列出 51 个工具并完成一次"新建→存盘→导鸟瞰图"；`nul` 与配置不一致项消除。

#### S0 执行记录（2026-09-18）

| 项 | 状态 | 证据 |
| --- | --- | --- |
| 2. MCP 接线 | **已完成** | 工作区三处配置（Codex `.codex/config.toml`、Cursor `.cursor/mcp.json`、Reasonix `reasonix.toml`）；`codex mcp list` 实测列出 `ra3-map` |
| 2. 探测脚本 | **已完成** | [scripts/mcp_probe.ps1](../scripts/mcp_probe.ps1)：握手 + **51 个工具**（51/51 带 inputSchema）+ 建图/保存/落盘 + **真实渲染 17 秒 succeeded** + `preview.inspect` 返回 1024×1024 PNG（1.7 MB ImageContent） |
| 3. AGENTS / CLAUDE | **已完成** | 两层结构、依赖方向 `Facade <- Automation <- Agent`、测试命令、变更-验证矩阵、PowerShell 5.1/BOM 坑位 |
| 4. ADR | **已完成** | [adr/0001-component-boundaries-and-tfm-matrix.md](adr/0001-component-boundaries-and-tfm-matrix.md)（三部件职责、进程边界、TFM 矩阵、D-C 排除模型层） |
| 4. 清理 `nul` | **已完成** | 已删除（内容为误重定向产生的 `del: command not found`） |
| 1. 提交 | 进行中 | 内核+测试、文档+工具两组提交 |
| 5. `emmyrc.json` Lua 版本 | **已完成** | `Lua5.4` → `Lua5.1`（EmmyLua 无 Lua 4 选项，取最保守的近似）；并在 `ra3-map-lua4` 技能中写明"语言服务器不报错 ≠ 语法合法" |
| 5. skill 绝对路径 | **不需处理** | 实测仅 12 处，且 `execution.md` 已声明"路径不存在时重新定位，不要求复制到固定盘符"；其余为指向本机真实仓库的位置提示 |
| 5. skill 范围口径 | **已完成** | `ra3-map-authoring/references/execution.md` 原把 AI 地图工作台列为"可选地形来源"，已按 D-C 改为"已移出范围" |
| 5. `.lib_meta.json` 同步 | **不做（生成物）** | 37 个文件由伴侣侧的 Lua 库管理生成，内含 `FilePath`/`LibPath` 绝对路径；手工编辑会被下次生成覆盖，应由工具在移动后重新生成 |
| skill 工具口径核对 | **已完成** | 把 `execution.md` 提到的 31 个命令与 `command-schemas.json` 的 51 个逐一对账：**0 个"文档有、实现无"** |

**S0 实测发现（供后续阶段注意）**：作业状态字段是 `data.state`（不是 `data.status`），取值 `running|succeeded|failed|cancelled`；128×128 图的真实鸟瞰图渲染约 **17 秒**。

#### D1 / D5 实测记录（2026-09-18）

**D1 渲染基线已落定**：`map-task-launch.json` 的 `modConfig` 指向 `CoronaData\RA3_english_1.12.Skudef`，改动前备份在同目录 `map-task-launch.json.before-d1-baseline.bak`。依据是同一张官方图 `官方地图_无限岛_InfinityIsle` 在两个 profile 下各渲染一次：corona_6.321 出 4,940,899 B、RA3_english_1.12 出 4,940,581 B，降采样到 256×184 后仅 **0.37%** 像素有差异（meanAbsDelta 0.107、maxDelta 26）。两者对官方资源几乎等效，取原版基线符合 D1。

**D5 规格有充分先例**（`map.open` + `map.info` 实测语料库）：

| 语料库地图 | 可玩尺寸 |
| --- | --- |
| `Verdansk1.1` | 1000×1000 |
| `World_Battle` | 750×750 |
| `官方地图_卡巴纳_CabanaRepublic` | 680×480 |
| `magmatropolis_mp6` | 620×600 |
| `官方地图_混乱火山湖_CalderaOfChaos` | 540×540 |

故 500×500（+border 8 = 516）落在既有发布范围内，无需为尺寸本身做引擎侧验证。

**D5 性能基线**（新建 → 3 次 `design.apply` → 保存）：

| 指标 | 256×256 | 500×500 |
| --- | --- | --- |
| `map.create` | 0.5 s | 0.9 s |
| `map.save` | 0.1 s | 0.5 s |
| `.map` 体积 | 7.4 KB | 17 KB |
| Agent 峰值 RSS | 98 MB | 175 MB |

**历史索引膨胀已修**（原列在 S5，因阻塞 D5 而提前）。根因不是"地图大"而是结构性的：`index.json` 每个修订条目都存该修订的**完整状态**，其中设计实体的 `heights` 是逐格 `{x,y,before,after}` 对象——一个 200×200 platform 就是 40,000 个对象；实测 256 与 500 的索引体积**逐字节相同**，证明成本只由实体覆盖面积决定。此外文件按缩进写盘，仅 minify 即省 3.2×。

修法（内存模型不变：每修订仍是完整状态，撤销/重做仍是直接查表）：磁盘上改为**按修订增量**，只写负载真正变化的实体与被移除的实体 id，读取时前向回填；`schemaVersion` 8 → 9，旧读取器会明确拒绝而不是静默把每个修订读成"没有设计实体"。

| 同一负载（1 个 200×200 platform + 120 个对象） | 修复前 | 修复后 |
| --- | --- | --- |
| 4 次修订后 `index.json` | 21,908.3 KB | **1,730.1 KB** |
| 每修订增量 | ≈ +5,476 KB | **≈ +8 KB** |
| 峰值 RSS（256×256） | 296 MB | 214 MB |

真正的收益是**增长行为**：从"随会话长度线性无界增长"变为"只随真实编辑增长"。旧 v8 工作区已用改动前生成的真实文件实测：可直接打开并自动迁移（491 KB → 153.3 KB，设计实体 `base-1` 的 3600 格完整恢复，`designHash` 不变且 `designDirty=false`，说明哈希算法未被改动）。

**仍未处理**：内存中每个修订仍保留完整状态，长会话 RSS 仍随"修订数 × 设计实体总面积"增长；`waypointObjectIds`/`unitObjectIds` 也仍按修订全量存储。

#### S1 执行记录（2026-09-18，进行中；玩法/脚本按本轮决策推迟）

**素材目录已建成**（`catalog.json`，232.9 KB，`catalogHash` 随内容变化）。新增三个入口：`--build-catalog` 生成、`assets.catalog_info` 汇报、`assets.search` 检索。

| 项 | 数量 | 说明 |
| --- | --- | --- |
| 纹理 | 406 | 来自引擎 `TextureEnum`：`Grass` 110、`Pavement` 99（含 `Pave` 15）、`Dirt` 51、`Transition` 45、`Cliff` 24… |
| 物体 | 1688 | 编辑器 `ObjectCategory.json` + `ObjWndTrans.json` |
| 分类 | 24 | 树草 222、悬崖斜坡 277、道路块 184、墙 139… |

**新增的可用语义**（此前完全没有）：纹理按 `<表面>_<主题><变体>` 解析出 `surface` / `theme` / `kind`，主题即地域（Yucatan 45、CapeCod 39、Heidelberg 33、Iceland 24…），`Transition_*` 单独标为过渡材质——这是「材质过渡有依据」的第一块砖。

两条**从数据里学到的**规则，都写进了测试：
- `Pave_*` 与 `Pavement_*` 是同一表面，合并（→ 99）。
- **主题只在「余部全小写」时才算截断**：`Heidel` 是 `Heidelberg` 的截断（→ 33）；而 `Geneva` 与 `GenevaClockA/B/C/D` 是不同地点，绝不能合并——最初的宽松前缀规则把 11 个 Geneva 吞进了 `GenevaClockA`，已修。

**修正一个先前的错误假设**：§3 G2 与 §5 S1 原写「3041 张缩略图就在编辑器里却没接入」，读起来像接上就有物体图。实测（`assets.search onlyMissingScreenshot`）：**1688 个物体只有 482 个有同名截图，1206 个没有**；未匹配的 2559 张是单位/建筑名单（`AlliedAirfield` 之类），属于另一套语义。所以物体图册必须靠**自建渲染**补，不能指望编辑器截图。

**新增/变化的接口**：`assets.catalog_info`、`assets.search`（按 `kind`/`surface`/`theme`/`category`/`onlyMissingScreenshot`/`onlyTransition` 过滤；未指定 `kind` 时由过滤器推断——设了 surface/theme 即只搜纹理）。命令总数 51 → 53。

**顺带修掉一个协议级坑**：宿主现在容忍首条消息前缀的 UTF-8 BOM。实测每次运行的第一条 JSONL 都会带上 BOM，导致**整个会话的第一个请求**必然失败，且错误信息是误导性的 `'0xEF' is an invalid start of a value`。修在 `AgentJson.WithoutBom`，有测试。

**物体图册生成器已完成**：`--build-album <目录>` 把物体摆到网格测试图上、真实渲染、再切成单个物体的图，并写出 `album.json`（含每张图的来源渲染哈希）。

- **为什么必须自建**：1688 个物体里有 **1206 个没有编辑器截图**，靠索引救不了它们。
- **分辨率是硬约束**：鸟瞰图长边固定 2048 px。128 格图 = 16 px/格，长椅只有 16 px；**64 格图 = 32 px/格**，代价是每批对象数减半。取 64 格 + 14 格间距 → 每批 25 个、共 49 批。
- **没有占地数据也能正确取景**：固定裁剪框不可能对「长椅」和「打捞船」同时合适（实测长椅只占画面 1/6）。测试图地面均匀，所以物体就是「与主色不同的像素」；再按**连通分量**取离窗口中心最近的一坨，避免邻物体与阴影把包围盒撑大——这是长椅从「一个小点」变成「填满画面」的关键，已目视确认。
- 每张图都带来源渲染的 `mapContentHash` / `imageHash` / `rendererConfigHash`，可回溯到具体一次渲染；摆放失败的物体带错误码记入 `failures`，不静默丢弃。
- 接口：`assets.album`（经 MCP 返回 PNG ImageContent）＋ `assets.search` 报 `hasAlbum`/`albumFile`。命令总数 53 → 54。

**占地目录已完成**（`--build-footprints`）：从**已有图册渲染**量得，不重新出图。编辑器数据里没有任何声明尺寸，所以这是唯一可得的一手来源——正交俯视下物体的像素范围就是它的平面尺寸。

两条**走错又纠正**的路，都值得记下来：
- 第一版用「窗口内主色」当背景，实测 **445/1181（37.7%）不可信**，且分布双峰（大量物体被测成满窗口 8 格，另一批被测成 0.09 格）。根因是物体占据窗口大部分时，主色变成**物体自己的颜色**，掩膜整个反转。
- 改用「多批次逐像素中位数」合成地面参考也**不成立**：各批次复用同样的 25 个位置，那个「中位数」其实是 5 个**不同物体**的中位数。正确做法是让 `--build-album` 先渲一张**空网格图**并存进图册，占地量测对它做差分。

- **已知偏差**：量测包含渲染器绘制的阴影，是**偏保守的上界**；对不阻挡通行的贴地装饰物会偏大。`Source` 区分 `rendered-extent` 与 `declared`，不掩饰。
- 无法量测的（贴地贴花、边缘裁切）记入 `failures` 并带原因，不假定为 1×1。
- 手改值写在**独立覆盖文件**里，重建量测不会抹掉它们。
- **消费端已升级**：`objects.scatter` 在调用方未传 `footprints` 时自动取用目录值（仅当相关类型**全部**有量测），结果里报 `footprintSource: "catalog"`；不完整则维持原行为（跳过碰撞检查并记入 `notEvaluated`），**不猜**。这正是「从调用参数升级为持久目录」的落点。

**实测结果**：1200 个目标中 **973 个可量测**（中位数 3.12 格、长椅 1.28×0.72、公交站 2.81×1.5），227 个失败并**按原因分类**——失败本身是有用信息，不是遮掩：

| 失败码 | 数量 | 含义 |
| --- | --- | --- |
| `NO_VISUAL_DIFFERENCE` | 170 | 俯视与地面**逐像素一致**：贴地道路/停车场贴花、纯灯光 |
| `NO_FLAT_REFERENCE` | 54 | 整窗与平地参考不同：**改地形**的物体（`BB_Cliff*` 等悬崖） |
| `ONLY_DEBRIS` | 3 | 只检出碎屑 |

可量测项中 10.1% 小于 0.5 格（细杆、薄片一类），保留原值不强行取整。跨过阈值（12→64）失败数几乎不变，说明失败与阈值无关，而是方法本身对这两类物体不适用。

接口：`footprints.get` / `footprints.list` / `footprints.set`；`assets.search` 同时报 `footprintWidthCells`/`footprintDepthCells`。命令总数 54 → 57。

**下一步（S1 收尾）**：把量测覆盖到已有编辑器截图的 482 个物体/建筑（`--build-album --album-all`，约多 20 次渲染），然后进入 S2。

### S1 内容库（素材 / prefab / 范例）—— 直击 G2（10–15 天）

- `tools/catalog-builder`：从编辑器安装目录与 `origin_maps` 抽取并固化
  - `data/config/ObjectCategory.json` + `ObjWndTrans.json` + `ObjectCategory_<mod>.json`
  - `data/objectScreenShot/*.jpg`（3041 张，但**只能覆盖 482/1688 个物体**；见 S1 执行记录）
  - `data/objectsTemplate/*.object.bin`
  - `ScriptActionNew.json` + `ScriptConditonNew.json` + `scriptTrans.json`
  - 从 65 张精品图抽取"已使用过的对象/纹理组合""已验证地图片段"
  产出带内容哈希与来源的版本化目录（`objects.json` / `textures.json` / `prefabs/*` / `thumbnails/*` / `templates/*`），**按 Mod 分区**。
- 新 MCP 工具族：`assets.catalog_info`、`assets.search`（返回**缩略图 PNG**）、`assets.album`、`footprints.get/set`（**持久化**占地）、`prefabs.list/get/place`。
- 打通 `.object.bin`：模板 = 一组对象 + 路径点 + 道路的相对坐标集合，支持旋转与对称派生（复用 `derived.rotate180`）。
- 语料侧只读引用 `origin_maps` 的 65 个地图包做统计与抽取，**不依赖被排除仓库的 `dataset/` 管线**。

**验收**：`assets.search "海岸 岩石"` 返回带缩略图候选且可直接 `objects.place`；`prefabs.place` 的结果与编辑器手工 `Shift+C` 放置一致（真实鸟瞰图对照）；同输入同结果、失败整次回滚。
**风险**：`.object.bin` 格式需逆向（差分法：编辑器放置 → 保存 → 用 Parser 读回）。

#### S2 执行记录（2026-09-18，进行中）

**美术规则已从语料量出**（`--analyze-corpus`，`art.rules` 命令汇报）。扫描 `origin_maps` 的 **63 张 shipped 地图**（3 张因 Parser 报 `String (3294770) not registered` 无法解析，记为失败而非中断整轮——语料是外部输入，单图失败必须隔离）。

| 指标 | 中位数 | P25 – P75 |
| --- | --- | --- |
| 单图地形纹理种类 | **30** | 21 – 37 |
| 主导纹理占比 | **28%** | 21% – 46% |
| 前三纹理合计 | 56% | 48% – 72% |
| 发生混合的格占比 | **19%** | 12% – 24% |
| 物体密度（每千格） | **12.0** | 9.3 – 14.8 |
| 聚簇指数（8 格样方 方差/均值） | **2.99** | 2.39 – 3.81 |

这些数字把三条模糊要求变成了阈值：**「不要一种材质铺满」**→ 主导纹理 ≤ 46%；**「装饰密度」**→ 12 个/千格（D5 的 500×500 图约 **3000 个物体**）；**「禁止规则散布」**→ 聚簇指数应约 3，规则网格会得到小于 1。

**修正一个我先前的夸大**：我曾把 45 个 `Transition_*` 纹理称作「材质过渡的第一块砖」。实测：**只有 23/63 张图用到它，占比中位数 0%（上限 3.6%），出现频率前 40 的纹理配对里一次都没出现**。真正的过渡手段是**两种普通表面材质互相混合**（19% 的格）。`Transition_*` 是声明存在但基本不用的家族——这是「先量再断」的又一个例子。

**最有价值的产出是实测纹理配对**——哪些材质真的在一起用：

| 配对 | 出现在 |
| --- | --- |
| `Grass_Yucatan02` + `Grass_Yucatan07` | 14 张图 |
| `Grass_Yucatan02` + `Grass_Yucatan04` | 12 张图 |
| `Grass_Yucatan02` + `Rock_Yucatan01` | 11 张图 |
| `Dirt_Yucatan03` + `Dirt_Yucatan04` | 10 张图 |

**装饰配方**（各分类占已放置物体的比例，中位数）：树草 44%、墙 8%、悬崖斜坡 6%、冰块 6%、浮岛要塞装饰 4%、道路块 3%、铁路 2%、灯路牌 2%、平民载具 1%、桌椅 1%、碎石 1%。

**接口**：`--analyze-corpus`、`art.rules`。命令总数 57 → 58。

**下一步**：固定镜头评审集（`review.render_set`：总览 + 4 局部 + 斜视，带坐标与哈希，**与语料同镜头对照**）+ rubric，把 `art-review.json` 从 `needs-iteration` 推出来。
#### S2 评审闭环（2026-09-18）

**`review.render_set` 已建成**：一次渲染 → 北朝上总览 + 4 张**固定比例**局部（1/4、3/4 象限，**不是**按出生点对齐，因为「与语料同镜头对照」要求每张图裁切偏移一致）→ 合成**一张 1024×1280 评审大图**（MCP 一个 image 块即可看全）→ 同一个修订同时产出 rubric 判决。

`art.profile`（内核命令）负责测量，评审集负责判分——**测量口径与语料分析是同一份代码**，否则阈值不可比。rubric 每一条都写明：实测值、阈值、以及**阈值出处**。

**`combatReadability` 明确标为 not-evaluated**：它需要人眼看渲染图，统计量判不了。宁可说不判，也不编一个替代指标。

**首次真实评审**（256×256 演示图，按实测配对选材：`Grass_Yucatan02` 打底 + 21 种 Yucatan 纹理 + 260 棵树）：

| 检查 | 实测 | 阈值 | 结果 |
| --- | --- | --- | --- |
| materialVariety | 22 | ≥21（语料 P25） | ✅ |
| dominantMaterialShare | **63.6%** | ≤46%（P75） | ❌ |
| transitionCoverage | **4.4%** | ≥12%（P25） | ❌ |
| decorationDensity | **3.97** | 9.3–14.8 /千格 | ❌ |
| clumping | **0.92** | ≥2.39（P25） | ❌ |
| landmarkPresence | 0 | ≥3 类 | ❌ |
| combatReadability | — | — | n/a |

**判决 `needs-iteration`，而且评审大图与数字互相印证**：画面就是绿色底料铺满 64%、21 种材质呈**明显的圆形规则排布**、树是**直线点列**——正是路线图 S2 明令禁止的「单材质铺满」与「规则几何散布」。

### 这次评审抓出的真问题（下一步）

**`objects.scatter` 自身会产出违反聚簇规则的图**：它用 `minDistanceCells` 做最小间距排斥，结果是**欠离散（规则）**分布，实测聚簇指数 **0.92**，而语料好图是 **2.39–3.81**。也就是说，用现在的散布工具去装饰，**系统性地**做不出 shipped 地图那种成群分布。

修法方向：散布应先生成**簇心**（例如 Poisson 簇过程或分层播种），再在簇内抖动，而不是全图均匀排斥。这是 S2 的下一件事，且**有量化验收**（聚簇指数落入 2.4–3.8）。

**接口**：`art.profile`、`review.render_set`。命令总数 58 → 60。
### S2 美术与质量闭环 —— 直击 G1（12–18 天）

- **语义标签体系**：纹理标（色调/颗粒/尺度/适用高度/水陆/主题），对象标（用途/主题/尺寸/占地/可放置高度）。让 agent 按规则选材，而不是猜名字。
- **从 65 张精品图提炼美术规则**：官方遭遇战图（`官方地图_*`）作为"什么算好"的对照；用它们统计纹理搭配、装饰密度、地标分布，产出可执行的取舍规则。
- **固定镜头评审集**：`review.render_set`（总览 + 4 局部 + 斜视），附坐标与哈希；rubric 固化为模板（主题一致 / 过渡 / 地标 / 疏密 / 战斗可读性），**与 `origin_maps` 同镜头对照**。
- **图册工具化**：`create_texture_palette.py`、`create_object_palette.py` 升级为 MCP `assets.palette`。
- **美术方法写进 skill**：骨架 → 地标 → 地域差异 → 材质过渡 → 装饰密度梯度 → 局部修正；明确禁止"单材质铺满""规则几何散布"，要求每区域有选材依据。

**验收**：同一张双人图两轮迭代后达到：无单色铺满、树不成规则图案、≥3 个可辨识地标、材质过渡有依据、局部镜头能看出起伏；固定镜头对照 + rubric 判定通过，`art-review.json` 才能离开 `needs-iteration`。
**风险**：主观质量目标，必须靠 rubric + 固定镜头把主观变可比。

#### S2·2 散布聚簇修复与首次达成（2026-09-18）

**`objects.scatter` 的欠离散问题已修**。原因：它靠 `minDistanceCells` 做最小间距排斥，产出均匀分布。现在支持 **簇心播种 + 簇内抖动**（`clusters` / `clusterRadiusCells` / `clusterSpacingCells`），并把实测 `clumpingIndex` 回写在结果里，让调用方**当场**能看到自己是 0.92 还是 4.0，而不是等评审才发现。

- 均匀模式（`clusters=0`）**保留原行为**，只报告指数——不悄悄改变既有输出。
- 4 个测试固定了验收：均匀模式 **< 1.6**，簇心模式 **≥ 2.4**（语料 P25），且簇化后仍摆满全部请求的对象。

**按规则重做一张图，评审从 `needs-iteration` 推到 `acceptable`**（256×256，43 种材质、230 处抖动贴片、720 棵簇化装饰、11 个地标）：

| 检查 | 首版 | 终版 | 阈值 |
| --- | --- | --- | --- |
| materialVariety | 22 | **43** ✅ | ≥21 |
| dominantMaterialShare | 63.6% ❌ | **16.8%** ✅ | ≤46% |
| transitionCoverage | 4.4% ❌ | **13.5%** ✅ | ≥12% |
| decorationDensity | 3.97 ❌ | **11.15** ✅ | 9.3–14.8 |
| clumping | 0.92 ❌ | **4.00** ✅ | ≥2.39 |
| landmarkPresence | 0 ❌ | 2 ⚠️ | ≥3 类 |

`scatter` 同时报出 `clustered clusters=26 clumping=4.058 source=catalog`——簇化与占地自动解析都在生效。

### 但必须说清楚：**判决过了，图并不好看**

评审大图显示这张图是一片**随机拼贴**：材质是明显的圆形印章、装饰是纠缠的团块。**rubric 全过，视觉上不合格。**

这不是 rubric 实现有 bug，而是它的**边界**：它度量的是**必要条件**（「没有明显坏掉」——单一材质铺满、规则网格散布、密度差一个数量级），**不是充分条件**。所有检查都是**全图聚合量**，而**空间构成**——材质是否形成有理由的连续区域、是否跟着地形走、看起来是否有意图——**一条都没测**。

具体缺口可量化：我的图有 230 个随机贴片，**每个材质的面积被切得极碎**；shipped 地图里材质是**大块连续区域 + 有意识的细节**。缺的检查是**斑块结构**。**注：我最初猜的指标（平均斑块尺寸、最大连通块占比）实测并不成立**，见下面的 S2·3。

**landmarkPresence 只到 2** 同样是真话：`雕像和石柱`、`冰块` 这两类里有占地测量的成员不足 3 个——这是 S1 「占地只覆盖了 1200/1688 个物体」的直接后果。

**下一步（顺序）**：① 加斑块/连通性检查（并用语料标定阈值）；② 补量剩余物体的占地；③ 把「按地形与主题组织材质」的方法写进 skill，而不是继续随机贴片。
#### S2·3 斑块形状检查（2026-09-18）—— 两条被数据否掉的猜想，第三条成立

目标：让 rubric 能抓住「圆形印章式拼贴」。我先猜了两个指标，**语料实测把两个都否掉了**：

| 我猜的指标 | 语料中位（P25–P75） | 我的拼贴图 | 结论 |
| --- | --- | --- | --- |
| 平均斑块尺寸 | **4 格**（2–5） | 97 格 | 方向正好相反：shipped 地图**比我假设的碎得多** |
| 最大连通块占比 | **46%**（33%–58%） | 0.56 | **落在语料带内**，抓不到 |

如果按我猜的阈值（例如「最大连通块 ≥ 85%」）去做，**语料自己的图会全被判失败**（中位只有 46%）。这是「先量再断」第三次救回一个错误结论。

**成立的指标是斑块紧凑度**（周长²/面积，按面积加权）：正圆约 12.6，边界越自然越高。

| | 语料 | 我的拼贴图 |
| --- | --- | --- |
| 紧凑度中位 | **559.7** | **158.7** |
| P25–P75 | 320.2 – 959.6 | 低于 P25 → **失败** |

它抓住了：我的材质是圆形印章（低紧凑度），而 shipped 地图是锯齿状自然边界。

**评审重跑结果**：`patchNaturalness = 158.73 < 320` → **fail**，而 `materialCohesion = 0.56` 仍 **pass**——两个检查的对比正好证明「尺寸类指标看不见这个失败模式，形状类指标才看得见」。判决由 `acceptable` 回到 **`needs-iteration`**，**这才是这张图应得的评价**。

已知局限：这条只设下限（拦住过于规整的），**没有上限**——异常锯齿的图不会被拦。

**接口**：`art.profile` 与 `review.render_set` 增加 `patches`（斑块数、中位尺寸、最大连通块占比、每千格斑块数、平均尺寸、平均紧凑度）；`--analyze-corpus` 增加对应分布。

**下一步**：材质必须**按地形与主题组织**（跟着高度/坡度走、海岸是沙、内陆是草、高处是岩），而不是随机贴片——这是「好看」剩下的那一半，也是 `combatReadability` 之外唯一还没被度量的维度。
### S3 编辑器桥接 + 游戏侧观察闭环 —— 直击 G6（探针 3 天 + 实施 10–15 天）

- **S3.0 探针（先做）**：枚举 `MapCoreLib.Core.Scripts.*` 与 `MapCoreLib.dll`/`NewUI.dll` 可用类型，确认插件触发方式（菜单/加载/保存）与是否存在相机、截图、选区等 hook。产出"能做/不能做"清单。
- S3.1 `BridgePlugin`：`reload_map`、`get_status`。
- S3.2 `set_camera` + `capture_view`（带 Revision/哈希/坐标映射），与 `preview.inspect` 统一坐标语义。
- S3.3 `select_region` + 编辑器内执行（避免文件交接）。
- S3.4 把 30034 游戏侧接口接进 Agent：`playtest.observe`（暂停/单步/快进到帧 + 查询选中物体 + 读日志），作为**可选的抽样验证**（用户已排除"必须游戏加载"，此处是"可选增强"）。
- 降级路径：探针失败则退回 CLI 任务 + 人工截图，**不影响 S1/S2**。

**验收**：不重启编辑器即可刷新地图、把相机移到指定格、截指定视角并拿哈希与坐标矩阵；失败有明确错误码；同一时刻只有一个写入方。
**风险**：**最高**。插件 API 能力未证实；`NewUI.dll`/`wbCore.dll` 是高度改造的闭源二进制。

### S4 玩法层 —— 直击 G7（15–20 天）

- 道路：身份、编辑、与路径点/对象交互（`the_train` 已有外部 C# 兜底实现可参考）。
- 队伍/玩家配置命令；基地与资源 prefab；资源经济校验（收益、矿权，而非只有距离）。
- `scripts.*` 命令族：生成触发器骨架，按 `ScriptActionNew.json`/`ScriptConditonNew.json`/`scriptContent.xsd` 校验合法性；复用 `RA3CoronaMapLuaLib/doc` 的 **503 动作 + 149 条件** 与 `ra3-map-lua4` skill；Lua 4 语法校验走 30033。
- 地图类型模板：双人/四人遭遇战（对照 `官方地图_*`）、任务图（含胜负脚本骨架、`map.str`、附属文件清单）。

**验收**：生成含胜负规则的双人图；Lua 4 语法验证通过；脚本结构在编辑器中正确显示（编辑器证据）；导出包含 `map.str` 与附属文件的完整地图包。

### S5 规模化与交付 —— 直击 G5 / G8（8–12 天）

- 性能与健壮性：500/800 图实测；**修 `index.json` 膨胀（`designEntities` 每修订全量序列化、无去重，256 格图 4 次修订已 30 MB）**；候选/快照配额；崩溃恢复与跨进程 exactly-once 的诚实表述。
- 交付：安装器 / 一页式接入文档；**MCP 客户端配置的自动写入**（当前"未自动修改任何客户端配置"）；版本矩阵与兼容性检查。
- 与伴侣侧的集成收口：确定 30033/30034 与 Agent MCP 的共存方式（谁提供工具、谁只做转发），避免两个工具面互相覆盖。

**验收**：大图打开/编辑/保存/预览的实测指标入文档；历史磁盘增长受控；新机器按一页文档完成接入并列出全部工具。

## 6. 建议立即执行的 10 件事

1. 提交 `feat/automation`，跑绿三套测试。
2. **把 Automation MCP 写进真实 MCP 客户端配置**，并验证工具清单可见（当前最高杠杆）。
3. 补 `AGENTS.md`/`CLAUDE.md` 的 Automation 与 Agent 章节 + 变更-验证矩阵。
4. 写三部件分工 ADR 与 TFM/版本矩阵。
5. 跑 S3.0 插件 API 探针（成本低、信息量最大、决定 S3 命运）。
6. 写 `catalog-builder`，先把 3041 张缩略图与 Mod 分类接进 `assets.*`。
7. 把 `footprints` 从"调用参数"升级为"持久目录"。
8. 逆向 `.object.bin`（编辑器放置 → 保存 → Parser 读回差分）。
9. 修 `index.json` 膨胀 + 建 500/800 图性能基线。
10. 用 S1/S2 成果重做双人示例，以 `官方地图_*` 为对照把 `art-review.json` 推到通过。

## 7. 决策（已于 2026-09-18 定案）

| 编号 | 决策 | 对开发的影响 |
| --- | --- | --- |
| **D1** | 基线 = **原版 RA3**（非日冕）。实测当前渲染配置是 `corona_6.321.skudef`，**需切换到原版 profile** | 素材目录、prefab、美术 rubric 一律以原版资源为准；rendererConfig 纳入每张图的溯源 |
| **D2** | 水域与灯光**保持默认**，不解禁 | 美术上限受限（无水面反射、海岸线氛围）；不再就此事反复讨论，rubric 按"默认水域/灯光"评分 |
| **D3** | **先不管** `ra3_map_diffusion` | 同 D-C：不训练、不接推理、不依赖其管线、不预留接口位；`origin_maps` 仅作只读语料 |
| **D4** | **维持"不做真实游戏加载验证"**，鸟瞰图视为足够 | 所有玩法结论标注"未经游戏验证"；S4/S5 不用 30034 的游戏内抽样作为验收前置 |
| **D5** | 首批目标 = **4 人图，可玩尺寸 500×500**（含 border 8 → 实际 516×516） | 决定 prefab 数量（4 家对称）、模板与 rubric 的投入方向；**也是压力最大的规格**，需先建性能基线 |
| **D6** | 伴侣侧**只开发 v2**；未来再把 v1 功能（含编辑器插件宿主）迁到 v2 ——**迁移属未来计划，不在本轮** | S3 编辑器桥接的落点必须在 v2；v1 只作参考实现与行为对照，不新增功能 |

### D5 的直接后果（需立即验证）

500×500 是已知风险的放大器。实测在 **256×256 图 4 次修订**后 `.automation/History/index.json` 已达 **30 MB**（每修订全量序列化 `designEntities`、无去重）。按格数线性外推，500×500 同修订数将达 **约 115 MB**——这会让长会话不可用，且磁盘增长不可控。

因此 **D5 的落地前置是"先建 500×500 性能基线并修 `index.json` 膨胀"**，而不是先做美术。该验证已列为 S1 的第一件事。

## 8. 风险登记

| 风险 | 等级 | 缓解 |
| --- | --- | --- |
| 编辑器插件 API 能力不足 | 高 | S3.0 先探针；失败退回 CLI 任务，S1/S2 不受影响 |
| 两侧长期并行、重复建设（如两边各做一套素材抽取） | 高 | D3 尽快定案 + ADR；S1 统一由 `catalog-builder` 单一来源产出 |
| 失去模型后地形塑造手段偏薄 | 中 | S2 必须交付可复现的地形/材质方法（模板、噪声、雕刻规则、范例反推），不把质量寄托于未来的模型 |
| 美术质量提升不成比例 | 中 | rubric + 固定镜头 + `origin_maps` 对照；必要时扩充范例库而非继续加工具 |
| `index.json` 等膨胀在大图上失控 | 中 | S5 实测并修；先加配额与告警 |
| 未验证项被误报为"已验证" | 中 | 沿用"证据分级 + notEvaluated"纪律，不缩减既有声明要求 |
| 把伴侣侧空壳当成已有能力（KnowledgeBase、"从零建图"） | 中 | 规划一律以代码事实为准；空壳项在 S1/S2 明确列为"待建"而非"已有" |
| 本地服务无认证（30033/30034 仅环回） | 低–中 | 保持环回绑定；新增桥接端点时必须加令牌，不放宽到 0.0.0.0 |

## 9. 约束（沿用既有约定）

- 不覆盖也不改写既有 Agent 文档；本文只做**整合与路线**。
- 水域与灯光保持默认/原有状态，除非 D2 改判。
- 按用户最新要求，真实游戏加载不是验收前置；相关结论必须标注"未经游戏验证"。
- 遵循仓库 `AGENTS.md` 的变更-验证矩阵；Automation / Agent 的验证命令需在 S0 补入矩阵。
- 安装目录中的 `data/scripts`、`data/objectsTemplate`、`data/objectScreenShot` 属于用户地编安装，**只读引用**，不写入开发产物。
- `origin_maps`（在 `ra3_map_diffusion` 目录下）与其他磁盘语料**只读引用**，不改动原始地图包。
- **`ra3_map_diffusion` / TerrainForge 全层不纳入本次开发**：不训练、不接入推理、不依赖其数据管线，也不在计划中为它保留接口位。
