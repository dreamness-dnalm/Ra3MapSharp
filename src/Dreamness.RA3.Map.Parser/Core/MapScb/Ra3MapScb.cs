using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Parser.Core.MapScb;

public class Ra3MapScb
{
    private Ra3MapScb(){}

    public string? ScbFilePath { get; private set; }

    public MapScbContext Context = new MapScbContext();

    public static Ra3MapScb FromBytes(byte[] bytes)
    {
        var mapScb = new Ra3MapScb();
        
        using var binaryReader = MapFileCodec.CreatePayloadReader(bytes);
        MapFileCodec.ReadContext(binaryReader, mapScb.Context);

        return mapScb;
    }

    public static Ra3MapScb FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Scb file not found", filePath);
        }
        var bytes = File.ReadAllBytes(filePath);
        var mapScb = FromBytes(bytes);
        mapScb.ScbFilePath = Path.GetFullPath(filePath);
        return mapScb;
    }

    public void SaveAs(string filePath, bool compress = false)
    {
        ScbFilePath = MapFileCodec.AtomicWrite(filePath, MapFileCodec.Encode(Context, compress));
    }

    public void Save(bool compress = false)
    {
        if (ScbFilePath == null)
        {
            throw new System.Exception("ScbFilePath is null, if it's a new scb, use SaveAs method");
        }

        SaveAs(ScbFilePath, compress);
    }

    public static void Main()
    {
        var o = FromFile("");
        
        Console.WriteLine(o);
    }

}
