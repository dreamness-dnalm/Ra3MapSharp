using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Util;

public class Vec3D: Ra3MapWritable
{
    private float _x;
    public float X 
    { 
        get => _x; 
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
        get => _y; 
        set 
        {
            if (_y != value)
            {
                _y = value;
                MarkModified();
            }
        }
    }

    private float _z;
    public float Z 
    { 
        get => _z; 
        set 
        {
            if (_z != value)
            {
                _z = value;
                MarkModified();
            }
        }
    }

    public Vec3D()
    {
    }

    // public Vec3D(float X, float Y): this(X, Y, 0f) { }
    
    public Vec3D(float X, float Y, float Z)
    {
        _x = X;
        _y = Y;
        _z = Z;
        
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        binaryWriter.Write(_x);
        binaryWriter.Write(_y);
        binaryWriter.Write(_z);
        binaryWriter.Flush();
        Data = memoryStream.ToArray();
        
    }

    public Vec3D(double X, double Y): this(X, Y, 0d) { }
    
    public Vec3D(double X, double Y, double Z): this((float)X, (float)Y, (float)Z) { }
    
    public static Vec3D FromBinaryReader(BinaryReader reader)
    {
        var memoryStream = new MemoryStream();
        var binaryWriter = new BinaryWriter(memoryStream);
        var x = reader.ReadSingle();
        binaryWriter.Write(x);
        var y = reader.ReadSingle();
        binaryWriter.Write(y);
        var z = reader.ReadSingle();
        binaryWriter.Write(z);
        return new Vec3D(x, y, z);
    }

    public override string ToString()
    {
        return $"({X},{Y},{Z})";
    }

    public override byte[] ToBytes(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(X);
        binaryWriter.Write(Y);
        binaryWriter.Write(Z);
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}