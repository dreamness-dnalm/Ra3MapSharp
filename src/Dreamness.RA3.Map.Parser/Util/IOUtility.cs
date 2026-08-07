using System;
using System.IO;

namespace Dreamness.Ra3.Map.Parser.Util;
public class IOUtility
{
    public static T[,] ReadArray<T>(BinaryReader br, int width, int height) where T : struct
    {
        ValidateArrayDimensions(width, height);
        var expectedBytes = GetSerializedByteCount<T>(width, height);
        if (br.BaseStream.CanSeek && br.BaseStream.Length - br.BaseStream.Position < expectedBytes)
        {
            throw new EndOfStreamException(
                $"Truncated {typeof(T).Name}[{width},{height}] array: expected {expectedBytes} bytes.");
        }

        T[,] array = new T[width, height];
        Type type = typeof(T);
        if (type == typeof(bool))
        {
            byte temp = 0;
            for (int y2 = 0; y2 < height; y2++)
            {
                for (int x2 = 0; x2 < width; x2++)
                {
                    if (x2 % 8 == 0)
                    {
                        temp = br.ReadByte();
                    }
                    array[x2, y2] = (T)(object)((temp & (1 << x2 % 8)) > 0);
                }
            }
        }
        else
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (type == typeof(int))
                    {
                        array[x, y] = (T)(object)br.ReadInt32();
                        continue;
                    }
                    if (type == typeof(short))
                    {
                        array[x, y] = (T)(object)br.ReadInt16();
                        continue;
                    }
                    if (type == typeof(ushort))
                    {
                        array[x, y] = (T)(object)br.ReadUInt16();
                        continue;
                    }
                    if (type == typeof(byte))
                    {
                        array[x, y] = (T)(object)br.ReadByte();
                        continue;
                    }
                    throw new System.Exception($"Type: {type.Name} is not supported for method ReadArray");
                }
            }
        }
        return array;
    }
    
    public static void WriteArray<T>(BinaryWriter bw, T[,] array) where T : struct
    {
        int width = array.GetLength(0);
        int height = array.GetLength(1);
        Type type = typeof(T);
        if (type == typeof(bool))
        {
            byte temp = 0;
            for (int y2 = 0; y2 < height; y2++)
            {
                int x2;
                for (x2 = 0; x2 < width; x2++)
                {
                    temp = (byte)(temp | (byte)((((bool)(object)array[x2, y2]) ? 1 : 0) << x2 % 8));
                    if (x2 % 8 == 7)
                    {
                        bw.Write(temp);
                        temp = 0;
                    }
                }
                if ((x2 - 1) % 8 != 7)
                {
                    bw.Write(temp);
                }
            }
            return;
        }
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (type == typeof(int))
                {
                    bw.Write((int)(object)array[x, y]);
                    continue;
                }
                if (type == typeof(short))
                {
                    bw.Write((short)(object)array[x, y]);
                    continue;
                }
                if (type == typeof(ushort))
                {
                    bw.Write((ushort)(object)array[x, y]);
                    continue;
                }
                if (type == typeof(byte))
                {
                    bw.Write((byte)(object)array[x, y]);
                    continue;
                }
                throw new System.Exception($"Type: {type.Name} is not supported for method WriteArray");
            }
        }
    }

    private static void ValidateArrayDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > 16_384 || height > 16_384)
        {
            throw new InvalidDataException($"Invalid array dimensions: {width} x {height}.");
        }

        var cells = (long)width * height;
        if (cells > 64_000_000)
        {
            throw new InvalidDataException($"Array contains too many elements: {cells}.");
        }
    }

    private static long GetSerializedByteCount<T>(int width, int height) where T : struct
    {
        if (typeof(T) == typeof(bool))
        {
            return ((long)width + 7) / 8 * height;
        }

        var elementSize = typeof(T) == typeof(byte) ? 1
            : typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ? 2
            : typeof(T) == typeof(int) ? 4
            : throw new NotSupportedException($"Type {typeof(T).Name} is not supported by ReadArray.");
        return (long)width * height * elementSize;
    }
}
