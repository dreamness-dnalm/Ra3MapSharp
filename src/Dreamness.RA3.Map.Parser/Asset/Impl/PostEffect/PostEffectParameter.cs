using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Asset.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;

public class PostEffectParameter: Ra3MapWritable
{
    private object value;
    public object Value { get => value is float[] floats ? (float[])floats.Clone() : value; private set => this.value = value; }
    
    public string Name { get; private set; }
    
    public string Type { get; private set; }

    private PostEffectParameter(string name, string type, object value)
    {
        Value = value;
        Name = name;
        Type = type;
    }

    public static PostEffectParameter Of(string name, string type, object value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ValidateValue(type, value);
        if (value is float[] floats) value = (float[])floats.Clone();
        var postEffectParameter = new PostEffectParameter(name, type, value);
        postEffectParameter.MarkModified();
        return postEffectParameter;
    }
    
    public static PostEffectParameter FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var name = AssetReadUtility.ReadString(binaryReader);
        binaryWriter.WriteDefaultString(name);
        var type = AssetReadUtility.ReadString(binaryReader);
        binaryWriter.WriteDefaultString(type);
        object value = null;
        
        switch (type)
        {
            case "Float":
                var v1 = binaryReader.ReadSingle();
                value = v1;
                binaryWriter.Write(v1);
                break;
            case "Float4":
                value = new float[4];
                for (var i = 0; i < 4; i++)
                {
                    var ele = binaryReader.ReadSingle();
                    binaryWriter.Write(ele);
                    ((float[])value)[i] = ele;
                }
                break;
            case "Texture":
                var texture = AssetReadUtility.ReadString(binaryReader);
                binaryWriter.WriteDefaultString(texture);
                value = texture;
                break;
            case "Int":
                var vI = binaryReader.ReadInt32();
                binaryWriter.Write(vI);
                value = vI;
                break;
            default:
                throw new InvalidDataException("Unknown post-effect type: " + type);
        }

        var postEffectParameter = new PostEffectParameter(name, type, value);
        
        binaryWriter.Flush();
        postEffectParameter.Data = memoryStream.ToArray();
        
        return postEffectParameter;
    }

    /// <summary>Updates a typed value and propagates the modification to its effect/map.</summary>
    public void SetValue(object value)
    {
        ValidateValue(Type, value);
        Value = value is float[] floats ? (float[])floats.Clone() : value;
        MarkModified();
    }

    private static void ValidateValue(string type, object value)
    {
        var valid = type switch
        {
            "Float" => value is float,
            "Float4" => value is float[] floats && floats.Length == 4,
            "Texture" => value is string,
            "Int" => value is int,
            _ => false
        };
        if (!valid) throw new ArgumentException($"Invalid value or unsupported post-effect type '{type}'.", nameof(value));
    }
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            AssetReadUtility.WriteString(binaryWriter, Name);
            AssetReadUtility.WriteString(binaryWriter, Type);

            switch (Type)
            {
                case "Float":
                    binaryWriter.Write((float)Value);
                    break;
                case "Float4":
                    if (Value is float[] floatArray)
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            binaryWriter.Write(floatArray[i]);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("Invalid data for Float4 type.");
                    }
                    break;
                case "Texture":
                    AssetReadUtility.WriteString(binaryWriter, (string)Value);
                    break;
                case "Int":
                    binaryWriter.Write((int)Value);
                    break;
                default:
                    throw new InvalidDataException("Unknown post-effect type: " + Type);
            }
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
}
