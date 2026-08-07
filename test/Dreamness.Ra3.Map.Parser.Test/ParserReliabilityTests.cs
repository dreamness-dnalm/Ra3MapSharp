using System.Text;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Default;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Water;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.ClipBoard;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.RA3.Map.Parser.Core.MapScb;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Test;

[NonParallelizable]
public class ParserReliabilityTests
{
    [Test]
    public void NewMap_ShouldContainEditableCommonAssetsAndRoundTrip()
    {
        var tempDir = CreateTempDir();
        try
        {
            var map = Ra3Map.NewMap(64, 64, 8);

            Assert.Multiple(() =>
            {
                Assert.That(map.Context.AssetDict.Count, Is.EqualTo(16));
                Assert.That(map.Context.MPPositionListAsset.MPPositionInfos.Count, Is.EqualTo(6));
                Assert.That(map.Context.BuildListsAsset.BuildLists.Count, Is.EqualTo(17));
                Assert.That(map.Context.LibraryMapListsAsset.LibraryMapsList.Count, Is.EqualTo(17));
                Assert.That(map.Context.AssetDict.Values.All(asset => asset.Id > 0), Is.True);
            });

            var path = Path.Combine(tempDir, "new.map");
            map.SaveAs(path, compress: true);
            var reopened = Ra3Map.Open(path);

            Assert.Multiple(() =>
            {
                Assert.That(reopened.Errored, Is.False);
                Assert.That(reopened.Context.MPPositionListAsset.MPPositionInfos.Count, Is.EqualTo(6));
                Assert.That(reopened.Context.BuildListsAsset.BuildLists.Count, Is.EqualTo(17));
                Assert.That(reopened.Context.LibraryMapListsAsset.LibraryMapsList.Count, Is.EqualTo(17));
                Assert.That(map.MapFilePath, Is.EqualTo(Path.GetFullPath(path)));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void NewMap_SideMetadata_ShouldAlignAndPreserveRetainedEntries()
    {
        var map = Ra3Map.NewMap(32, 32, 4);
        var sides = map.Context.SideListAsset;
        var libraries = map.Context.LibraryMapListsAsset;
        var builds = map.Context.BuildListsAsset;

        var expectedFactions = new[]
        {
            "PlayerTemplate:Null",
            "PlayerTemplate:Civilian", "PlayerTemplate:Civilian", "PlayerTemplate:Civilian",
            "PlayerTemplate:Civilian", "PlayerTemplate:Civilian", "PlayerTemplate:Civilian",
            "PlayerTemplate:Civilian", "PlayerTemplate:Civilian", "PlayerTemplate:Civilian",
            "PlayerTemplate:Civilian", "PlayerTemplate:Random", "PlayerTemplate:Soviet",
            "PlayerTemplate:Allies", "PlayerTemplate:Japan", "PlayerTemplate:Null",
            "PlayerTemplate:Observer"
        };

        Assert.Multiple(() =>
        {
            Assert.That(libraries.LibraryMapsList.Count, Is.EqualTo(sides.PlayerDataList.Count));
            Assert.That(builds.BuildLists.Count, Is.EqualTo(sides.PlayerDataList.Count));
            Assert.That(builds.BuildLists.Select(x => x.Faction), Is.EqualTo(expectedFactions));
        });

        libraries.LibraryMapsList[0].Add("Libraries\\Keep\\Keep.map");
        builds.BuildLists[0].RawCount = 7;
        libraries.LibraryMapsList[2].Add("Libraries\\Shifted\\Shifted.map");
        builds.BuildLists[2].RawCount = 9;
        map.Context.RemoveSide(sides.PlayerDataList[1]);

        var added = PlayerData.Of("ExtraPlayer", map.Context);
        added.Faction = "Japan";
        map.Context.AddSide(added);

        Assert.Multiple(() =>
        {
            Assert.That(libraries.LibraryMapsList.Count, Is.EqualTo(sides.PlayerDataList.Count));
            Assert.That(builds.BuildLists.Count, Is.EqualTo(sides.PlayerDataList.Count));
            Assert.That(libraries.LibraryMapsList[0].MapNames, Does.Contain("Libraries\\Keep\\Keep.map"));
            Assert.That(builds.BuildLists[0].RawCount, Is.EqualTo(7));
            Assert.That(libraries.LibraryMapsList[1].MapNames,
                Does.Contain("Libraries\\Shifted\\Shifted.map"));
            Assert.That(builds.BuildLists[1].RawCount, Is.EqualTo(9));
            Assert.That(builds.BuildLists[builds.BuildLists.Count - 1].Faction,
                Is.EqualTo("PlayerTemplate:Japan"));
        });
    }

    [Test]
    public void AssetList_ShouldRejectCountAndPayloadLengthMismatch()
    {
        var context = new MapContext();
        var asset = new AssetListAsset
        {
            Data = new byte[] { 1, 0, 0, 0 },
            DataSize = sizeof(int)
        };

        Assert.Throws<InvalidDataException>(() => asset.Parse(context));
    }

    [Test]
    public void AssetList_DependencyEditing_ShouldDeduplicateAndRoundTrip()
    {
        var context = new MapContext();
        var source = AssetListAsset.Default(context);
        const uint typeId = 0x12345678;
        const uint instanceId = 0x9ABCDEF0;

        Assert.Multiple(() =>
        {
            Assert.That(source.AddDependency(typeId, instanceId), Is.True);
            Assert.That(source.AddDependency(typeId, instanceId), Is.False);
            Assert.That(source.ContainsDependency(typeId, instanceId), Is.True);
        });

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var reopened = (AssetListAsset)AssetParser.FromBinaryReader(reader, context);

        Assert.Multiple(() =>
        {
            Assert.That(reopened.ContainsDependency(typeId, instanceId), Is.True);
            Assert.That(reopened.RemoveDependency(typeId, instanceId), Is.True);
            Assert.That(reopened.RemoveDependency(typeId, instanceId), Is.False);
        });
    }

    [Test]
    public void Save_ShouldSynchronizeBuildMetadataAfterDirectFactionEdit()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "side-metadata.map");
            var map = Ra3Map.NewMap(32, 32, 4);
            var lastIndex = map.Context.SideListAsset.PlayerDataList.Count - 1;
            map.Context.SideListAsset.PlayerDataList[lastIndex].Faction = "Soviet";

            map.SaveAs(path, compress: true);
            var reopened = Ra3Map.Open(path);

            Assert.That(reopened.Context.BuildListsAsset.BuildLists[lastIndex].Faction,
                Is.EqualTo("PlayerTemplate:Soviet"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ObjectsList_WaypointIdsAndNames_ShouldRemainUniqueAfterReload()
    {
        var context = new MapContext();
        var list = ObjectsListAsset.Default(context);
        var first = list.AddWaypoint("first", new Vec3D(0, 0, 0), context);
        var second = list.AddWaypoint("second", new Vec3D(1, 1, 1), context);

        Assert.Multiple(() =>
        {
            Assert.That(first.Properties.GetProperty<int>("waypointID"), Is.EqualTo(0));
            Assert.That(second.Properties.GetProperty<int>("waypointID"), Is.EqualTo(1));
        });

        using var stream = new MemoryStream(list.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (ObjectsListAsset)AssetParser.FromBinaryReader(reader, context);

        Assert.Throws<System.Exception>(() => parsed.AddWaypoint("first", new Vec3D(2, 2, 2), context));
        var third = parsed.AddWaypoint("third", new Vec3D(3, 3, 3), context);
        Assert.That(third.Properties.GetProperty<int>("waypointID"), Is.EqualTo(2));
    }

    [Test]
    public void RoadObject_ShouldUseDedicatedWrapperAndPreserveRawOptions()
    {
        var context = new MapContext();
        var source = ObjectAsset.OfRoad(
            "YucatanDirtRoad01 0",
            "YucatanDirtRoad01",
            new Vec3D(10, 20, 0),
            45,
            new RoadOptions(196),
            "PlyrNeutral/teamPlyrNeutral",
            context);

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (ObjectAsset)AssetParser.FromBinaryReader(reader, context);
        var wrapped = ObjectWrap.Of(parsed);

        Assert.Multiple(() =>
        {
            Assert.That(parsed.IsRoad, Is.True);
            Assert.That(parsed.IsWaypoint, Is.False);
            Assert.That(parsed.RoadOption, Is.EqualTo(196));
            Assert.That(parsed.Properties.PropertyNames, Does.Not.Contain("objectName"));
            Assert.That(wrapped, Is.TypeOf<RoadObjectWrap>());
            Assert.That(((RoadObjectWrap)wrapped).Options.RawValue, Is.EqualTo(196));
            Assert.That(((RoadObjectWrap)wrapped).Options.ContainsBits(128), Is.True);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ((RoadObjectWrap)wrapped).Options = new RoadOptions(0));
    }

    [Test]
    public void RoadObject_ParsedOptionEdit_ShouldPropagateAndPersist()
    {
        var context = new MapContext();
        var source = ObjectsListAsset.Default(context);
        source.AddRoad(
            context,
            "YucatanDirtRoad01",
            new Vec3D(10, 20, 0),
            new RoadOptions(2));

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (ObjectsListAsset)AssetParser.FromBinaryReader(reader, context);
        var road = (RoadObjectWrap)ObjectWrap.Of(parsed.MapObjectList.Single());

        Assert.That(parsed._modified, Is.False);
        road.Options = new RoadOptions(68);
        Assert.Multiple(() =>
        {
            Assert.That(road.Obj._modified, Is.True);
            Assert.That(parsed.MapObjectList._modified, Is.True);
            Assert.That(parsed._modified, Is.True);
        });

        using var stream2 = new MemoryStream(parsed.ToBytes(context));
        using var reader2 = new BinaryReader(stream2);
        var reopened = (ObjectsListAsset)AssetParser.FromBinaryReader(reader2, context);
        Assert.That(reopened.MapObjectList.Single().RoadOption, Is.EqualTo(68));
    }

    [Test]
    public void BlendTileData_ParsedChildChanges_ShouldPropagateAndPersist()
    {
        var context = new MapContext();
        var heightMap = Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain.HeightMapDataAsset.Default(32, 32, 0, context);
        context.RegisterAsset(heightMap);
        var source = BlendTileDataAsset.Default(32, 32, 0, "Dirt_Yucatan03", context);
        source.BlendInfos.Add(BlendInfo.Create(0, BlendInfo.BlendDirectionEnum.Top));

        using var stream = new MemoryStream(source.ToBytes(context));
        using var reader = new BinaryReader(stream);
        var parsed = (BlendTileDataAsset)AssetParser.FromBinaryReader(reader, context);

        Assert.That(parsed._modified, Is.False);
        parsed.DynamicShrubberies[0, 0] = 42;
        Assert.That(parsed._modified, Is.True);

        using var blendStream = new MemoryStream(source.ToBytes(context));
        using var blendReader = new BinaryReader(blendStream);
        var parsedBlend = (BlendTileDataAsset)AssetParser.FromBinaryReader(blendReader, context);

        Assert.That(parsedBlend._modified, Is.False);
        parsedBlend.BlendInfos[0].I4 = 0x12345678U;
        Assert.That(parsedBlend._modified, Is.True);

        using var stream2 = new MemoryStream(parsed.ToBytes(context));
        using var reader2 = new BinaryReader(stream2);
        var reopened = (BlendTileDataAsset)AssetParser.FromBinaryReader(reader2, context);
        Assert.That(reopened.DynamicShrubberies[0, 0], Is.EqualTo(42));

        using var blendStream2 = new MemoryStream(parsedBlend.ToBytes(context));
        using var blendReader2 = new BinaryReader(blendStream2);
        var reopenedBlend = (BlendTileDataAsset)AssetParser.FromBinaryReader(blendReader2, context);
        Assert.That(reopenedBlend.BlendInfos[0].I4, Is.EqualTo(0x12345678U));
    }

    [Test]
    public void SaveAs_WhenSerializationFails_ShouldPreserveExistingFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var path = Path.Combine(tempDir, "existing.map");
            var original = Encoding.ASCII.GetBytes("keep this file");
            File.WriteAllBytes(path, original);

            var map = Ra3Map.NewMap(16, 16);
            var unsupported = new DefaultAsset
            {
                Id = map.Context.RegisterStringDeclare("UnsupportedModifiedAsset"),
                AssetType = "UnsupportedModifiedAsset",
                Version = 1,
                DataSize = 0,
                Data = Array.Empty<byte>()
            };
            unsupported.MarkModified();
            map.Context.RegisterAsset(unsupported);

            Assert.Throws<NotImplementedException>(() => map.SaveAs(path));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SaveAsAsync_ShouldWriteAndUpdateCurrentPath()
    {
        var tempDir = CreateTempDir();
        try
        {
            var map = Ra3Map.NewMap(16, 16);
            var path = Path.Combine(tempDir, "async.map");

            Assert.That(await map.SaveAsAsync(path, compress: false), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.True);
                Assert.That(map.MapFilePath, Is.EqualTo(Path.GetFullPath(path)));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ScbAndClipboard_ShouldRejectInvalidMagicAndReadCompressedPayload()
    {
        var invalid = new byte[8];
        Assert.Throws<InvalidDataException>(() => Ra3MapScb.FromBytes(invalid));
        Assert.Throws<InvalidDataException>(() => Ra3MapClipboard.FromBytes(invalid));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(CompressConst.UnCompressFlag);
        writer.Write(1);
        writer.Write(new string('A', 256));
        writer.Write(1);
        writer.Flush();
        Assert.That(stream.ToArray().RefPackCompress(out var compressed), Is.True);

        Assert.DoesNotThrow(() => Ra3MapScb.FromBytes(compressed));
        Assert.DoesNotThrow(() => Ra3MapClipboard.FromBytes(compressed));
    }

    [Test]
    public void RefPack_ShouldRejectInvalidOffsetsAndOversizedOutput()
    {
        Assert.Throws<InvalidDataException>(() =>
            Ra3MapScb.FromBytes(CreateRefPackContainer(1, 0x00, 0x00)));
        Assert.Throws<InvalidDataException>(() =>
            Ra3MapScb.FromBytes(CreateRefPackContainer(RefpackComrpessor.DefaultMaxOutputSize + 1)));
    }

    [Test]
    public void TruncatedAssetAndUnknownAssetStatus_ShouldBeReportedAccurately()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(CompressConst.UnCompressFlag);
        writer.Write(1);
        writer.Write("UnknownChunk");
        writer.Write(1);
        writer.Write(1);
        writer.Write((short)1);
        writer.Write(0);
        writer.Flush();

        var scb = Ra3MapScb.FromBytes(stream.ToArray());
        var unknown = scb.Context.AssetDict["UnknownChunk"];
        Assert.Multiple(() =>
        {
            Assert.That(unknown.Parsed, Is.True);
            Assert.That(unknown.IsSupported, Is.False);
            Assert.That(unknown.RawPreserved, Is.True);
        });

        var truncated = stream.ToArray();
        BitConverter.GetBytes(100).CopyTo(truncated, truncated.Length - sizeof(int));
        Assert.Throws<InvalidDataException>(() => Ra3MapScb.FromBytes(truncated));
    }

    [Test]
    public void DefaultStringEncoding_ShouldBeConfigurable()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var original = StreamExtension.DefaultStringEncoding;
        try
        {
            StreamExtension.DefaultStringEncoding = Encoding.GetEncoding(1252);
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.WriteDefaultString("café");
            writer.Flush();
            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            Assert.That(reader.ReadDefaultString(), Is.EqualTo("café"));
        }
        finally
        {
            StreamExtension.DefaultStringEncoding = original;
        }
    }

    [Test]
    public void AssetProperty_StringArray_ShouldFailAtConstructionInsteadOfSerialization()
    {
        var context = new MapContext();
        Assert.Throws<ArgumentException>(() => AssetProperty.Of("bad", new[] { "a", "b" }, context));
    }

    [Test]
    public void BlendInfo_DefaultMagic_ShouldMatchLatestWorldBuilderValue()
    {
        var info = BlendInfo.Create(0, BlendInfo.BlendDirectionEnum.Top);
        var bytes = info.ToBytes(new MapContext());
        Assert.That(BitConverter.ToUInt32(bytes, bytes.Length - sizeof(uint)), Is.EqualTo(0x7ACDCD00U));
    }

    [Test]
    public void CommonAssets_ParsedEdits_ShouldPropagateAndRoundTrip()
    {
        var tempDir = CreateTempDir();
        try
        {
            var seedPath = Path.Combine(tempDir, "seed.map");
            var outputPath = Path.Combine(tempDir, "edited.map");
            Ra3Map.NewMap(32, 32, 4).SaveAs(seedPath, compress: false);

            var map = Ra3Map.Open(seedPath);
            map.Context.HeightMapDataAsset.Version = 99;
            map.Context.GlobalWaterSettingsAsset.Version = 0;
            map.Context.MPPositionListAsset.MPPositionInfos[0].Team = 7;
            map.Context.LibraryMapListsAsset.LibraryMapsList[0].Add("TestLibraryMap");
            map.Context.BuildListsAsset.BuildLists[0].Count = 2;
            map.Context.GlobalWaterSettingsAsset.Reflection = true;
            map.Context.GlobalLightingAsset.Time = 123;
            map.Context.PostEffectsChunkAsset.PostEffects[0].Name = "DistortionEdited";
            map.Context.StandingWaterAreasAsset.StandingWaterAreas.Add(
                StandingWaterArea.Of(
                    1,
                    "TestWater",
                    0.06f,
                    new[] { new Vec2D(0, 0), new Vec2D(10, 0), new Vec2D(0, 10) },
                    200));

            Assert.Multiple(() =>
            {
                Assert.That(map.Context.MPPositionListAsset._modified, Is.True);
                Assert.That(map.Context.LibraryMapListsAsset._modified, Is.True);
                Assert.That(map.Context.BuildListsAsset._modified, Is.True);
                Assert.That(map.Context.GlobalWaterSettingsAsset._modified, Is.True);
                Assert.That(map.Context.GlobalLightingAsset._modified, Is.True);
                Assert.That(map.Context.PostEffectsChunkAsset._modified, Is.True);
                Assert.That(map.Context.StandingWaterAreasAsset._modified, Is.True);
            });

            map.SaveAs(outputPath, compress: true);
            var reopened = Ra3Map.Open(outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(reopened.Context.MPPositionListAsset.MPPositionInfos[0].Team, Is.EqualTo(7));
                Assert.That(reopened.Context.LibraryMapListsAsset.LibraryMapsList[0].MapNames,
                    Does.Contain("TestLibraryMap"));
                Assert.That(reopened.Context.BuildListsAsset.BuildLists[0].Count, Is.EqualTo(2));
                Assert.That(reopened.Context.GlobalWaterSettingsAsset.Reflection, Is.True);
                Assert.That(reopened.Context.GlobalWaterSettingsAsset.Version,
                    Is.EqualTo(reopened.Context.GlobalWaterSettingsAsset.GetVersion()));
                Assert.That(reopened.Context.HeightMapDataAsset.Version, Is.EqualTo(99));
                Assert.That(reopened.Context.GlobalLightingAsset.Time, Is.EqualTo(123));
                Assert.That(reopened.Context.PostEffectsChunkAsset.PostEffects[0].Name,
                    Is.EqualTo("DistortionEdited"));
                Assert.That(reopened.Context.StandingWaterAreasAsset.StandingWaterAreas[0].Name,
                    Is.EqualTo("TestWater"));
            });
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "Ra3MapSharp_Reliability_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] CreateRefPackContainer(int declaredSize, params byte[] commands)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(CompressConst.CompressFlag);
        writer.Write(declaredSize);
        writer.Write((byte)0x80);
        writer.Write((byte)0xFB);
        writer.Write((byte)(declaredSize >> 24));
        writer.Write((byte)(declaredSize >> 16));
        writer.Write((byte)(declaredSize >> 8));
        writer.Write((byte)declaredSize);
        writer.Write(commands);
        writer.Flush();
        return stream.ToArray();
    }
}
