using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;

public class PostEffectParameter: Ra3MapWritable
{
    public object Value { get; private set; }
    
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
        var postEffectParameter = new PostEffectParameter(name, type, value);
        postEffectParameter.MarkModified();
        return postEffectParameter;
    }
    
    public static PostEffectParameter FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var name = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(name);
        var type = binaryReader.ReadDefaultString();
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
                var texture = binaryReader.ReadDefaultString();
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
        postEffectParameter.Value = memoryStream.ToArray();
        
        return postEffectParameter;
    }
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.WriteDefaultString(Name);
            binaryWriter.WriteDefaultString(Type);

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
                    binaryWriter.WriteDefaultString((string)Value);
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