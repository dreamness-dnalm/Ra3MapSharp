using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Property;

public class AssetProperty: Ra3MapWritable
{
    private AssetProperty(){}

    public AssetPropertyType propertyType { get; private set; }
    public int Id { get; private set; }
    public string Name { get; private set; }

    private object _value;

    public object Value
    {
        get => _value;
        set 
        {
            if (Equals(_value, value))
            {
                return;
            }
            
            _value = value;
            MarkModified();
        }
    }

    public void MarkModified()
    {
        if (_modified)
        {
            return;
        }
            
        _modified = true;
        ObservableUtil.Notify(this, new NotifyEventArgs("Modified", true));
    }
    
    
    public static AssetProperty FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var property = new AssetProperty();
        
        property.propertyType = (AssetPropertyType) binaryReader.ReadByte();
        property.Id = binaryReader.ReadUInt24();
        
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        binaryWriter.Write((byte)property.propertyType);
        binaryWriter.WriteUInt24((uint)property.Id);
        
        
        property.Name = context.GetDeclaredString(property.Id);
        switch (property.propertyType)
        {
            case AssetPropertyType.boolType:
                var v1 = binaryReader.ReadBoolean();
                property._value = v1;
                binaryWriter.Write(v1);
                break;
            case AssetPropertyType.intType:
                var v2 = binaryReader.ReadInt32();
                property._value = v2;
                binaryWriter.Write(v2);
                break;
            case AssetPropertyType.floatType:
                var v3 = binaryReader.ReadSingle();
                property._value = v3;
                binaryWriter.Write(v3);
                break;
            case AssetPropertyType.stringType:
                var v4 = binaryReader.ReadDefaultString();
                property._value = v4;
                binaryWriter.WriteDefaultString(v4);
                break;
            case AssetPropertyType.stringUnicodeType:
                var v5 = binaryReader.ReadUnicodeString();
                property._value = v5;
                binaryWriter.WriteUnicodeString(v5);
                break;
            case AssetPropertyType.stringNameValueType:
                var v6 = binaryReader.ReadDefaultString();
                property._value = v6;
                binaryWriter.WriteDefaultString(v6);
                break;
            default:
                throw new System.Exception("unknown type:" + property.propertyType);
                break;
        }

        binaryWriter.Flush();
        property.Data = memoryStream.ToArray();

        return property;
    }

    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write((byte)propertyType);
            binaryWriter.WriteUInt24((uint)Id);
            switch (propertyType)
            {
                case AssetPropertyType.boolType:
                    binaryWriter.Write((bool)Value);
                    break;
                case AssetPropertyType.intType:
                    binaryWriter.Write((int)Value);
                    break;
                case AssetPropertyType.floatType:
                    binaryWriter.Write((float)Value);
                    break;
                case AssetPropertyType.stringType:
                    binaryWriter.WriteDefaultString((string)Value);
                    break;
                case AssetPropertyType.stringUnicodeType:
                    binaryWriter.WriteUnicodeString((string)Value);
                    break;
                case AssetPropertyType.stringNameValueType:
                    binaryWriter.WriteDefaultString((string)Value);
                    break;
                default:
                    throw new System.Exception("unknown type:" + propertyType);
            }
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }

    public static AssetProperty Of(string name, AssetPropertyType propertyType, object value, BaseContext context)
    {
        var assetProperty = new AssetProperty();
        assetProperty.Value = value;
        assetProperty.propertyType = propertyType;
        assetProperty.Name = name;
        assetProperty.Id = context.RegisterStringDeclare(name);
        assetProperty.MarkModified();
        return assetProperty;
    }
    
    public static AssetProperty Of(string name, object data, BaseContext context)
    {
        var assetProperty = new AssetProperty();
        assetProperty.Value = data;
        assetProperty.Name = name;

        if (data is bool)
        {
            assetProperty.propertyType = AssetPropertyType.boolType;
        }
        else if (data is int)
        {
            assetProperty.propertyType = AssetPropertyType.intType;
        }
        else if (data is float)
        {
            assetProperty.propertyType = AssetPropertyType.floatType;
        }
        else if (data is string)
        {
            if (name == "playerDisplayName")
            {
                assetProperty.propertyType = AssetPropertyType.stringUnicodeType;
            }
            else
            {
                assetProperty.propertyType = AssetPropertyType.stringType;
            }
        }
        else if (data is string[])
        {
            assetProperty.propertyType = AssetPropertyType.stringNameValueType;
        }
        else
        {
            assetProperty.propertyType = AssetPropertyType.intType;
        }

        assetProperty.Id = context.RegisterStringDeclare(name);
        
        assetProperty.MarkModified();

        return assetProperty;
    }
    
    public enum AssetPropertyType
    {
        boolType = 0,
        intType = 1,
        floatType = 2,
        stringType = 3,
        stringUnicodeType = 4,
        stringNameValueType = 5,
        unknownType = byte.MaxValue
    }
    public override string ToString()
    {
        return $"{{{Name}:{propertyType}={Value}}}";
    }
}