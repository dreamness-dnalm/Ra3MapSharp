using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;


namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public class ElevationDim2Array: Dim2WritableArray<float>
{
    public ElevationDim2Array(float[,] array) : base(array)
    {
    }
    

    protected override byte[] ElementToBytes(float element, BaseContext context)
    {
        return BitConverter.GetBytes(StreamExtension.ToSageFloat16(element));
    }
}