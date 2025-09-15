using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class PlayerScriptsList: BaseAsset
{
    public WritableList<ScriptList> ScriptLists { get; } = new WritableList<ScriptList>();
    
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.PlayerScriptsList;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        while (binaryReader.BaseStream.Position < DataSize)
        {
            ScriptLists.Add(AssetParser.FromBinaryReader(binaryReader, context) as ScriptList, ignoreModified: true);
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(ScriptLists.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static PlayerScriptsList Default(BaseContext context)
    {
        var asset = new PlayerScriptsList();
        
        asset.ApplyBasicInfo(context);

        for (int i = 0; i < SidesListAsset.DefaultPlayerNames.Length; i++)
        {
            asset.ScriptLists.Add(ScriptList.Empty(context));
        }
        
        ObservableUtil.Subscribe(asset.ScriptLists, asset);
        
        asset.MarkModified();
        
        return asset;
    }
}