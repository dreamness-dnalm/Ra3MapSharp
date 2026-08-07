using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

/// <summary>
/// Ordered multiplayer-slot policies used by the lobby and skirmish setup.
/// </summary>
public class MPPositionListAsset: BaseAsset
{
    public WritableList<MPPositionInfo> MPPositionInfos { get; } = new WritableList<MPPositionInfo>();
    
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
        
        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var positionInfo = AssetParser.FromBinaryReader(binaryReader, context) as MPPositionInfo
                ?? throw new InvalidDataException("MPPositionList contains an asset other than MPPositionInfo.");
            if (positionInfo.Errored)
            {
                throw new InvalidDataException("Failed to parse MPPositionInfo.", positionInfo.ErrorException);
            }
            MPPositionInfos.Add(positionInfo, ignoreModified:true);
        }

        if (MPPositionInfos.Count == 0 || MPPositionInfos.Count > 16)
        {
            throw new InvalidDataException($"MPPositionList contains an invalid entry count: {MPPositionInfos.Count}.");
        }
        ObservableUtil.Subscribe(MPPositionInfos, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        return MPPositionInfos.ToBytes(context);
    }
    
    public static MPPositionListAsset Default(BaseContext context)
    {
        var asset = new MPPositionListAsset();
        
        asset.ApplyBasicInfo(context);
        
        for (int i = 0; i < 6; i++)
        {
            asset.MPPositionInfos.Add(
                MPPositionInfo.Of(true, true, true, UInt32.MaxValue, Array.Empty<string>(), context),
                ignoreModified:true);
        }
        
        asset.MarkModified();
        
        return asset;
    }
}
