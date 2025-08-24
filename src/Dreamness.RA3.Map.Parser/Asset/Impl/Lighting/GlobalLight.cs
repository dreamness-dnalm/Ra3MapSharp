using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;

public class GlobalLight: Ra3MapWritable
{
    private Vec3D ambient;

    public Vec3D Ambient
    {
        get => ambient;
        set
        {
            if (ambient != value)
            {
                ObservableUtil.Unsubscribe(ambient, this);
                ambient = value;
                ObservableUtil.Subscribe(ambient, this);
                MarkModified();
            }
        }
    }

    private Vec3D color;
    
    public Vec3D Color
    {
        get => color;
        set
        {
            if (color != value)
            {
                ObservableUtil.Unsubscribe(color, this);
                color = value;
                ObservableUtil.Subscribe(color, this);
                MarkModified();
            }
        }
    }

    private Vec3D direction;
    
    public Vec3D Direction
    {
        get => direction;
        set
        {
            if (direction != value)
            {
                ObservableUtil.Unsubscribe(direction, this);
                direction = value;
                ObservableUtil.Subscribe(direction, this);
                MarkModified();
            }
        }
    }
    
    public static GlobalLight FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        

        var ambient = Vec3D.FromBinaryReader(binaryReader);
        binaryWriter.WriteVec3D(ambient, context);

        var color = Vec3D.FromBinaryReader(binaryReader);
        binaryWriter.WriteVec3D(color, context);

        var direction = Vec3D.FromBinaryReader(binaryReader);
        binaryWriter.WriteVec3D(direction, context);
        
        binaryWriter.Flush();
        
        var globalLight = new GlobalLight(ambient, color, direction);
        
        globalLight.Data = memoryStream.ToArray();
        
        return globalLight;
    }
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.WriteVec3D(Ambient, context);
            binaryWriter.WriteVec3D(Color, context);
            binaryWriter.WriteVec3D(Direction, context);
            
            binaryWriter.Flush();
            
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }

    private GlobalLight(Vec3D ambient, Vec3D color, Vec3D direction)
    {
        this.ambient = ambient;
        ObservableUtil.Subscribe(ambient, this);
        this.color = color;
        ObservableUtil.Subscribe(color, this);
        this.direction = direction;
        ObservableUtil.Subscribe(direction, this);
    }
    
    public static GlobalLight Of(Vec3D ambient, Vec3D color, Vec3D direction)
    {
        var globalLight = new GlobalLight(ambient, color, direction);
        globalLight.MarkModified();

        return globalLight;
    }

    public override string ToString()
    {
        return "GlobalLight{" +
               "ambient=" + Ambient +
               ", color=" + Color +
               ", direction=" + Direction +
               '}' + base.ToString();
    }
}