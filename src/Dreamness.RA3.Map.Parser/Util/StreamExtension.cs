using System.Text;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Util;

public static class StreamExtension
{
    static StreamExtension()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        gbkEncoding = Encoding.GetEncoding("GBK");
        utf8Encoding = Encoding.GetEncoding("UTF-8");
    }
    
    private static Encoding gbkEncoding = null;
    private static Encoding utf8Encoding = null;
    
    public static string ReadDefaultString(this BinaryReader br)
    {
        ushort len = br.ReadUInt16();
        // return Encoding.Default.GetString(br.ReadBytes(len));
        return gbkEncoding.GetString(br.ReadBytes(len));
    }
    
    public static string ReadUnicodeString(this BinaryReader br)
    {
        ushort len = br.ReadUInt16();
        return Encoding.Unicode.GetString(br.ReadBytes(len * 2));
    }

    public static string ReadUtf8String(this BinaryReader br)
    {
        ushort len = br.ReadUInt16();
        return utf8Encoding.GetString(br.ReadBytes(len));
    }
    
    public static Vec3D ReadVec3D(this BinaryReader br)
    {
        // return new Vec3D(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        return Vec3D.FromBinaryReader(br);
    }
    
    public static ColorRgbF ReadColorRgbF(this BinaryReader reader)
    {
        return new ColorRgbF(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
    }

    public static void WriteColorRgbF(this BinaryWriter bw, ColorRgbF colorRgbF)
    {
        bw.Write(colorRgbF.R);
        bw.Write(colorRgbF.G);
        bw.Write(colorRgbF.B);
    }
    
    public static void WriteDefaultString(this BinaryWriter bw, string str)
    {
        var bytes = gbkEncoding.GetBytes(str);

        bw.Write((ushort)bytes.Length);
        // bw.Write(Encoding.Default.GetBytes(str));
        bw.Write(bytes);
    }
    
    public static void WriteUnicodeString(this BinaryWriter bw, string str)
    {
        // bw.Write((ushort)str.Length);
        // bw.Write(Encoding.Unicode.GetBytes(str));
        
        var bytes = Encoding.Unicode.GetBytes(str);
        bw.Write((ushort)(bytes.Length / 2));
        bw.Write(bytes);
    }

    public static void WriteUtf8String(this BinaryWriter bw, string str)
    {
        var bytes = utf8Encoding.GetBytes(str);
        bw.Write((ushort)(bytes.Length));
        bw.Write(bytes);
    }
    
    public static void WriteVec3D(this BinaryWriter bw, Vec3D vec3D, BaseContext context)
    {
        // bw.Write(vec3D.X);
        // bw.Write(vec3D.Y);
        // bw.Write(vec3D.Z);
        bw.Write(vec3D.ToBytes(context));
    }
    
    public static int ReadUInt24(this BinaryReader reader)
    {
        var result = 0u;
        for (var i = 0; i < 3; i++)
        {
            result |= ((uint) reader.ReadByte() << (i * 8));
        }
        return (int) result;
    }
    
    public static void WriteUInt24(this BinaryWriter writer, uint value)
    {
        for (var i = 0; i < 3; i++)
        {
            writer.Write((byte) ((value >> (i * 8)) & 0xFF));
        }
    }

    public static float ReadSageFloat16(this BinaryReader br)
    {
        return FromSageFloat16(br.ReadUInt16());
    }
    
    public static ushort WriteSageFloat16(this BinaryWriter bw, float value)
    {
        var encoded = ToSageFloat16(value);
        bw.Write(encoded);
        return encoded;
    }
    
    public static T[,] ReadArray<T>(this BinaryReader br, int width, int height) where T : struct
    {
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

    public static void WriteArray<T>(this BinaryWriter bw, T[,] array) where T : struct
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

    /// <summary>Height represented by one step of the unsigned 16-bit terrain encoding.</summary>
    public const float SageFloat16Scale = 10f / 256f;

    /// <summary>Largest representable terrain height: 2559.9609375.</summary>
    public const float SageFloat16MaxValue = ushort.MaxValue * SageFloat16Scale;

    /// <summary>Encodes a terrain height using nearest-step rounding, with midpoints rounded up.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The height is not finite or is outside the representable range.</exception>
    public static ushort ToSageFloat16(float v)
    {
        if (!float.IsFinite(v) || v < 0f || v > SageFloat16MaxValue)
            throw new ArgumentOutOfRangeException(nameof(v), v,
                $"Terrain height must be finite and between 0 and {SageFloat16MaxValue}.");

        // Use double precision so rounding cannot move a value just below a midpoint above it.
        return (ushort)Math.Round((double)v / SageFloat16Scale, MidpointRounding.AwayFromZero);
    }

    public static float FromSageFloat16(ushort v)
    {
        return v * SageFloat16Scale;
    }
}

