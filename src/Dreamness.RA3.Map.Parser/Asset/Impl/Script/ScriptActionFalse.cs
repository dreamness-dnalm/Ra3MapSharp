using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptActionFalse: ScriptAction
{
    public override string GetAssetType()
    {
        return AssetNameConst.ScriptActionFalse;
    }
}