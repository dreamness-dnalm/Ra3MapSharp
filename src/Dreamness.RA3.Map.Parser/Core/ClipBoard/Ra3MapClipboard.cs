using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Core.ClipBoard;

public class Ra3MapClipboard
{
    private Ra3MapClipboard()
    {}

    public string? ClipboardFilePath { get; private set; }
    
    public ClipBoardContext Context = new ClipBoardContext();
    
    /// <summary>
    /// Build clipboard object from raw clipboard bytes.
    /// </summary>
    public static Ra3MapClipboard FromBytes(byte[] bytes)
    {
        var clipboard = new Ra3MapClipboard();
        
        using var binaryReader = MapFileCodec.CreatePayloadReader(bytes);
        MapFileCodec.ReadContext(binaryReader, clipboard.Context);

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
        clipboard.ClipboardFilePath = Path.GetFullPath(filePath);
        return clipboard;
    }

    /// <summary>
    /// Save clipboard data to target file path (supports .paste/.bin by content format).
    /// </summary>
    /// <param name="filePath">Target output file path.</param>
    /// <param name="compress">Whether to refpack-compress output. Default is false.</param>
    public void SaveAs(string filePath, bool compress = false)
    {
        ClipboardFilePath = MapFileCodec.AtomicWrite(filePath, MapFileCodec.Encode(Context, compress));
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
