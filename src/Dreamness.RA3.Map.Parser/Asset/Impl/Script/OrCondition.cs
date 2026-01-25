using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class OrCondition: BaseAsset
{
    public WritableList<ScriptConditionContent> Conditions = new WritableList<ScriptConditionContent>();
    
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
            // var condition = ScriptConditionContent.FromBinaryReader(binaryReader, context);
            var condition = AssetParser.FromBinaryReader(binaryReader, context) as ScriptConditionContent;
            Conditions.Add(condition,ignoreModified:true);
        }
        ObservableUtil.Subscribe(Conditions, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        return Conditions.ToBytes(context);
    }

    public JsonNode ToJsonNode()
    {
        var jsonArr = new JsonArray();

        foreach (var o in Conditions)
        {
            jsonArr.Add(o.ToJsonNode());
        }
        
        return jsonArr;
    }

    public static OrCondition Empty(BaseContext context)
    {
        var orCondition = new OrCondition();
        orCondition.ApplyBasicInfo(context);
        ObservableUtil.Subscribe(orCondition.Conditions, orCondition);
        return orCondition;
    }
    
    public static OrCondition FromJsonNode(JsonNode jsonNode, BaseContext context)
    {
        var orCondition = Empty(context);

        foreach (var item in jsonNode.AsArray())
        {
            var condition = ScriptConditionContent.FromJsonNode(item, context);
            orCondition.Conditions.Add(condition);
        }
        orCondition.MarkModified();

        return orCondition;
    }
}