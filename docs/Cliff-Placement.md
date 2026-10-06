# 放置悬崖美化物体

`terrain.detect_cliffs` 识别区域内的高度突变，`objects.place_cliffs` 沿识别出的边线放置一组指定风格的物体。两者均使用现有 Automation / Agent 命令入口；MCP 名称分别为 `ra3_terrain_detect_cliffs` 和 `ra3_objects_place_cliffs`。

## 识别

```json
{
  "command": "terrain.detect_cliffs",
  "sessionId": "$current",
  "arguments": {
    "region": { "x": 0, "y": 0, "width": 128, "height": 128 },
    "minDrop": 20,
    "minSlopeDegrees": 60
  }
}
```

region 支持 rectangle、circle、polygon，输入可使用 playableGrid 或 mapGrid。输出始终为 playableGrid（不含边界）。相邻高度采样的世界高度差必须同时达到 minDrop 和 minSlopeDegrees（每格10世界单位）。边线位于相邻采样之间；只有两端采样均被区域选中才识别，地图最下/左边界不生成越界半格线段。

线段有方向，**高地始终在左侧**，包含 lowHeight/highHeight。连接为确定顺序的开放线或闭合线；分叉处切断，不任意跨接。该模型适用于单格高度突变，不会把多格陡坡合成为一条完整崖面，也不读取游戏引擎的悬崖标志。

## 风格和放置

```json
{
  "command": "objects.place_cliffs",
  "sessionId": "$current",
  "expectedRevision": 3,
  "arguments": {
    "region": { "x": 0, "y": 0, "width": 128, "height": 128 },
    "seed": 42,
    "style": {
      "name": "自定义岩壁",
      "pieces": [
        {
          "typeName": "REPLACE_WITH_REAL_CLIFF_TYPE",
          "lengthCells": 4,
          "depthCells": 2,
          "angleOffsetDegrees": 0,
          "normalOffsetCells": 0,
          "zOffset": 0
        }
      ]
    },
    "gapCells": 0,
    "maxBendDegrees": 50,
    "baseHeight": "low",
    "avoidExisting": true,
    "maxObjects": 2000,
    "exclusions": []
  }
}
```

示例名称是占位符，尺寸仅演示参数格式，必须替换为实际资源和测量值。style 是随命令传入的配置，可保存为 JSON 后复用；当前没有独立的持久风格注册命令。pieces 最多64种且资源名唯一，使用指定 seed 的固定 LCG 算法选择能够装入剩余线长的物体。

| 字段 | 含义 |
| --- | --- |
| lengthCells | 沿悬崖方向的覆盖长度，0.25–4096格 |
| depthCells | 垂直悬崖方向的厚度，正数，最多4096格 |
| angleOffsetDegrees | 模型相对沿线方向的旋转修正，±360度 |
| normalOffsetCells | 放置中心向高地偏移的格数，负数向低地，±128格 |
| originOffsetAlongCells / originOffsetNormalCells | 从覆盖中心到模型原点的沿线/法线偏移，±128格 |
| role | straight（默认）、outerCorner、innerCorner；转角按高地侧判断 |
| zOffset | 世界单位偏移，±4096；terrain 模式直接写入地图的地形相对 Z |
| gapCells | 沿线的非负间隔，0–128格 |
| maxBendDegrees | 覆盖段内每条边与拟合方向的最大夹角，0–180度；默认50 |
| baseHeight | terrain（默认）/ low / high；后两者把目标绝对高度换算为模型原点处的地形相对 Z |

省略尺寸时，从宿主加载的 FootprintCatalog 解析。目录中的未旋转宽/深会根据 angleOffsetDegrees 投影为沿线长度和厚度；显式尺寸已经在沿线坐标系内。没有测量就返回 FOOTPRINT_MISSING，不假定物体大小。目录测量来自俯视图轮廓，可能包含阴影；准确覆盖长度、模型原点、朝向及底座高度仍需在实际崖面上校准。

物体中心按沿线弧长采样，以覆盖段首尾拟合朝向；超过弯曲阈值、矩形占地超出选区/可玩区域、与禁放/保护格子或已规划物体相交时跳过该段。直线模型不会自动补齐拐角和不足一个模型长度的末尾空隙。返回 placed、skipped、objectIds 和每个物体的位置/朝向，以及尺寸来源；skipped 不包含装不下的末尾余量。允许部分覆盖，需要调用者检查返回值和图像。

默认检查已有物体占地：本风格类型使用风格尺寸，其他类型必须有目录测量，否则请求失败。显式设置 avoidExisting=false 可忽略已有对象，但仍避让本次计划中对象、保护区及禁放区。检查是矩形规划，不是引擎碰撞检测。单次最多2000个新对象；超过上限整体失败并回滚，不截断输出。

## 鸟瞰图校准流程

1. 得到实际物体名单后，用现有 `--build-album` / `--build-footprints` 或显式量测确定尺寸。`footprints.get` 可以查看来源，`footprints.set` 保存手工修正。覆盖长度需要结合崖面接缝检查，不能完全等同于包含阴影的轮廓宽度。
2. 在测试地图上分别建立直崖、转角、闭合台地与多层崖。先放置单一类型，校准 angleOffsetDegrees、normalOffsetCells、baseHeight 和 zOffset，再验证混合 pieces。
3. 通过 `edits.prepare` 在隔离候选中执行 `objects.place_cliffs`，使用 `preview.start` 的 preparedPlanId/planHash 渲染，轮询 jobs.status，再用 preview.inspect 看图。宿主 stdin 必须保持打开。确认后 edits.apply 提交该候选，history.undo/redo 可撤销重做。
4. 真实渲染遵循 NewWorldBuilder/MAP_TASKS.md：WbLauncher --export-overview，检查退出码0，最长边2048像素，不能只凭旧 PNG 存在判断成功。需要当前模组正确初始化。

## 已读取的浮岛要塞资源与实测结果

对象库中共有20个相关条目。实际 GameObject 名称为 `FI_EdgePieceXX`，不能用编辑器显示名称替代。`FI_EdgePiece19` 是校准图中的黑色模型，已按用户要求记录在 `data/cliff-styles/floating-island.exclusions.json`，工作流生成的可用目录和校准摆放均排除它；原始数据仍保留以便追溯。

`scripts/ra3_cliff_data.py` 只读解析游戏 `WBData.big` 中的 worldbuilder manifest/bin。先读取 SDK XML 的 GameObject、模型引用和 Geometry，再依据 SDK 的 W3D XSD，读取编译模型包围盒、容器子模型、骨骼父子变换和 FixupMatrix，合成结构包围盒（排除 FXLIGHT）。XML 的120×120×5 Geometry 不能代表这些模型的实际外观尺寸，Art 中的 W3X 也是占位文件。尝试了 IDA MCP 的 WorldBuilder 数据库，最终尺寸来源是编译资源与 schema，并非猜测反编译字段。

`data/cliff-styles/floating-island.straight.json` 使用27、28号直段，覆盖长度分别为5格和2.5格。`floating-island.json` 另加入26号外转角；已用真实鸟瞰图校准模型原点、方向和 Z。当前预设针对高度差150的水平/垂直崖面，其他高度差需重新校准。地图对象 Z 是相对地形偏移，直接写入低地绝对高度会把模型抬高。

可重复运行：

```powershell
dotnet build src/Dreamness.RA3.Map.Agent/Dreamness.RA3.Map.Agent.csproj --no-restore
python scripts/cliff_workflow.py --library "C:\Users\mmmmm\AppData\Roaming\RA3MapLab\NewWorldBuilder\object-library.json" --sdk "N:\Program Files (x86)\Red Alert 3(Incomplete)\Ra3ModSDK" --launcher "N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe" --data-root "N:\SteamLibrary\steamapps\common\Command and Conquer Red Alert 3\Data" --output artifacts/cliff-workflow/new-run
```

输出包含20个资源的 `units.json`、排除黑色模型后的 `templates.json`、每次请求的 `evidence.json` 和 `summary.json`。流程创建独立测试地图，识别悬崖、渲染基线、准备候选、渲染候选、提交、撤销、重做和保存。20261003/run2 的直线测试放置32个对象且视觉连续；闭合台地生成四个外转角，完整事务流程通过，但转角与直段仍存在可见接缝和末尾余量，尚不能称为无缝铺设。内转角和多层崖未完成外观校准。

指定转角 role 后会在方向变化处分割直段，并为转角预留覆盖范围。矩形占地和原点校正支持确定性规划，但包围盒不等于实际可拼接边缘；返回的 `notEvaluated` 仍表示运行时没有自动判定视觉接缝。
