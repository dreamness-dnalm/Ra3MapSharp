using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

/// <summary>
/// Dependency manifest containing the external SAGE assets referenced by a map.
/// </summary>
public class AssetListAsset: BaseAsset
{
    public WritableList<AssetBlock> AssetBlocks { get; } = new WritableList<AssetBlock>();

    public bool ContainsDependency(uint typeId, uint instanceId)
    {
        return AssetBlocks.Any(x => x.TypeId == typeId && x.InstanceId == instanceId);
    }

    public bool AddDependency(uint typeId, uint instanceId)
    {
        if (ContainsDependency(typeId, instanceId))
        {
            return false;
        }

        AssetBlocks.Add(AssetBlock.Of(typeId, instanceId));
        return true;
    }

    public bool RemoveDependency(uint typeId, uint instanceId)
    {
        var dependency = AssetBlocks.FirstOrDefault(
            x => x.TypeId == typeId && x.InstanceId == instanceId);
        if (dependency == null)
        {
            return false;
        }

        AssetBlocks.Remove(dependency);
        return true;
    }
    
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
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        var blockCnt = binaryReader.ReadInt32();
        if (blockCnt < 0 || blockCnt > 100_000)
        {
            throw new InvalidDataException($"Invalid asset dependency count: {blockCnt}.");
        }

        var remainingBytes = binaryReader.BaseStream.Length - binaryReader.BaseStream.Position;
        if (remainingBytes != (long)blockCnt * 8)
        {
            throw new InvalidDataException(
                $"AssetList size does not match its declared dependency count: {blockCnt}.");
        }

        for (var i = 0; i < blockCnt; i++)
        {
            AssetBlocks.Add(AssetBlock.FromBinaryReader(binaryReader, context), ignoreModified: true);
        }
        
        ObservableUtil.Subscribe(AssetBlocks, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        binaryWriter.Write(AssetBlocks.Count);
        binaryWriter.Write(AssetBlocks.ToBytes(context));

        binaryWriter.Flush();
        return memoryStream.ToArray();
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
