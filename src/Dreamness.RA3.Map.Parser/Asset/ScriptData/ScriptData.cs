using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Parser.Asset.ScriptData;

public static class ScriptData
{
    public static readonly Dictionary<string, ScriptDeclareModel> ActionDict = 
        JsonSerializer.Deserialize<List<ScriptDeclareModel>>(
            GetEmbeddingResourceText(@"data\script_declare\ScriptAction.json"))
        .ToDictionary(item => item.CommandWord, item => item);
    
    public static readonly Dictionary<string, ScriptDeclareModel> ConditionDict = 
        JsonSerializer.Deserialize<List<ScriptDeclareModel>>(
        GetEmbeddingResourceText(@"data\script_declare\ScriptCondition.json"))
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
    
    private static string GetEmbeddingResourceText(string resourceName)
    {
        Assembly LibAssembly = typeof(Ra3Map).Assembly;
        using var stream = LibAssembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException(
                               $"Resource not found: {resourceName}\n" +
                               $"Available:\n{string.Join("\n", LibAssembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    
    
}