using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Property;

public class AssetProperties: Ra3MapWritable
{
    private AssetProperties(BaseContext context)
    {
        this.context = context;
    }
    [JsonIgnore]
    private BaseContext context;

    public Dictionary<string, AssetProperty> PropertiesDict { get; private set; } = new Dictionary<string, AssetProperty>();

    public int PropertyCount => PropertiesDict.Count;
    
    public string[] PropertyNames => PropertiesDict.Keys.ToArray();
    
    public void AddProperty(AssetProperty property)
    {
        PropertiesDict.Add(property.Name, property);
        ObservableUtil.Subscribe(property, this);
        MarkModified();
    }
    
    public void PutProperty(AssetProperty property)
    {
        if (PropertiesDict.ContainsKey(property.Name))
        {
            ObservableUtil.Unsubscribe(PropertiesDict[property.Name], this);
            PropertiesDict[property.Name] = property;
            ObservableUtil.Subscribe(property, this);
            MarkModified();
        }
        else
        {
            AddProperty(property);
        }
    }

    public void RemoveProperty(string name)
    {
        if (PropertiesDict.ContainsKey(name))
        {
            ObservableUtil.Unsubscribe(PropertiesDict[name], this);
            PropertiesDict.Remove(name);
            MarkModified();
        }
    }
    
    public void SetProperty(string name, object value)
    {
        if (PropertiesDict.ContainsKey(name))
        {
            if (!PropertiesDict[name].Equals(value))
            {
                PropertiesDict[name].Value = value;
                MarkModified();
            }
        }
        else
        {
            throw new KeyNotFoundException($"Property '{name}' not found.");
        }
    }

    public void PutProperty(string name, object value)
    {
        if (PropertiesDict.ContainsKey(name))
        {
            SetProperty(name, value);
        }
        else
        {
            AddProperty(AssetProperty.Of(name, value, context));
        }
    }
    
    public void PutPropertyOrRemove(string name, object value)
    {
        if (value == null)
        {
            RemoveProperty(name);
        }
        else
        {
            if (PropertiesDict.ContainsKey(name))
            {
                SetProperty(name, value);
            }
            else
            {
                AddProperty(AssetProperty.Of(name, value, context));
            }
        }

    }
    
    public void PutProperty(string name, AssetProperty.AssetPropertyType type, object value)
    {
        if (PropertiesDict.ContainsKey(name))
        {
            SetProperty(name, value);
        }
        else
        {
            AddProperty(AssetProperty.Of(name, type, value, context));
        }
    }

    public T GetProperty<T>(string name) 
    {
        object? ret = null;
        if (PropertiesDict.ContainsKey(name))
        {
            ret = PropertiesDict[name].Value;
        }

        if (ret != null)
        {
            return (T)ret;
        }
        else
        {
            return default;
        }
    }
    
    public T GetProperty<T>(string name, T defaultValue) 
    {
        object? ret = null;
        if (PropertiesDict.ContainsKey(name))
        {
            ret = PropertiesDict[name].Value;
        }

        if (ret != null)
        {
            return (T)ret;
        }
        else
        {
            return defaultValue;
        }
    }
    
    
    public static AssetProperties FromBytes(byte[] bytes, BaseContext context)
    {
        var properties = new AssetProperties(context);
        
        properties.Data = bytes;
        
        using var memoryStream = new MemoryStream(bytes);
        using var binaryReader = new BinaryReader(memoryStream);

        var cnt = binaryReader.ReadInt16();
        // properties.Data = binaryReader.ReadBytes(bytes.Length - 2);

        using var memoryStream2 = new MemoryStream(binaryReader.ReadBytes(bytes.Length - 2));
        using var binaryReader2 = new BinaryReader(memoryStream2);
        
        for (var i = 0; i < cnt; i++)
        {
            var property = AssetProperty.FromBinaryReader(binaryReader2, context);
            properties.PropertiesDict.Add(property.Name, property);
            ObservableUtil.Subscribe(property, properties);
        }

        return properties;
    }
    
    public static AssetProperties FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var properties = new AssetProperties(context);

        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var cnt = binaryReader.ReadInt16();
        binaryWriter.Write(cnt);

        for (int i = 0; i < cnt; i++)
        {
            var property = AssetProperty.FromBinaryReader(binaryReader, context);
            properties.PropertiesDict.Add(property.Name, property);
            ObservableUtil.Subscribe(property, properties);
            binaryWriter.Write(property.ToBytes(context));
        }
        binaryWriter.Flush();
        properties.Data = memoryStream.ToArray();

        return properties;
    }

    public static AssetProperties Empty(BaseContext context)
    {
        var assetProperties = FromBytes(BitConverter.GetBytes((short)0), context);
        assetProperties.MarkModified();
        return assetProperties;
    }

    public static AssetProperties FromDict(Dictionary<string, object> dict, BaseContext context)
    {
        var properties = Empty(context);

        properties.AddProperties(dict);
        
        return properties;
    }

    public void AddProperties(Dictionary<string, object> dict)
    {
        foreach (var pair in dict)
        {
            AddProperty(AssetProperty.Of(pair.Key, pair.Value, context));
        }
    }

    public void PutProperties(Dictionary<string, object> dict)
    {
        foreach (var pair in dict)
        {
            PutProperty(AssetProperty.Of(pair.Key, pair.Value, context));
        }
    }
    

    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
        
            binaryWriter.Write((short)PropertyCount);
            foreach (var property in PropertiesDict.Values)
            {
                binaryWriter.Write(property.ToBytes(context));
            }
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }

    public override string ToString()
    {
        return $"AssetProperties[Cnt={PropertyCount}, Contents={string.Join(", \n", PropertiesDict.Values.Select(p => p.ToString()))}}}";
    }

    public AssetProperties Clone(BaseContext context)
    {
        var dict = new Dictionary<string, object>();
        foreach (var property in PropertiesDict.Values)
        {
            dict[property.Name] = property.Value;
        } 
        return FromDict(dict, context);
    }
}