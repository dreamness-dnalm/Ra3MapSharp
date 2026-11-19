using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;


namespace Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;

public class ElevationDim2Array: Dim2WritableArray<float>
{
    private ushort[,]? _originalCodes;

    public ElevationDim2Array(float[,] array) : base(array)
    {
    }

    public void KeepOriginalCodes(ushort[,] codes)
    {
        if (codes.GetLength(0) != Width || codes.GetLength(1) != Height)
            throw new ArgumentException("Elevation code size does not match the height grid.", nameof(codes));
        _originalCodes = codes;
    }

    protected override byte[] ElementToBytes(float element, BaseContext context)
    {
        return BitConverter.GetBytes(StreamExtension.ToSageFloat16(element));
    }

    public override byte[] ToBytes(BaseContext context)
    {
        var memoryStream = new MemoryStream();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var value = this[x, y];
                var encoded = _originalCodes is not null && StreamExtension.FromSageFloat16(_originalCodes[x, y]) == value
                    ? _originalCodes[x, y]
                    : StreamExtension.ToSageFloat16(value);
                memoryStream.Write(BitConverter.GetBytes(encoded));
            }
        }
        memoryStream.Flush();
        return memoryStream.ToArray();
    }
}