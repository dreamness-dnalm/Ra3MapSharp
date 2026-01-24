using Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private PlayerScriptsList _playerScriptsList;
    
    public string ExportPlayerScriptsListToJsonStr()
    {
        return ra3Map.Context.ExportPlayerScriptsListToJson();
    }
    
    public void ImportPlayerScriptsListFromJsonStr(string jsonStr)
    {
        ra3Map.Context.ImportPlayerScriptsListFromJson(jsonStr);
        LoadScriptPart();
    }
    
    
    // -------- init --------
    private void LoadScriptPart()
    {
        _playerScriptsList = ra3Map.Context.PlayerScriptsList;
    }
}