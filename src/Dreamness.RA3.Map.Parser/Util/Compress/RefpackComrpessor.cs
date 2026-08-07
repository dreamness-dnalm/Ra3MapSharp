namespace Dreamness.Ra3.Map.Parser.Util.Compress;

public static class RefpackComrpessor
{
    public const int DefaultMaxOutputSize = 512 * 1024 * 1024;

    public static void Decompress(BinaryReader input, BinaryWriter output)
    {
        Decompress(input, output, DefaultMaxOutputSize);
    }

    public static void Decompress(BinaryReader input, BinaryWriter output, int maxOutputSize)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.BaseStream.CanSeek)
        {
            throw new NotSupportedException("RefPack input stream must be seekable.");
        }

        var expectedSize = ReadExpectedSize(input, maxOutputSize);
        var result = new List<byte>(expectedSize);
        var finished = false;

        while (!finished)
        {
            var code = ReadByte(input, "RefPack command");

            if ((code & 0x80) == 0)
            {
                var code2 = ReadByte(input, "RefPack short-copy offset");
                AppendLiterals(input, result, code & 3, expectedSize);
                var offset = 1 + code2 + (code & 0x60) * 8;
                var count = (code & 0x1C) / 4 + 3;
                AppendCopy(result, offset, count, expectedSize);
            }
            else if ((code & 0x40) == 0)
            {
                var code2 = ReadByte(input, "RefPack medium-copy offset high byte");
                var code3 = ReadByte(input, "RefPack medium-copy offset low byte");
                AppendLiterals(input, result, code2 >> 6, expectedSize);
                var offset = 1 + ((code2 & 0x3F) << 8) + code3;
                var count = (code & 0x3F) + 4;
                AppendCopy(result, offset, count, expectedSize);
            }
            else if ((code & 0x20) == 0)
            {
                var code2 = ReadByte(input, "RefPack long-copy offset high byte");
                var code3 = ReadByte(input, "RefPack long-copy offset low byte");
                var code4 = ReadByte(input, "RefPack long-copy length byte");
                AppendLiterals(input, result, code & 3, expectedSize);
                var offset = 1 + (((code & 0x10) >> 4) << 16) + (code2 << 8) + code3;
                var count = (((code & 0x0C) >> 2) << 8) + code4 + 5;
                AppendCopy(result, offset, count, expectedSize);
            }
            else
            {
                var count = (code & 0x1F) * 4 + 4;
                if (count <= 112)
                {
                    AppendLiterals(input, result, count, expectedSize);
                }
                else
                {
                    AppendLiterals(input, result, code & 3, expectedSize);
                    finished = true;
                }
            }
        }

        if (result.Count != expectedSize)
        {
            throw new InvalidDataException(
                $"RefPack output length mismatch: expected {expectedSize}, decoded {result.Count}.");
        }

        output.Write(result.ToArray());
        output.Flush();
        output.BaseStream.Position = 0;
    }

    private static int ReadExpectedSize(BinaryReader input, int maxOutputSize)
    {
        if (maxOutputSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOutputSize));
        }

        var originalPosition = input.BaseStream.Position;
        if (originalPosition < 8 || input.BaseStream.Length - originalPosition < 5)
        {
            throw new InvalidDataException("Truncated RefPack header.");
        }

        input.BaseStream.Position = 4;
        var containerSize = input.ReadInt32();
        input.BaseStream.Position = originalPosition;

        var flags = ReadByte(input, "RefPack flags");
        var signature = ReadByte(input, "RefPack signature");
        if (signature != 0xFB)
        {
            throw new InvalidDataException($"Invalid RefPack signature: 0x{signature:X2}.");
        }

        var sizeByteCount = (flags & 0x80) != 0 ? 4 : 3;
        var encodedSize = 0;
        for (var i = 0; i < sizeByteCount; i++)
        {
            encodedSize = checked((encodedSize << 8) | ReadByte(input, "RefPack output size"));
        }

        if (containerSize <= 0 || encodedSize != containerSize)
        {
            throw new InvalidDataException(
                $"Invalid RefPack output size: container={containerSize}, stream={encodedSize}.");
        }

        if (containerSize > maxOutputSize)
        {
            throw new InvalidDataException(
                $"RefPack output size {containerSize} exceeds the configured limit {maxOutputSize}.");
        }

        return containerSize;
    }

    private static byte ReadByte(BinaryReader input, string field)
    {
        if (input.BaseStream.Position >= input.BaseStream.Length)
        {
            throw new EndOfStreamException($"Unexpected end of stream while reading {field}.");
        }

        return input.ReadByte();
    }

    private static void AppendLiterals(BinaryReader input, List<byte> result, int count, int expectedSize)
    {
        EnsureOutputCapacity(result.Count, count, expectedSize);
        var bytes = input.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException(
                $"Unexpected end of RefPack stream: expected {count} literal bytes, got {bytes.Length}.");
        }

        result.AddRange(bytes);
    }

    private static void AppendCopy(List<byte> result, int offset, int count, int expectedSize)
    {
        if (offset <= 0 || offset > result.Count)
        {
            throw new InvalidDataException(
                $"Invalid RefPack copy offset {offset} at output position {result.Count}.");
        }

        EnsureOutputCapacity(result.Count, count, expectedSize);
        for (var i = 0; i < count; i++)
        {
            result.Add(result[result.Count - offset]);
        }
    }

    private static void EnsureOutputCapacity(int currentSize, int appendCount, int expectedSize)
    {
        if (appendCount < 0 || currentSize > expectedSize - appendCount)
        {
            throw new InvalidDataException(
                $"RefPack command would exceed declared output size {expectedSize}.");
        }
    }
}
