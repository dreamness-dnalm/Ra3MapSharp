using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;

public class GlobalLightingAsset: BaseAsset
{
    private int time;
    
    public int Time
    {
        get => time;
        set
        {
            if (time != value)
            {
                time = value;
                MarkModified();
            }
        }
    }
    
    public WritableList<GlobalLightingConfig> Configs { get; private set; } = new WritableList<GlobalLightingConfig>();
    
    private MapColorArgb shadowColor;

    public MapColorArgb ShadowColor
    {
        get => shadowColor;
        
        set
        {
            if (shadowColor != value)
            {
                shadowColor = value;
                MarkModified();
            }
        }
    }

    public ColorRgbF noCloudFactor;
    
    public ColorRgbF NoCloudFactor
    {
        get => noCloudFactor;
        set
        {
            if (noCloudFactor != value)
            {
                noCloudFactor = value;
                MarkModified();
            }
        }
    }
    
    public override short GetVersion()
    {
        return 11;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.GlobalLighting;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var reader = new BinaryReader(memoryStream);
        
        time = reader.ReadInt32();
        for (var i = 0; i < 4; i++)
        {
            var config = GlobalLightingConfig.FromBinaryReader(reader, context);
            Configs.Add(config, ignoreModified: true);
        }
        ObservableUtil.Subscribe(Configs, this);
        
        shadowColor = MapColorArgb.FromBinaryReader(reader);

        noCloudFactor = reader.ReadColorRgbF();
        
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(time);
        binaryWriter.Write(Configs.ToBytes(context));
        binaryWriter.Write(ShadowColor.ToBytes(context));
        binaryWriter.WriteColorRgbF(NoCloudFactor);
        
        binaryWriter.Flush();
        
        return memoryStream.ToArray();
    }
    
    public static GlobalLightingAsset Default(BaseContext context)
    {
        var asset = new GlobalLightingAsset();
        asset.ApplyBasicInfo(context);
        
        ObservableUtil.Subscribe(asset.Configs, asset);

        asset.time = 1;
        

        asset.Configs.Add(
            GlobalLightingConfig.Of(
                GlobalLight.Of(new Vec3D(0.1411765f,0.1333333f,0.1254902f), new Vec3D(1.247059f,1.207843f,1.043137f), new Vec3D(-0.6291487f,0.3487428f,-0.6946585f)),
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.6901961f,0.6666667f,0.6901961f), new Vec3D(0.8070462f,0.5863534f,-0.06975648f)),
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.7450981f,0.8313726f,0.8941177f), new Vec3D(0.328771f,-0.9032905f,-0.2756374f))
                
                
                )
            );
        
        asset.Configs.Add(
            GlobalLightingConfig.Of(
                GlobalLight.Of(new Vec3D(0.1568628f,0.1411765f,0.1568628f), new Vec3D(1.247059f,0.9960784f,0.9019608f), new Vec3D(0.4059417f,0.6496425f,-0.6427876f)),
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.345098f,0.6117647f,0.7764706f), new Vec3D(-0.7697511f,-0.5389856f,-0.3420202f)), 
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.4470588f,0.6666667f,0.5803922f), new Vec3D(0.4995398f,-0.5953282f,-0.6293205f))
                
            )
        );
        
        asset.Configs.Add(
            GlobalLightingConfig.Of(
                GlobalLight.Of(new Vec3D(0.1803922f,0.1411765f,0.08627451f), new Vec3D(1.670588f,1.105882f,0.7450981f), new Vec3D(0.6730281f,0.5450075f,-0.5000001f)),
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.3215686f,0.5019608f,0.7764706f), new Vec3D(-0.9395494f,0.01639982f,-0.3420202f)), 
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.4862745f,0.5019608f,0.6666667f), new Vec3D(0.3036552f,-0.7153665f,-0.6293205f))
                
            )
        );
        
        asset.Configs.Add(
            GlobalLightingConfig.Of(
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.9411765f,0.9411765f,1.247059f), new Vec3D(-0.3210197f,0.3210196f,-0.8910066f)),
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.2509804f,0.2509804f,0.3764706f), new Vec3D(0.9781476f,0.2079117f,-4.371139E-08f)), 
                GlobalLight.Of(new Vec3D(0.0f,0.0f,0.0f), new Vec3D(0.2509804f,0.2509804f,0.3764706f), new Vec3D(-0.05233583f,-0.9986296f,-4.371139E-08f)) 
                
            )
        );

        asset.shadowColor = new MapColorArgb(127, 160, 160, 160);
        asset.noCloudFactor = new ColorRgbF(1f, 1f, 1f);
        asset.MarkModified();
        return asset;
    }
}