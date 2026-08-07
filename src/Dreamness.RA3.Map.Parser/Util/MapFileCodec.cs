using System.Text;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Util;

internal static class MapFileCodec
{
    internal const int MaxStringDeclarations = 1_000_000;
    internal const int MaxDeclaredStringBytes = 1024 * 1024;
    internal const int MaxAssetDataSize = 512 * 1024 * 1024;

    public static BinaryReader CreatePayloadReader(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 8)
        {
            throw new InvalidDataException("Map container is shorter than its header.");
        }

        using var containerReader = new BinaryReader(new MemoryStream(bytes, writable: false));
        var flag = containerReader.ReadUInt32();
        if (flag == CompressConst.UnCompressFlag)
        {
            var stream = new MemoryStream(bytes, writable: false);
            stream.Position = sizeof(uint);
            return new BinaryReader(stream);
        }

        if (flag != CompressConst.CompressFlag)
        {
            throw new InvalidDataException($"Invalid map container magic: 0x{flag:X8}.");
        }

        containerReader.BaseStream.Position = 8;
        using var outputStream = new MemoryStream();
        using (var outputWriter = new BinaryWriter(outputStream, Encoding.UTF8, leaveOpen: true))
        {
            RefpackComrpessor.Decompress(containerReader, outputWriter);
        }

        var uncompressed = outputStream.ToArray();
        if (uncompressed.Length < 8 || BitConverter.ToUInt32(uncompressed, 0) != CompressConst.UnCompressFlag)
        {
            throw new InvalidDataException("RefPack payload does not contain a valid uncompressed map header.");
        }

        var payloadStream = new MemoryStream(uncompressed, writable: false);
        payloadStream.Position = sizeof(uint);
        return new BinaryReader(payloadStream);
    }

    public static void ReadContext(BinaryReader reader, BaseContext context)
    {
        if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(int))
        {
            throw new InvalidDataException("Missing string declaration count.");
        }

        var declarationCount = reader.ReadInt32();
        if (declarationCount < 0 || declarationCount > MaxStringDeclarations)
        {
            throw new InvalidDataException($"Invalid string declaration count: {declarationCount}.");
        }

        for (var i = 0; i < declarationCount; i++)
        {
            var name = ReadDeclaredString(reader);
            if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(int))
            {
                throw new EndOfStreamException("Missing string declaration ID.");
            }

            var id = reader.ReadInt32();
            context.RegisterStringDeclare(id, name);
        }

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 10)
            {
                throw new InvalidDataException("Truncated asset header at the end of the map.");
            }

            context.RegisterAsset(AssetParser.FromBinaryReader(reader, context));
        }
    }

    public static byte[] Encode(BaseContext context, bool compress)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(CompressConst.UnCompressFlag);
        writer.Write(context.ToBytes());
        writer.Flush();

        var uncompressed = stream.ToArray();
        if (compress && uncompressed.RefPackCompress(out var compressed) && compressed is not null)
        {
            return compressed;
        }

        return uncompressed;
    }

    public static string AtomicWrite(string filePath, byte[] data)
    {
        var fullPath = PrepareTarget(filePath);
        var tempPath = CreateTempPath(fullPath);
        try
        {
            File.WriteAllBytes(tempPath, data);
            File.Move(tempPath, fullPath, overwrite: true);
            return fullPath;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public static async Task<string> AtomicWriteAsync(
        string filePath,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        var fullPath = PrepareTarget(filePath);
        var tempPath = CreateTempPath(fullPath);
        try
        {
            await File.WriteAllBytesAsync(tempPath, data, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, fullPath, overwrite: true);
            return fullPath;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string PrepareTarget(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A non-empty output path is required.", nameof(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new ArgumentException("The output path has no parent directory.", nameof(filePath));
        Directory.CreateDirectory(directory);
        return fullPath;
    }

    private static string CreateTempPath(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath)!;
        return Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static string ReadDeclaredString(BinaryReader reader)
    {
        var byteCount = Read7BitEncodedInt(reader);
        if (byteCount < 0 || byteCount > MaxDeclaredStringBytes)
        {
            throw new InvalidDataException($"Invalid declared string length: {byteCount}.");
        }

        var bytes = reader.ReadBytes(byteCount);
        if (bytes.Length != byteCount)
        {
            throw new EndOfStreamException(
                $"Truncated declared string: expected {byteCount} bytes, got {bytes.Length}.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static int Read7BitEncodedInt(BinaryReader reader)
    {
        uint result = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                throw new EndOfStreamException("Truncated 7-bit encoded integer.");
            }

            var value = reader.ReadByte();
            result |= (uint)(value & 0x7F) << shift;
            if ((value & 0x80) == 0)
            {
                if (result > int.MaxValue)
                {
                    throw new InvalidDataException("7-bit encoded integer exceeds Int32.MaxValue.");
                }

                return (int)result;
            }
        }

        throw new InvalidDataException("Invalid 7-bit encoded integer.");
    }
}
