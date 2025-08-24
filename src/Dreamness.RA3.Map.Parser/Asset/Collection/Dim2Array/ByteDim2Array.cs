using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public class ByteDim2Array: Dim2WritableArray<byte>
{
    public ByteDim2Array(byte[,] array) : base(array)
    {
    }
    

    protected override byte[] ElementToBytes(byte element, BaseContext context)
    {
        return BitConverter.GetBytes(element);
    }
}