using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

public class LibraryMapListsAsset: BaseAsset
{
    public WritableList<LibraryMaps> LibraryMapsList { get; } = new WritableList<LibraryMaps>();
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.LibraryMapLists;
    }

    protected override void _Parse(BaseContext context)
    {
        // TODO
    }

    protected override byte[] Deparse(BaseContext context)
    {
        // TODO
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(LibraryMapsList.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static LibraryMapListsAsset Default(BaseContext context)
    {
        var asset = new LibraryMapListsAsset();
        
        asset.ApplyBasicInfo(context);
        asset.MarkModified();
        return asset;
    }
}