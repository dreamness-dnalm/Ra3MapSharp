using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;

public class BorderData: Ra3MapWritable
{
    private int _x1;

    public int X1
    {
        get => _x1;
        set
        {
            if (_x1 != value)
            {
                _x1 = value;
                MarkModified();
            }
        }
    }
    private int _y1;

    public int Y1
    {
        get => _y1;
        set
        {
            if (_y1 != value)
            {
                _y1 = value;
                MarkModified();
            }
        }
    }
    
    private int _x2;
    public int X2
    {
        get => _x2;
        set
        {
            if (_x2 != value)
            {
                _x2 = value;
                MarkModified();
            }
        }
    }
    
    private int _y2;
    public int Y2
    {
        get => _y2;
        set
        {
            if (_y2 != value)
            {
                _y2 = value;
                MarkModified();
            }
        }
    }
    
    public BorderData(int x1, int y1, int x2, int y2)
    {
        _x1 = x1;
        _y1 = y1;

        _x2 = x2;
        _y2 = y2;
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(_x1);
            binaryWriter.Write(_y1);
            binaryWriter.Write(_x2);
            binaryWriter.Write(_y2);
            binaryWriter.Flush();
            return memoryStream.ToArray();
    }

    public override string ToString()
    {
        return $"BorderData(({X1}, {Y1}) - ({X2}, {Y2}))";
    }
}