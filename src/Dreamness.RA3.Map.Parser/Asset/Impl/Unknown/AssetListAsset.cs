using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

public class AssetListAsset: BaseAsset
{
    public WritableList<AssetBlock> AssetBlocks { get; private set; } = new WritableList<AssetBlock>();
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.AssetList;
    }

    protected override void _Parse(BaseContext context)
    {
        var memoryStream = new MemoryStream(Data); 
        var binaryReader = new BinaryReader(memoryStream);

        var blockCnt = binaryReader.ReadInt32();

        for (int i = 0; i < blockCnt; i++)
        {
            AssetBlocks.Add(AssetBlock.FromBinaryReader(binaryReader, context));
        }
        
        ObservableUtil.Subscribe(AssetBlocks, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        if (_modified)
        {
            var memoryStream = new MemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.Write(AssetBlocks.Count);
            binaryWriter.Write(AssetBlocks.ToBytes(context));
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }

    public static AssetListAsset Default(BaseContext context)
    {
        var asset = new AssetListAsset();

        asset.ApplyBasicInfo(context);
        
        asset.AssetBlocks.Add(AssetBlock.Of(568797146u, 864929218u));
        asset.AssetBlocks.Add(AssetBlock.Of(568797146u, 2206724476u));
        asset.AssetBlocks.Add(AssetBlock.Of(568797146u, 2782672656u));
        asset.AssetBlocks.Add(AssetBlock.Of(2407383451u, 3048591129u));
        
        asset.MarkModified();
        return asset;
    }
}