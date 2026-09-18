# MCP 接入

构建 `src/Dreamness.RA3.Map.Agent` 后，将 MCP 客户端的进程配置设为：

```json
{
  "command": "dotnet",
  "args": [
    "N:\\workspace\\ra3\\Ra3MapSharp\\src\\Dreamness.RA3.Map.Agent\\bin\\Debug\\net6.0\\Dreamness.RA3.Map.Agent.dll",
    "--mcp",
    "--launcher",
    "N:\\Program Files (x86)\\Red Alert 3(Incomplete)\\CoronaLauncher\\CoronaResources\\NewWorldBuilder\\WbLauncher.exe"
  ]
}
```

这只是通用进程配置字段，外围配置格式依客户端而定。省略 launcher 仍可编辑文件；真实预览需要有效编辑器配置。资源目录配置与 JSONL 相同。

建议同时加 `--artifacts <目录>` 固定产物位置，否则默认为 `<当前工作目录>/artifacts/agent-previews`，随客户端启动目录漂移。

提供 MCP stdio，协商版本2025-06-18；实现 initialize、initialized 通知、ping、tools/list、tools/call。标准输出只写逐行 JSON-RPC，关闭标准输入结束宿主。协议依据 [MCP 生命周期](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle) 与 [stdio 传输规范](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)。不提供 HTTP、资源订阅或提示模板。

工具名将命令点号替换为下划线并加 ra3_，例如 `terrain.ramp` 对应 `ra3_terrain_ramp`。工具参数为：

```json
{
  "sessionId": "$current",
  "expectedRevision": 2,
  "requestId": "edit-3",
  "arguments": { "objectId": "obj-1", "x": 12, "y": 15 }
}
```

创建/打开地图不需要 sessionId；修改需要明确 expectedRevision。requestId 可省略，重试同一个逻辑编辑时应显式复用。工具发现返回完整参数描述；错误参数返回协议错误，地图操作失败返回 isError=true 和结构化 CommandResult 文本。JSONL 原入口保留，`system.schema` 可读取同一份描述。

命令串行执行，真实预览独立后台运行。使用 preview.start 获取 jobId，再调用 jobs.status/jobs.cancel。当前 MCP 取消通知不抢占正在执行的同步编辑；宿主关闭会取消后台渲染。请求超时后先查询地图修订与任务状态，避免盲目重复操作。

渲染成功后调用 ra3_preview_inspect，arguments 为 jobId、maxEdge，可选原始像素矩形 crop。工具同时返回坐标/哈希元数据文本和原生 PNG ImageContent，模型无需另找文件查看入口。裁剪后的坐标使用返回矩阵；不要继续套用原图坐标。脚本 `mcp_smoke.py --launcher <WbLauncher.exe>` 可验证真实渲染、图像传输和接收内容哈希。

`ra3_diagnostics_render` 可在没有编辑器的环境返回高度、基础纹理分类、对象中心、地形通行或保护区图，同样包含 PNG ImageContent 与图例、坐标和溯源元数据。需要 sessionId；arguments.layer 选择 height/textures/objects/passability/constraints，通行层另需显式 profile。普通 `mcp_smoke.py` 已覆盖五层图像传输、内容哈希和共同快照来源检查。

候选流程：ra3_edits_prepare（baseRevision、commands）→ ra3_diagnostics_render/ra3_preview_start（同时传 preparedPlanId、planHash）→ ra3_edits_apply（expectedRevision、preparedPlanId、planHash）。图像携带候选标识，修订为来源修订；应用会拒绝过期来源，并使用准备结果直接提交。ra3_edits_discard 释放候选。烟测已覆盖准备不改变活动地图、候选看图、应用后内容一致；带 --launcher 时还验证候选的真实编辑器预览。

实体布局使用 ra3_design_prepare（baseRevision、patch.upsert/remove）、ra3_design_query 和 ra3_design_apply，支持 platform/ramp/objects/scatter。复用候选图像入口与 discard；设计绑定及原始高度随历史保存。参数形状及所有权限制见 Agent-Usage.md。`mcp_smoke.py --design --launcher <WbLauncher.exe>` 验证实体布局的真实候选预览、应用与重复应用不增加对象。

实体支持 derived（sourceEntityId、transform=rotate180），坡道支持平台目标高度引用 startHeightFrom/endHeightFrom；引用自动进入依赖图，参数 schema 同步描述这些形式。`mcp_smoke.py --symmetric --launcher <WbLauncher.exe>` 验证生成实体的180°镜像、引用高度、真实预览和上游修改后的连带重建。

`ra3_starts_list/place` 查询/放置玩家出生路径点；设计支持 playerStart，派生出生点需显式 playerSlot。对称烟测覆盖两个不同编号的出生点、重复应用不增加路径点，以及自动加载分类/译名两份目录后检索 OreNode。它不验证游戏基地初始化或采矿。

机器描述由 `scripts/build_command_schemas.py` 生成并嵌入程序集；新增命令需同步描述。MCP 校验该生成器使用的有限 schema 词汇，不是通用 JSON Schema 服务。原 JSONL 的参数兼容行为没有改为严格 schema 校验。

安装官方 Python `mcp==2.2.0` SDK 的开发环境可执行 `python scripts/mcp_smoke.py`，验证真实进程的握手、发现、schema、编辑、保护、导出与副本恢复，证据输出到 artifacts/mcp-smoke。该脚本使用 SDK 2.x 的字段命名。

## 客户端接线

推荐先用无依赖探测脚本确认本机可用，再配置具体客户端：

```powershell
# 握手 + 51 工具清单 + 建图/查询/落盘
powershell -NoProfile -File scripts/mcp_probe.ps1 -Smoke
# 再加真实渲染闭环（preview.start -> jobs.status -> preview.inspect）
powershell -NoProfile -File scripts/mcp_probe.ps1 -Smoke -Render
```

脚本只用标准库（兼容 Windows PowerShell 5.1），退出码 0 表示可用。注意脚本文件必须保存为 **UTF-8 with BOM**，否则 5.1 会按 ANSI 读取导致解析失败。

已接线的客户端（工作区 `ra3_map_workspace`，2026-09-18 配置）：

| 客户端 | 配置文件 | 键 |
| --- | --- | --- |
| Codex CLI | `.codex/config.toml` | `[mcp_servers.ra3-map]` |
| Cursor | `.cursor/mcp.json` | `mcpServers.ra3-map` |
| Reasonix | `reasonix.toml` | `[[plugins]]`（`name = "ra3-map"`） |

Codex 示例（其余客户端字段名不同，命令与参数一致）：

```toml
[mcp_servers.ra3-map]
command = "dotnet"
args = [
  "N:\\workspace\\ra3\\Ra3MapSharp\\src\\Dreamness.RA3.Map.Agent\\bin\\Debug\\net6.0\\Dreamness.RA3.Map.Agent.dll",
  "--mcp",
  "--launcher", "N:\\Program Files (x86)\\Red Alert 3(Incomplete)\\CoronaLauncher\\CoronaResources\\NewWorldBuilder\\WbLauncher.exe",
  "--artifacts", "N:\\workspace\\ra3\\Ra3MapSharp\\artifacts"
]
```

验证接线是否真的生效（不要只依赖"配置写过了"）：

```powershell
cd "N:\Program Files (x86)\Red Alert 3(Incomplete)\ra3_map_workspace"
codex mcp list      # 应出现 ra3-map 一行，Command=dotnet，Args 含 --mcp
```

配置指向 `bin/Debug` 产物；重新构建后路径不变，无需改配置。若改用 Release 或 `dotnet publish` 输出，请同步更新三处配置。
## 5. 让素材与规则可用（一次性构建）

`assets.*` / `footprints.*` / `art.*` 依赖四份**生成数据**。它们不在仓库里，需要先构建一次（约 20 分钟，之后长期可用），**并保证客户端的 `--artifacts` 指向同一层**。

```powershell
$dll = "src\Dreamness.RA3.Map.Agent\bin\Debug\net6.0\Dreamness.RA3.Map.Agent.dll"
$launcher = "N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe"
$cat = "N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\data\config\ObjectCategory.json"
$tr  = "N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\data\config\ObjWndTrans.json"

dotnet $dll --build-catalog artifacts\catalog\catalog.json --launcher $launcher --artifacts artifacts
dotnet $dll --build-album artifacts\album --asset-catalog artifacts\catalog\catalog.json --artifacts artifacts\album-work --launcher $launcher
dotnet $dll --build-footprints artifacts\footprints\footprints.json --artifacts artifacts
dotnet $dll --analyze-corpus artifacts\art-rules\art-rules.json --corpus "E:\ai_workspace\ra3_map_diffusion\dataset\origin_maps" --object-catalog $cat --object-translations $tr --artifacts artifacts
```

**验收**（任一条不符，先检查 `--artifacts` 是否与构建时一致）：

| 工具 | 期望 |
| --- | --- |
| `assets.catalog_info` | `counts.textures` 406、`counts.objects` 1688 |
| `art.rules` | `maps` 63、`rules` 数十条 |
| `footprints.list {limit:1}` | `total` 约 970 |
| `assets.search {kind:"texture",theme:"Yucatan"}` | `total` 45 |
| `assets.album {typeName:"AM_PLANTS01"}` | 返回 `image/png` 内容块 |

已知缺口：`footprints` 只覆盖 **1200/1688** 个物体（有编辑器截图的那 482 个未量测），所以 `footprints.get CC_Bush01` 会返回 `FOOTPRINT_MISSING`——这是数据缺口，不是接线故障。

