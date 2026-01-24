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
    
    public static ScriptGroup Empty(string name, bool isActive, bool isSubroutine, BaseContext context)
    {
        var asset = new ScriptGroup();
        asset.Name = name;
        asset.IsActive = isActive;
        asset.IsSubroutine = isSubroutine;
        asset.ApplyBasicInfo(context); // TODO: check if required
        
        ObservableUtil.Subscribe(asset.Scripts, asset);
        ObservableUtil.Subscribe(asset.ScriptGroups, asset);
        
        asset.MarkModified();
        return asset;
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
            if (asset.Errored)
            {
                throw asset.ErrorException;
            }
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

        if (IsActive == false)
        {
            jsonObj["IsActive"] = IsActive;
        }

        if (IsSubroutine)
        {
            jsonObj["IsSubroutine"] = IsSubroutine;
        }
        
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
    
    public void Add(Object o)
    {
        if (o is Script script)
        {
            Scripts.Add(script);
        }else if (o is ScriptGroup scriptGroup)
        {
            ScriptGroups.Add(scriptGroup);
        }
        else
        {
            throw new InvalidDataException();
        }
    }
    
    
    
    public static ScriptGroup FromJsonNode(JsonNode node, BaseContext context)
    {
        var jsonObj = node as JsonObject;
        if (!jsonObj.ContainsKey("Name"))
        {
            throw new InvalidDataException("Missing Name in ScriptGroup FromJsonNode");
        }
        var name = (string)jsonObj["Name"];
        var isActive = jsonObj.ContainsKey("IsActive")? (bool)jsonObj["IsActive"]: true;
        var isSubroutine = jsonObj.ContainsKey("IsSubroutine")? (bool)jsonObj["IsSubroutine"]: false;
        
        var scriptGroup = Empty(name, isActive, isSubroutine, context);

        if (jsonObj.ContainsKey("Content"))
        {
            var contentArr = jsonObj["Content"] as JsonArray;

            foreach (var item in contentArr)
            {
                var o = item as JsonObject;
                var t = (string)o["Type"];
                switch (t)
                {
                    case "Script":
                        scriptGroup.Add(Script.FromJsonNode(o, context));
                        break;
                    case "Folder":
                        scriptGroup.Add(ScriptGroup.FromJsonNode(o, context));
                        break;
                    default:
                        throw new InvalidDataException($"Unexpected Type in ScriptList FromJsonNode: {t}");
                }
            }
        }

        return scriptGroup;
    }
}