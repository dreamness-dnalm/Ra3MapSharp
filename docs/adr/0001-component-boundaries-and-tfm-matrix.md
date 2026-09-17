# ADR 0001：部件边界与版本矩阵（Agent 地图制作系统）

- 状态：**已接受**
- 日期：2026-09-18
- 相关文档：[Agent-System-Roadmap.md](../Agent-System-Roadmap.md)、[MCP-Usage.md](../MCP-Usage.md)、[Agent-Usage.md](../Agent-Usage.md)

## 背景

让 Agent 具备"制作优秀 RA3 地图"的能力，需要三件彼此独立的东西：

1. 一个**确定性的编辑内核**（能建图、改地形、放物体、存盘），以及把它暴露给模型的**工具面**；
2. 一个**人能用的外壳**（查看、导入导出、服务与游戏/编辑器桥接）；
3. 一套**给 Agent 看的技能与规则**（怎么做出一张好图，而不是只把命令跑通）。

历史上这三件事分散在多个仓库里各自生长，出现了"能力都在、但彼此不知道对方存在"的状态：内核有 51 个工具却没有任何 MCP 客户端指向它；外壳的 MCP 服务与内核的 MCP 服务互不认识；技能文档描述了一套**从未接线**的工具。

本 ADR 固定边界，避免后续每个新功能都要重新讨论"这个该放哪"。

## 决策

### D-A：三部件职责（外加一个外部进程）

| 部件 | 仓库 | 职责（**只做这些**） |
| --- | --- | --- |
| **编辑内核 + 工具面** | `Ra3MapSharp` | `.map` 解析/序列化、Facade API、Automation 命令内核、Agent 宿主与 MCP 工具、渲染编排。**唯一的地图写入口**。 |
| **外壳与服务** | `Ra3MapUtils` | 人机界面、导入/导出、资源浏览、本地服务（30033 MCP / 30034 游戏内桥接）。**不自己实现地图编辑语义**，需要时调用内核。 |
| **技能与工程** | `ra3_map_workspace` | 面向 Agent 的技能（SKILL.md）、Lua 地图工程、模板与工作流约定。**不含编译产物**。 |
| **外部渲染器** | 新地编安装目录 `WbLauncher.exe` | 真实鸟瞰图与编辑器 UI。由内核以子进程调用，不作为部件依赖。 |

不属于任何部件、但在本系统中被当作**只读外部输入**的：地编安装目录的 `data/objectsTemplate`、`data/objectScreenShot`、`data/config`，以及 `origin_maps` 精品图语料。

### D-B：跨部件走进程协议，不共享程序集

内核与外壳之间**不建立项目引用**，一律通过进程边界通信（MCP / HTTP / CLI）。理由：

- 内核是 net6.0，外壳 v2 是 net10.0-windows，中间隔着 4 个 .NET 大版本；
- 内核需要保持"可被任意宿主嵌入"，一旦被 WPF 引用就会拖入 Windows 桌面依赖；
- 进程边界天然隔离崩溃与生命周期，符合"外部渲染器都可能挂"的现实。

代价是类型无法共享，跨部件数据一律走 JSON 契约（见 `Agent-Usage.md` 的请求/响应信封）。

### D-C：生成/模型层本次不纳入

`ra3_map_diffusion` / TerrainForge 的生成模型效果不理想，**本次开发不纳入**：不训练、不接入推理、不依赖其数据管线，也**不为它保留接口位**。

- 其目录下的 `dataset/origin_maps`（65 个精品地图包）仅作为**只读参考语料**（范例抽取与评测对照）。
- 地形能力退回**模板 + 确定性工具**（雕刻/平滑/坡道/噪声/区域）保底路径；"优秀"由内容库与美术闭环承担。
- 若日后重新评估，需先满足：语料规模、稳定的无条件生成质量、明确的许可与分发方式。相关事实记录见 Roadmap §2.4。

## 版本矩阵（2026-09-18 实测）

| 组件 | 路径 | TFM | 角色 | 验证方式 |
| --- | --- | --- | --- | --- |
| Ra3MapSharp 内核 | `src/Dreamness.RA3.Map.{Parser,Facade,Transform,Visualization,Lua,Automation,Agent}` | `net6.0` | 编辑内核 + MCP 工具面 | build + Automation.Test(159) + Agent.Test(23) + `scripts/mcp_probe.ps1` |
| Ra3MapUtils v2 | `src/UI`(`net10.0-windows`)、`src/Core`(`net10.0`) | `net10.0` / `net10.0-windows` | 外壳与本地服务 | `tests/AiMaps*.Tests`（Exe 型，人工运行） |
| Ra3MapUtils v1（遗留） | `Ra3MapUtils/`(`net8.0-windows`)、`KnowledgeBase{Lib,Cli}/`(`net8.0`) | `net8.0-windows` | 旧界面与**编辑器插件宿主** | `Ra3MapUtils.Tests` |
| RA3 地图工作区 | `ra3_map_workspace` | 无（Lua + Markdown） | 技能与 Lua 工程 | Lua 语法/大小校验 |
| 外部渲染器 | `WbLauncher.exe`（新地编） | 外部 exe | 真实鸟瞰图 / 编辑器 | `--export-overview` 退出码 0–8 |

**未决**：外壳交付目标树是 v1 还是 v2（Roadmap §7 D6）。当前事实是**插件宿主只有 v1 有**，而新功能都在 v2——这直接影响 S3 编辑器桥接的落点。

## 后果

**正面**

- 内核的边界清晰：任何新地图能力都落在 `CommandRegistry` + `command-schemas.json` 一处，外壳与技能自动受益。
- 三部件可独立构建与测试，CI 不必拖入 WPF。
- 内核无桌面依赖，可被 MCP 宿主、批处理脚本、未来的服务任意嵌入。

**负面 / 需持续投入**

- 跨进程契约需要**双份维护**（内核命令定义 ↔ 外壳工具描述），必须用文档与探测脚本卡住（`scripts/mcp_probe.ps1` 是这一点的执行手段）。
- 类型不共享意味着参数错误只能在运行时暴露；因此每个命令都必须带 JSON Schema，且 `expectedRevision` 必须显式。
- 排除模型层后，"优秀"完全依赖内容库与美术闭环（Roadmap S1/S2），不能再等模型兜底。

## 后续动作

1. 外壳侧若要编辑地图，**必须经由内核**（MCP/CLI），不得自行实现编辑语义。
2. 技能文档（`ra3-map-authoring`）中的工具清单必须以 `scripts/mcp_probe.ps1` 的实测输出为准，不允许出现未接线的工具。
3. D6（v1/v2 交付树）定案后，更新本 ADR 的版本矩阵与 Roadmap §5 S3。
