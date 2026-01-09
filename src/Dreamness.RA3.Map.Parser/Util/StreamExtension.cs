using System.Text;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Util;

public static class StreamExtension
{
    static StreamExtension()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        gbkEncoding = Encoding.GetEncoding("GBK");
    }
    
    private static Encoding gbkEncoding = null;
    
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
        var value = br.ReadUInt16();
        byte upper = (byte)(value >> 8);
        byte lower = (byte)(value & 0xFFu);
        return (float)(int)upper * 10f + (float)(int)lower * 9.96f / 256f;
    }
    
    public static ushort WriteSageFloat16(this BinaryWriter bw, float value)
    {
        byte upper = (byte)((value - value % 10f) / 10f);
        byte lower = (byte)((double)(value % 10f * 256f) / 9.96);
        return (ushort)((upper << 8) | lower);
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

    public static ushort ToSageFloat16(float v)
    {
        byte upper = (byte)((v - v % 10f) / 10f);
        byte lower = (byte)((double)(v % 10f * 256f) / 9.96);
        return (ushort)((upper << 8) | lower);
    }

    public static float FromSageFloat16(ushort v)
    {
        byte upper = (byte)(v >> 8);
        byte lower = (byte)(v & 0xFFu);
        return (float)(int)upper * 10f + (float)(int)lower * 9.96f / 256f;
    }
}

