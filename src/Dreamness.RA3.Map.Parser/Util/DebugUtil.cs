namespace Dreamness.Ra3.Map.Parser.Util;

public class DebugUtil
{
    public static byte[] ReadRange(Stream stream, long start, long end, bool restorePosition = true)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanSeek) throw new NotSupportedException("Stream must support seeking (CanSeek == true).");
        if (!stream.CanRead) throw new NotSupportedException("Stream must support reading (CanRead == true).");
        if (start < 0 || end < 0) throw new ArgumentOutOfRangeException("start/end must be non-negative.");
        if (end < start) throw new ArgumentException("end must be >= start.");

        long originalPos = stream.Position;

        try
        {
            long lenLong = end - start;
            if (lenLong == 0) return Array.Empty<byte>();
            if (lenLong > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(end), "Range too large to fit into a single byte[] ( > 2GB ).");

            int len = (int)lenLong;
            byte[] buffer = new byte[len];

            stream.Position = start;

            int offset = 0;
            while (offset < len)
            {
                int read = stream.Read(buffer, offset, len - offset);
                if (read == 0)
                    throw new EndOfStreamException($"Unexpected EOF while reading range [{start}, {end}).");
                offset += read;
            }

            return buffer;
        }
        finally
        {
            if (restorePosition)
                stream.Position = originalPos;
        }
    }
}