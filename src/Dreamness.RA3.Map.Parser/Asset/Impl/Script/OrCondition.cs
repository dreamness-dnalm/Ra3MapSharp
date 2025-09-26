using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class OrCondition: BaseAsset
{
    public WritableList<ScriptCondition> Conditions = new WritableList<ScriptCondition>();
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.OrCondition;
    }

    protected override void _Parse(BaseContext context)
    {
        var memoryStream = new MemoryStream(Data);
        var binaryReader = new BinaryReader(memoryStream);

        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var condition = ScriptCondition.FromBinaryReader(binaryReader, context);
            Conditions.Add(condition,ignoreModified:true);
        }
        ObservableUtil.Subscribe(Conditions, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        return Conditions.ToBytes(context);
    }
}