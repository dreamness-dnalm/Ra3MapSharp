using System.Security.Cryptography;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;

public class BlendInfo: Ra3MapWritable
{
    private int secondaryTextureTile;

    public int SecondaryTextureTile
    {
        get { return secondaryTextureTile; }
        set
        {
            if (secondaryTextureTile != value)
            {
                secondaryTextureTile = value;
                MarkModified();
            }
        }
    }
    
    private BlendDirectionEnum blendDirection;
    
    public BlendDirectionEnum BlendDirection
    {
        get { return blendDirection; }
        set
        {
            if (blendDirection != value)
            {
                blendDirection = value;
                MarkModified();
            }
        }
    }

    private uint i3;
    
    public uint I3
    {
        get { return i3; }
        set
        {
            if (i3 != value)
            {
                i3 = value;
                MarkModified();
            }
        }
    }
    
    private uint i4;
    
    public uint I4
    {
        get { return i4; }
        set
        {
            if (i4 != value)
            {
                i4 = value;
                MarkModified();
            }
        }
    }


    
    private BlendInfo(int secondaryTextureTile, uint i3, uint i4, BlendDirectionEnum blendDirection)
    {
        SecondaryTextureTile = secondaryTextureTile;
        I3 = i3;
        I4 = i4;
        BlendDirection = blendDirection;
    }
    
    public static BlendInfo FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var secondaryTextureTile = binaryReader.ReadInt32();
        binaryWriter.Write(secondaryTextureTile);

        var originBlendDirection = binaryReader.ReadBytes(6);
        var blendDirection = ToBlendDirection(originBlendDirection);
        binaryWriter.Write(originBlendDirection);

        var i3 = binaryReader.ReadUInt32();
        binaryWriter.Write(i3);
        var i4 = binaryReader.ReadUInt32();
        binaryWriter.Write(i4);


        var blendInfo = new BlendInfo(secondaryTextureTile, i3, i4, blendDirection);
        blendInfo.Data = memoryStream.ToArray();

        return blendInfo;
    }

    /// <summary>
    /// Creates a new BlendInfo instance.
    /// </summary>
    /// <param name="secondaryTextureTile">Secondary texture tile index</param>
    /// <param name="direction">Blend direction flags</param>
    /// <param name="i3">Magic number field (must be 0xFFFFFFFF)</param>
    /// <param name="i4">Magic number field (must be 0x7ACDCD00)</param>
    /// <returns>New BlendInfo instance</returns>
    public static BlendInfo Create(int secondaryTextureTile,
                                   BlendDirectionEnum direction,
                                   uint i3 = uint.MaxValue,
                                   uint i4 = 2061107200U)
    {
        var blendInfo = new BlendInfo(secondaryTextureTile, i3, i4, direction);
        blendInfo.MarkModified();
        return blendInfo;
    }
    
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.Write(SecondaryTextureTile);
            binaryWriter.Write(FromBlendDirection(BlendDirection));
            binaryWriter.Write(I3);
            binaryWriter.Write(I4);
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
    
    
    private static BlendDirectionEnum ToBlendDirection(byte[] bytes)
    {
        int x = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 1)
            {
                x |= 1 << i;
            }
        }
        return (BlendDirectionEnum)x;
    }
        
    public static byte[] FromBlendDirection(BlendDirectionEnum bd)
    {
        byte[] bytes = new byte[6];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (((uint)bd & (uint)(1 << i)) != 0)
            {
                bytes[i] = 1;
            }
        }
        return bytes;
    }
    
    
    [Flags]
    public enum BlendDirectionEnum : byte
    {
        BottomRight = 0x28,
        Bottom = 0x2,
        BottomLeft = 0x24,
        Right = 0x11,
        Left = 0x1,
        TopRight = 0x38,
        Top = 0x12,
        TopLeft = 0x34,
        ExceptBottomRight = 0x14,
        ExceptBottomLeft = 0x18,
        ExceptTopRight = 0x4,
        ExceptTopLeft = 0x8
    }
}