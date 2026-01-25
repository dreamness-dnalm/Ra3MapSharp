using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.RA3.Map.Parser.Asset.ScriptData;
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
    
    public ArgumentModel ArgumentModel { get; private set; }
    
    public ScriptArgument(ArgumentModel argumentModel)
    {
        if (argumentModel == null)
        {
            throw new ArgumentNullException(nameof(argumentModel));
        }
        ArgumentModel = argumentModel;
    }

    public static ScriptArgument Of(ArgumentModel argumentModel, string value)
    {
        var scriptArgument = new ScriptArgument(argumentModel);
        scriptArgument.ArgumentType = argumentModel.TypeNumber;
        switch (argumentModel.RealType)
        {
            case "String":
                scriptArgument.StringValue = value;
                break;
            case "Int32":
                scriptArgument.IntValue = int.Parse(value);
                break;
            case "Double":
                scriptArgument.FloatValue = float.Parse(value);
                break;
            case "Vec3D":
                var parts = value.Split(',');
                if (parts.Length != 3)
                {
                    throw new FormatException($"Invalid Vec3D format: {value}");
                }
                var x = float.Parse(parts[0]);
                var y = float.Parse(parts[1]);
                var z = float.Parse(parts[2]);
                scriptArgument.Position = new Vec3D(x, y, z);
                break;
            default:
                throw new InvalidDataException($"Unexpected RealType in ScriptArgument: {argumentModel.RealType}");
        }
        scriptArgument.MarkModified();

        return scriptArgument;
    }

    public static ScriptArgument FromBinaryReader(BinaryReader binaryReader, BaseContext context, ArgumentModel argumentModel)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var asset = new ScriptArgument(argumentModel);
        
        asset.ArgumentType = (int)binaryReader.ReadUInt32();
        binaryWriter.Write(asset.ArgumentType);
        
        // Console.WriteLine($"typeNumber: {asset.ArgumentType}");

        if (asset.argumentType != argumentModel.TypeNumber)
        {
            throw new InvalidDataException($"ArgumentType mismatch in ScriptArgument: expected {argumentModel.TypeNumber}, got {asset.argumentType}");
        }
        
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

    public string ToJsonNode()
    {
        // throw new NotImplementedException();

        // var jsonObj = new JsonObject();
        // jsonObj["Type"] = "????";
        // jsonObj["Value"] = "????";
        // public static string INT = "Int32";
        // public static string FLOAT = "Double";
        // public static string STRING = "String";
        // public static string POSITION = "Vec3D";
        
        // todo argumentType?
        
        switch (ArgumentModel.RealType)
        {
            case "String":
                return StringValue;
            case "Int32":
                return IntValue.ToString();
            case "Double":
                return FloatValue.ToString();
            case "Vec3D":
                return $"{Position.X},{Position.Y},{Position.Z}";
            default:
                throw new InvalidDataException($"Unexpected RealType in ScriptArgument: {ArgumentModel.RealType}");
            
            
            // case 2:
            //     return IntValue.ToString();
            // case 4:
            //     return FloatValue.ToString();
            // case 8:
            //     return StringValue;
            // case 16:
            //     return $"{Position.X},{Position.Y},{Position.Z}";
            // default:
            //     return IntValue.ToString();
                // throw new InvalidDataException($"Unexpected ArgumentType in ScriptArgument: {ArgumentType}");
        }
    }
}