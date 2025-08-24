namespace Dreamness.Ra3.Map.Parser.Util;

public class ColorRgbF
{
    public readonly float R;
    public readonly float G;
    public readonly float B;

    public ColorRgbF(float r, float g, float b)
    {
        R = r;
        G = g;
        B = b;
    }

    public Vec3D ToVector3()
    {
        return new Vec3D(R, G, B);
    }
    
    // public static ColorRgbF FromBinaryReader(BinaryReader reader)
    // {
    //     return reader.ReadColorRgbF();
    // }

    public override string ToString()
    {
        return $"ColorRgbF(R: {R:F2}, G: {G:F2}, B: {B:F2})";
    }
}