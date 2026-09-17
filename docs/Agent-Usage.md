# Agent 命令入口（实现中）

当前提供持久 JSONL 进程、MCP stdio 和类库 `AgentRuntime`。MCP 配置见 [MCP-Usage.md](MCP-Usage.md)。实际命令以 `system.capabilities` 为准，`system.schema` 返回机器可读参数描述；不能把详细设计中的全部接口视为已实现。

## 构建与运行

在仓库根运行：

```powershell
dotnet build src/Dreamness.RA3.Map.Agent/Dreamness.RA3.Map.Agent.csproj
dotnet src/Dreamness.RA3.Map.Agent/bin/Debug/net6.0/Dreamness.RA3.Map.Agent.dll --stdio --launcher 'N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe'
```

stdin 每行一个请求，stdout 每行一个结果。宿主必须保持运行，sessionId 和 jobId 才有效；关闭 stdin 会取消未结束的渲染并关闭会话（保留未保存工作副本）。后台生成/预览期间可继续提交编辑请求。

可省略 `--launcher` 做文件编辑；也可用环境变量 `RA3_WB_LAUNCHER`。`--artifacts <目录>` 指定预览快照/图像输出，建议较短的 Windows 路径。源地图不会被渲染器打开，渲染对象是独立快照。

只需要批量编辑时使用 `--requests <JSON数组文件>`，按顺序执行且首个失败停止。该模式结束即关闭宿主，不用于等待异步渲染。脚本集成范例见 [agent_smoke.py](../scripts/agent_smoke.py)。

## 素材目录

`--launcher` 会自动读取同目录 `data/config/ObjectCategory.json`，并在存在时合并 ObjWndTrans.json；也可用 `--object-catalog <文件>` 显式指定分类，再用 `--object-translations <文件>` 显式指定译名表。显式分类不自动合并其他译名表。目录以宿主启动时的内容为准；更换目录后重启宿主。显式目录加载失败会退出，不静默退回无目录模式。

无需打开地图即可调用 `assets.objects`，参数 `query` 支持名称或中文分类/译名子串搜索，`offset=0`、`limit=50`（1–200）。返回精确 typeName、分类、sourcePath、sources（每份来源的路径/哈希/种类）、catalogHash 和 `editor-declared` 证据等级。合并时 catalogHash 包含两份来源身份。提供的分类表未收录 OreNode，译名表声明了 OreNode/矿脉；译名证据不等于当前 Mod 已验证该单位行为。

加载目录时 objects.place 拒绝未收录名称；可附带检索得到的 catalogHash 防止跨目录误用。已有地图内未收录对象仍可查询、移动和删除。目录不是 Mod 资源清单或玩法认证，不保证占地、碰撞、采矿、建造等行为。无目录时底层放置仍可使用，但返回 unverified。

## 请求示例

```json
{"requestId":"create-1","command":"map.create","arguments":{"parentPath":"N:\\workspace\\ra3\\Ra3MapSharp\\artifacts\\maps","mapName":"MyAgentMap","playableWidth":64,"playableHeight":64,"border":8}}
```

创建/打开结果返回 sessionId、revision。后续可以传真实 sessionId，或在当前进程内用 `"sessionId":"$current"` 引用最近打开的会话。写操作必须带明确 expectedRevision；宿主不自动猜版本。

```json
{"requestId":"hill-1","sessionId":"$current","expectedRevision":0,"command":"terrain.sculpt","arguments":{"region":{"kind":"circle","space":"playableGrid","centerX":32,"centerY":32,"radius":12},"mode":"raise","value":60,"falloff":1}}
```

## 已实现编辑命令参数

| 命令 | arguments |
| --- | --- |
| `map.create` | parentPath、mapName、playableWidth、playableHeight；border=8、defaultTexture=Dirt_Yucatan03、compress=true |
| `map.open` | parentPath、mapName |
| `map.info` | 无 |
| `protections.list` | 无；返回当前保护区 |
| `protections.add` | id、region、layers（terrain/textures/passability/objects）；最多100区，id 唯一 |
| `protections.remove` | id |
| `map.save` | compress=true |
| `map.save_as` | parentPath、mapName、compress=true、overwrite=false；是导出，不改当前会话归属 |
| `map.export_project` | parentPath、mapName；导出当前修改、附属文件、对象标识、保护区和完整可达历史，赋予独立 MapId |
| `map.export_package` | parentPath、mapName；导出当前地图与附属文件，不携带 .automation |
| `terrain.query` | x、y、space=playableGrid |
| `terrain.analyze` | profile 必填；points 可选（最多100个整数 x/y 可玩格坐标）；只读连通性诊断 |
| `terrain.rebuild_passability` | maxSlopeDegrees=45、expandCardinalHalo=true；全图重建普通标记，保留玩家/空军限制和 ExtraPassable |
| `terrain.ramp` | polyline（2–64个连续可玩格坐标 x/y）、startHeight、endHeight；widthCells=4（≥2）、transitionCells=2（≥0）、maxSlopeDegrees=35 |
| `terrain.set_height` | region 矩形、height |
| `terrain.sculpt` | region 矩形/圆、mode=set/raise、value、falloff=0（0–1，边缘平滑过渡比例） |
| `terrain.smooth` | region 矩形/圆、iterations=1（1–32）、strength=1（0–1）；只写 region，邻域只读 |
| `textures.list` | query、offset=0、limit=50（1–200）；需要 session；返回库声明，不保证 Mod 资源逐项验证 |
| `texture.paint` | region 矩形/圆、texture（枚举准确名称）、autoBlend=true；返回额外混合邻域范围 |
| `waypoints.place` | name 可选、x、y、z=0、space=playableGrid、anchor=center |
| `waypoints.move` | objectId、x、y、z=0、space=playableGrid、anchor=center |
| `waypoints.delete` | objectId |
| `objects.query` | objectId、typeName 可选精确过滤；offset=0、limit=100（1–200）；只查询普通对象 |
| `objects.place` | typeName、x、y；name 可选、z=0、angleRadians=0、space=playableGrid、anchor=center |
| `objects.move` | objectId、x、y；z、angleRadians 省略则保留原值；space=playableGrid、anchor=center |
| `objects.delete` | objectId；按句柄删除精确实例，允许游戏 uniqueID 重复 |
| `objects.scatter` | region、profile、seed、count（1–2000）、typeNames（1–64）；minDistanceCells=2（≥1）、exclusions=[]（最多100区）、catalogHash 可选 |
| `history.undo/redo` | 无；也需要 expectedRevision |
| `batch.execute` | commands 数组，每项 command、commandVersion=1、arguments；最多100项，仅支持编辑命令，整批一个修订 |

矩形 region：`kind=rectangle, space=playableGrid/mapGrid, x,y,width,height`；圆 region：`kind=circle, space, centerX,centerY,radius`。圆心和半径为连续格坐标，按格心选中。整个区域需在指定坐标空间内，不静默裁剪。

坡道沿折线弧长插值高度，核心外使用平滑过渡和圆头。整个过渡带必须在可玩区域内。先计算量化结果，再检查核心内相邻格坡度；超限返回 SLOPE_LIMIT_EXCEEDED，整次修改回滚。核心外接路、过渡带坡度与碰撞不由此命令保证，完成后调用 terrain.analyze 检查两端连通。不要用宽度参数代替实际净空诊断。测试脚本增加 `--ramp` 可生成高低平台并验证接通前后状态；它会覆盖脚本内此前生成的地形。

普通对象返回 `obj-N` 句柄、世界坐标和 assetValidation（目录声明为 editor-declared，否则 unverified）。道路与路径点不在普通对象查询范围。Z 为地图对象的原始 Z 参数，尚未提供自动贴地或占地避障。

历史版本升级为2，普通对象和路径点句柄随撤销/重做恢复；保存后重开且用户文件未变时保留历史、修订和句柄。旧版历史只有在所有快照的普通对象序列一致时才迁移，否则返回 `HISTORY_MIGRATION_REQUIRED`。外部修改干净地图后重开会建立新基线，应重新查询句柄；未保存工作区与外部修改冲突时拒绝打开。

高度编辑当前返回 `passabilityUpdated=false`。它尚未自动推导通行性，不代表已经可玩。保护区已支持持久保存；资源组合件、玩法验证尚在开发，不要将地形测试图用于声称完整地图生成通过。

需要重建通行标记时显式调用 terrain.rebuild_passability。它会替换全图（含边界）的普通 Passable/Impassable 标记，包括手工设置的普通阻塞；不要对需保留这些标记的旧图直接调用。默认45度阈值与一格四邻域扩张均可配置，特殊标记保留。命令支持事务和撤销，不计算水域、对象碰撞、建造或单位移动规则。

## 设计实体与候选

`design.prepare` 接受 baseRevision 和 patch（schemaVersion=1、upsert、remove），每次修改1–100个实体，总量最多1000。upsert 项为 id、kind、parameters，可选 label；缺席的旧实体保留，remove 显式删除。当前支持六类实体：

| kind | parameters |
| --- | --- |
| platform | region、value（目标高度）、falloff=0；使用固定 set 模式 |
| ramp | 同 terrain.ramp 参数；端点可用 startHeightFrom/endHeightFrom 引用平台 ID |
| objects | placements 数组，每项同 objects.place 参数，1–2000项 |
| scatter | 同 objects.scatter 参数，必须指定 seed、profile |
| derived | sourceEntityId、transform=rotate180；复制源实体生成的高度格和普通对象位置/朝向 |
| playerStart | playerSlot（1–6）、x/y，可选 z/space/anchor；按 starts.place 放置出生路径点 |

```json
{"sessionId":"$current","command":"design.prepare","arguments":{"baseRevision":0,"patch":{"upsert":[{"id":"base-a","kind":"platform","parameters":{"region":{"x":8,"y":8,"width":12,"height":12},"value":300,"falloff":0.4}},{"id":"landmark","kind":"objects","parameters":{"placements":[{"typeName":"CC_Tree01","x":25,"y":25}]}}]}}}
```

候选经 diagnostics.render/preview.start 查看后，用 `design.apply` 提交（expectedRevision、preparedPlanId、planHash）。设计候选与 edits 候选的应用命令不能混用。`design.query` 支持 entityId、offset=0、limit=50（1–200）；返回规格、对象句柄和拥有的高度格数量，includeHeightCells=true 才返回原始/生成高度明细，坐标为含边界的 mapGrid。

更新实体先核对其对象指纹和高度格，再移除旧生成对象、恢复原始高度并生成新内容；不删除未绑定对象。参数不变且依赖没有变化时不重复生成，label 变化仅更新设计说明。重新生成对象会分配新句柄，实体 ID 保持不变。人工修改拥有的内容后返回 ENTITY_CONFLICT。平台/坡道记录算法选中的完整格集，包含过渡格，不只记录数值变化格。

实体可声明 dependsOn（最多32个不重复 ID）。按依赖顺序生成，互不依赖的实体按 ID 的 ordinal 顺序；不要依赖 upsert 数组顺序。上层地形允许覆盖依赖链中的底层地形，其他重叠仍返回 ENTITY_OVERLAP（共同依赖一个父实体不代表两个兄弟实体可以相互覆盖）。例如坡道声明 `dependsOn:["base-a"]` 后可以覆盖 base-a 平台；需要基于坡道结果筛选位置的树林声明依赖该坡道。

修改底层实体会沿旧、新依赖图计算全部受影响后代，先按逆序恢复，再按新依赖序生成；结果的 automaticallyRebuilt 列出未显式 upsert 的连带更新实体。这些实体也会重新分配对象句柄。原样重复整个布局不重建。检查人工修改时使用层叠后的可见高度，不把合法的坡道覆盖误判为平台被篡改。

不存在的依赖返回 ENTITY_DEPENDENCY，环返回 ENTITY_DEPENDENCY_CYCLE。删除父实体必须同时删除后代或更新后代依赖，不静默级联删除。dependsOn 本身只声明生成顺序、更新传播和高度覆盖关系，显式数值仍保持不变。

坡道可分别用 startHeightFrom/endHeightFrom 替代对应数值高度，两种形式不能同时出现。引用只允许 platform 或平台的 derived 链，读取其量化后的目标 value，并隐式建立依赖。它不读取坡道端点实际采样高度，也不改变 polyline；存在 falloff 或平台边缘时仍需检查接路与坡度。

derived 隐式依赖 sourceEntityId，复制该实体拥有的生成高度与普通对象，不包含其后代。180° 高度格使用 `(mapWidth-1-x,mapHeight-1-y)`，世界位置使用 `(playableWidth*10-x,playableHeight*10-y)`，Z 保留，朝向加 π。复制树林使用已生成的位置，不重新抽样。普通对象使用新名称/ID和句柄，当前复制类型、位置、Z、朝向、所属队伍及 settings 支持的十项属性；不复制其他属性、脚本关联、纹理或通行标记，也不重映射玩家所属。

源实体须与其对称地形格不相交，目标对象中心不得与已有普通对象重合，否则 SYMMETRY_OVERLAP；这不是物体占地验证。叠加镜像坡道时，除了隐式源依赖，还需 dependsOn 指向目标侧镜像平台；镜像平台与镜像坡道分别作为实体声明。上游变化后派生实体重建，仍保留地图中未绑定的内容，不进行整图旋转。已被人工修改的源实体不能作为未核实的派生输入。

出生点使用 `starts.place`，按 Player_N_Start 规范命名；拒绝已有编号（含大小写冲突）、中心重合及可玩区域外的位置。`starts.list` 返回玩家编号、wp-N 句柄、非法名称和重复编号；已有路径点移动/删除使用 waypoints.move/delete，设计实体仍受指纹冲突检查。playerStart 实体不能认领未绑定的已有出生点；调整实体会替换它拥有的路径点并返回新句柄。

derived 复制单个 playerStart 时，parameters 必须增加新的 playerSlot。路径点位置做180°变换并使用新玩家编号命名，不复制同名出生点。这些操作只建立出生位置，不验证出生空间、玩家/队伍配置、基地初始化或游戏胜负规则；不能据此声称完整对战地图已可玩。

设计规格、普通对象/路径点绑定、原始高度和依赖进入历史版本7，与地图共同撤销/重做和导出；版本4–6保留原设计迁移，更旧历史无设计。仅设计变化时 designDirty=true，原有 dirty 保持地图/保护变化语义，hasUnexportedChanges 汇总两者；map.save 同时保存。预览元数据增加 designHash，比较候选时也需核对。

这不是完整主题/玩法 DesignSpec 编译器：自动路线、玩家资源组、纹理实体和占地约束仍待实现。高层设计示例中的 zones/routes 等字段还不能直接传入本入口。

## 真实预览任务

复杂编辑可先用 `edits.prepare` 提交 baseRevision 和 commands（与 batch.execute 相同，1–100条普通编辑命令）。准备过程在独立地图对象上运行、落盘规范化并检查保护区，不推进修订、不写入工作副本或历史；返回 preparedPlanId、planHash、输入/候选地图与保护哈希、素材目录哈希和各命令结果。它是命令级候选基础设施，尚不是 DesignSpec 布局编译器。

将 preparedPlanId、planHash 同时传给 diagnostics.render 或 preview.start，即可查看未提交候选；返回图像元数据也携带候选身份。此时 revision 是候选的 baseRevision，并不意味着该图像已经提交。过期来源的候选仍可查看以便比较，但不能应用。

检查后调用 `edits.apply`（expectedRevision、arguments.preparedPlanId、arguments.planHash）。它核对当前修订、来源文件/保护状态和素材目录，提交已准备的规范化内容及句柄/保护状态，不重新运行散布等算法，整次应用只有一个历史步骤。准备后发生任何编辑或撤销/重做，均需重新准备；同一 requestId 重试成功 apply 不重复提交。

候选在当前会话内存中保留30分钟，最多32份，关闭/重启后不可恢复；`edits.discard` 按 preparedPlanId 释放。应用后的结果进入持久历史，支持撤销、重做和工程导出。prepare 目前持有会话锁执行，期间其他编辑等待；候选不能嵌套 apply、批次、导出、保存或历史命令。当前返回原始子命令及结果，尚未提供完整的修改区域/混合邻域差异报告。

`diagnostics.render` 无需 WorldBuilder，读取指定修订的独立快照，生成 height（高度灰度）、textures（基础纹理分类）、objects（对象中心）、passability（阻塞与连通区）、constraints（持久保护区）五类 PNG。参数为 layer（默认 height）、requiredRevision 可选、maxEdge=1024（1–2048）；passability 必须显式提供 profile，格式同 terrain.analyze。此命令不修改地图，也不重建通行标记。

```json
{"sessionId":"$current","command":"diagnostics.render","arguments":{"layer":"passability","requiredRevision":2,"profile":{"maxSlopeDegrees":35,"waterLevel":200},"maxEdge":1024}}
```

waterLevel=200 仅适用于已知默认环境。图像覆盖可玩区域、北向上，按格采样并保持长宽比例，可放大至 maxEdge；返回 legend、profile、notEvaluated、修订、地图与保护区哈希、pixelToPlayableGrid。MCP 返回原生 PNG 内容，JSONL 返回文件路径。各输出目录同时保存快照与 diagnostic.json。只有哈希及修订匹配的图层才可作为同一地图状态比较。

纹理图使用分类色，不显示材质混合；对象图仅显示固定像素大小的中心标记；保护区重叠时使用最后一区颜色，图例格数包含区域中可能位于边界的格子。通行图沿用静态地形模型，不包含物体碰撞或游戏规则。诊断图帮助定位问题，美术检查仍需真实预览，玩法仍需游戏验证。

完整导出要求目标目录不存在且不位于源工程内部，先在同级临时目录构建，再整体移入目标；发生冲突不发布半份目录。复制源地图目录下的附属文件及其层次结构，排除根 .automation 和已保存的旧地图文件；输出使用当前编辑状态，不会替源工程保存。源同名 .tga 随地图改名，脚本内容及其他文件名不重写；依赖地图名的脚本需自行核对。符号链接/联接点不跟随。返回逐文件哈希；工程导出的 `.automation/export.json` 记录来源和哈希。它们不会自动安装地图或证明游戏加载、玩法正确。

保护区保存在 `.automation` 工程历史中（版本3），随事务、撤销/重做和重开恢复，元数据变化也需 map.save。保护按选中格执行，对象按中心所在格判断，不能代替物体占地。保护检查覆盖事务前后两组区域，因此同一批里新增/移除保护区并修改该区域会失败；解除保护需独立提交后再修改。objects.scatter 自动排除对象保护格。map.save_as 当前只导出地图文件，不携带工程保护元数据；不要把导出副本当作受保护工程的备份。

散布以固定 `lcg32-cell-jitter-v2` 算法选取位置与朝向，按地形诊断 profile 筛选，并避让已有普通对象中心。计量单位为格，Z 写0；实际占地、碰撞与自动贴地尚未验证。禁放区保守地排除选中格的整个格子，不是持久保护区。相同地图状态、参数和种子复现相同布置；已有对象变化会改变结果。采样次数有上限，未达到 count 时返回 PLACEMENT_FAILED 并回滚，不等价于证明数学上无解。脚本 `--ramp --scatter` 可检查留空中间通道的树林布置。

通行诊断 profile：`maxSlopeDegrees=35`（0≤值<90）、`waterLevel` 可选、`maxWaterDepth=0`、`clearanceCells=0`（0–32）、`respectPassability=true`。默认坡度仅是分析参数，不是游戏单位认证。无 waterLevel 时不评估水深。仅对已知默认新图使用200，不会改写水域。

模型按四邻格最大高差/10计算坡度，再按方形净空膨胀障碍（相对圆形单位保守），使用四邻接连通，不穿对角墙角。返回 componentSizes、points 各自 component（0为阻塞）、allPointsConnected、修订和 notEvaluated。少于两个点时连通结论为 null。对象碰撞、桥梁、建造与游戏移动规则仍未评估；结果不能代表游戏寻路验收，也不写地图通行标记。

依赖已提供的 `NewWorldBuilder/MAP_TASKS.md`：先正常启动一次地编选定游戏/Mod，生成 `data/config/map-task-launch.json`。宿主不会替用户选择另一 Mod。该配置的快照和哈希写入预览报告。

```json
{"requestId":"preview-1","sessionId":"$current","command":"preview.start","arguments":{"requiredRevision":1}}
```

返回 `jobId`。继续发送：

```json
{"requestId":"poll-1","command":"jobs.status","arguments":{"jobId":"替换成实际返回值"}}
```

状态为 queued/running/succeeded/failed/cancelled。仅 succeeded 的 result 是可使用的图像；其中包含 imagePath、图片尺寸、revision、mapContentHash、rendererConfig、pixelToPlayableGrid。agent 应使用可用的本地图片查看工具读取 imagePath。

`jobs.cancel` 使用相同 jobId；仅取消本宿主创建的任务。多个渲染任务串行使用 GPU，但编辑独立继续。去重只在当前宿主进程有效，同 requestId 重试相同 preview.start 返回原任务。

`preview.inspect` 参数为 jobId、maxEdge=1024（1–2048），可选 crop（原始预览像素 x/y/width/height，左上为原点）。仅接受成功任务，先核对原图哈希，再裁剪和缩小（不放大），将 PNG 放在该渲染的输出目录。返回修订、地图/原图/派生图哈希和更新后的 pixelToPlayableGrid；MCP 还返回原生 PNG 图像内容，JSONL 返回文件路径。每次查看生成独立文件，后续可在该元数据上定位修改区域。

图片最长边2048，覆盖可玩区域。像素边界坐标 `(u,v)` 通过 `[a,b,c,d,e,f]` 映射为 `(a*u+b*v+c,d*u+e*v+f)`；像素中心使用 `(u+0.5,v+0.5)`。相机与朝向契约来自编辑器文档，游戏世界坐标再乘10。

预览失败时检查 error.details.exitCode、snapshotPath 和 `%LOCALAPPDATA%\NewWorldBuilder\MapTasks` 日志。成功须退出码0、PNG可解码、源快照未改、渲染配置未变，不能只凭旧文件存在判断。

## 可复现端到端验证

```powershell
python scripts/agent_smoke.py --landscape --launcher 'N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe'
```

该脚本生成独立测试图，调用批量编辑、雕刻、平滑、纹理、保存和真实预览，输出 `artifacts/agent-smoke/<时间>/evidence.json`。图形仅用于验证制图/渲染链路，不是美术或玩法成品。

增加 `--objects` 可覆盖普通对象放置、移动、删除、撤销，以及保存重开后对象与句柄一致性检查。

## 朝向单位与旧版实体

Agent 的 `angleRadians` 始终使用弧度；Parser/Facade 的 `Angle` 使用度数，`.map` 二进制保存弧度。放置、移动、查询、散布和180°派生在边界进行转换。例如 `angleRadians=1.5707963268` 对应 Facade 的90°。

早期实现把弧度直接写入度数属性，旧散布 `lcg32-cell-jitter-v1` 和旧实体可能存在错误朝向。当前散布为 v2，实体编译器为 `entity-compiler-v6`。打开地图不自动修正旧对象。显式 upsert 旧版本实体时，即使参数不变也生成新候选，并重新生成依赖实体；检查候选后调用 `design.apply` 才生效。已有手工修改仍须通过实体冲突检查。非实体对象需按已知意图显式设置角度，不能从现有小角度猜测是否曾受影响。

## 不打开编辑会话的地图检查

`map.inspect_file` 从源文件复制独立快照并读取，不创建源地图的 `.automation`，不改变当前 session。参数：`path` 必填；`typeNames` 可选、最多32项、精确匹配；`offset=0`、`limit=50`（1–100）；`includeProperties=false`。

```json
{"command":"map.inspect_file","arguments":{"path":"C:\\Maps\\Reference\\Reference.map","typeNames":["OreNode","OilDerrick"],"includeProperties":true,"limit":100}}
```

返回源路径、SHA256、快照路径、地图尺寸、玩家/队伍/出生点，以及筛选后对象总数和分页对象。对象包含 `sourceIndex`、所属队伍、世界坐标、`angleRadians` 和 `angleDegrees`；启用属性时还返回原始属性值和类型。报告另存为快照目录下的 `inspection.json`。这些信息标记为 `file-observed`，不证明地图来源真实性、当前 Mod 兼容性或游戏行为。

## 对象所属与属性配置

`objects.place` 可传 `ownerTeam` 和 `settings`。省略时保留底层默认值。`objects.configure` 以 `objectId` 定位，可修改 `name`、`ownerTeam`、`settings`，不改变位置和朝向；至少提供一项修改。查询/放置/配置结果均包含当前 ownerTeam 和支持的 settings。

ownerTeam 使用队伍完整名称（如 `PlyrNeutral/teamPlyrNeutral`），必须唯一匹配现有队伍且其玩家存在，否则返回 INVALID_OWNER。不自动建立队伍；可从 map.inspect_file 的 teams/player 信息了解参考文件配置。

settings 使用真实属性名，只接受以下类型：

- 布尔：objectEnabled、objectIndestructible、objectUnsellable、objectPowered、objectRecruitableAI、objectTargetable、objectSleeping。
- Int32 整数：objectInitialHealth、objectBasePriority、objectBasePhase。这里只核对存储类型，不保证任意整数在游戏中的有效性。

省略属性保持原值，不接受 null 删除、uniqueID 写入或其他任意属性。未知字段/错误属性类型使事务回滚。配置可用于 batch/候选编辑；受 objects 保护区约束，修改已绑定实体的属性会在后续实体更新时触发 ENTITY_CONFLICT。

```json
{"command":"objects.configure","sessionId":"$current","expectedRevision":6,"arguments":{"objectId":"obj-2","ownerTeam":"PlyrNeutral/teamPlyrNeutral","settings":{"objectInitialHealth":100,"objectEnabled":true}}}
```

entity-compiler-v6 的180°派生保留上述属性和所属队伍，另建名称/ID。对玩家所属对象，它仍保留源玩家，不隐式切换至对手；资源队伍选择和采矿行为需要模板及游戏验证。`scripts/mcp_smoke.py --resources --launcher <WbLauncher.exe>` 覆盖矿点候选渲染、对称朝向、属性配置、保存与文件检查；该小图专用于验证接口，资源占地重叠，不能作为可玩地图。

## 显式对象占地与预留通道

`objects.analyze_space` 是只读检查，必填 `footprints`，可成对提供 `preparedPlanId/planHash` 检查未应用候选；省略则检查当前工作地图。`limit=100`（1–1000）分别限制重叠明细和越界明细，统计总数仍完整，超出时 `truncated=true`。当前最多2000个普通对象，不含出生路径点和道路。

```json
{
  "command":"objects.analyze_space",
  "sessionId":"$current",
  "arguments":{
    "footprints":[
      {"typeName":"OreNode","boxes":[
        {"label":"body","widthCells":18,"depthCells":18},
        {"label":"access","widthCells":10,"depthCells":8,"offsetXCells":14}
      ]},
      {"typeName":"CC_Tree01","boxes":[{"label":"canopy","widthCells":3,"depthCells":3}]}
    ]
  }
}
```

以上尺寸只是烟测中的保守规划输入，不是引擎占地数据。每类1–8个矩形、类型最多256个，label 在同类内唯一。宽深为正，偏移默认0，所有尺寸/偏移绝对值最大4096格。坐标以对象原点为基准，单位是格（1格=10世界单位），先偏移再按对象朝向旋转。可用额外矩形预留通道，矩形标签仅用于解释，所有矩形同等参与避让。同一对象内的矩形允许重叠；不同对象间使用旋转矩形分离轴检测，边缘刚好接触允许通过，需要额外净距时扩大定义。

结果携带 mapContentHash、profileHash、候选标识、overlapCount/outsideCount 与明细。`coverageComplete=false` 表示有未知类型，unknownTypes 列出类型与数量；只有覆盖全部普通对象、无矩形重叠且无越界时，`clearUnderProfile=true`。这不证明碰撞、建造、采矿或寻路通过。

`objects.scatter` 可传同样的 footprints：开启后必须覆盖全部已有及待散布普通对象，缺少时返回 FOOTPRINT_MISSING；最多合计2000个对象。除了原中心间距，还检查完整旋转矩形：不越过可玩边界，不交叠其他对象的矩形，不进入禁放/对象保护区选择的格子或 terrain profile 阻挡格。矩形可能伸出散布选择区域，但必须满足上述约束。已有对象之间的问题不会由散布修复，完成后仍应 analyze_space。

未传 footprints 时保留原中心间距行为。定义当前是每次调用的输入：放在 scatter 设计实体参数里时随实体规格持久化；普通 place/move 不自动使用上次分析的定义。因此应在候选应用前重新检查，不能把一次分析当作永久保护规则。对象高度贴地、真实引擎占地和采矿通道验证仍未实现。

坐标协议补充：grid anchor 支持 `center` 或 `gridPoint`；旧 MCP schema 误写的 `corner` 已修正为与 handler 一致的 `gridPoint`。

## 考虑对象阻挡的路线分析

terrain.analyze 可选 footprints 和 routes。提供 footprints 时，全部普通对象都必须有定义，否则 FOOTPRINT_MISSING；每个矩形的 `blocksMovement` 默认 true。设为 false 的矩形仍参与对象占地/散布避让，但不阻挡路线，适合预留通道。真实矿点通行区和方向仍须实测，不能仅凭 access 标签推断。

阻挡矩形覆盖到的整格被保守标记，然后与地形阻挡一起按 profile.clearanceCells 做方形扩张；地图边界也计入净空。结果增加 objectBlockedCells（扩张前被对象覆盖的格数）与 footprintProfileHash。可分析当前工作地图，也可成对传 preparedPlanId/planHash 分析候选。返回 mapContentHash、候选标识和来源 revision；候选检查不提交地图。diagnostics.render 的 passability 图层可传相同 footprints/profile，以相同模型显示阻挡和连通分量。

```json
{"command":"terrain.analyze","sessionId":"$current","arguments":{"profile":{"waterLevel":200,"clearanceCells":1},"footprints":[{"typeName":"CC_Tree01","boxes":[{"widthCells":3,"depthCells":3}]}],"routes":[{"from":{"x":10,"y":10},"to":{"x":40,"y":40}}],"maxRouteCells":2048}}
```

routes 最多16条，端点是可玩区域的整数格坐标。每条返回 found、blocked-endpoint 或 disconnected；found 包含四邻域最短路径格序列、steps 和平面 distanceWorldUnits（步数×10）。maxRouteCells 为1–10000，默认2048；超过时只返回路径前缀并标记 truncated，步数和距离仍是全程值。不使用三维路程、转弯成本或单位速度，不能直接当作游戏行军时间。未提供 footprints 时保持地形分析行为，未提供 routes 则不搜索路线。

候选验收时，将相同 preparedPlanId/planHash、profile、footprints 传给 terrain.analyze 和 diagnostics.render（layer=passability），核对 mapContentHash 与 footprintProfileHash 一致；图例 blocked 格数应等于分析结果 blockedCells。诊断图记录完整 footprints 便于复现。footprints 暂只用于 passability 图层，传给其他图层会拒绝，避免参数被静默忽略。路线格序列尚未叠加在图上。

## 双人制作流程草稿

运行 `python scripts/create_duel_example.py --launcher <WbLauncher.exe>`，需要已构建的 Agent DLL。脚本仅依赖 Python 标准库，在 artifacts/duel-example 的新时间戳目录生成独立256×256草稿，不安装到游戏 Maps 目录。省略 launcher 可执行静态流程，但没有编辑器素材目录与真实预览证据。

流程包括：目录核实、统一基础高度、平台/坡道/出生点实体、八处矿点和两处油井、180棵树的占地散布、候选占地/路线分析、应用、材质绘制、通行标记重建、最终检查、分层图、可选真实预览、保存导出及独立文件复读。每次调用写入 evidence.json，失败写入 report.json；design-patch.json 与 footprints.json 保留输入。游戏玩法和美术审阅默认 pending/not-tested，不以静态成功自动标成可玩成品。

当前示例是 VerdantDivideDraft；结构检查通过仍需美术迭代及基地建造、采矿、胜负规则验证。导出包只含实际生成的地图文件，脚本不虚构 map.str 或玩法脚本。检查导出采用 map.inspect_file，避免在交付目录创建 .automation。

示例后续视觉版本改为12个疏密不同的林群（总计仍180树）、两处局部高地和变化宽度的地面路径。运行脚本会使用当前版本；历史输出保留以便比较。所有版本都需依据真实预览进行单独美术审阅，当前仍标为草稿。

## 多边形区域

接受通用 region 的雕刻/平滑、材质绘制、散布和保护区支持 `kind=polygon`：

```json
{"kind":"polygon","space":"playableGrid","vertices":[{"x":10,"y":10},{"x":30,"y":12},{"x":26,"y":24},{"x":18,"y":20},{"x":12,"y":30}]}
```

顶点3–64个，支持顺时针/逆时针和凹多边形；不重复首点，不支持洞、自交、自接触或折返重叠边。所有顶点必须有限并位于指定坐标空间范围内；几何计算额外限制绝对坐标不超过1000000。按格心选择，边界上的格心计入；无格心时拒绝。terrain.set_height 仍只接受矩形，任意轮廓使用 terrain.sculpt。

多边形雕刻 falloff 以到最近边的距离计算，过渡宽度为包围盒较短边的一半乘 falloff；狭窄凹部可能没有达到目标高度的内部平台，应查看高度诊断和实测值。散布在格内抖动后重新检查点仍在轮廓内；传 footprints 时仍遵循已有占地规则，轮廓约束针对对象中心。材质混合的一格邻域可能超出轮廓，保护区仍按实际变化检查。

polygon 可存入设计实体和保护历史。旧矩形/圆形记录省略新增 vertices，避免改变旧保护哈希；旧版程序不支持 polygon，应使用当前版本继续编辑。当前双人示例用同一多边形制作山脊高度与岩石材质，并通过180°派生保持两侧对应。

## 统一静态验收 map.validate

必填 expectedPlayers（1–6）、profile（地形移动假设）、footprints（全部普通对象占地）；可传 resources 数量规则、routes 关键路线，以及成对的 preparedPlanId/planHash 检查候选。resources 最多32个唯一类型，每项 typeName、minCount=0、maxCount=Int32最大值；routes 最多16条 from/to 整数格端点。

检查包括：玩家出生槽位是否恰为1..N、出生位置边界/重合、玩家定义缺失/重复、普通对象所属队伍及玩家是否存在、要求的资源数量、对象占地完整性/重叠/越界、出生点连通性和指定路线。缺少占地定义会使占地检查失败并跳过路线，不能得到 passed。

命令外层 `status=succeeded` 只表示生成报告成功；必须检查 `data.status`（passed/failed）和 failedChecks。报告带 schemaVersion、地图哈希、来源 revision、候选标识、输入 profileHash 和每项检查详情。passed 仅针对 requested-static-rules；没有指定 resources 时不检查资源数量。建造空间、经济平衡、脚本语义、美术与输入占地准确性仍在 notEvaluated 中；真实游戏验证按用户要求不作为交付前置。

```json
{"command":"map.validate","sessionId":"$current","arguments":{"expectedPlayers":2,"profile":{"waterLevel":200,"clearanceCells":2},"footprints":[{"typeName":"OreNode","boxes":[{"widthCells":18,"depthCells":18}]}],"resources":[{"typeName":"OreNode","minCount":8,"maxCount":8}]}}
```

示例需要按实际地图补齐树木、油井等所有对象类型。create_duel_example.py 已在候选阶段和最终导出前调用统一报告，写入 report.json 的 candidateValidation/finalValidation，并在失败时停止导出流程。
## 基地预留区域验收

`map.validate` 可选 `buildAreas`（最多16项，id唯一），每项包含 `id`、`region`，以及可选 `maxHeightDifference`（默认1，高度单位）、`playerSlot`（要求区域每格与该出生点连通）。例如：

```json
{"id":"base-1","region":{"kind":"rectangle","space":"playableGrid","x":42,"y":122,"width":12,"height":12},"maxHeightDifference":0.1,"playerSlot":1}
```

区域支持矩形、圆、多边形，以格心选择格子。检查选中整格均在可玩边界内、高度极差不超限、按 movement profile（包括净空）通行，且不与任何对象占地或预留矩形重叠。`blocksMovement:false` 的采矿预留区仍占用建造空间。未知对象占地不会通过；mapGrid 的物理边框也不会通过。结果为 `build-area:<id>`，包含阻塞、占用、越界和不连通格数；规则进入 profileHash，并适用于候选快照。它验证规划假设，不模拟引擎的具体建筑放置规则；真实游戏加载不属于验收要求。

## 纹理设计实体

`design.prepare` 支持 `kind: "texture"`，parameters 与 `texture.paint` 相同（region、texture、autoBlend）。例如：

```json
{"id":"base-ground","kind":"texture","parameters":{"region":{"kind":"circle","centerX":48,"centerY":128,"radius":20},"texture":"Dirt_Yucatan02","autoBlend":true}}
```

实体拥有选中格及自动混合的一格邻域，记录原始/生成的瓦片、混合索引及定义指纹。`design.query` 返回 ownedTextureCells；相同参数不重复涂画，删除/重建恢复原始格值。依赖链允许分层覆盖，非依赖实体的混合邻域重叠也会拒绝；手工改变实体拥有的纹理格后，后续更新/删除报 ENTITY_CONFLICT。先准备候选，再预览和应用，撤销/保存重开保持绑定。

历史版本为8，旧1–7版经现有检查迁移；nullable 纹理状态不改变旧设计哈希。算法 entity-compiler-v8 会在显式更新旧实体时重建。纹理表和混合表保持追加/复用，移除实体恢复格引用但不压缩未使用表项。自动混合重绘前会清除旧普通混合，避免统一材质后残留旧边缘，不改变单边和悬崖混合字段。双人示例保存 texture-patch.json，将表面图层按顺序纳入实体依赖链。

纹理可作为 `derived` 的 `rotate180` 来源。先按源区域格心选择格子，再将 mapGrid 格子映射到 `(mapWidth-1-x,mapHeight-1-y)`，因此矩形、圆、多边形与物理边框坐标都使用相同离散规则。目标使用来源材质并按目标邻域重算普通混合，保留目标原有单边/悬崖混合。源目标的选中区域或混合邻域相交时拒绝 SYMMETRY_OVERLAP；目标与非依赖实体冲突、保护区或手工修改仍拒绝。派生链可继续引用派生纹理；改变来源材质会重建后代。此功能旋转材质布局，不旋转位图像素或既有复杂混合方向，不代表全图像素对称。

```json
{"id":"base-ground-mirror","kind":"derived","parameters":{"sourceEntityId":"base-ground","transform":"rotate180"}}
```

## 资源获取距离验收

`map.validate.resourceAccess` 可选最多16种唯一 typeName，每种规则引用该类型 footprints 中 `blocksMovement:false` 的矩形 label。例如：

```json
{"typeName":"OreNode","accessLabel":"access","nearestCount":2,"maxDistanceCells":30,"maxRankDistanceSpreadCells":2,"requireAllResourcesReachable":true}
```

对每个出生点计算四邻接最短距离场，然后取到每个资源预留矩形内可通行格心的最短步数（每步一格，非游戏行驶时间）。沿用 profile 的坡度、水深、通行标记和净空，也考虑全部对象阻挡。按每位玩家到资源的距离排序，比较前 nearestCount 项中相同排名的最大/最小距离差。每位玩家都必须有足够数量的可达资源，且这些距离不超过 maxDistanceCells；默认 requireAllResourcesReachable 要求每个该类资源至少被一个玩家到达。默认 nearestCount=1、maxDistanceCells=2147483647、maxRankDistanceSpreadCells=0（严格相等）。可根据格子离散误差或布局目标显式设容差。

结果 `resource-access:<typeName>` 包含每位玩家的最近距离、各排名的差值、不可达资源数及各资源的距离数组（数组索引0对应玩家1；null表示不可达；详情最多100条，全部资源仍参与计算）。原始出生点世界坐标向下取整为可玩格，因此几何对称不必得到零距离差。规则进入 profileHash；候选与最终地图都可检查。覆盖缺失或出生点无效会失败，缺少指定预留矩形/将阻挡矩形作为入口报 INVALID_ARGUMENT。

此项衡量显式规划入口的接近距离，不自动分配资源归属，也不评估矿产总量、收益、争夺优势、矿车具体停靠点或整体战略平衡。双人示例要求最近两矿在30格内、同排名差不超过2格；全部八矿至少可由一方到达。

## 路线诊断叠加

`diagnostics.render` 的 passability 图层可传 `routes`（最多16条，from/to 为可玩整数格），使用同一次 TerrainTraversal 分析绘制路径，并返回 `routes` 元数据。例：

```json
{"layer":"passability","profile":{"waterLevel":200,"clearanceCells":2},"routes":[{"from":{"x":48,"y":128},"to":{"x":208,"y":128}}]}
```

需要考虑对象时同时传 footprints；候选传 preparedPlanId/planHash。路线元数据包含索引、颜色、两端格坐标、各端阻挡状态及与 terrain.analyze 相同的 GridRoute（状态、步数、距离、格序列）。PNG 为北向上，路线连接格心；绿色圆点为起点、粉色方块为终点、白色叉号表示端点被阻挡。断路只画端点，不画直线代替路径。路线颜色每8条循环；重合路径/端点由后画的覆盖，可分别渲染需要观察的路线。

每条最多显示前10000格，超出时 metadata 的 truncated=true，并列入未评估/显示限制；不将尾部直接连接到终点。线宽仅为显示像素宽度，不代表单位净空。阻挡图例计数仍是底层全部阻挡格，叠加线与标记会遮住部分底图像素。验收时比较地图哈希、profile、footprintProfileHash 和路线数据；它是规划模型可视化，不是引擎寻路轨迹。双人示例的 passability 图已叠加三条主要检查路线。

## 真实材质对照

选材前可运行 `scripts/create_texture_palette.py --launcher <WbLauncher.exe路径>`。需要已构建的 Agent；仅依赖 Python 标准库。默认比较16种 Yucatan 地表，也可用 `--textures Grass_Yucatan05 Dirt_Yucatan01 ...` 指定1–64种唯一名称，`--columns 4` 设置列数，`--tile-cells 32` 设置每个样片格数。

工具先通过 textures.list 核实名称，在独立地图中生成高度260的材质块（默认水灯，关闭材质边缘混合），保存后调用真实 WorldBuilder 预览。输出在 artifacts/texture-palette 下：`index.html` 提供有名称的对照页；`report.json` 记录每块的地图区域、像素裁切框、地图/图像/渲染配置哈希；`evidence.json` 保留请求；`source` 保留源地图；原始PNG保存在 observations。HTML直接引用原图，不需要网络，不改写PNG。分享时保留整个输出目录。

按同一次渲染比较颜色、重复频率和纹理尺度，选定后回到真实布局检查融合。对照图只说明记录配置下平地材质的可见效果，不证明不同Mod、混合边界、坡地或对象搭配效果。默认样片观察显示 Dirt_Yucatan02 的斑纹对比较强；双人示例已据此改用较均匀的 Dirt_Yucatan01 路面。
