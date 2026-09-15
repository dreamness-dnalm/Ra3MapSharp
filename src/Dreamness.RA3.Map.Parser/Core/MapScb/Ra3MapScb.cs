using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.RA3.Map.Parser.Core.MapScb;

public class Ra3MapScb
{
    private Ra3MapScb(){}

    public string? ScbFilePath { get; private set; }

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
        mapScb.ScbFilePath = Path.GetFullPath(filePath);
        return mapScb;
    }

    /// <summary>
    /// 将 SCB 保存到指定路径。SCB 默认保存为未压缩格式。
    /// </summary>
    public void SaveAs(string filePath, bool compress = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A non-empty output path is required.", nameof(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);
        var dirPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        binaryWriter.Write(CompressConst.UnCompressFlag);
        binaryWriter.Write(Context.ToBytes());
        binaryWriter.Flush();

        byte[] output;
        if (compress)
        {
            memoryStream.ToArray().RefPackCompress(out output);
        }
        else
        {
            output = memoryStream.ToArray();
        }

        File.WriteAllBytes(fullPath, output);
        ScbFilePath = fullPath;
    }

    /// <summary>
    /// 保存到当前 SCB 的来源路径。由 <see cref="FromFile"/> 或 <see cref="SaveAs"/> 设置。
    /// </summary>
    public void Save(bool compress = false)
    {
        if (ScbFilePath == null)
        {
            throw new System.Exception(
                "ScbFilePath is null, if it's a new scb, use SaveAs method");
        }

        SaveAs(ScbFilePath, compress);
    }

    public static void Main()
    {
        var o = FromFile("");
        
        Console.WriteLine(o);
    }

}
