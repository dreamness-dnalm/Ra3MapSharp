using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Util;

internal static class AssetReadUtility
{
    public static void RequireVersion(BaseAsset asset, short version)
    {
        if (asset.Version != version)
            throw new InvalidDataException($"Unsupported {asset.AssetType} version {asset.Version}; expected {version}.");
    }

    public static int ReadCount(BinaryReader reader, int minimumItemSize)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / minimumItemSize)
            throw new InvalidDataException("Invalid collection count.");
        return count;
    }

    public static string ReadString(BinaryReader reader)
    {
        var start = reader.BaseStream.Position;
        var size = reader.ReadUInt16();
        if (size > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new EndOfStreamException("Truncated map string.");
        reader.BaseStream.Position = start;
        return reader.ReadDefaultString();
    }

    public static void WriteString(BinaryWriter writer, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // Existing map strings use GBK, with a uint16 byte length.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        if (System.Text.Encoding.GetEncoding("GBK").GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("Map string exceeds 65535 bytes.", nameof(value));
        writer.WriteDefaultString(value);
    }

    public static void RequireEnd(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Unexpected trailing asset data.");
    }

    public static void RequireWritable(BaseAsset asset)
    {
        if (asset.Errored)
            throw new InvalidOperationException($"Cannot rewrite unparsed {asset.AssetType}.", asset.ErrorException);
    }
}
