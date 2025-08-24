using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public class UshortDim2Array: Dim2WritableArray<ushort>
{
    public UshortDim2Array(ushort[,] array) : base(array)
    {
    }
    

    protected override byte[] ElementToBytes(ushort element, BaseContext context)
    {
        return BitConverter.GetBytes(element);
    }
}