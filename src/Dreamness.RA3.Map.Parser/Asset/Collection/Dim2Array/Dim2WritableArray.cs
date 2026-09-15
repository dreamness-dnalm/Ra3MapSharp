using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public abstract class Dim2WritableArray<T> : Ra3MapWritable
{
    private T[,] _array;

    public Dim2WritableArray(T[,] array)
    {
        _array = array;
    }

    // public Dim2WritableArray(int width, int height)
    // {
    //     _array = new T[width, height];
    // }

    public T this[int x, int y]
    {
        get => _array[x, y];
        set
        {
            if (!Equals(_array[x, y], value))
            {
                _array[x, y] = value;
                MarkModified();
            }
        }
    }
    
    public T[,] Array => _array;

    public int Width => _array.GetLength(0);
    public int Height => _array.GetLength(1);
    
    protected abstract byte[] ElementToBytes(T element, BaseContext context);

    public override byte[] ToBytes(BaseContext context)
    {
        var memoryStream = new MemoryStream();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                memoryStream.Write(ElementToBytes(_array[x, y], context));
            }
        }
        memoryStream.Flush();
        return memoryStream.ToArray();
    }
}
