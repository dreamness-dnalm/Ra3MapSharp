using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.RA3.Map.Parser.Asset.ScriptData;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptAction: BaseAsset
{
    private int contentType;
    
    public int ContentType
    {
        get => contentType;
        set
        {
            if (contentType != value)
            {
                contentType = value;
                MarkModified();
            }
        }
    }

    private AssetProperty.AssetPropertyType assetPropertyType;
    
    public AssetProperty.AssetPropertyType AssetPropertyType
    {
        get => assetPropertyType;
        set
        {
            if (assetPropertyType != value)
            {
                assetPropertyType = value;
                MarkModified();
            }
        }
    }

    private string contentName;
    
    public string ContentName
    {
        get => contentName;
    }
    
    public void SetContentName(string name, BaseContext context)
    {
        var index = context.RegisterStringDeclare(name);
        NameIndex = index;
        contentName = name;
    }

    private int nameIndex;
    
    public int NameIndex
    {
        get => nameIndex;
        private set
        {
            if (nameIndex != value)
            {
                nameIndex = value;
                MarkModified();
            }
        }
    }

    private bool enabled;
    
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled != value)
            {
                enabled = value;
                MarkModified();
            }
        }
    }
    
    public ScriptDeclareModel ScriptDeclareModel => ScriptData.GetActionScriptDeclareFromCommandWord(contentName);
    
    public WritableList<ScriptArgument> Arguments { get; } = new WritableList<ScriptArgument>();
    
    public override short GetVersion()
    {
        return 3;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.ScriptAction;
    }
    
    public static ScriptAction Of(string name, List<string> arguments, BaseContext context)
    {
        var action = new ScriptAction();
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

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        contentType = binaryReader.ReadInt32();
        assetPropertyType = (AssetProperty.AssetPropertyType)binaryReader.ReadByte();
        nameIndex = binaryReader.ReadUInt24();
        contentName = context.GetDeclaredString(nameIndex);

        var argCnt = binaryReader.ReadInt32();
        
        var scriptDeclareModel = ScriptDeclareModel;
        if (scriptDeclareModel.Arguments.Count != argCnt)
        {
            throw new ArgumentException($"Script condition argument count mismatch for {contentName}: expected {scriptDeclareModel.Arguments.Count}, got {argCnt}");
        }

        if (scriptDeclareModel.EditorNumber != contentType)
        {
            throw new ArgumentException($"Script condition content type mismatch for {contentName}: expected {scriptDeclareModel.EditorNumber}, got {contentType}");
        }

        for (int i = 0; i < argCnt; i++)
        {
            Arguments.Add(ScriptArgument.FromBinaryReader(binaryReader, context, scriptDeclareModel.Arguments[i]));
        }
        ObservableUtil.Subscribe(Arguments, this);

        enabled = binaryReader.ReadInt32() == 1;
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(contentType);
        binaryWriter.Write((byte)assetPropertyType);
        binaryWriter.WriteUInt24((uint)nameIndex);

        binaryWriter.Write(Arguments.Count);
        binaryWriter.Write(Arguments.ToBytes(context));
        
        binaryWriter.Write(Enabled ? 1 : 0);
        
        binaryWriter.Flush();
        
        return memoryStream.ToArray();
    }
    
    public JsonNode ToJsonNode()
    {
        var jsonObj = new JsonObject();
        
        var argJsonArr = new JsonArray();
        var argTypesArr = new JsonArray();
        foreach (var arg in Arguments)
        {
            argJsonArr.Add(arg.ToJsonNode());
            argTypesArr.Add(arg.ArgumentModel.RealType);
        }

        jsonObj["Name"] = contentName;
        if (enabled == false)
        {
            jsonObj["Enabled"] = enabled;
        }

        if (argJsonArr.Count > 0)
        {
            jsonObj["Arguments"] = argJsonArr;
        }
        
        if (ScriptDeclareModel.ScriptDesc != "")
        {
            jsonObj["Desc_IGNORE"] = ScriptDeclareModel.ScriptDesc;
        }
        jsonObj["Name_EN_IGNORE"] = $"[{ScriptDeclareModel.EditorNumber}] {ScriptDeclareModel.ScriptName.Trim()}";
        jsonObj["Name_ZH_IGNORE"] = $"[{ScriptDeclareModel.EditorNumber}] {ScriptDeclareModel.ScriptTrans.Trim()}";
        jsonObj["ArgumentsDesc_IGNORE"] = ScriptDeclareModel.ScriptArg;
        jsonObj["ArgumentsType_IGNORE"] = argTypesArr;

        // var argJsonArr = new JsonArray();
        // foreach (var arg in Arguments)
        // {
        //     argJsonArr.Add(arg.ToJsonNode());
        // }
        // jsonObj["Argument"] = argJsonArr;
        

        return jsonObj;
    }
    
    public static ScriptAction FromJsonNode(JsonNode jsonNode, BaseContext context)
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
        var action = ScriptAction.Of(name, arguments, context);
        
        if(jsonObj.ContainsKey("Enabled"))
        {
            action.Enabled = (bool)jsonObj["Enabled"];
        }

        return action;
    }
}