using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;

public class Texture: Ra3MapWritable
{
    public int CellStart { get; private set; }
    
    public int CellCount { get; private set; }
    
    public int CellSize { get; private set; }

    private static readonly int MagicValue = 0;
    
    public string Name { get; private set; }
    
    private Texture(int cellStart, int cellCount, int cellSize, string name)
    {
        CellStart = cellStart;
        CellCount = cellCount;
        CellSize = cellSize;
        Name = name;
    }
    
    public static Texture Of(int start, string name, int count = 16, int size = 4)
    {
        var texture = new Texture(start, count, size, name);
        texture.MarkModified();
        
        return texture;
    }
    
    public static Texture FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var cellStart = binaryReader.ReadInt32();
        var cellCount = binaryReader.ReadInt32();
        var cellSize = binaryReader.ReadInt32();
        var magicValue = binaryReader.ReadInt32();
        var name = binaryReader.ReadDefaultString();

        if (magicValue != MagicValue)
        {
            throw new InvalidDataException($"Invalid magic value for texture: {magicValue}");
        }

        return new Texture(cellStart, cellCount, cellSize, name);
    }
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(CellStart);
        binaryWriter.Write(CellCount);
        binaryWriter.Write(CellSize);
        binaryWriter.Write(MagicValue);
        binaryWriter.WriteDefaultString(Name);
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}