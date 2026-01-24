using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.RA3.Map.Parser.Asset.ScriptData;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptConditionContent: BaseAsset
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
    
    private bool isInverted;
    
    public bool IsInverted
    {
        get => isInverted;
        set
        {
            if (isInverted != value)
            {
                isInverted = value;
                MarkModified();
            }
        }
    }
    
    public ScriptDeclareModel ScriptDeclareModel => ScriptData.GetConditionScriptDeclareFromCommandWord(contentName);
    
    public WritableList<ScriptArgument> Arguments { get; } = new WritableList<ScriptArgument>();
    
    public static ScriptConditionContent Of(string name, List<string> arguments, BaseContext context)
    {
        var asset = new ScriptConditionContent();
        asset.SetContentName(name, context);
        asset.ApplyBasicInfo(context);
        asset.ContentType = asset.ScriptDeclareModel.EditorNumber;
        asset.Enabled = true;
        asset.IsInverted = false;
        asset.AssetPropertyType = AssetProperty.AssetPropertyType.stringType;
        
        ObservableUtil.Subscribe(asset.Arguments, asset);
        
        if(arguments.Count != asset.ScriptDeclareModel.Arguments.Count)
        {
            throw new ArgumentException($"Script condition argument count mismatch for {name}: expected {asset.ScriptDeclareModel.Arguments.Count}, got {arguments.Count}");
        }
        
        for (int i = 0; i < arguments.Count; i++)
        {
            var arg = ScriptArgument.Of(asset.ScriptDeclareModel.Arguments[i], arguments[i]);
            asset.Arguments.Add(arg);
        }
        
        
        
        asset.MarkModified();
        
        return asset;
    }
    
    
    
    public override short GetVersion()
    {
        return 0;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.ScriptConditionContent;
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
        
        isInverted = binaryReader.ReadInt32() == 1;
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
        binaryWriter.Write(IsInverted ? 1 : 0);
        
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

        if (isInverted)
        {
            jsonObj["IsInverted"] = isInverted;
        }

        if (argTypesArr.Count > 0)
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
        
        return jsonObj;
    }

    public static ScriptConditionContent FromJsonNode(JsonNode jsonNode, BaseContext context)
    {
        var jsonObj = (JsonObject)jsonNode;

        if (!jsonObj.ContainsKey("Name"))
        {
            throw new ArgumentException("Script condition content must have a Name field.");
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
        
        var scriptConditionContent = ScriptConditionContent.Of(name, arguments, context);
        
        if (jsonObj.ContainsKey("Enabled"))
        {
            scriptConditionContent.Enabled = (bool)jsonObj["Enabled"];
        }

        if (jsonObj.ContainsKey("IsInverted"))
        {
            scriptConditionContent.IsInverted = (bool)jsonObj["IsInverted"];
        }
        
        scriptConditionContent.MarkModified();
        
        return scriptConditionContent;
        
    }
    
}