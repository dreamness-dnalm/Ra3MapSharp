using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptList: BaseAsset
{
    public WritableList<Script> Scripts { get; } = new WritableList<Script>();

    public WritableList<ScriptGroup> ScriptGroups { get; } = new WritableList<ScriptGroup>();
    
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetName()
    {
        return AssetNameConst.ScriptList;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        while (binaryReader.BaseStream.Position < DataSize)
        {
            var asset = AssetParser.FromBinaryReader(binaryReader, context);
            if (asset is Script script)
            {
                Scripts.Add(script, ignoreModified: true);
            }
            else if (asset is ScriptGroup scriptGroup)
            {
                ScriptGroups.Add(scriptGroup, ignoreModified: true);
            }
            else
            {
                throw new InvalidDataException(
                    $"Unexpected asset type in ScriptList: {asset.GetType().Name}. Expected Script or ScriptGroup.");
            }
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(Scripts.ToBytes(context));
        
        binaryWriter.Write(ScriptGroups.ToBytes(context));
        
        binaryWriter.Flush();
        
        return memoryStream.ToArray();
    }

    public static ScriptList Empty(BaseContext context)
    {
        var asset = new ScriptList();
        asset.ApplyBasicInfo(context);
        
        ObservableUtil.Subscribe(asset.Scripts, asset);
        ObservableUtil.Subscribe(asset.ScriptGroups, asset);
        
        asset.MarkModified();
        return asset;
    }
}