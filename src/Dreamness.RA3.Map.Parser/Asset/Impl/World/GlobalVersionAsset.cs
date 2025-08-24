using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.World;

public class GlobalVersionAsset: BaseAsset
{
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetName()
    {
        return AssetNameConst.GlobalVersion;
    }

    protected override void _Parse(BaseContext context)
    {
        
    }

    protected override byte[] Deparse(BaseContext context)
    {
        return new byte[0];
    }

    public static GlobalVersionAsset Default(BaseContext context)
    {
        var asset = new GlobalVersionAsset();

        asset.ApplyBasicInfo(context);
        asset.MarkModified();
        return asset;
    }
}