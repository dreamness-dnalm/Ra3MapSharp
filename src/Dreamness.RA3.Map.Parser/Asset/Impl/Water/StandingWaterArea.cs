using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Water;

public class StandingWaterArea: Ra3MapWritable
{
    public int Id { get; private set; }
    
    public string Name { get; private set; }
    
    public float UVScrollSpeed { get; private set; }
    
    public bool AdditiveBlending { get; private set; }
    
    public string BumpmapTexture { get; private set; }
    
    public string SkyTexture { get; private set; }

    public WritableList<Vec2D> Points { get; private set; } = new WritableList<Vec2D>();
    
    public int WaterHeight { get; private set; }
    
    public string FxShader { get; private set; }
    
    public string DepthColors { get; private set; }
    
    private StandingWaterArea(int id, string name, float uvScrollSpeed, bool additiveBlending, 
        string bumpmapTexture, string skyTexture, WritableList<Vec2D> points, int waterHeight, 
        string fxShader, string depthColors)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        
        Id = id;
        Name = name;
        UVScrollSpeed = uvScrollSpeed;
        AdditiveBlending = additiveBlending;
        BumpmapTexture = bumpmapTexture;
        SkyTexture = skyTexture;
        Points = points;
        WaterHeight = waterHeight;
        FxShader = fxShader;
        DepthColors = depthColors;

        ObservableUtil.Subscribe(Points, this);
    }
    
    public static StandingWaterArea FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var id = binaryReader.ReadInt32();
        binaryWriter.Write(id);
        var name = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(name);
        var magic = binaryReader.ReadInt16();
        binaryWriter.Write(magic);
        var uvScrollSpeed = binaryReader.ReadSingle();
        binaryWriter.Write(uvScrollSpeed);
        var additiveBlending = binaryReader.ReadBoolean();
        binaryWriter.Write(additiveBlending);
        var bumpmapTexture = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(bumpmapTexture);
        var skyTexture = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(skyTexture);

        // Read points
        var pointCount = binaryReader.ReadInt32();
        binaryWriter.Write(pointCount);
        var points = new WritableList<Vec2D>();
        for (int i = 0; i < pointCount; i++)
        {
            points.Add(Vec2D.FromBinaryReader(binaryReader, context));
        }
        binaryWriter.Write(points.ToBytes(context));

        var waterHeight = binaryReader.ReadInt32();
        binaryWriter.Write(waterHeight);
        var fxShader = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(fxShader);
        var depthColors = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(depthColors);

        var standingWaterArea = new StandingWaterArea(id, name, uvScrollSpeed, additiveBlending, bumpmapTexture, skyTexture, 
            points, waterHeight, fxShader, depthColors);
        
        binaryWriter.Flush();
        standingWaterArea.Data = memoryStream.ToArray();
        
        return standingWaterArea;
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);

            binaryWriter.Write(Id);
            binaryWriter.WriteDefaultString(Name);
            binaryWriter.Write((short)0);
            binaryWriter.Write(UVScrollSpeed);
            binaryWriter.Write(AdditiveBlending);
            binaryWriter.WriteDefaultString(BumpmapTexture);
            binaryWriter.WriteDefaultString(SkyTexture);

            // Write points
            binaryWriter.Write(Points.Count);
            binaryWriter.Write(Points.ToBytes(context));

            binaryWriter.Write(WaterHeight);
            binaryWriter.WriteDefaultString(FxShader);
            binaryWriter.WriteDefaultString(DepthColors);

            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
    
    public static StandingWaterArea Of(int id, string name, float uvScrollSpeed, 
        Vec2D[] points, int waterHeight, bool additiveBlending=false, string bumpmapTexture="WaterRippleBump", string skyTexture="SkyEnv", 
        string fxShader = "FXOceanRA3", string depthColors = "LUTDepthTint.tga")
    {
        var pointList = new WritableList<Vec2D>();
        for (int i = 0; i < points.Length; i++)
        {
            pointList.Add(points[i]);
        }

        var standingWaterArea = new StandingWaterArea(id, name, uvScrollSpeed, additiveBlending, bumpmapTexture, skyTexture,
            pointList, waterHeight, fxShader, depthColors);
        standingWaterArea.MarkModified();
        return standingWaterArea;
    }
}