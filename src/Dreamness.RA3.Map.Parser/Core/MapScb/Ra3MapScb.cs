using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.RA3.Map.Parser.Core.MapScb;

public class Ra3MapScb
{
    private Ra3MapScb(){}

    public MapScbContext Context = new MapScbContext();

    public static Ra3MapScb FromBytes(byte[] bytes)
    {
        var mapScb = new Ra3MapScb();
        
        using var memoryStream = new MemoryStream(bytes);
        using var binaryReader = new BinaryReader(memoryStream);

        var compressFlag = binaryReader.ReadUInt32();

        if (compressFlag == CompressConst.CompressFlag)
        {
            throw new NotImplementedException();
        }
        
        var sectionDeclareCount = binaryReader.ReadInt32();
        for (int i = 0; i < sectionDeclareCount; i++)
        {
            var name = binaryReader.ReadString();
            var id = binaryReader.ReadInt32();
            mapScb.Context.RegisterStringDeclare(id, name);
        }

        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var asset = AssetParser.FromBinaryReader(binaryReader, mapScb.Context);
            mapScb.Context.RegisterAsset(asset);
        }
        
        binaryReader.Close();

        return mapScb;
    }

    public static Ra3MapScb FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Scb file not found", filePath);
        }
        var bytes = File.ReadAllBytes(filePath);
        return FromBytes(bytes);
    }

    public static void Main()
    {
        var o = FromFile("");
        
        Console.WriteLine(o);
    }

}