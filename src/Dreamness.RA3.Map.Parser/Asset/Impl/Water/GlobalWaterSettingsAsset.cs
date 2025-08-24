using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Water;
// TODO: ???
public class GlobalWaterSettingsAsset: BaseAsset
{
    private bool reflection = true;
    
    public bool Reflection
    {
        get => reflection;
        set
        {
            if (reflection != value)
            {
                reflection = value;
                MarkModified();
            }
        }
    }

    private float reflectionPlaneHeight = 200;
    
    public float ReflectionPlaneHeight
    {
        get => reflectionPlaneHeight;
        set
        {
            if (reflectionPlaneHeight != value)
            {
                reflectionPlaneHeight = value;
                MarkModified();
            }
        }
    }
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetName()
    {
        return AssetNameConst.GlobalWaterSettings;
    }

    protected override void _Parse(BaseContext context)
    {
        // TODO: ???
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(reflection ? 1: 0);
        binaryWriter.Write(reflectionPlaneHeight);
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static GlobalWaterSettingsAsset Default(BaseContext context)
    {
        var asset = new GlobalWaterSettingsAsset();
        
        asset.ApplyBasicInfo(context);
        asset.MarkModified();
        return asset;
    }
}