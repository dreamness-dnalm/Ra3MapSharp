using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private TeamsAsset _teamsAsset { get; set; }
    
    
    public WritableList<TeamAsset> GetTeams()
    {
        return _teamsAsset.TeamList;
    }

    public string ExportTeamsToJsonStr()
    {
        return ra3Map.Context.ExportTeamsAssetToJson();
    }
    
    public void ImportTeamsFromJsonStr(string jsonStr)
    {
        ra3Map.Context.ImportTeamsAssetFromJson(jsonStr);
        LoadTeams();
    }
    
    
    // ---- init ----
    
    private void LoadTeams()
    {
        _teamsAsset = ra3Map.Context.TeamsAsset;
    }
}