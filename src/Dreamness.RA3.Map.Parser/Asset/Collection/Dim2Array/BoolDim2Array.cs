using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public class BoolDim2Array: Dim2WritableArray<bool>
{
    public BoolDim2Array(bool[,] array) : base(array)
    {
    }
    

    protected override byte[] ElementToBytes(bool element, BaseContext context)
    {
        return BitConverter.GetBytes(element);
    }
}