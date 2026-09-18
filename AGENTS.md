# AGENTS.md

本文件是本仓库的人类与 AI 协作者统一执行指南。目标是降低上手成本、减少误改风险、提高变更可验证性。

## 1. 项目结构与依赖图

### 1.1 源码项目（`src/`）

- `Dreamness.RA3.Map.Parser`
  - 底层二进制解析与序列化核心（`.map/.scb/.bin/.paste` 相关能力主要在此）。
  - 关键概念：`BaseContext`、`MapContext`、`ClipBoardContext`、`MapScbContext`、Asset 系统。
- `Dreamness.RA3.Map.Facade`
  - 面向业务的高层 API，封装 Parser。
  - `Ra3MapFacade` 由多个 partial 文件分模块实现（`TilePart`、`ScriptPart`、`ObjectPart` 等）。
- `Dreamness.RA3.Map.Transform`
  - 地图变换层（旋转、镜像、缩放等），依赖 Facade。
- `Dreamness.Ra3.Map.Visualization`
  - 地图可视化能力（如预览图输出），依赖 Facade。
- `Dreamness.RA3.Map.Lua`
  - Lua 语法相关能力（ANTLR）。
- `Dreamness.RA3.Map.Automation`
  - **面向 AI Agent 的命令内核（WoWA）**：会话/修订/事务/撤销重做、请求去重、对象句柄、候选（prepared plan）、设计实体、依赖图、保护区域、几何与地形算子。
  - 不依赖 Facade 之外的 UI；纯确定性、可回放，是"编辑器能力"的机器可调用形态。
  - 入口概念：`CommandRegistry`（注册全部命令）、`AgentRuntime`（会话与作业宿主）。
- `Dreamness.RA3.Map.Agent`
  - **Agent 宿主**：MCP stdio 服务（`Protocol/McpServer.cs`）+ JSONL/批处理模式，工具定义在 `Protocol/command-schemas.json`。
  - 渲染：`Rendering/{DiagnosticRenderer,WorldBuilderRenderer,PreviewInspection}.cs`。诊断图纯托管；真实鸟瞰图交给外部 `WbLauncher.exe`。
  - 评审闭环：`art.profile` 用与语料分析**同一份代码**测量当前地图；`review.render_set` 渲染总览 + 4 张固定比例局部并合成一张评审大图，同时对同一修订产出 rubric（每条检查写明实测值、阈值与阈值出处；`combatReadability` 标为 not-evaluated）。同时报告 `patches`（斑块数/中位尺寸/最大连通块占比/平均紧凑度），rubric 用**紧凑度**判分——实测「斑块尺寸」与「最大连通块占比」都抓不到「圆形印章式拼贴」，只有形状类指标抓得到。
  - 美术规则：`--analyze-corpus --corpus <origin_maps>` 从 shipped 地图语料量出材质/密度/聚簇阈值，`art.rules` 汇报（含实测纹理配对）。单图解析失败会记为 failure 而不中断整轮。
  - 素材目录与图册：`--build-catalog` 生成 `<artifacts>/catalog/catalog.json`，`--build-album <artifacts>/album` 生成物体外观图册（并附一张**空网格参考渲染**），`--build-footprints` 由图册量得占地；宿主自动加载，由 `assets.catalog_info` / `assets.search` / `assets.album` / `footprints.get|list|set` 提供。`objects.scatter` 在未传 `footprints` 且相关类型全部有量测时自动取用目录值。

### 1.2 测试项目（`test/`）

- `Dreamness.Ra3.Map.Parser.Test` -> 测试 Parser。
- `Dreamness.Ra3.Map.Facade.Test` -> 测试 Facade。
- `Dreamness.Ra3.Map.Transform.Test` -> 测试 Transform。
- `Dreamness.Ra3.Map.Visualization.Test` -> 测试 Visualization。
- `Dreamness.RA3.Map.Lua.Test` -> 测试 Lua。
- `Dreamness.RA3.Map.Automation.Test` -> 测试 Automation 命令内核（不依赖本机 RA3 数据）。
- `Dreamness.RA3.Map.Agent.Test` -> 测试 Agent 协议与渲染装配（不依赖本机 RA3 数据）。

### 1.3 依赖关系（核心方向）

- `Parser` <- `Facade` <- (`Transform`, `Visualization`)
- `Facade` <- `Automation` <- `Agent`
- `Lua` 独立，不依赖上述链路；`Automation` 目前不依赖 `Lua`。

## 2. 环境与前置条件

- 平台：Windows + PowerShell（仓库现有脚本与路径习惯以此为主）。
- SDK：`.NET 6.x`（见 `global.json`，固定 `6.0.0`，`rollForward=latestMinor`）。
- 构建系统：`dotnet` CLI + solution `Ra3MapSharp.sln`。
- 仓库全局配置：`Directory.Build.props`（版本、打包元信息、符号包、SourceLink 等）。
- 若执行部分 Facade/Transform/Visualization 测试，需要本机存在 RA3 地图目录数据（见 `Ra3PathUtil.RA3MapFolder`）。
- 真实鸟瞰图（`preview.start` / `--export-overview`）需要外部 `WbLauncher.exe`（新地编启动器），路径通过 `--launcher` 参数或环境变量 `RA3_WB_LAUNCHER` 指定；缺失时文件编辑仍可用，仅真实渲染不可用。
- 控制台脚本注意：本机只有 **Windows PowerShell 5.1**（无 pwsh 7）。无 BOM 的 `.ps1` 会被按 ANSI 读取而导致中文乱码与解析失败，仓库内 `scripts/*.ps1` 一律保存为 **UTF-8 with BOM**，且避免使用 .NET Core 专属 API（`ProcessStartInfo.ArgumentList`、`StandardInputEncoding`、`ConvertFrom-Json -Depth`）。
- **改完 `.ps1` 要重新确认 BOM**：编辑工具写出的是**无 BOM** 的 UTF-8。含中文的 `scripts/*.ps1` 被编辑后 BOM 会丢失，PowerShell 5.1 随即按 ANSI 读取，报 `Unexpected token` 之类的解析错误（错误信息本身还会显示为乱码）。改完用 `[System.IO.File]::ReadAllBytes($p)[0..2]` 确认首字节是 `239,187,191`，不是就用 `[System.IO.File]::WriteAllText($p, (Get-Content $p -Raw), (New-Object System.Text.UTF8Encoding($true)))` 写回。
- **协议层的 BOM 坑**：宿主曾因首条 JSONL 消息前缀的 UTF-8 BOM 而让**整个会话的第一个请求**必失败，错误信息是具有误导性的 `'0xEF' is an invalid start of a value`。现已由 `AgentJson.WithoutBom` 容忍（有测试）。写客户端或测试脚本时，注意 `function F($x, $args)` 这类把 `$args`（PowerShell 自动变量）当参数名的写法会让它变成数组，且 `$null = F ...` 会吞掉函数内所有 `Write-Output`。
- **反方向的 BOM 坑**：`Set-Content -Encoding UTF8` 会**写出** BOM。外部工具的 JSON 配置不接受 BOM——给地编启动器的 `data/config/map-task-launch.json` 写配置时，用 `Set-Content` 会让渲染在 2 秒内失败并报 `'0xEF' is an invalid start of a value. LineNumber: 0`。写这类文件必须用 `[System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))`，改完用 `[System.IO.File]::ReadAllBytes($path)[0..2]` 确认首字节不是 `239,187,191`。

## 3. 常用命令

所有命令默认在仓库根目录执行。

### 3.1 构建

```powershell
dotnet build Ra3MapSharp.sln
dotnet build Ra3MapSharp.sln -c Release
dotnet build src/Dreamness.RA3.Map.Parser/Dreamness.RA3.Map.Parser.csproj
```

### 3.2 测试

```powershell
# 稳定、推荐优先执行
dotnet test test/Dreamness.Ra3.Map.Parser.Test/Dreamness.Ra3.Map.Parser.Test.csproj --no-restore
dotnet test test/Dreamness.RA3.Map.Lua.Test/Dreamness.RA3.Map.Lua.Test.csproj --no-restore

# Automation / Agent（同样稳定；UsageExamples 类是文档性用例，默认排除）
dotnet test test/Dreamness.RA3.Map.Automation.Test/Dreamness.RA3.Map.Automation.Test.csproj --no-restore --filter "TestCategory!=UsageExamples"
dotnet test test/Dreamness.RA3.Map.Agent.Test/Dreamness.RA3.Map.Agent.Test.csproj --no-restore

# MCP 端到端连通性探测（握手 + 工具清单；加 -Render 会真正调用 WbLauncher 出图）
powershell -NoProfile -File scripts/mcp_probe.ps1 -Smoke

# 环境依赖较强（需本机 RA3 地图数据）
dotnet test test/Dreamness.Ra3.Map.Facade.Test/Dreamness.Ra3.Map.Facade.Test.csproj --no-restore
dotnet test test/Dreamness.Ra3.Map.Transform.Test/Dreamness.Ra3.Map.Transform.Test.csproj --no-restore
dotnet test test/Dreamness.Ra3.Map.Visualization.Test/Dreamness.Ra3.Map.Visualization.Test.csproj --no-restore
```

> 不建议默认执行 `dotnet test Ra3MapSharp.sln` 作为快速验证入口（详见“已知坑位”）。

### 3.3 打包与发布

```powershell
dotnet pack Ra3MapSharp.sln -c Release
```

仓库提供 `publish.ps1`：

- 会自动 `pack` 到 `artifacts/`。
- 需要提前配置环境变量 `NUGET_API_KEY`。
- 会推送 `.nupkg` 与 `.snupkg` 到 nuget.org，并使用 `--skip-duplicate`。

## 4. 测试策略（稳定 vs 环境依赖）

### 4.1 稳定可跑（建议 PR 最低保障）

- `Parser.Test`
- `Lua.Test`
- `Automation.Test`（`--filter "TestCategory!=UsageExamples"`）
- `Agent.Test`

这些测试通常不依赖本机 RA3 安装目录中的真实地图文件。

### 4.2 环境依赖（按需执行）

- `Facade.Test`
- `Transform.Test`
- `Visualization.Test`

这些项目中大量测试直接调用 `Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "地图名")`，依赖本机 `%APPDATA%\Red Alert 3\Maps` 下具体地图存在。缺少对应地图时，结果不可复现或可能失败/阻塞。

## 5. 代码修改守则（本仓库特化）

### 5.1 Parser 与 Asset 系统

- 修改 Parser 时优先保持以下不变式：
  - `BaseContext` 的字符串声明表（`RegisterStringDeclare`）与 `ToBytes()` 序列化顺序。
  - `BaseAsset` 的 `Id/Version/DataSize/AssetType/Data` 协议字段语义。
  - 懒解析/容错解析路径（`Parse` vs `ParseTolerance`）行为。
- 对新/改资产类型，优先检查：
  - `AssetNameConst` 声明；
  - `AssetParser.FromBinaryReader` 分发分支；
  - `Default` 与 `FromJson/ToJson` 的一致性（如存在）。

### 5.2 保存语义（`Save/SaveAs`）

- `Save()` 应仅在已有源路径时可用；新对象应要求 `SaveAs()`。
- 当前约定：`SaveAs()` 默认是否压缩由各类型默认参数决定；调用者可显式覆盖。
- 针对 `.map/.scb/.bin/.paste`：
  - 扩展名本身不是唯一语义来源，解析以文件内容结构为准；
  - `Clipboard` 相关流程可处理 `.paste` 和 `.bin`（按内容一致处理）。

### 5.3 目录与命名约定

- 保持既有目录命名兼容性（例如 `Core/ClipBoard`、`Core/MapScb` 的大小写风格）。
- 不要轻易重命名已有公开类型/命名空间，除非同步处理所有上层依赖与测试。
- Parser 项目的 `data/script_declare/*.json` 为嵌入资源，修改路径或文件名时需同步 `.csproj`。

## 6. 变更-验证矩阵（最低建议）

- 仅改文档/注释：
  - 手工检查 Markdown 渲染与路径正确性。
- 改 `Parser`：
  - `dotnet build src/Dreamness.RA3.Map.Parser/Dreamness.RA3.Map.Parser.csproj --no-restore`
  - `dotnet test test/Dreamness.Ra3.Map.Parser.Test/Dreamness.Ra3.Map.Parser.Test.csproj --no-restore`
- 改 `Lua`：
  - `dotnet build src/Dreamness.RA3.Map.Lua/Dreamness.RA3.Map.Lua.csproj --no-restore`
  - `dotnet test test/Dreamness.RA3.Map.Lua.Test/Dreamness.RA3.Map.Lua.Test.csproj --no-restore`
- 改 `Facade`：
  - `dotnet build src/Dreamness.RA3.Map.Facade/Dreamness.RA3.Map.Facade.csproj --no-restore`
  - 如有环境，补跑 `Facade.Test`。
- 改 `Transform`：
  - `dotnet build src/Dreamness.RA3.Map.Transform/Dreamness.RA3.Map.Transform.csproj --no-restore`
  - 如有环境，补跑 `Transform.Test`。
- 改 `Visualization`：
  - `dotnet build src/Dreamness.Ra3.Map.Visualization/Dreamness.Ra3.Map.Visualization.csproj --no-restore`
  - 如有环境，补跑 `Visualization.Test`。
- 改 `Automation`：
  - `dotnet build src/Dreamness.RA3.Map.Automation/Dreamness.RA3.Map.Automation.csproj --no-restore`
  - `dotnet test test/Dreamness.RA3.Map.Automation.Test/Dreamness.RA3.Map.Automation.Test.csproj --no-restore --filter "TestCategory!=UsageExamples"`
  - 新增/修改命令时**必须同步 `src/Dreamness.RA3.Map.Agent/Protocol/command-schemas.json`**，否则工具描述与参数校验不一致。
- 改 `Agent`（含 MCP 协议、渲染装配、`command-schemas.json`）：
  - `dotnet build src/Dreamness.RA3.Map.Agent/Dreamness.RA3.Map.Agent.csproj --no-restore`
  - `dotnet test test/Dreamness.RA3.Map.Agent.Test/Dreamness.RA3.Map.Agent.Test.csproj --no-restore`
  - `powershell -NoProfile -File scripts/mcp_probe.ps1 -Smoke`（真实渲染链路改动时加 `-Render`）

## 7. 发布与打包

- 发布脚本：`publish.ps1`（仓库根目录）。
- 前置条件：
  - 已完成 Release 构建与必要测试；
  - `NUGET_API_KEY` 已配置。
- 产物目录：
  - `artifacts/`（`.nupkg` + `.snupkg`）。

## 8. 已知坑位

- `dotnet test Ra3MapSharp.sln` 在当前环境下可能耗时极长或出现卡住，不作为默认入口命令。
- 部分测试项目（特别是 Facade/Transform/Visualization）强依赖本机 RA3 地图数据与具体地图名，CI 或新机器上不可直接复现。
- 当前仓库曾存在一个未跟踪的保留名文件 `nul`（某次误重定向产生，内容为 `del: command not found`），已于 2026-09-18 清理；提交前请确认 `git status` 不再出现它。
- `Automation` 的会话历史索引（`.automation/History/index.json`，schemaVersion 9）**在磁盘上按修订增量存储**：每个修订只写负载真正变化的设计实体与被移除的实体 id，读取时前向回填为完整状态。同一负载（1 个 200×200 platform + 120 个对象、4 次修订）由 21.9 MB 降到 1.73 MB，每修订增量由约 +5.5 MB 降到约 +8 KB。**内存中每个修订仍保留完整状态**（撤销/重做是直接查表），故长会话 RSS 仍随"修订数 × 设计实体总面积"增长；`waypointObjectIds`/`unitObjectIds` 也仍按修订全量存储。改这块时**务必保持 `DesignHash()` 的算法不变**，否则现存工作区会被误判为"设计已脏"。
- `preview.start` 是**作业制**：返回 `jobId` 后需轮询 `jobs.status`；前端进程必须保持 stdin 打开，EOF 会取消未完成作业。

## 9. 与 `CLAUDE.md` 的关系

- `CLAUDE.md` 保留，用于补充背景说明。
- `AGENTS.md` 是面向通用协作者（人类/AI）的主执行规范；若两者冲突，以仓库维护者最新要求为准。

