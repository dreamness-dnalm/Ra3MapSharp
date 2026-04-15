using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class ObserverPropagationTests
{
    [Test]
    public void TeamAsset_FromBinaryReader_Change_ShouldPropagateModifiedToParentList()
    {
        var context = new MapContext();
        var seedProperties = AssetProperties.FromDict(
            new Dictionary<string, object>
            {
                ["teamName"] = "teamPlayer_1",
                ["teamOwner"] = "Player_1",
                ["teamIsSingleton"] = true
            },
            context
        );

        var serializedProperties = seedProperties.ToBytes(context);

        using var stream = new MemoryStream(serializedProperties);
        using var reader = new BinaryReader(stream);

        var team = TeamAsset.FromBinaryReader(reader, context);
        var teams = new WritableList<TeamAsset>();
        teams.Add(team, ignoreModified: true);

        Assert.That(team._modified, Is.False);
        Assert.That(teams._modified, Is.False);

        team.Name = "teamPlayer_1_modified";

        Assert.That(team._modified, Is.True);
        Assert.That(teams._modified, Is.True);
    }

    [Test]
    public void UnitObjectWrap_FromBinaryReader_Change_ShouldPropagateModifiedToParentList()
    {
        var context = new MapContext();
        var seed = ObjectAsset.OfObj(
            uniqueId: "test_obj_1",
            typeName: "AlliedPowerPlant",
            position: new Vec3D(100f, 200f, 0f),
            angle: 0f,
            name: "obj1",
            belongToTeam: "PlyrNeutral/teamPlyrNeutral",
            context: context
        );

        var serialized = seed.ToBytes(context);
        using var stream = new MemoryStream(serialized);
        using var reader = new BinaryReader(stream);

        var parsed = (ObjectAsset)AssetParser.FromBinaryReader(reader, context);
        var wrap = new UnitObjectWrap(parsed);
        var objects = new WritableList<ObjectAsset>();
        objects.Add(parsed, ignoreModified: true);

        Assert.That(parsed._modified, Is.False);
        Assert.That(objects._modified, Is.False);

        wrap.BelongToTeam = "Player_1/teamPlayer_1";

        Assert.That(parsed._modified, Is.True);
        Assert.That(objects._modified, Is.True);
    }

    [Test]
    public void ObjectsListAsset_AddSaveReload_ModifyExistingUnitObject_ShouldPropagateModifiedToAsset()
    {
        var context = new MapContext();
        var seedList = ObjectsListAsset.Default(context);
        seedList.AddObj(
            context: context,
            typeName: "AlliedPowerPlant",
            position: new Vec3D(500f, 600f, 0f),
            angle: 0f,
            belongToTeam: "PlyrNeutral/teamPlyrNeutral",
            objName: "existing_obj"
        );
        
        // add unit should mark list/asset dirty
        Assert.That(seedList.MapObjectList._modified, Is.True);
        Assert.That(seedList._modified, Is.True);

        // serialize/deserialize here is equivalent to save + reopen
        var serialized = seedList.ToBytes(context);
        using var stream = new MemoryStream(serialized);
        using var reader = new BinaryReader(stream);

        var parsedList = (ObjectsListAsset)AssetParser.FromBinaryReader(reader, context);
        var existing = parsedList.MapObjectList.First(o => !o.IsWaypoint);
        var wrap = new UnitObjectWrap(existing);

        Assert.That(existing._modified, Is.False);
        Assert.That(parsedList.MapObjectList._modified, Is.False);
        Assert.That(parsedList._modified, Is.False);

        wrap.BelongToTeam = "Player_2/teamPlayer_2";

        Assert.That(existing._modified, Is.True);
        Assert.That(parsedList.MapObjectList._modified, Is.True);
        Assert.That(parsedList._modified, Is.True);
    }
}
