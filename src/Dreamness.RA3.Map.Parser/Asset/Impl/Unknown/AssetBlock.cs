using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

public class AssetBlock: Ra3MapWritable
{


    private uint typeId;
    
    public uint TypeId
    {
        get => typeId;
    }

    private uint instanceId;

    public uint InstanceId
    {
        get => instanceId;
    }
    
    private AssetBlock(){}
    
    public static AssetBlock Of(uint typeId, uint instanceId)
    {
        var block = new AssetBlock
        {
            typeId = typeId,
            instanceId = instanceId
        };
        block.MarkModified();
        return block;
    }


    public static AssetBlock FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var typeId = binaryReader.ReadUInt32();
        binaryWriter.Write(typeId);

        var instanceId = binaryReader.ReadUInt32();
        binaryWriter.Write(instanceId);

        var block = new AssetBlock();
        block.typeId = typeId;
        block.instanceId = instanceId;
        
        binaryWriter.Flush();
        block.Data = memoryStream.ToArray();
        
        return block;
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(typeId);
            binaryWriter.Write(instanceId);
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
    
    
}