using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class Script: BaseAsset
{
    public override short GetVersion()
    {
        return 4;
    }

    public override string GetName()
    {
        return AssetNameConst.Script;
    }

    protected override void _Parse(BaseContext context)
    {
        throw new NotImplementedException();
    }

    protected override byte[] Deparse(BaseContext context)
    {
        throw new NotImplementedException();
    }
}