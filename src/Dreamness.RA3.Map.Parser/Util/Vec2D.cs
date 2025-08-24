using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Util;

public class Vec2D: Ra3MapWritable
{
    private float _x;

    public float X
    {
        get { return _x; }
        set
        {
            if (_x != value)
            {
                _x = value;
                MarkModified();
            }
        }
    }
    
    private float _y;
    
    public float Y
    {
        get { return _y; }
        set
        {
            if (_y != value)
            {
                _y = value;
                MarkModified();
            }
        }
    }
    
    public Vec2D(float x, float y)
    {
        _x = x;
        _y = y;
    }
    
    public static Vec2D FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var x = binaryReader.ReadSingle();
        var y = binaryReader.ReadSingle();
        return new Vec2D(x, y);
    }
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(X);
        binaryWriter.Write(Y);
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}