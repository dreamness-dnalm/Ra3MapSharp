using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Default;

public class DefaultAsset: BaseAsset
{

    public override short GetVersion()
    {
        throw new NotImplementedException();
    }

    public override string GetAssetType()
    {
        throw new NotImplementedException();
    }

    protected override void _Parse(BaseContext context)
    {

    }

    protected override byte[] Deparse(BaseContext context)
    {
        throw new NotImplementedException();
    }
    
}