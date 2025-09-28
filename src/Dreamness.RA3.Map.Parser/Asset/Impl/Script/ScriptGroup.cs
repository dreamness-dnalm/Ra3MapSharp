using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptGroup: BaseAsset
{
    private string name;

    public string Name
    {
        get { return name; }
        set
        {
            if (name != value)
            {
                name = value;
                MarkModified();
            }
        }
    }

    private bool isActive;
    
    public bool IsActive
    {
        get { return isActive; }
        set
        {
            if (isActive != value)
            {
                isActive = value;
                MarkModified();
            }
        }
    }

    private bool isSubroutine;
    
    public bool IsSubroutine
    {
        get { return isSubroutine; }
        set
        {
            if (isSubroutine != value)
            {
                isSubroutine = value;
                MarkModified();
            }
        }
    }
    
    public WritableList<Script> Scripts { get; set; } = new WritableList<Script>();
    
    public WritableList<ScriptGroup> ScriptGroups { get; set; } = new WritableList<ScriptGroup>();
    
    public override short GetVersion()
    {
        return 3;
    }

    public override string GetAssetType()
    {
        return Name;
        // return AssetNameConst.ScriptGroup;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        name = binaryReader.ReadDefaultString();
        isActive = binaryReader.ReadBoolean();
        isSubroutine = binaryReader.ReadBoolean();
        
        while (binaryReader.BaseStream.Position < DataSize)
        {
            var asset = AssetParser.FromBinaryReader(binaryReader, context);
            if (asset is Script script)
            {
                Scripts.Add(script, ignoreModified: true);
            }
            else if (asset is ScriptGroup scriptGroup)
            {
                ScriptGroups.Add(scriptGroup, ignoreModified: true);
            }
            else
            {
                throw new InvalidDataException(
                    $"Unexpected asset type in ScriptList: {asset.GetType().Name}. Expected Script or ScriptGroup.");
            }
        }
        
    }

    protected override byte[] Deparse(BaseContext context)
    {
        // Id = context.RegisterStringDeclare(GetName());
        
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.WriteDefaultString(Name);
        binaryWriter.Write(IsActive);
        binaryWriter.Write(IsSubroutine);
        binaryWriter.Write(Scripts.ToBytes(context));
        binaryWriter.Write(ScriptGroups.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    public JsonNode ToJsonNode()
    {
        var jsonObj = new JsonObject();
        
        jsonObj["Type"] = "Folder";
        jsonObj["Name"] = Name;
        jsonObj["IsActive"] = IsActive;
        jsonObj["IsSubroutine"] = IsSubroutine;
        
        var content = new JsonArray();
        foreach (var o in ScriptGroups)
        {
            content.Add(o.ToJsonNode());
        }

        foreach (var o in Scripts)
        {
            content.Add(o.ToJsonNode());
        }
        jsonObj["Content"] = content;
        
        return jsonObj;
    }
    
    public static ScriptGroup FromJsonNode(JsonNode node)
    {
        throw new NotImplementedException();
    }
}