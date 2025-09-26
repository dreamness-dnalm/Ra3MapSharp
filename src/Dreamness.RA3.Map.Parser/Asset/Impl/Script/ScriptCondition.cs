using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptCondition: Ra3MapWritable
{
    private ScriptConditionContent _conditionContent;

    // private bool isInverted;
    //
    // public bool IsInverted
    // {
    //     get => isInverted;
    //     set
    //     {
    //         if (isInverted != value)
    //         {
    //             isInverted = value;
    //             MarkModified();
    //         }
    //     }
    // }
    
    
    public static ScriptCondition FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var asset = new ScriptCondition();
        var contentAsset = AssetParser.FromBinaryReader(binaryReader, context);
        asset._conditionContent = contentAsset as ScriptConditionContent;
        ObservableUtil.Subscribe(asset._conditionContent, asset);
        binaryWriter.Write(asset._conditionContent.ToBytes(context));
        
        // asset.IsInverted = binaryReader.ReadInt32() == 1;
        // binaryWriter.Write(asset.IsInverted? 1: 0);
        
        binaryWriter.Flush();
        asset.Data = memoryStream.ToArray();
        return asset;
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.Write(_conditionContent.ToBytes(context));
            // binaryWriter.Write(IsInverted? 1: 0);
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
}