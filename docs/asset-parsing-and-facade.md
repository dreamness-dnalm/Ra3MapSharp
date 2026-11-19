# 光照、后处理与地图逻辑 Asset


MPPositionList / MPPositionInfo 解析及席位 Facade API 已恢复。当前地编保存时固定写出六条席位记录，八席位数据被地编另存后的行为仍待确认。

本次新增/启用七类 Asset：GlobalLighting v11、PostEffectsChunk v2、MPPositionList v0（嵌套 MPPositionInfo v1）、TriggerAreas v1、FogSettings v1、MissionHotSpots v1、WaypointsList v1。

已有公开类型、命名空间和方法签名保留。新增 Facade 接口放在 EnvironmentPart.cs 与 MapLogicPart.cs，MapContext 增加可空的类型化属性。已有 GetWaypoints() 仍返回 ObjectsList 内的路点对象；GetWaypointConnections() 返回连接记录。

## Facade 用法

```csharp
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
using Dreamness.Ra3.Map.Parser.Util;

var map = Ra3MapFacade.Open(inputPath);

// 光照：0..3 对应文件内四组配置；具体地编时段名称请以地编为准。
map.SetLightingTime(2);
map.SetShadowColor(new MapColorArgb(127, 160, 160, 160));
map.SetNoCloudFactor(new ColorRgbF(1, 1, 1));
var lighting = map.GetGlobalLighting();
if (lighting != null)
    lighting.Configs[0].TerrainSun.Ambient.X = 0.2f;

// 现有后处理可用 SetPostEffectParameter 编辑；不存在时可添加自定义效果。
if (!map.GetPostEffects().Any(e => e.Name == "Bloom"))
    map.AddPostEffect("Bloom", PostEffectParameter.Of("Intensity", "Float", 0.5f));
map.SetPostEffectParameter("Bloom", "Intensity", 1.0f);

// 是渲染距离雾，不是战争迷雾。
map.ConfigureFog(true, 300, 1600, new ColorRgbF(0.65f, 0.7f, 0.8f));

// 多人槽位索引从0开始。保留文件实际槽位数，显式resize才增删。
map.ResizeMultiplayerPositions(6);
map.SetMultiplayerPosition(0, true, false, false, uint.MaxValue, new[] { "Allies" });

// 区域顶点使用世界坐标。新区域自动分配ID，默认层为空字符串。
map.AddTriggerArea("ReviewArea", new[] {
    new Vec2D(100,100), new Vec2D(300,100), new Vec2D(300,300), new Vec2D(100,300)
});

// 热点Title/Description保存字符串表key；游戏内文本另由map.str等资源提供。
map.AddMissionHotSpot("ReviewHotSpot", "HOTSPOT:ReviewTitle", "HOTSPOT:ReviewDescription");

map.AddWaypoint("Route_A", 150, 150);
map.AddWaypoint("Route_B", 450, 450);
map.AddWaypointConnection("Route_A", "Route_B");
map.SaveAs(outputPath);
```

集合 Get 方法返回列表快照，其中记录仍可编辑；清空返回的 List 不会删除地图记录。删除请使用 RemovePostEffect、RemoveTriggerArea、RemoveMissionHotSpot、RemoveWaypointConnection，槽位数量使用 ResizeMultiplayerPositions。修改解析后记录的属性、嵌套顶点/向量、WritableList 会通知上层保存。

读取缺失的可选 Asset 返回 null/空列表，不会插入默认区块；第一次调用修改接口才创建所需区块。未知版本或解析失败保留原字节，未修改时仍可另存；Facade 拒绝读取为可编辑模型或替换该失败区块，以免丢失数据。支持的版本会严格检查完整消费和集合/字符串边界。

后处理参数支持 Float、Float4（恰好4个float）、Int、Texture。使用 parameter.SetValue(value) 修改值；Float4 与 MPPositionInfo.SideRestrictions 的 getter 返回副本，请修改副本后调用 SetValue 或赋回 SideRestrictions。这避免数组原地变更漏掉修改通知。

MPPositionInfo.FromBinaryReader 保留原来的“读取负载”接口；嵌套区块头由 AssetParser 处理。新地图默认生成六个槽位；现有空列表和八槽位文件原样读取。已有直接公开的 GlobalLightingAsset.noCloudFactor 字段与 PostEffectsChunkAsset.PostEffects 字段为兼容保留；请通过 NoCloudFactor 属性/Facade和原列表编辑，直接替换这些字段后需要自行 MarkModified（替换列表也会绕开原订阅）。

## 验证结果

- Parser 与 Facade 项目构建成功。
- Parser 全部测试：356通过，包括315份不同真实区块样本（光照98、后处理62、槽位14、触发区域54、雾16、任务热点16、路点连接55）。每份不仅测试未修改保存，还强制重新编码相关子记录并检查与原字节完全一致。
- Facade 新增测试：7通过，覆盖压缩/未压缩保存、重开后二次编辑、删除、可选区块、非法输入、失败区块保护、Context替换后的读取。另一个显式测试已生成并重开验证三张人工评审地图。
- 整个solution构建遇到原有问题：Transform的SymmetryStrategy.cs:113使用RoadObjectWrap，但HEAD中的RoadObjectWrap.cs已经是空文件。此次未修改该类型或Transform，未扩大修改范围解决这个问题。
- 未运行包含本机特定地图/写回操作的全部旧Facade用例；本次新增Facade测试全部使用独立临时地图，清理自己的输出，不修改原地图。

路点连接测试发现并修正原有计数器笔误：ObjectsListAsset.AddWaypoint创建路点后应更新maxWaypointId，原来更新maxObjectId，导致连续添加的路点得到同一个ID。

## 请在地编确认

测试地图位于 `test/asset_audit/manual_review/` 下三个各自命名的目录；每个目录的 .map 与目录同名。可直接在地编打开，或复制整个目录到自己的 Maps 后打开副本：

1. AssetReview_Baseline：日间默认光照、Bloom强度0.5、距离雾关闭、ReviewArea为四边形、Route_A/Route_B有一条连接、六槽位。
2. AssetReview_Edited：从Baseline重新解析后编辑生成。Time=3、Bloom=1.5、距离雾打开（300..1600），区域第二顶点X从300变400，首槽位只允许人类/Allies、队伍字段1、AI脚本关闭，热点描述key改为HOTSPOT:ReviewDescriptionEdited。
3. AssetReview_EightSlots：基于Edited把槽位列表扩为8，专门检查地编对现有八项格式的处理；不能据此认定游戏支持八人。

请确认以下几项，并将地编另存到新文件以便差分复核：

- 地图是否能正常打开，光照四时段的索引名称是否匹配；Bloom和距离雾在设置窗口中数值与视觉效果是否一致。
- ReviewArea的轮廓和图层是否正确。真实样本中所有区域的最后int32字段都为0，暂以Height保存并保留字段；非零值的实际含义及非默认图层需要额外地编差分样本，尚未凭空指定它的业务语义。
- Route_A/Route_B是否出现连接线，交换连接端点后是否改变方向；当前API只保存有序ID对，不自动插入反向连接。
- ReviewHotSpot是否出现在热点列表，标题/描述key是否正确。此地图未附map.str或激活热点的脚本；游戏内文字、激活及对象/路点关联需要有相应资源与脚本的地图验证。
- Edited的首槽位设置是否对应地编显示；EightSlots是否能打开，以及地编重新保存会保留、裁剪还是拒绝额外两项。

这些属于地编/游戏解释与显示确认；自动测试已经验证读取、修改、保存与再次读取的二进制一致性。尚未把人工确认视为已完成。


## 路径点面板字段

Facade 的 `GetWaypoint(name)` 返回可修改的 `WaypointWrap`；找不到时返回 null。
已有 `GetWaypoints()`、`WaypointType`（原始整数）、`WaypointName`、`Position` 保持兼容。
新增三个路线标签、双向标志与类型设置，缺失的可选字符串读作空字符串，缺失双向标志读作 false；读取不会创建属性。

```csharp
var waypoint = map.GetWaypoint("Route_A")
    ?? throw new KeyNotFoundException("Route_A");
waypoint.PathLabel1 = "RouteMain";
waypoint.PathLabel2 = "RouteAlternate";
waypoint.PathLabel3 = "";
waypoint.PathBiDirectional = true;
waypoint.WaypointTypeOption = "";
waypoint.Position.Z = 10;
map.SaveAs(outputPath);
```

`PathBiDirectional` 只修改 `waypointPathBiDirectional` 属性，不自动增加或删除 WaypointsList 中的连接。
`WaypointType` 保留未知整数值，暂不声明 Portal 等类型枚举。

需要地编配合确认：在同一张地图的同一个路径点上，为三个路线标签分别填写不同字符串，勾选双向，并选择 Portal；保存为 A.map。
再取消双向、改选另一种路径类型（记录下拉选项名称），若类型设置允许填写则填写一个明确的值；另存为 B.map。
提供两个地图及选项名称后，可以核实 UI 属性类型、路径类型数值映射和类型设置的保存规则。
双向标志对单位沿路线移动的影响需要另外在游戏中验证，当前测试只证明二进制字段保存正确。
