using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;

public class GlobalLightingConfig: Ra3MapWritable
{
    private GlobalLight terrainSun;
    
    public GlobalLight TerrainSun
    {
        get => terrainSun;
        set
        {
            if (terrainSun != value)
            {
                ObservableUtil.Unsubscribe(terrainSun, this);
                terrainSun = value;
                ObservableUtil.Subscribe(terrainSun, this);
                MarkModified();
            }
        }
    }

    private GlobalLight terrainAccent1;
    
    public GlobalLight TerrainAccent1
    {
        get => terrainAccent1;
        set
        {
            if (terrainAccent1 != value)
            {
                ObservableUtil.Unsubscribe(terrainAccent1, this);
                terrainAccent1 = value;
                ObservableUtil.Subscribe(terrainAccent1, this);
                MarkModified();
            }
        }
    }
    
    private GlobalLight terrainAccent2;
    
    public GlobalLight TerrainAccent2
    {
        get => terrainAccent2;
        set
        {
            if (terrainAccent2 != value)
            {
                ObservableUtil.Unsubscribe(terrainAccent2, this);
                terrainAccent2 = value;
                ObservableUtil.Subscribe(terrainAccent2, this);
                MarkModified();
            }
        }
    }

    private GlobalLightingConfig(GlobalLight terrainSun, GlobalLight terrainAccent1, GlobalLight terrainAccent2)
    {
        this.terrainSun = terrainSun;
        ObservableUtil.Subscribe(terrainSun, this);
        this.terrainAccent1 = terrainAccent1;
        ObservableUtil.Subscribe(terrainAccent1, this);
        this.terrainAccent2 = terrainAccent2;
        ObservableUtil.Subscribe(terrainAccent2, this);
    }

    public static GlobalLightingConfig Of(GlobalLight terrainSun, GlobalLight terrainAccent1,
        GlobalLight terrainAccent2)
    {
        var globalLightingConfig = new GlobalLightingConfig(terrainSun, terrainAccent1, terrainAccent2);
        globalLightingConfig.MarkModified();
        return globalLightingConfig;
    }
    
    public static GlobalLightingConfig FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var terrainSun = GlobalLight.FromBinaryReader(binaryReader, context);
        binaryWriter.Write(terrainSun.ToBytes(context));
        
        var terrainAccent1 = GlobalLight.FromBinaryReader(binaryReader, context);
        binaryWriter.Write(terrainAccent1.ToBytes(context));
        
        var terrainAccent2 = GlobalLight.FromBinaryReader(binaryReader, context);
        binaryWriter.Write(terrainAccent2.ToBytes(context));
        
        var config = new GlobalLightingConfig(terrainSun, terrainAccent1, terrainAccent2);
        
        binaryWriter.Flush();
        config.Data = memoryStream.ToArray();
        
        return config;
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(TerrainSun.ToBytes(context));
            binaryWriter.Write(TerrainAccent1.ToBytes(context));
            binaryWriter.Write(TerrainAccent2.ToBytes(context));
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }

    public override string ToString()
    {
        return $"GlobalLightingConfig(TerrainSun={TerrainSun}, TerrainAccent1={TerrainAccent1}, TerrainAccent2={TerrainAccent2})";
    }
}