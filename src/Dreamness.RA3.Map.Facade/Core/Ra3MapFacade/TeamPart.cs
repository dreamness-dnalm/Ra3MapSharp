using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private TeamsAsset _teamsAsset { get; set; }
    
    /// <summary>
    /// 获取所有队伍
    /// </summary>
    /// <returns></returns>
    public List<TeamAsset> GetTeams()
    {
        return _teamsAsset.TeamList.ToList();
    }

    /// <summary>
    /// 将队伍数据导出为json字符串
    /// </summary>
    /// <returns></returns>
    public string ExportTeamsToJsonStr()
    {
        return ra3Map.Context.ExportTeamsAssetToJson();
    }
    
    /// <summary>
    /// 从json字符串导入队伍数据
    /// </summary>
    /// <param name="jsonStr"></param>
    public void ImportTeamsFromJsonStr(string jsonStr)
    {
        ra3Map.Context.ImportTeamsAssetFromJson(jsonStr);
        LoadTeams();
    }

    /// <summary>
    /// 添加一个队伍
    /// </summary>
    /// <param name="teamName">队伍名(短名)</param>
    /// <param name="owerPlayerName">所有者的玩家名</param>
    /// <returns></returns>
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