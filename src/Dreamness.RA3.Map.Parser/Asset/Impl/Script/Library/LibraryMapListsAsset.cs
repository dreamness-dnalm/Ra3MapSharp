using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Util;


// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

/// <summary>
/// Index-aligned library-map imports for the entries in SidesList.
/// </summary>
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
        using var stream = new MemoryStream(Data);
        using var reader = new BinaryReader(stream);
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var libraryMaps = AssetParser.FromBinaryReader(reader, context) as LibraryMaps
                ?? throw new InvalidDataException("LibraryMapLists contains an asset other than LibraryMaps.");
            if (libraryMaps.Errored)
            {
                throw new InvalidDataException("Failed to parse a LibraryMaps entry.", libraryMaps.ErrorException);
            }
            LibraryMapsList.Add(libraryMaps, ignoreModified: true);
        }

        ObservableUtil.Subscribe(LibraryMapsList, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(LibraryMapsList.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static LibraryMapListsAsset Default(BaseContext context)
    {
        return Default(SidesListAsset.DefaultPlayerNames.Length, context);
    }

    public static LibraryMapListsAsset Default(SidesListAsset sides, BaseContext context)
    {
        ArgumentNullException.ThrowIfNull(sides);
        return Default(sides.PlayerDataList.Count, context);
    }

    public static LibraryMapListsAsset Default(int sideCount, BaseContext context)
    {
        if (sideCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sideCount));
        }

        var asset = new LibraryMapListsAsset();

        asset.ApplyBasicInfo(context);
        for (var i = 0; i < sideCount; i++)
        {
            asset.LibraryMapsList.Add(LibraryMaps.Empty(context), ignoreModified: true);
        }
        ObservableUtil.Subscribe(asset.LibraryMapsList, asset);
        asset.MarkModified();
        return asset;
    }

    /// <summary>
    /// Keeps the per-side library-map lists aligned without discarding entries
    /// that remain at the same player index.
    /// </summary>
    public void SynchronizeWithSideCount(int sideCount, BaseContext context)
    {
        if (sideCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sideCount));
        }

        while (LibraryMapsList.Count < sideCount)
        {
            LibraryMapsList.Add(LibraryMaps.Empty(context));
        }

        while (LibraryMapsList.Count > sideCount)
        {
            LibraryMapsList.Remove(LibraryMapsList[LibraryMapsList.Count - 1]);
        }
    }
}
