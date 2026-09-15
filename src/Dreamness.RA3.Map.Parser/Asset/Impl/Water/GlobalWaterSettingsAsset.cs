using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Water;
// TODO: ???
public class GlobalWaterSettingsAsset: BaseAsset
{
    private bool reflection;
    
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

    private float reflectionPlaneHeight;
    
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

    public override string GetAssetType()
    {
        return AssetNameConst.GlobalWaterSettings;
    }

    protected override void _Parse(BaseContext context)
    {
        if (Data.Length != 8)
        {
            throw new InvalidDataException($"GlobalWaterSettings must be 8 bytes, but found {Data.Length}.");
        }

        using var stream = new MemoryStream(Data);
        using var reader = new BinaryReader(stream);
        var reflectionValue = reader.ReadInt32();
        if (reflectionValue is not 0 and not 1)
        {
            throw new InvalidDataException($"Invalid reflection flag: {reflectionValue}.");
        }

        reflection = reflectionValue == 1;
        reflectionPlaneHeight = reader.ReadSingle();
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
        asset.Reflection = true;
        asset.ReflectionPlaneHeight = 200;
        asset.MarkModified();
        return asset;
    }
}
