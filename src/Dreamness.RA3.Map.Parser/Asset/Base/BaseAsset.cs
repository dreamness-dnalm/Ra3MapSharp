using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Base;

public abstract class BaseAsset: Ra3MapWritable
{
    public int Id { get; set; }
    public short Version { get; set; }
    public string Name { get; set; }
    public int DataSize { get; set; }

    public byte[] Data { get; set; }
    
    public bool Parsed { get; set; } = false;
    
    public bool Errored { get; set; } = false;
    
    public System.Exception ErrorException { get; set; }
    
    public abstract short GetVersion();

    public abstract String GetName();

    public void ApplyBasicInfo(BaseContext context)
    {
        this.Version = GetVersion();
        this.Name = GetName();
        this.Id = context.RegisterStringDeclare(Name);
    }

    public override byte[] ToBytes(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(Id);
        binaryWriter.Write(Version);
        
        if (_modified)
        {
            var dataBytes = Deparse(context);
            binaryWriter.Write(dataBytes.Length);
            binaryWriter.Write(dataBytes);
        }
        else
        {
            binaryWriter.Write(DataSize);
            binaryWriter.Write(Data);
        }
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    public void Parse(BaseContext context)
    {
        if (Parsed)
        {
            throw new System.Exception("Could not be parsed twice.");
        }
        _Parse(context);
        Parsed = true;
    }

    public void ParseTolerance(BaseContext context)
    {
        if (Parsed)
        {
            throw new System.Exception("Could not be parsed twice.");
        }

        try
        {
            _Parse(context);
        }
        catch (System.Exception e)
        {
            Errored = true;
            ErrorException = e;
        }
        Parsed = true;
    }
    protected abstract void _Parse(BaseContext context);
    
    protected abstract byte[] Deparse(BaseContext context);
    
    public T Clone<T>() where T : BaseAsset, new()
    {
        var clonedAsset = new T
        {
            Id = this.Id,
            Version = this.Version,
            Name = this.Name,
            DataSize = this.DataSize,
            Data = this.Data
        };
        
        return clonedAsset;
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

    public override string ToString()
    {
        return "Asset: " + Name + 
               " (Id: " + Id + 
               ", Version: " + Version + 
               ", DataSize: " + DataSize + 
               ", Parsed: " + Parsed + 
               ", Errored: " + Errored + ")";
    }
}