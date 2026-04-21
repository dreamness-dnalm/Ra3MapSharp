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

### 1.2 测试项目（`test/`）

- `Dreamness.Ra3.Map.Parser.Test` -> 测试 Parser。
- `Dreamness.Ra3.Map.Facade.Test` -> 测试 Facade。
- `Dreamness.Ra3.Map.Transform.Test` -> 测试 Transform。
- `Dreamness.Ra3.Map.Visualization.Test` -> 测试 Visualization。
- `Dreamness.RA3.Map.Lua.Test` -> 测试 Lua。

### 1.3 依赖关系（核心方向）

- `Parser` <- `Facade` <- (`Transform`, `Visualization`)
- `Lua` 独立，不依赖上述链路。

## 2. 环境与前置条件

- 平台：Windows + PowerShell（仓库现有脚本与路径习惯以此为主）。
- SDK：`.NET 6.x`（见 `global.json`，固定 `6.0.0`，`rollForward=latestMinor`）。
- 构建系统：`dotnet` CLI + solution `Ra3MapSharp.sln`。
- 仓库全局配置：`Directory.Build.props`（版本、打包元信息、符号包、SourceLink 等）。
- 若执行部分 Facade/Transform/Visualization 测试，需要本机存在 RA3 地图目录数据（见 `Ra3PathUtil.RA3MapFolder`）。

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
- 当前仓库存在一个未跟踪文件 `nul`，通常应避免将其纳入提交。

## 9. 与 `CLAUDE.md` 的关系

- `CLAUDE.md` 保留，用于补充背景说明。
- `AGENTS.md` 是面向通用协作者（人类/AI）的主执行规范；若两者冲突，以仓库维护者最新要求为准。

