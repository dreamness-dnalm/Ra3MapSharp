using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Core.ClipBoard;

public class Ra3MapClipboard
{
    private Ra3MapClipboard()
    {}

    public string ClipboardFilePath { get; private set; } = null!;
    
    public ClipBoardContext Context = new ClipBoardContext();
    
    /// <summary>
    /// Build clipboard object from raw clipboard bytes.
    /// </summary>
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
    
    /// <summary>
    /// Load clipboard data from a file path (commonly .paste or .bin).
    /// </summary>
    /// <remarks>
    /// The file extension is not used for parsing. The content format decides how it is interpreted.
    /// </remarks>
    public static Ra3MapClipboard FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Clipboard file not found.", filePath);
        }
        
        var bytes = File.ReadAllBytes(filePath);
        var clipboard = FromBytes(bytes);
        clipboard.ClipboardFilePath = filePath;
        return clipboard;
    }

    /// <summary>
    /// Save clipboard data to target file path (supports .paste/.bin by content format).
    /// </summary>
    /// <param name="filePath">Target output file path.</param>
    /// <param name="compress">Whether to refpack-compress output. Default is false.</param>
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

    /// <summary>
    /// Save clipboard back to the source path recorded by <see cref="FromFile"/>.
    /// </summary>
    /// <param name="compress">Whether to refpack-compress output. Default is false.</param>
    /// <exception cref="System.Exception">
    /// Thrown when the instance is created from <see cref="FromBytes"/> and no file path is available.
    /// </exception>
    public void Save(bool compress = false)
    {
        if (ClipboardFilePath == null)
        {
            throw new System.Exception("ClipboardFilePath is null, if it's a new clipboard, use SaveAs method");
        }

        SaveAs(ClipboardFilePath, compress);
    }

    public static void Main()
    {
        var ra3MapClipboard = Ra3MapClipboard.FromFile("N:\\workspace\\ra3\\ra3_py_workspace\\data\\1.paste");
        
        Console.WriteLine(ra3MapClipboard);
    }
}
