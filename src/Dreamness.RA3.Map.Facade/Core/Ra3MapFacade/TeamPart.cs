using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private TeamsAsset _teamsAsset { get; set; }
    
    
    public List<TeamAsset> GetTeams()
    {
        return _teamsAsset.TeamList.ToList();
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

    public TeamAsset AddTeam(string teamName, string owerPlayerName)
    {
        var teamAsset = TeamAsset.Of(teamName, owerPlayerName, ra3Map.Context);
        _teamsAsset.TeamList.Add(teamAsset);
        return teamAsset;
    }
    
    
    // ---- init ----
    
    private void LoadTeams()
    {
        _teamsAsset = ra3Map.Context.TeamsAsset;
    }
}