using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Water;

public class StandingWaterArea: Ra3MapWritable
{
    private int id;
    private string name = string.Empty;
    private float uvScrollSpeed;
    private bool additiveBlending;
    private string bumpmapTexture = string.Empty;
    private string skyTexture = string.Empty;
    private int waterHeight;
    private string fxShader = string.Empty;
    private string depthColors = string.Empty;

    public int Id { get => id; set => SetField(ref id, value); }

    public string Name { get => name; set => SetString(ref name, value); }

    public float UVScrollSpeed { get => uvScrollSpeed; set => SetField(ref uvScrollSpeed, value); }

    public bool AdditiveBlending { get => additiveBlending; set => SetField(ref additiveBlending, value); }

    public string BumpmapTexture { get => bumpmapTexture; set => SetString(ref bumpmapTexture, value); }

    public string SkyTexture { get => skyTexture; set => SetString(ref skyTexture, value); }

    public WritableList<Vec2D> Points { get; private set; } = new WritableList<Vec2D>();
    
    public int WaterHeight { get => waterHeight; set => SetField(ref waterHeight, value); }

    public string FxShader { get => fxShader; set => SetString(ref fxShader, value); }

    public string DepthColors { get => depthColors; set => SetString(ref depthColors, value); }
    
    private StandingWaterArea(int id, string name, float uvScrollSpeed, bool additiveBlending, 
        string bumpmapTexture, string skyTexture, WritableList<Vec2D> points, int waterHeight, 
        string fxShader, string depthColors)
    {
        this.id = id;
        this.name = name;
        this.uvScrollSpeed = uvScrollSpeed;
        this.additiveBlending = additiveBlending;
        this.bumpmapTexture = bumpmapTexture;
        this.skyTexture = skyTexture;
        Points = points;
        this.waterHeight = waterHeight;
        this.fxShader = fxShader;
        this.depthColors = depthColors;

        ObservableUtil.Subscribe(Points, this);
    }

    private void SetField<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            MarkModified();
        }
    }

    private void SetString(ref string field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetField(ref field, value);
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
        if (magic != 0)
        {
            throw new InvalidDataException($"Invalid StandingWaterArea marker: {magic}.");
        }
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
        if (pointCount < 0 || pointCount > 1_000_000)
        {
            throw new InvalidDataException($"Invalid standing-water point count: {pointCount}.");
        }
        binaryWriter.Write(pointCount);
        var points = new WritableList<Vec2D>();
        for (int i = 0; i < pointCount; i++)
        {
            points.Add(Vec2D.FromBinaryReader(binaryReader, context), ignoreModified: true);
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
