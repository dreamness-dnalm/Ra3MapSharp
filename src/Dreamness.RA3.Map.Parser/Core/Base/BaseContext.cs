using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Core.Base;

public abstract class BaseContext
{
    private Dictionary<int, string> StringDict = new Dictionary<int, string>();
    private Dictionary<string, int> StringRevertDict = new Dictionary<string, int>();

    public int RegisterStringDeclare(int id, string name)
    {
        StringDict.Put(id, name);
        StringRevertDict.Put(name, id);
        return id;
    }

    public int RegisterStringDeclare(string name)
    {
        if (StringRevertDict.ContainsKey(name))
        {
            return StringRevertDict[name];
        }

        var id = StringDict.Count == 0 ? 0 : StringDict.Keys.Max() + 1;
        return RegisterStringDeclare(id, name);
    }
    
    
    public string GetDeclaredString(int key)
    {
        if (StringDict.ContainsKey(key))
        {
            return StringDict[key];
        }
        else
        {
            throw new System.Exception($"String ({key}) not registered");
        }
    }

    public Dictionary<string, BaseAsset> AssetDict { get; private set; } = new Dictionary<string, BaseAsset>();

    public void RegisterAsset(BaseAsset asset)
    {
        AssetDict.Add(asset.Name, asset);
    }
    
    public void OverrideAsset(BaseAsset asset)
    {
        if (AssetDict.ContainsKey(asset.Name))
        {
            AssetDict[asset.Name] = asset;
        }
        else
        {
            RegisterAsset(asset);
        }
    }

    public byte[] ToBytes()
    {
        var memoryStream = new MemoryStream();
        var binaryWriter = new BinaryWriter(memoryStream);
        binaryWriter.Write(StringDict.Count);
        foreach (var pair in StringDict.OrderByDescending(i => i.Key))
        {
            binaryWriter.Write(pair.Value);
            binaryWriter.Write(pair.Key);
        }
        
        foreach (var asset in AssetDict.Values)
        {
            // Console.WriteLine(asset.Name);
            var assetBytes = asset.ToBytes(this);
            binaryWriter.Write(assetBytes);
        }
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}