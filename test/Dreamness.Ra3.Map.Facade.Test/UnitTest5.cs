using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest5
{
    [Test]
    public void Team_CloneAndAdd()
    {
        var ra3map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "NewMap6");

        var teams = ra3map.GetTeams();
        foreach (var teamAsset in teams)
        {
            Console.WriteLine(teamAsset.FullName);
        }
        
        Console.WriteLine("-----------------");
        // var teamTemplate = teams.Where(t => t.FullName == "Player_1/template_team").First();
        // for(int i = 1; i <= 5; i ++)
        // {
        //     var newTeam = teamTemplate.Clone();
        //     newTeam.FullName = $"Player_2/Cloned_Team_{i}";
        //     ra3map.AddTeam(newTeam);
        // }
        //
        // // var teams2 = ra3map.GetTeams();
        // // foreach (var teamAsset in teams2)
        // // {
        // //     Console.WriteLine(teamAsset.FullName);
        // // }
        // // //
        //
        // // ra3map.AddTeam("tmp_team", "Player_1");
        // //
        // ra3map.Save();
        //
        
    }
}