using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Trigger;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Fog;
using Dreamness.Ra3.Map.Parser.Asset.Impl.MissionHotSpot;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Waypoint;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class MapEnvironmentAssetsTests
{
    public record ChunkFixture(string Type, short Version, Dictionary<int, string> Declarations, byte[] Data, string Source);

    public static IEnumerable<TestCaseData> Fixtures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "chunk-fixtures.json")));
        var i = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var fixture = new ChunkFixture(item.GetProperty("type").GetString()!, item.GetProperty("version").GetInt16(),
                item.GetProperty("declarations").EnumerateObject().ToDictionary(p => int.Parse(p.Name), p => p.Value.GetString()!),
                Convert.FromBase64String(item.GetProperty("data").GetString()!), item.GetProperty("source").GetString()!);
            yield return new TestCaseData(fixture).SetName($"Corpus_{fixture.Type}_{i++}");
        }
    }

    public static BaseAsset Parse(byte[] bytes, MapContext context)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        var asset = AssetParser.FromBinaryReader(reader, context);
        Assert.That(reader.BaseStream.Position, Is.EqualTo(reader.BaseStream.Length));
        Assert.That(asset.Errored, Is.False, asset.ErrorException?.ToString());
        return asset;
    }

    public static byte[] Chunk(string name, short version, byte[] payload, MapContext context)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(context.RegisterStringDeclare(name)); writer.Write(version); writer.Write(payload.Length); writer.Write(payload);
        return stream.ToArray();
    }

    [TestCaseSource(nameof(Fixtures))]
    public void RealChunk_ParsesCleanly_AndForcedNestedRewriteMatchesOriginal(ChunkFixture fixture)
    {
        var context = new MapContext();
        foreach (var declaration in fixture.Declarations) context.RegisterStringDeclare(declaration.Key, declaration.Value);
        var original = Chunk(fixture.Type, fixture.Version, fixture.Data, context);
        var asset = Parse(original, context);
        Assert.That(asset, Is.Not.InstanceOf<Dreamness.Ra3.Map.Parser.Asset.Impl.Default.DefaultAsset>());
        Assert.That(asset._modified, Is.False, fixture.Source);
        Assert.That(asset.ToBytes(context), Is.EqualTo(original));
        // Rewrite each nested record, avoiding a test that only exercises BaseAsset's raw-data fallback.
        ForceRewrite(asset);
        Assert.That(asset.ToBytes(context), Is.EqualTo(original), fixture.Source);
        Parse(asset.ToBytes(context), context);
    }

    private static void ForceRewrite(BaseAsset asset)
    {
        switch (asset)
        {
            case GlobalLightingAsset lighting:
                foreach (var config in lighting.Configs)
                {
                    config.TerrainSun.MarkModified(); config.TerrainAccent1.MarkModified(); config.TerrainAccent2.MarkModified(); config.MarkModified();
                }
                break;
            case PostEffectsChunkAsset effects:
                foreach (var effect in effects.PostEffects)
                {
                    foreach (var parameter in effect.Parameters) parameter.MarkModified();
                    effect.MarkModified();
                }
                break;
            case MPPositionListAsset positions:
                foreach (var position in positions.MPPositionInfos) position.MarkModified();
                break;
        }
        asset.MarkModified();
    }

    [Test]
    public void NestedLightingMutation_Propagates_AndPreservesOtherConfigurations()
    {
        var context = new MapContext();
        var seed = GlobalLightingAsset.Default(context);
        var asset = (GlobalLightingAsset)Parse(seed.ToBytes(context), context);
        var untouched = asset.Configs[1].ToBytes(context);
        asset.Configs[0].TerrainSun.Ambient.X = 0.75f;
        Assert.That(asset._modified, Is.True);
        var reloaded = (GlobalLightingAsset)Parse(asset.ToBytes(context), context);
        Assert.That(reloaded.Configs[0].TerrainSun.Ambient.X, Is.EqualTo(0.75f));
        Assert.That(reloaded.Configs[1].ToBytes(context), Is.EqualTo(untouched));
        Assert.That(reloaded._modified, Is.False);
    }

    [TestCase("Float")]
    [TestCase("Float4")]
    [TestCase("Int")]
    [TestCase("Texture")]
    public void PostEffectTypedParameter_EditPropagates_AndUneditedSiblingSurvives(string type)
    {
        object initial = type switch { "Float" => 1f, "Float4" => new[] {1f,2f,3f,4f}, "Int" => 7, _ => "LUT_A" };
        object changed = type switch { "Float" => 2f, "Float4" => new[] {4f,3f,2f,1f}, "Int" => 8, _ => "LUT_B" };
        var context = new MapContext();
        var seed = PostEffectsChunkAsset.Default(context);
        seed.PostEffects.Add(PostEffect.Of("Custom", new[] { PostEffectParameter.Of("Value", type, initial) }));
        var asset = (PostEffectsChunkAsset)Parse(seed.ToBytes(context), context);
        Assert.That(asset.PostEffects[1].Parameters[0].Value, Is.EqualTo(initial));
        asset.PostEffects[1].Parameters[0].SetValue(changed);
        Assert.That(asset._modified, Is.True);
        var reloaded = (PostEffectsChunkAsset)Parse(asset.ToBytes(context), context);
        Assert.That(reloaded.PostEffects[0].Name, Is.EqualTo("Distortion"));
        Assert.That(reloaded.PostEffects[1].Parameters[0].Value, Is.EqualTo(changed));
    }

    [TestCase(0)]
    [TestCase(6)]
    [TestCase(8)]
    public void MultiplayerPositions_ActualCountAndNestedHeadersSurvive(int count)
    {
        var context = new MapContext();
        var seed = MPPositionListAsset.Default(context);
        seed.MPPositionInfos.Clear();
        for (var i = 0; i < count; i++) seed.MPPositionInfos.Add(MPPositionInfo.Of(true, false, true, (uint)i, new[] { "Allies", "Soviet" }, context));
        var asset = (MPPositionListAsset)Parse(seed.ToBytes(context), context);
        Assert.That(asset.MPPositionInfos.Count, Is.EqualTo(count));
        if (count == 0) return;
        Assert.That(asset.MPPositionInfos[0].Version, Is.EqualTo(1));
        Assert.That(asset.MPPositionInfos[0]._modified, Is.False);
        asset.MPPositionInfos[0].SideRestrictions = new[] { "Japan" };
        asset.MPPositionInfos[0].Team = uint.MaxValue;
        Assert.That(asset._modified, Is.True);
        var reloaded = (MPPositionListAsset)Parse(asset.ToBytes(context), context);
        Assert.That(reloaded.MPPositionInfos[0].SideRestrictions, Is.EqualTo(new[] { "Japan" }));
        Assert.That(reloaded.MPPositionInfos[0].Team, Is.EqualTo(uint.MaxValue));
        Assert.That(reloaded.MPPositionInfos[1].SideRestrictions, Is.EqualTo(new[] { "Allies", "Soviet" }));
    }

    [Test]
    public void MultiplayerPayloadReader_RemainsCompatible_WithoutPrematureDirtyState()
    {
        var context = new MapContext();
        var seed = MPPositionInfo.Of(true, false, true, 3, new[] { "Japan" }, context);
        using var reader = new BinaryReader(new MemoryStream(seed.ToBytes(context).Skip(10).ToArray()));
        var slot = MPPositionInfo.FromBinaryReader(reader, context);
        Assert.That(slot._modified, Is.False);
        Assert.That(slot.SideRestrictions, Is.EqualTo(new[] { "Japan" }));
        Assert.That(slot.ToBytes(context), Is.EqualTo(seed.ToBytes(context)));
        var copy = slot.SideRestrictions; copy[0] = "Allies";
        Assert.That(slot.SideRestrictions[0], Is.EqualTo("Japan"));
    }

    [Test]
    public void TriggerPolygon_ChineseNamesAndNestedPointMutationRoundTrip()
    {
        var context = new MapContext();
        var seed = TriggerAreasAsset.Default(context);
        seed.Areas.Add(TriggerArea.Of("区域一", "触发层", 9, new[] { new Vec2D(0,0), new Vec2D(100,0), new Vec2D(0,100) }, 50));
        var asset = (TriggerAreasAsset)Parse(seed.ToBytes(context), context);
        asset.Areas[0].Points[1].X = 200;
        Assert.That(asset._modified, Is.True);
        var reloaded = (TriggerAreasAsset)Parse(asset.ToBytes(context), context);
        Assert.That(reloaded.Areas[0].Name, Is.EqualTo("区域一"));
        Assert.That(reloaded.Areas[0].Height, Is.EqualTo(50));
        Assert.That(reloaded.Areas[0].Points[1].X, Is.EqualTo(200));
    }

    [Test]
    public void HotspotMutation_Propagates_AndPreservesSibling()
    {
        var context = new MapContext(); var seed = MissionHotSpotsAsset.Default(context);
        seed.HotSpots.Add(MissionHotSpot.Of("任务", "HOTSPOT:Title", "HOTSPOT:Desc"));
        seed.HotSpots.Add(MissionHotSpot.Of("second", "Title2", "Desc2"));
        var asset = (MissionHotSpotsAsset)Parse(seed.ToBytes(context), context);
        asset.HotSpots[0].Title = "HOTSPOT:Changed";
        Assert.That(asset._modified, Is.True);
        var reloaded = (MissionHotSpotsAsset)Parse(asset.ToBytes(context), context);
        Assert.That(reloaded.HotSpots[0].Title, Is.EqualTo("HOTSPOT:Changed"));
        Assert.That(reloaded.HotSpots[1].Description, Is.EqualTo("Desc2"));
    }

    [Test]
    public void WaypointConnectionMutationAndRemoval_RoundTrip()
    {
        var context = new MapContext(); var seed = WaypointsListAsset.Default(context);
        seed.Connections.Add(WaypointConnection.Of(1, 2)); seed.Connections.Add(WaypointConnection.Of(2, 3));
        var asset = (WaypointsListAsset)Parse(seed.ToBytes(context), context);
        asset.Connections[0].ToWaypointId = 3;
        asset.Connections.Remove(asset.Connections[1]);
        var reloaded = (WaypointsListAsset)Parse(asset.ToBytes(context), context);
        Assert.That(reloaded.Connections.Count, Is.EqualTo(1));
        Assert.That(reloaded.Connections[0].ToWaypointId, Is.EqualTo(3));
    }

    [TestCase("GlobalLighting", 11)]
    [TestCase("PostEffectsChunk", 2)]
    [TestCase("MPPositionList", 0)]
    [TestCase("TriggerAreas", 1)]
    [TestCase("FogSettings", 1)]
    [TestCase("MissionHotSpots", 1)]
    [TestCase("WaypointsList", 1)]
    public void UnsupportedVersion_PreservesRawBytes_AndRefusesRewrite(string name, short version)
    {
        var context = new MapContext(); var bytes = Chunk(name, (short)(version + 1), new byte[] { 1,2,3 }, context);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        var asset = AssetParser.FromBinaryReader(reader, context);
        Assert.That(asset.Errored, Is.True);
        Assert.That(asset.ToBytes(context), Is.EqualTo(bytes));
        asset.MarkModified();
        Assert.Throws<InvalidOperationException>(() => asset.ToBytes(context));
    }

    [TestCase("TriggerAreas")]
    [TestCase("MissionHotSpots")]
    [TestCase("WaypointsList")]
    public void NegativeCount_IsErroredInsteadOfSilentlyProducingEmptyList(string name)
    {
        var context = new MapContext(); var bytes = Chunk(name, 1, BitConverter.GetBytes(-1), context);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.That(AssetParser.FromBinaryReader(reader, context).Errored, Is.True);
    }

    [Test]
    public void AddedWaypoints_HaveDistinctIds_AndDoNotResetObjectIdCounter()
    {
        var context = new MapContext();
        var objects = Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject.ObjectsListAsset.Default(context);
        var first = objects.AddObj(context, "AlliedPowerPlant", new Vec3D(0,0,0));
        var a = objects.AddWaypoint("A", new Vec3D(0,0,0), context);
        var b = objects.AddWaypoint("B", new Vec3D(100,100,0), context);
        Assert.That(a.Properties.GetProperty<int>("waypointID"), Is.Not.EqualTo(b.Properties.GetProperty<int>("waypointID")));
        var second = objects.AddObj(context, "AlliedPowerPlant", new Vec3D(100,0,0));
        Assert.That(second.UniqueId, Is.Not.EqualTo(first.UniqueId));
    }

    [TestCase("FogSettings", 1, 23)]
    [TestCase("GlobalLighting", 11, 451)]
    public void TruncatedFixedLayout_PreservesOriginalForUnmodifiedSave(string name, short version, int size)
    {
        var context = new MapContext(); var bytes = Chunk(name, version, new byte[size], context);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        var asset = AssetParser.FromBinaryReader(reader, context);
        Assert.That(asset.Errored, Is.True);
        Assert.That(asset.ToBytes(context), Is.EqualTo(bytes));
    }

    [Test]
    public void TruncatedHotspotString_IsRejected()
    {
        var context = new MapContext();
        var bytes = Chunk(AssetNameConst.MissionHotSpots, 1, new byte[] { 1,0,0,0, 255,255, 0,0, 0,0 }, context);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.That(AssetParser.FromBinaryReader(reader, context).Errored, Is.True);
    }

    [Test]
    public void InvalidNestedMultiplayerChunk_IsRejected()
    {
        var context = new MapContext();
        var wrongChunk = Chunk(AssetNameConst.WaypointsList, 1, BitConverter.GetBytes(0), context);
        var bytes = Chunk(AssetNameConst.MPPositionList, 0, wrongChunk, context);
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.That(AssetParser.FromBinaryReader(reader, context).Errored, Is.True);
    }
}
