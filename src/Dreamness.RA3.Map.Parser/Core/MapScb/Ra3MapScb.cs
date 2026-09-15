using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.RA3.Map.Parser.Core.MapScb;

public class Ra3MapScb
{
    private Ra3MapScb(){}

    public string ScbFilePath { get; private set; } = null!;

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
        var mapScb = FromBytes(bytes);
        mapScb.ScbFilePath = filePath;
        return mapScb;
    }

    public void SaveAs(string filePath, bool compress = false)
    {
        var dirPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        byte[] data = Context.ToBytes();
        binaryWriter.Write(CompressConst.UnCompressFlag);
        binaryWriter.Write(data);

        using var fileStream = File.Create(filePath);

        if (compress)
        {
            byte[] output;
            memoryStream.GetBuffer().Skip(0).Take((int)memoryStream.Length).ToArray().RefPackCompress(out output);
            fileStream.Write(output, 0, output.Length);
        }
        else
        {
            fileStream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }
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
