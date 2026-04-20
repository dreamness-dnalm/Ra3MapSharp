using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Script;
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

    [Test]
    public void ScriptList_FromBinaryReader_AddScript_ShouldPropagateModifiedToPlayerScriptsList()
    {
        var context = new MapContext();
        var seed = PlayerScriptsList.Default(context);
        var parsed = RoundTripPlayerScriptsList(seed, context);

        var parsedScriptList = parsed.ScriptLists[0];

        Assert.That(parsedScriptList._modified, Is.False);
        Assert.That(parsed.ScriptLists._modified, Is.False);
        Assert.That(parsed._modified, Is.False);

        parsedScriptList.Add(Script.Default("new_script_after_parse", context));

        Assert.That(parsedScriptList._modified, Is.True);
        Assert.That(parsed.ScriptLists._modified, Is.True);
        Assert.That(parsed._modified, Is.True);
    }

    [Test]
    public void PlayerScriptsList_AddSaveReload_ModifyExistingScript_ShouldPropagateModified()
    {
        var context = new MapContext();
        var seed = PlayerScriptsList.Default(context);
        seed.ScriptLists[0].Add(Script.Default("existing_script", context));

        var parsed = RoundTripPlayerScriptsList(seed, context);
        var existingScript = parsed.ScriptLists[0].Scripts[0];

        Assert.That(existingScript._modified, Is.False);
        Assert.That(parsed.ScriptLists[0]._modified, Is.False);
        Assert.That(parsed.ScriptLists._modified, Is.False);
        Assert.That(parsed._modified, Is.False);

        existingScript.Name = "existing_script_modified";

        Assert.That(existingScript._modified, Is.True);
        Assert.That(parsed.ScriptLists[0]._modified, Is.True);
        Assert.That(parsed.ScriptLists._modified, Is.True);
        Assert.That(parsed._modified, Is.True);
    }

    [Test]
    public void ScriptGroup_FromBinaryReader_AddScript_ShouldPropagateModifiedToPlayerScriptsList()
    {
        var context = new MapContext();
        var seed = PlayerScriptsList.Default(context);
        seed.ScriptLists[0].Add(ScriptGroup.Empty("group_1", true, false, context));

        var parsed = RoundTripPlayerScriptsList(seed, context);
        var parsedScriptList = parsed.ScriptLists[0];
        var parsedGroup = parsedScriptList.ScriptGroups[0];

        Assert.That(parsedGroup._modified, Is.False);
        Assert.That(parsedScriptList._modified, Is.False);
        Assert.That(parsed.ScriptLists._modified, Is.False);
        Assert.That(parsed._modified, Is.False);

        parsedGroup.Add(Script.Default("group_child_script", context));

        Assert.That(parsedGroup._modified, Is.True);
        Assert.That(parsedScriptList._modified, Is.True);
        Assert.That(parsed.ScriptLists._modified, Is.True);
        Assert.That(parsed._modified, Is.True);
    }

    [Test]
    public void ScriptArgument_FromBinaryReader_ModifyActionAndConditionArgument_ShouldPropagateModified()
    {
        var context = new MapContext();
        var seed = BuildSeedPlayerScriptsWithArguments(context);

        var parsedForAction = RoundTripPlayerScriptsList(seed, context);
        var parsedActionScript = parsedForAction.ScriptLists[0].Scripts[0];
        var actionArg = parsedActionScript.ScriptActionOnTrue[0].Arguments[0];

        Assert.That(actionArg._modified, Is.False);
        Assert.That(parsedActionScript._modified, Is.False);
        Assert.That(parsedForAction.ScriptLists[0]._modified, Is.False);
        Assert.That(parsedForAction.ScriptLists._modified, Is.False);
        Assert.That(parsedForAction._modified, Is.False);

        actionArg.StringValue = "changed_action_argument";

        Assert.That(actionArg._modified, Is.True);
        Assert.That(parsedActionScript._modified, Is.True);
        Assert.That(parsedForAction.ScriptLists[0]._modified, Is.True);
        Assert.That(parsedForAction.ScriptLists._modified, Is.True);
        Assert.That(parsedForAction._modified, Is.True);

        var parsedForCondition = RoundTripPlayerScriptsList(seed, context);
        var parsedConditionScript = parsedForCondition.ScriptLists[0].Scripts[0];
        var conditionArg = parsedConditionScript.ScriptOrConditions[0].Conditions[0].Arguments[0];

        Assert.That(conditionArg._modified, Is.False);
        Assert.That(parsedConditionScript._modified, Is.False);
        Assert.That(parsedForCondition.ScriptLists[0]._modified, Is.False);
        Assert.That(parsedForCondition.ScriptLists._modified, Is.False);
        Assert.That(parsedForCondition._modified, Is.False);

        conditionArg.StringValue = "changed_condition_argument";

        Assert.That(conditionArg._modified, Is.True);
        Assert.That(parsedConditionScript._modified, Is.True);
        Assert.That(parsedForCondition.ScriptLists[0]._modified, Is.True);
        Assert.That(parsedForCondition.ScriptLists._modified, Is.True);
        Assert.That(parsedForCondition._modified, Is.True);
    }

    private static PlayerScriptsList RoundTripPlayerScriptsList(PlayerScriptsList source, MapContext context)
    {
        var serialized = source.ToBytes(context);
        using var stream = new MemoryStream(serialized);
        using var reader = new BinaryReader(stream);
        return (PlayerScriptsList)AssetParser.FromBinaryReader(reader, context);
    }

    private static PlayerScriptsList BuildSeedPlayerScriptsWithArguments(MapContext context)
    {
        var seed = PlayerScriptsList.Default(context);
        var script = Script.Default("script_with_arguments", context);

        var orCondition = OrCondition.Empty(context);
        var condition = ScriptConditionContent.Of("HAS_FINISHED_AUDIO", new List<string> { "sound_a" }, context);
        orCondition.Conditions.Add(condition);
        script.ScriptOrConditions.Add(orCondition);

        var action = ScriptAction.Of("DEBUG_STRING", new List<string> { "msg_a" }, context);
        script.ScriptActionOnTrue.Add(action);

        seed.ScriptLists[0].Add(script);
        return seed;
    }
}
