using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;

public class MapColorArgb
{
    public readonly byte A;
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    

    public MapColorArgb(byte a, byte r, byte g, byte b)
    {
        A = a;
        R = r;
        G = g;
        B = b;
    }

    public static MapColorArgb FromBinaryReader(BinaryReader reader)
    {
        var v = reader.ReadUInt32();
        
        var a = (byte)((v >> 24) & 0xFF);
        var r = (byte)((v >> 16) & 0xFF);
        var g = (byte)((v >> 8) & 0xFF);
        var b = (byte)(v & 0xFF);

        return new MapColorArgb(a, r, g, b);
    }
    
    
    public byte[] ToBytes(BaseContext context)
    {
            var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.Write((uint)((A << 24) | (R << 16) | (G << 8) | B));
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
    }

    public override bool Equals(object? obj)
    {
        return (obj is MapColorArgb other) &&
               A == other.A &&
               R == other.R &&
               G == other.G &&
               B == other.B;
    }

    public override string ToString()
    {
        return $"ARGB({A}, {R}, {G}, {B})";
    }
}