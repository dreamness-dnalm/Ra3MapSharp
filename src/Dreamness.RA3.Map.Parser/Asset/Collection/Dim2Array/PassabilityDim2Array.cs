using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public class PassabilityDim2Array: Dim2WritableArray<BlendTileDataAsset.Passability>
{
    public PassabilityDim2Array(BlendTileDataAsset.Passability[,] array) : base(array)
    {
    }
    

    protected override byte[] ElementToBytes(BlendTileDataAsset.Passability element, BaseContext context)
    {
        throw new NotImplementedException();
    }
}