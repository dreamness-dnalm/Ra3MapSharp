using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Parser.Asset.ScriptData;

public static class ScriptData
{
    public static readonly Dictionary<string, ScriptDeclareModel> ActionDict = 
        JsonSerializer.Deserialize<List<ScriptDeclareModel>>(
                File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "data", "script_declare", "ScriptAction.json")))
        .ToDictionary(item => item.CommandWord, item => item);
    
    public static readonly Dictionary<string, ScriptDeclareModel> ConditionDict = 
        JsonSerializer.Deserialize<List<ScriptDeclareModel>>(
                File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "data", "script_declare", "ScriptConditon.json")))
        .ToDictionary(item => item.CommandWord, item => item);


    public static ScriptDeclareModel GetActionScriptDeclareFromCommandWord(string commandWord)
    {
        if (ActionDict.ContainsKey(commandWord))
        {
            return ActionDict[commandWord];
        }
        else
        {
            throw new Exception($"Unknown action script command word: {commandWord}");
        }
    }
    
    public static ScriptDeclareModel GetConditionScriptDeclareFromCommandWord(string commandWord)
    {
        if (ConditionDict.ContainsKey(commandWord))
        {
            return ConditionDict[commandWord];
        }
        else
        {
            throw new Exception($"Unknown condition script command word: {commandWord}");
        }
    }
    
    
}