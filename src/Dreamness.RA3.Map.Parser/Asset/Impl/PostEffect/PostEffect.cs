using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Asset.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;

public class PostEffect: Ra3MapWritable
{
    public string Name { get; private set; }
    
    public WritableList<PostEffectParameter> Parameters { get; private set; } = new WritableList<PostEffectParameter>();
    
    private PostEffect(string name, WritableList<PostEffectParameter> parameters)
    {
        Name = name;
        Parameters = parameters;
        
        ObservableUtil.Subscribe(Parameters, this);
    }
    
    public static PostEffect Of(string name, PostEffectParameter[] parameters)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameters);
        var postEffectParameters = new WritableList<PostEffectParameter>();
        foreach (var parameter in parameters)
        {
            postEffectParameters.Add(parameter);
        }

        var postEffect = new PostEffect(name, postEffectParameters);
        postEffect.MarkModified();
        return postEffect;
    }

    public static PostEffect DistortionInstance()
    {
        return Of("Distortion", new PostEffectParameter[] { });
    }
    
    public static PostEffect LookupTableInstance(string textureName, float value)
    {
        return Of("LookupTable", new PostEffectParameter[]
        {
            PostEffectParameter.Of("BlendFactor", "Float4", new float[] { value, 0f, 0f, 0f}),
            PostEffectParameter.Of("LookupTexture", "Texture", textureName)
        });
    }

    public static PostEffect BloomInstance(float value)
    {
        return Of("Bloom", new PostEffectParameter[]
        {
            PostEffectParameter.Of("Intensity", "Float", value)
        });
    }
    
    public static PostEffect FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        var start = binaryReader.BaseStream.Position;
        var name = AssetReadUtility.ReadString(binaryReader);
        var parameterCount = AssetReadUtility.ReadCount(binaryReader, 4);
        
        var parameters = new WritableList<PostEffectParameter>();
        for (var i = 0; i < parameterCount; i++)
        {
            parameters.Add(PostEffectParameter.FromBinaryReader(binaryReader, context), ignoreModified: true);
        }
        
        var end = binaryReader.BaseStream.Position;
        binaryReader.BaseStream.Position = start;
        var effect = new PostEffect(name, parameters) { Data = binaryReader.ReadBytes(checked((int)(end - start))) };
        return effect;
    }
    
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            AssetReadUtility.WriteString(binaryWriter, Name);
            binaryWriter.Write(Parameters.Count);
            binaryWriter.Write(Parameters.ToBytes(context));
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
}
