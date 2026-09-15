using System.Collections;
using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;

public class WritableList<T>: Ra3MapWritable, IEnumerable<T> where T:Ra3MapWritable
{
    List<T> _assets = new List<T>();
    
    public static WritableList<T> FromJson(string json)
    {
        var ra3MapWritables = JsonSerializer.Deserialize<WritableList<T>>(json);

        foreach (var ele in ra3MapWritables)
        {
            ele.MarkModified();
            ObservableUtil.Subscribe(ele, ra3MapWritables);
        }
        return ra3MapWritables;
    }

    public void Add(T asset, bool ignoreModified = false)
    {
        _assets.Add(asset);
        ObservableUtil.Subscribe(asset, this);
        if (! ignoreModified)
        {
            MarkModified();
        }
        
    }
    
    public void Remove(T asset)
    {
        _assets.Remove(asset);
        ObservableUtil.Unsubscribe(asset, this);
        MarkModified();
    }

    public void Clear()
    {
        for (int i = _assets.Count - 1; i >= 0; i--)
        {
            Remove(_assets[i]);
        }
        MarkModified();
    }
    
    public List<T> GetAssets()
    {
        return _assets;
    }
    
    public int Count { get => _assets.Count; }
    
    public T this[int index]
    {
        get => _assets[index];
        set
        {
            if (_assets[index] != value)
            {
                ObservableUtil.Unsubscribe(_assets[index], this);
                _assets[index] = value;
                ObservableUtil.Subscribe(value, this);
                MarkModified();
            }
        }
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
        var memoryStream = new MemoryStream();
        foreach (var asset in _assets)
        {
            memoryStream.Write(asset.ToBytes(context));
        }
        memoryStream.Flush();
        return memoryStream.ToArray();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _assets.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return $"[Count={Count}, Assets={string.Join(", ", _assets)}]";
    }
}