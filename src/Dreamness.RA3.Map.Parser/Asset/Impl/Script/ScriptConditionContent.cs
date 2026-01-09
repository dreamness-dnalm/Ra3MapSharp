using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
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
    
    public WritableList<ScriptArgument> Arguments { get; } = new WritableList<ScriptArgument>();
    
    
    
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

        for (int i = 0; i < argCnt; i++)
        {
            Arguments.Add(ScriptArgument.FromBinaryReader(binaryReader, context));
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
        // throw new NotImplementedException();
        
        var jsonObj = new JsonObject();
        
        
        jsonObj["contentType"] = contentType; // todo remove ???????
        jsonObj["Name"] = contentName;
        jsonObj["enabled"] = enabled;
        jsonObj["isInverted"] = isInverted;

        var argJsonArr = new JsonArray();
        foreach (var arg in Arguments)
        {
            argJsonArr.Add(arg.ToJsonNode());
        }
        jsonObj["Argument"] = argJsonArr;
        
        return jsonObj;
    }
}