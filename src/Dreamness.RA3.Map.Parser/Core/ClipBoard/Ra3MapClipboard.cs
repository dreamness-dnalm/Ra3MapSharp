using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Core.ClipBoard;

public class Ra3MapClipboard
{
    private Ra3MapClipboard()
    {}
    
    public ClipBoardContext Context = new ClipBoardContext();
    
    public static Ra3MapClipboard FromBytes(byte[] bytes)
    {
        var clipboard = new Ra3MapClipboard();
        
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
            clipboard.Context.RegisterStringDeclare(id, name);
        }

        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var asset = AssetParser.FromBinaryReader(binaryReader, clipboard.Context);
            clipboard.Context.RegisterAsset(asset);
        }
        
        binaryReader.Close();

        return clipboard;
    }
    
    public static Ra3MapClipboard FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Clipboard file not found.", filePath);
        }
        
        var bytes = File.ReadAllBytes(filePath);
        return FromBytes(bytes);
    }

    public static void Main()
    {
        var ra3MapClipboard = Ra3MapClipboard.FromFile("N:\\workspace\\ra3\\ra3_py_workspace\\data\\1.paste");
        
        Console.WriteLine(ra3MapClipboard);
    }
}