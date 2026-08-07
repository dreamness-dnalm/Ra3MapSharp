using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;

/// <summary>
/// Paths of WorldBuilder library maps imported for one SidesList entry.
/// These are map script/team libraries, not Corona Lua library metadata.
/// </summary>
public class LibraryMaps : BaseAsset
{
    private readonly List<string> _mapNames = new();

    public IReadOnlyList<string> MapNames => _mapNames.AsReadOnly();

    public override short GetVersion() => 1;

    public override string GetAssetType() => AssetNameConst.LibraryMaps;

    public void Add(string mapName)
    {
        ArgumentNullException.ThrowIfNull(mapName);
        _mapNames.Add(mapName);
        MarkModified();
    }

    public bool Remove(string mapName)
    {
        var removed = _mapNames.Remove(mapName);
        if (removed)
        {
            MarkModified();
        }

        return removed;
    }

    protected override void _Parse(BaseContext context)
    {
        using var stream = new MemoryStream(Data);
        using var reader = new BinaryReader(stream);
        var count = reader.ReadInt32();
        if (count < 0 || count > 100_000)
        {
            throw new InvalidDataException($"Invalid LibraryMaps entry count: {count}.");
        }

        for (var i = 0; i < count; i++)
        {
            _mapNames.Add(reader.ReadDefaultString());
        }

        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException("LibraryMaps contains trailing data.");
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(_mapNames.Count);
        foreach (var mapName in _mapNames)
        {
            writer.WriteDefaultString(mapName);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static LibraryMaps Empty(BaseContext context)
    {
        var asset = new LibraryMaps();
        asset.ApplyBasicInfo(context);
        asset.MarkModified();
        return asset;
    }
}
