# Asset 解析现状调查（2026-10-06）

此报告记录实现前的调查结果。随后七类Asset已完成接入与Facade封装，最新接口、测试结果和人工检查步骤见 `docs/asset-parsing-and-facade.md`；下方“未接入”清单保留作历史调查证据。

范围：当前 `improve_for_swb` 分支（从本地 main 创建）；只读检查源码、本机 Maps 和原版遭遇战地图目录，IDA MCP 检查当前打开的 `WorldBuilder_Mod_1.12.exe.i64`。未修改 src，未保存原地图，未修改 IDB。该地编属于修改版，地址与其他二进制版本不通用。

## 结论

272 个地图文件的外层区块结构读取成功，0 个失败，发现 26 种顶层区块；其中 16 种没有进入 AssetParser 的类型化解析。此处“读取成功”只指解压、声明表和区块边界成功，不代表现有 Parser 的全部字段解析成功。样本包含自定义、模组、备份或生成地图，不是 272 张独立官方地图。

`DefaultAsset._Parse()` 是空函数，`ParseTolerance()` 仍将 Parsed 置为 true。因此 Parsed=true / Errored=false 不能证明区块已解析。未修改的 BaseAsset.ToBytes 会保留原始数据；DefaultAsset 一旦要求重新序列化则会抛 NotImplementedException。

## 未接入的顶层区块

出现数是文件中的区块次数。空列表与非空列表都计入；版本是样本实际值。

| Asset | 出现数 | 版本 | 负载字节数 | 实现状态 / 用途 |
|---|---:|---|---|---|
| GlobalLighting | 272 | 11 | 452 | 有解析类，分发注释掉；四组时段光照、太阳/辅助光、阴影色、无云因子 |
| StandingWaterAreas | 272 | 2 | 4–406 | 有解析类，分发注释掉；水域轮廓、高度、贴图、UV 滚动、着色器 |
| PostEffectsChunk | 272 | 2 | 20–264 | 有解析类，未分发；Bloom、LookupTable、Distortion 等后处理及参数 |
| MPPositionList | 272 | 0 | 0–168 | 有部分实现但布局有误；多人槽位，人类/电脑/AI脚本开关、队伍、阵营限制；样本存在6项和8项 |
| GlobalWaterSettings | 272 | 1 | 8 | _Parse 为空；水面反射开关与反射平面高度 |
| LibraryMapLists | 272 | 1 | 0–418 | _Parse 为空，LibraryMaps 是占位类；按玩家/阵营保存地图脚本库引用的可能性较高，具体引用语义仍需确认 |
| BuildLists | 237 | 1 | 29–568 | 实验性读取，无正式解析/反解析；玩家模板对应的 AI 基地建造计划、对象类型/名字、相对位置、朝向及行为标志 |
| TriggerAreas | 236 | 1 | 4–6006 | 无实现；命名触发多边形区域，供脚本区域条件/动作使用 |
| RiverAreas | 236 | 3 | 4–168 | 无实现；河流几何、水材质、纹理与流动等参数；235 个为空，只有1个有内容 |
| StandingWaveAreas | 236 | 4 | 4–1508 | 无实现；区域化水波/岸边波浪数据，具体参数名称待确认；41 个非空 |
| FogSettings | 236 | 1 | 24 | 无实现；渲染距离雾开关、两个距离值、RGB，非玩家探索战争迷雾 |
| MissionHotSpots | 236 | 1 | 4–580 | 无实现；任务界面热点，可激活/显示、链接路点或对象；16 个非空 |
| EnvironmentData | 236 | 4、5 | 42–53 | 无实现；两个浮点值及环境纹理引用，包括地形噪声/云纹理和环境 CubeMap；两浮点具体语义待确认 |
| NamedCameras | 236 | 2 | 4–999 | 无实现；命名摄像机预设，位置、名称和六个额外浮点参数；24 个非空 |
| CameraAnimationList | 236 | 3 | 4 | 无实现；镜头动画集合；本次所有样本都是空列表，不能验证非空格式 |
| WaypointsList | 236 | 1 | 4–1124 | 无实现；路点之间的连接关系表，不是路点坐标列表；81 个非空 |

已分发的顶层区块是 AssetList、GlobalVersion、HeightMapData、BlendTileData、WorldInfo、SidesList、Teams、PlayerScriptsList、ObjectsList、MissionObjectives。脚本与 Object 等嵌套区块另有分发。本次没有发现上述之外的新顶层类型；这不覆盖其他地图、scb、bin、paste 或所有嵌套类型。

## IDA 的可复核证据

- TriggerAreas：写入函数 0x659830，版本1，数量、两段字符串、整数与几何数据。
- RiverAreas：0x65DE80，版本3，数量、ID、字符串、float、byte、四个资源字符串及其他参数和几何。
- StandingWaveAreas：0x65EB80，版本4，数量、ID、两字符串、float、byte、几何及附加字符串。
- LibraryMapLists：0x65AAF0，版本1；由嵌套 LibraryMaps v1 组成，每项含字符串数量及字符串列表。外层没有简单的列表数量字段；加载注册在0x691F60中与玩家数据管理器共用上下文。
- BuildLists：0x65C4F0，版本1；数量、玩家模板引用、建造条目数量，每条包含字符串、相对XYZ、float和标志等。IDA 字符串另有 `Skirmish AI Build List` 与 Base Builder 建造列表相关脚本动作。不能把反编译误识别出来的 ParticleSystem/Apt 类型当作可靠语义。
- FogSettings：0x66B1D0，版本1，int32开关、两个float和三分量颜色。0x5EC440 实际执行 fwrite(...,4,1)，反编译器把形参误判成 char；因此不能依据 SLOBYTE 显示推断磁盘只写一个字节。
- MissionHotSpots：0x4DE470、0x4BC050，数量及每项三字符串；脚本声明字符串明确支持活跃状态、可见性、关联路点/对象。
- EnvironmentData：写0x6FC2A0、读0x66AAC0。v5 为两个float + 三个 uint16 长度字符串；v4 在两float后多一个旧byte字段。样本有 TSNoiseUrb、TSCloudMed、EVDefault。0x470AE0 的文件选择器明确是 CubeMap DDS Files (EV*.dds)，并调用第三字符串的setter。
- NamedCameras：0x674730，版本2，数量，每条XYZ、uint16长度名字、六个额外float。额外参数的具体含义尚未确认。
- CameraAnimationList：0x6B3FA0，版本3，数量及各动画的多态序列化。
- WaypointsList：0x4DE7D0、0x4E1CE0，版本1，数量后每条两个4字节值。与路点连接相关的语义仍应结合读取函数/编辑器差分继续确认。
- MPPositionList：0x6C5410 中写六项；0x6C5380 每项先写 `MPPositionInfo` v1 的标准10字节区块头，再写三个bool、uint32队伍、int32阵营限制数量和字符串。现有 MPPositionInfo.FromBinaryReader 直接读bool，漏头；当前类 GetVersion 返回0而地编写1。MPPositionList.Default 的循环 `i < 0` 还导致输出空负载。样本中还有168字节的列表，不能将该地编的六项循环视为所有文件的限制。

## 实现前需要处理的细节

1. GlobalLighting、StandingWaterAreas、PostEffectsChunk 不应仅打开分发就宣称完成：需要验证完整消费负载与修改后的往返序列化。
2. StandingWaterArea 读取了一个 short `magic`，但未保存在模型中，重新写入使用常量；其语义需确认。
3. PostEffect.FromBinaryReader 没有保存自己的原始 Data，而未修改时 ToBytes 返回 Data；PostEffectParameter.FromBinaryReader 又把原始字节赋给 Value 而非 Data，覆盖了已读取的类型化值。这些都需要在启用后处理分发之前核查。
4. 已分发也不等于完全理解：BlendTileData 保留 unfinished 标记、magic字段和悬崖数据计数；AssetList 的typeId/instanceId虽读取了，但引用对象语义仍未充分解释。

建议顺序：先补齐 GlobalWaterSettings、FogSettings、EnvironmentData 和 MPPositionList 的布局验证；再验证已有水域/光照/后处理类；接着做 TriggerAreas、WaypointsList、NamedCameras；最后深入 BuildLists、LibraryMapLists 和水波/镜头动画。需要镜头动画的非空样本或编辑器差分实验。

## 分析程序

候选布局验证最终0个错误：GlobalWaterSettings 272项、FogSettings 236项、EnvironmentData 236项、WaypointsList 236项均完整消费；MPPositionList 有234个六槽位列表、3个八槽位列表、35个空负载。八槽位确认为八个标准嵌套记录，不是未知尾部字节。这只能证明字段布局匹配，不能证明全部字段用途或修改后游戏行为。

- audit.py：独立实现只读解压与外层区块扫描，不调用任何生产解析器，避免 BuildLists 旧实验代码向外部目录写文件。
- census.json：全部区块的次数、版本、大小分布及少量样本路径/原始头部。
- verify.py / verification.json：验证 MP 嵌套区块、路点连接、距离雾、水反射和环境字段布局；只说明候选布局消费全部字节，不是生产解析器测试，也不是游戏运行验证。

运行：`python test/asset_audit/audit.py <地图目录> [其他目录] --out test/asset_audit/census.json`；随后 `python test/asset_audit/verify.py test/asset_audit/census.json <相同地图目录> [其他目录]`。
