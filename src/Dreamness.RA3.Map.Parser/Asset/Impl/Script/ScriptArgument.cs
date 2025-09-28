using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptArgument: Ra3MapWritable
{
    private int argumentType = 0;
    
    public int ArgumentType
    {
        get => argumentType;
        set
        {
            if (argumentType != value)
            {
                argumentType = value;
                MarkModified();
            }
        }
    }

    private Vec3D position = new Vec3D(0f, 0f, 0f);
    
    public Vec3D Position
    {
        get => position;
        set
        {
            if (position != value)
            {
                ObservableUtil.Unsubscribe(position, this);
                position = value;
                ObservableUtil.Subscribe(position, this);
                MarkModified();
            }
        }
    }
    
    
    
    private int intValue = 0;
    
    public int IntValue
    {
        get => intValue;
        set
        {
            if (intValue != value)
            {
                intValue = value;
                MarkModified();
            }
        }
    }

    private float floatValue = 0f;
    
    public float FloatValue
    {
        get => floatValue;
        set
        {
            if (floatValue != value)
            {
                floatValue = value;
                MarkModified();
            }
        }
    }
    
    private string stringValue = string.Empty;
    
    public string StringValue
    {
        get => stringValue;
        set
        {
            if (stringValue != value)
            {
                stringValue = value;
                MarkModified();
            }
        }
    }

    public static ScriptArgument FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var asset = new ScriptArgument();
        
        asset.ArgumentType = (int)binaryReader.ReadUInt32();
        binaryWriter.Write(asset.ArgumentType);
        
        if (asset.ArgumentType == 16)
        {
            asset.position = binaryReader.ReadVec3D();
        }
        else
        {
            asset.intValue = binaryReader.ReadInt32();
            asset.FloatValue = binaryReader.ReadSingle();
            asset.StringValue = binaryReader.ReadDefaultString();
        }
        ObservableUtil.Subscribe(asset.position, asset);
        
        binaryWriter.Flush();
        asset.Data = memoryStream.ToArray();
        return asset;
    }
    
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.Write((uint)ArgumentType);
            
            if (ArgumentType == 16)
            {
                binaryWriter.WriteVec3D(Position, context);
            }
            else
            {
                binaryWriter.Write(IntValue);
                binaryWriter.Write(FloatValue);
                binaryWriter.WriteDefaultString(StringValue);
            }
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }

    public JsonNode ToJsonNode()
    {
        throw new NotImplementedException();

        var jsonObj = new JsonObject();
        jsonObj["Type"] = "????";
        jsonObj["Value"] = "????";


        return jsonObj;
    }
}