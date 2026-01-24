using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptActionFalse: ScriptAction
{
    public override string GetAssetType()
    {
        return AssetNameConst.ScriptActionFalse;
    }
    
    public static ScriptActionFalse Of(string name, List<string> arguments, BaseContext context)
    {
        var action = new ScriptActionFalse();
        action.SetContentName(name, context);
        action.ApplyBasicInfo(context);
        action.Enabled = true;
        

        var scriptDeclareModel = action.ScriptDeclareModel;
        
        if(scriptDeclareModel.Arguments.Count != arguments.Count)
        {
            throw new ArgumentException($"Script action argument count mismatch for {name}: expected {scriptDeclareModel.Arguments.Count}, got {arguments.Count}");
        }
        
        ObservableUtil.Subscribe(action.Arguments, action);
        
        for(int i = 0; i < arguments.Count; i++)
        {
            var argModel = scriptDeclareModel.Arguments[i];
            var argValue = arguments[i];
            var arg = ScriptArgument.Of(argModel, argValue);
            action.Arguments.Add(arg);
        }
        
        action.ContentType = scriptDeclareModel.EditorNumber;
        action.AssetPropertyType = AssetProperty.AssetPropertyType.stringType; 
        
        action.MarkModified();
        
        return action;
    }

    public static ScriptActionFalse FromJsonNode(JsonNode jsonNode, BaseContext context)
    {
        var jsonObj = jsonNode.AsObject();
        
        if(!jsonObj.ContainsKey("Name"))
        {
            throw new ArgumentException("Script action must have a Name field.");
        }
        var name = jsonObj["Name"].ToString();
        
        var arguments = new List<string>();
        
        if (jsonObj.ContainsKey("Arguments"))
        {
            var argJsonArr = (JsonArray)jsonObj["Arguments"];
            
            for (int i = 0; i < argJsonArr.Count; i++)
            {
                arguments.Add(argJsonArr[i].ToString());
            }
        }
        var action = ScriptActionFalse.Of(name, arguments, context);
        
        if(jsonObj.ContainsKey("Enabled"))
        {
            action.Enabled = (bool)jsonObj["Enabled"];
        }

        return action;
    }
}