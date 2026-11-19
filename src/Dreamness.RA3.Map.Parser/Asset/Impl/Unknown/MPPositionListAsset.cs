using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Asset.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

public class MPPositionListAsset: BaseAsset
{
    public WritableList<MPPositionInfo> MPPositionInfos { get; } = new WritableList<MPPositionInfo>();

    public MPPositionListAsset() => ObservableUtil.Subscribe(MPPositionInfos, this);
    
    public override short GetVersion()
    {
        return 0;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.MPPositionList;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        AssetReadUtility.RequireVersion(this, 0);
        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var item = AssetParser.FromBinaryReader(binaryReader, context);
            if (item is not MPPositionInfo info || info.Errored)
                throw new InvalidDataException("Invalid MPPositionInfo record.", item.ErrorException);
            MPPositionInfos.Add(info, ignoreModified:true);
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        AssetReadUtility.RequireWritable(this);
        return MPPositionInfos.ToBytes(context);
    }
    
    public static MPPositionListAsset Default(BaseContext context)
    {
        var asset = new MPPositionListAsset();
        
        asset.ApplyBasicInfo(context);
        
        for (int i = 0; i < 6; i++)
        {
            asset.MPPositionInfos.Add(MPPositionInfo.Of(true, true, true, UInt32.MaxValue, null, context), ignoreModified:true);
        }
        
        asset.MarkModified();
        
        return asset;
    }
}
