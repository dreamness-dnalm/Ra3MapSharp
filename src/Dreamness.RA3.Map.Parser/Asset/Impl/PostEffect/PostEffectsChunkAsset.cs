using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;

public class PostEffectsChunkAsset: BaseAsset
{
    public WritableList<PostEffect> PostEffects = new WritableList<PostEffect>();
    
    public override short GetVersion()
    {
        return 2;
    }

    public override string GetName()
    {
        return AssetNameConst.PostEffectsChunk;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        int effectCount = binaryReader.ReadInt32();
        for (int i = 0; i < effectCount; i++)
        {
            PostEffects.Add(PostEffect.FromBinaryReader(binaryReader, context), ignoreModified:true);
        }
        ObservableUtil.Subscribe(PostEffects, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(PostEffects.Count);
        binaryWriter.Write(PostEffects.ToBytes(context));
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static PostEffectsChunkAsset Default(BaseContext context)
    {
        var asset = new PostEffectsChunkAsset();
        
        asset.ApplyBasicInfo(context);
        
        asset.PostEffects.Add(PostEffect.DistortionInstance());
        
        asset.MarkModified();
        
        return asset;
    }
}