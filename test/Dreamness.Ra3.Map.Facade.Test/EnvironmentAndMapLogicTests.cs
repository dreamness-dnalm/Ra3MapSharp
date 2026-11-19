using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Fog;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.MissionHotSpot;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Trigger;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Waypoint;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class EnvironmentAndMapLogicTests
{
    private string directory = null!;

    [SetUp]
    public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "Ra3EnvironmentTests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static Ra3MapFacade NewMap() => Ra3MapFacade.NewMap(32, 32, 2, 0);
    private static Vec2D[] Triangle() => new[] {new Vec2D(40,40), new Vec2D(140,40), new Vec2D(40,140)};

    private Ra3MapFacade Reload(Ra3MapFacade map, bool compress)
    {
        var path = Path.Combine(directory, "review.map");
        map.SaveAs(path, compress);
        return Ra3MapFacade.Open(path);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WaypointPanelFields_EditParsedObjectAndSaveAgain(bool compress)
    {
        var map = NewMap();
        var waypoint = map.AddWaypoint("Route_A", 150, 150, 10);
        waypoint.PathLabel1 = "路线一";
        waypoint.PathLabel2 = "RouteTwo";
        waypoint.PathLabel3 = "RouteThree";
        waypoint.PathBiDirectional = true;
        waypoint.WaypointType = 12345; // Unknown type codes must survive without enum coercion.
        waypoint.WaypointTypeOption = "设置值";
        waypoint.Properties.PutProperty("customWaypointProperty", "preserved");
        var to = map.AddWaypoint("Route_B", 450, 450);
        map.AddWaypointConnection(waypoint.WaypointID, to.WaypointID);

        var loaded = Reload(map, compress);
        var parsed = loaded.GetWaypoint("Route_A")!;
        Assert.That(parsed.PathLabel1, Is.EqualTo("路线一"));
        Assert.That(parsed.PathLabel2, Is.EqualTo("RouteTwo"));
        Assert.That(parsed.PathLabel3, Is.EqualTo("RouteThree"));
        Assert.That(parsed.PathBiDirectional, Is.True);
        Assert.That(parsed.WaypointType, Is.EqualTo(12345));
        Assert.That(parsed.WaypointTypeOption, Is.EqualTo("设置值"));
        Assert.That(parsed.Position.Z, Is.EqualTo(10));
        parsed.PathLabel1 = "Changed";
        parsed.PathLabel2 = "";
        parsed.PathLabel3 = "ChangedThree";
        parsed.PathBiDirectional = false;
        parsed.WaypointType = -7;
        parsed.WaypointTypeOption = "";
        parsed.Position.X = 175;

        var twice = Reload(loaded, compress);
        var edited = twice.GetWaypoint("Route_A")!;
        Assert.That(edited.PathLabel1, Is.EqualTo("Changed"));
        Assert.That(edited.PathLabel2, Is.Empty);
        Assert.That(edited.PathLabel3, Is.EqualTo("ChangedThree"));
        Assert.That(edited.PathBiDirectional, Is.False);
        Assert.That(edited.WaypointType, Is.EqualTo(-7));
        Assert.That(edited.WaypointTypeOption, Is.Empty);
        Assert.That(edited.Position.X, Is.EqualTo(175));
        Assert.That(edited.Properties.GetProperty<string>("customWaypointProperty"), Is.EqualTo("preserved"));
        Assert.That(twice.GetWaypoint("Route_B")!.PathLabel1, Is.Empty);
        Assert.That(twice.GetWaypointConnections().Count, Is.EqualTo(1));
    }

    [Test]
    public void NewWaypoint_ReadsMissingTypeAsNormal()
    {
        var map = NewMap();
        var waypoint = map.AddWaypoint("Route_A", 10, 10);
        Assert.That(waypoint.Properties.PropertyNames, Does.Not.Contain("waypointType"));
        Assert.That(waypoint.WaypointType, Is.EqualTo((int)WaypointWrap.WaypointTypeKind.Normal));
        Assert.That(waypoint.TryGetKnownWaypointType(out var kind), Is.True);
        Assert.That(kind, Is.EqualTo(WaypointWrap.WaypointTypeKind.Normal));
        Assert.That(WaypointWrap.WaypointTypeNames, Is.EqualTo(new[]
        {
            "Normal",
            "Portal",
            "WalkPortal",
            "ClimbPortal",
            "PreClimbPortal",
            "Beacon",
            "Spline (CatmullRom)",
            "FakePathfindPortal (don't use)",
            "MineshaftPortal (don't use)"
        }));
        waypoint.WaypointType = (int)WaypointWrap.WaypointTypeKind.SplineCatmullRom;
        Assert.That(waypoint.TryGetKnownWaypointType(out kind), Is.True);
        Assert.That(kind, Is.EqualTo(WaypointWrap.WaypointTypeKind.SplineCatmullRom));
        waypoint.WaypointType = 12345;
        Assert.That(waypoint.TryGetKnownWaypointType(out _), Is.False);
        Assert.That(waypoint.WaypointType, Is.EqualTo(12345));
    }

    [Test]
    public void WaypointOptionalPanelReads_DoNotInsertProperties()
    {
        var map = NewMap();
        var waypoint = map.AddWaypoint("Route_A", 150, 150);
        waypoint.Properties.RemoveProperty("waypointTypeOption");
        var count = waypoint.Properties.PropertyCount;
        Assert.That(waypoint.PathLabel1, Is.Empty);
        Assert.That(waypoint.PathLabel2, Is.Empty);
        Assert.That(waypoint.PathLabel3, Is.Empty);
        Assert.That(waypoint.PathBiDirectional, Is.False);
        Assert.That(waypoint.WaypointTypeOption, Is.Empty);
        Assert.That(waypoint.Properties.PropertyCount, Is.EqualTo(count));
        Assert.That(map.GetWaypoint("missing"), Is.Null);
        Assert.Throws<ArgumentNullException>(() => waypoint.PathLabel1 = null!);
        Assert.Throws<ArgumentNullException>(() => waypoint.PathLabel2 = null!);
        Assert.Throws<ArgumentNullException>(() => waypoint.PathLabel3 = null!);
        Assert.Throws<ArgumentNullException>(() => waypoint.WaypointTypeOption = null!);
        Assert.That(waypoint.Properties.PropertyCount, Is.EqualTo(count));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SevenAssets_AddEditSaveReopen_PreservesAllFields(bool compress)
    {
        var map = NewMap();
        map.SetLightingTime(2);
        map.SetShadowColor(new MapColorArgb(127, 11, 22, 33));
        map.SetNoCloudFactor(new ColorRgbF(0.7f, 0.8f, 0.9f));
        map.GetGlobalLighting()!.Configs[0].TerrainSun.Color.X = 0.25f;
        map.AddPostEffect("Bloom", PostEffectParameter.Of("Intensity", "Float", 1.5f));
        map.ResizeMultiplayerPositions(8);
        map.SetMultiplayerPosition(7, false, true, false, 2, new[] {"Japan"});
        map.AddTriggerArea("区域", Triangle(), "默认触发层", 50);
        map.ConfigureFog(true, 100, 2000, new ColorRgbF(0.2f,0.3f,0.4f));
        map.AddMissionHotSpot("任务一", "HOTSPOT:Title", "HOTSPOT:Desc");
        var from = map.AddWaypoint("路点一", 50,50);
        var to = map.AddWaypoint("路点二", 150,150);
        Assert.That(from.WaypointID, Is.Not.EqualTo(to.WaypointID));
        map.AddWaypointConnection(from.WaypointID, to.WaypointID);

        var reloaded = Reload(map, compress);
        Assert.That(reloaded.GetGlobalLighting()!.Time, Is.EqualTo(2));
        Assert.That(reloaded.GetGlobalLighting()!.ShadowColor.R, Is.EqualTo(11));
        Assert.That(reloaded.GetGlobalLighting()!.NoCloudFactor.B, Is.EqualTo(0.9f));
        Assert.That(reloaded.GetGlobalLighting()!.Configs[0].TerrainSun.Color.X, Is.EqualTo(0.25f));
        Assert.That(reloaded.GetPostEffects().Single(e => e.Name == "Bloom").Parameters[0].Value, Is.EqualTo(1.5f));
        Assert.That(reloaded.GetMultiplayerPositions().Count, Is.EqualTo(8));
        Assert.That(reloaded.GetMultiplayerPositions()[7].Team, Is.EqualTo(2));
        Assert.That(reloaded.GetMultiplayerPositions()[7].SideRestrictions, Is.EqualTo(new[] {"Japan"}));
        Assert.That(reloaded.GetTriggerArea("区域")!.Points.Count, Is.EqualTo(3));
        Assert.That(reloaded.GetTriggerArea("区域")!.LayerName, Is.EqualTo("默认触发层"));
        Assert.That(reloaded.GetTriggerArea("区域")!.Height, Is.EqualTo(50));
        Assert.That(reloaded.GetFogSettings()!.RangeEnd, Is.EqualTo(2000));
        Assert.That(reloaded.GetFogSettings()!.Color.G, Is.EqualTo(0.3f));
        Assert.That(reloaded.GetMissionHotSpot("任务一")!.Description, Is.EqualTo("HOTSPOT:Desc"));
        Assert.That(reloaded.GetWaypointConnections()[0].FromWaypointId, Is.EqualTo(from.WaypointID));
        Assert.That(reloaded.GetWaypoints().Count, Is.EqualTo(2));

        // Edit previously parsed children. This catches missing observer subscriptions after loading.
        reloaded.GetGlobalLighting()!.Configs[1].TerrainAccent1.Direction.Y = 0.5f;
        reloaded.SetPostEffectParameter("Bloom", "Intensity", 0.75f);
        reloaded.GetMultiplayerPositions()[0].Team = 3;
        reloaded.GetTriggerArea("区域")!.Points[0].Y = 80;
        reloaded.GetFogSettings()!.Enabled = false;
        reloaded.GetMissionHotSpot("任务一")!.Title = "HOTSPOT:Changed";
        reloaded.GetWaypointConnections()[0].ToWaypointId = from.WaypointID;
        var twice = Reload(reloaded, compress);
        Assert.That(twice.GetGlobalLighting()!.Configs[1].TerrainAccent1.Direction.Y, Is.EqualTo(0.5f));
        Assert.That(twice.GetPostEffects().Single(e => e.Name == "Bloom").Parameters[0].Value, Is.EqualTo(0.75f));
        Assert.That(twice.GetPostEffects().First().Name, Is.EqualTo("Distortion"));
        Assert.That(twice.GetMultiplayerPositions()[0].Team, Is.EqualTo(3));
        Assert.That(twice.GetTriggerArea("区域")!.Points[0].Y, Is.EqualTo(80));
        Assert.That(twice.GetFogSettings()!.Enabled, Is.False);
        Assert.That(twice.GetMissionHotSpot("任务一")!.Title, Is.EqualTo("HOTSPOT:Changed"));
        Assert.That(twice.GetWaypointConnections()[0].ToWaypointId, Is.EqualTo(from.WaypointID));
    }

    [Test]
    public void OptionalReads_DoNotCreateChunks_AndMutationsCreateOnlyRequiredChunk()
    {
        var map = NewMap();
        string[] names = {AssetNameConst.GlobalLighting, AssetNameConst.PostEffectsChunk, AssetNameConst.MPPositionList,
            AssetNameConst.TriggerAreas, AssetNameConst.FogSettings, AssetNameConst.MissionHotSpots, AssetNameConst.WaypointsList};
        foreach (var name in names) map.ra3Map.Context.AssetDict.Remove(name);
        var count = map.ra3Map.Context.AssetDict.Count;
        Assert.That(map.GetGlobalLighting(), Is.Null);
        Assert.That(map.GetPostEffects(), Is.Empty);
        Assert.That(map.GetMultiplayerPositions(), Is.Empty);
        Assert.That(map.GetTriggerAreas(), Is.Empty);
        Assert.That(map.GetFogSettings(), Is.Null);
        Assert.That(map.GetMissionHotSpots(), Is.Empty);
        Assert.That(map.GetWaypointConnections(), Is.Empty);
        Assert.That(map.ra3Map.Context.AssetDict.Count, Is.EqualTo(count));
        map.AddMissionHotSpot("h", "t", "d");
        Assert.That(map.ra3Map.Context.AssetDict.Count, Is.EqualTo(count + 1));
        map.SetLightingTime(1); map.AddPostEffect("Bloom", PostEffectParameter.Of("Intensity", "Float", 1f));
        map.ResizeMultiplayerPositions(6); map.AddTriggerArea("a", Triangle());
        map.ConfigureFog(false, 0, 1000, new ColorRgbF(1,1,1));
        var a=map.AddWaypoint("a",0,0); var b=map.AddWaypoint("b",100,100);
        map.AddWaypointConnection(a.WaypointID,b.WaypointID);
        var loaded = Reload(map, false);
        Assert.That(loaded.GetPostEffects().Count, Is.EqualTo(1));
        Assert.That(loaded.GetMultiplayerPositions().Count, Is.EqualTo(6));
        Assert.That(loaded.GetTriggerAreas().Count, Is.EqualTo(1));
    }

    [Test]
    public void CollectionRemovals_PersistAfterReload()
    {
        var map=NewMap();
        map.AddTriggerArea("a",Triangle()); map.AddMissionHotSpot("h","t","d");
        var a=map.AddWaypoint("a",0,0); var b=map.AddWaypoint("b",100,100);
        map.AddWaypointConnection(a.WaypointID,b.WaypointID);
        var loaded=Reload(map,false);
        Assert.That(loaded.RemoveTriggerArea("a"),Is.True);
        Assert.That(loaded.RemoveMissionHotSpot("h"),Is.True);
        Assert.That(loaded.RemoveWaypointConnection(a.WaypointID,b.WaypointID),Is.True);
        Assert.That(loaded.RemovePostEffect("Distortion"),Is.True);
        loaded.ResizeMultiplayerPositions(0);
        var empty=Reload(loaded,false);
        Assert.That(empty.GetTriggerAreas(),Is.Empty);
        Assert.That(empty.GetMissionHotSpots(),Is.Empty);
        Assert.That(empty.GetWaypointConnections(),Is.Empty);
        Assert.That(empty.GetPostEffects(),Is.Empty);
        Assert.That(empty.GetMultiplayerPositions(),Is.Empty);
    }

    [Test]
    public void Validation_RejectsDuplicatesMissingEndpointsAndBadParameters_WithoutChangingMap()
    {
        var map=NewMap();
        map.AddTriggerArea("a",Triangle()); map.AddMissionHotSpot("h","t","d");
        var count=map.ra3Map.Context.AssetDict.Count;
        Assert.Throws<ArgumentException>(()=>map.AddTriggerArea("a",Triangle()));
        Assert.Throws<ArgumentException>(()=>map.AddTriggerArea("b",new[] {new Vec2D(0,0)}));
        Assert.Throws<ArgumentException>(()=>map.AddMissionHotSpot("h","t","d"));
        Assert.Throws<ArgumentException>(()=>map.AddWaypointConnection(100,200));
        Assert.Throws<ArgumentException>(()=>map.ConfigureFog(true,200,100,new ColorRgbF(1,1,1)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>map.SetLightingTime(4));
        Assert.Throws<ArgumentOutOfRangeException>(()=>map.SetMultiplayerPosition(9,true,true,true,0));
        Assert.Throws<ArgumentException>(()=>PostEffectParameter.Of("bad","Float4",new[] {1f}));
        Assert.That(map.ra3Map.Context.AssetDict.Count,Is.EqualTo(count));
        Assert.That(map.GetFogSettings(),Is.Null);
    }

    [Test]
    public void ErroredChunk_CanBeSavedUnchanged_ButFacadeRefusesToReplaceIt()
    {
        var map=NewMap();
        var broken=new FogSettingsAsset { AssetType=AssetNameConst.FogSettings, Version=2,
            Id=map.ra3Map.Context.RegisterStringDeclare(AssetNameConst.FogSettings), Data=new byte[] {1,2,3}, DataSize=3 };
        broken.ParseTolerance(map.ra3Map.Context);
        map.ra3Map.Context.OverrideAsset(broken);
        var original=broken.ToBytes(map.ra3Map.Context);
        Assert.Throws<InvalidOperationException>(()=>map.ConfigureFog(true,0,100,new ColorRgbF(1,1,1)));
        Assert.That(broken.ToBytes(map.ra3Map.Context),Is.EqualTo(original));
        var loaded=Reload(map,false);
        Assert.That(loaded.ra3Map.Context.FogSettingsAsset!.Errored,Is.True);
        Assert.Throws<InvalidOperationException>(()=>loaded.GetFogSettings());
    }

    [Test]
    public void Facade_UsesCurrentContextAfterAssetReplacement_AndListSnapshotsDoNotMutateMap()
    {
        var map=NewMap(); map.AddMissionHotSpot("h","t","d");
        var snapshot=map.GetMissionHotSpots(); snapshot.Clear();
        Assert.That(map.GetMissionHotSpots().Count,Is.EqualTo(1));
        var replacement=MissionHotSpotsAsset.Default(map.ra3Map.Context);
        map.ra3Map.Context.OverrideAsset(replacement);
        Assert.That(map.GetMissionHotSpots(),Is.Empty);
    }

    [Test, Explicit("Creates review maps only when selected; set RA3_ASSET_REVIEW_OUTPUT to a workspace directory.")]
    public void GenerateManualReviewMaps()
    {
        var output=Environment.GetEnvironmentVariable("RA3_ASSET_REVIEW_OUTPUT");
        Assert.That(output,Is.Not.Null.And.Not.Empty);
        var map=Ra3MapFacade.NewMap(64,64,4,2);
        map.AddWaypoint("Route_A",150,150);
        map.AddWaypoint("Route_B",450,450);
        map.AddWaypointConnection("Route_A","Route_B");
        map.AddTriggerArea("ReviewArea",new[] {new Vec2D(100,100),new Vec2D(300,100),new Vec2D(300,300),new Vec2D(100,300)});
        map.AddMissionHotSpot("ReviewHotSpot","HOTSPOT:ReviewTitle","HOTSPOT:ReviewDescription");
        map.AddPostEffect("Bloom",PostEffectParameter.Of("Intensity","Float",0.5f));
        map.ConfigureFog(false,300,1600,new ColorRgbF(0.65f,0.7f,0.8f));
        map.SaveAs(output!,"AssetReview_Baseline",true);
        // Reopen before editing so review exercises the actual parser, not only constructors.
        map=Ra3MapFacade.Open(Path.Combine(output!,"AssetReview_Baseline","AssetReview_Baseline.map"));
        map.SetLightingTime(3);
        map.SetPostEffectParameter("Bloom","Intensity",1.5f);
        map.ConfigureFog(true,300,1600,new ColorRgbF(0.65f,0.7f,0.8f));
        map.GetTriggerArea("ReviewArea")!.Points[1].X=400;
        map.GetMissionHotSpot("ReviewHotSpot")!.Description="HOTSPOT:ReviewDescriptionEdited";
        map.SetMultiplayerPosition(0,true,false,false,1,new[] {"Allies"});
        map.SaveAs(output!,"AssetReview_Edited",true);
        map.ResizeMultiplayerPositions(8);
        map.SaveAs(output!,"AssetReview_EightSlots",true);
        foreach(var name in new[] {"AssetReview_Baseline","AssetReview_Edited","AssetReview_EightSlots"})
        {
            var path=Path.Combine(output!,name,name+".map");
            var loaded=Ra3MapFacade.Open(path);
            Assert.That(loaded.GetTriggerAreas().Count,Is.EqualTo(1));
            Assert.That(loaded.GetWaypointConnections().Count,Is.EqualTo(1));
        }
    }
}
