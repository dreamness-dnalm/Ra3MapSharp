using Dreamness.Ra3.Map.Parser.Core.Map;

namespace Dreamness.Ra3.Map.Parser.Test;

public class UnitTest2
{

    public string source = @"C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\seal_test\seal_test.map";
    public string source2 = @"C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\test_unseal_1\test_unseal_1.map";
    public string target = @"C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\seal_test_mix2\seal_test_mix2.map";
    public string newMapPath = @"C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\n\n.map";
    
    
    [Test]
    public void mod()
    {
        // --source "C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\seal_test\seal_test.map" --target "C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\seal_test_mix2\seal_test_mix2.map"

        var ra3Map = Ra3Map.Open(source);
        
        ra3Map.Context.TeamsAsset.MarkModified();
        
        ra3Map.SaveAs(target);
    }


    [Test]
    public void check()
    {
        var ra3Map2 = Ra3Map.Open(target);

        var assetDictKeys = ra3Map2.Context.AssetDict.Keys;

        foreach (var assetKey in assetDictKeys)
        {
            Console.WriteLine($"{assetKey} - {ra3Map2.Context.AssetDict[assetKey].Errored}");
        }

        Console.WriteLine(0);
    }

    [Test]
    public void Check2()
    {
        var ra3Map = Ra3Map.Open(source2);

        // var teams = ra3Map.Context.TeamsAsset;
        // var bytes = teams.ToBytes(ra3Map.Context);
        // teams.MarkModified();
        // var bytes2 = teams.ToBytes(ra3Map.Context);
        // Console.WriteLine(bytes.Length + " - " + bytes2.Length);
        
        for(int i = 0; i < ra3Map.Context.TeamsAsset.TeamList.Count; i++)
        {
            var team = ra3Map.Context.TeamsAsset.TeamList[i];
            var bytes = team.ToBytes(ra3Map.Context);
            team.MarkModified();
            var bytes2 = team.ToBytes(ra3Map.Context);
            Console.WriteLine(i + " - " + bytes.Length + " - " + bytes2.Length);
        }
        // var team = ra3Map.Context.TeamsAsset.TeamList[];
        // var bytes = team.ToBytes(ra3Map.Context);
        // team.MarkModified();
        // var bytes2 = team.ToBytes(ra3Map.Context);
        // // var assetProperties = team.Properties;
        // //
        // // var bytes = assetProperties.ToBytes(ra3Map.Context);
        // // assetProperties.MarkModified();
        // // var bytes2 = assetProperties.ToBytes(ra3Map.Context);
        //
        // Console.WriteLine(bytes.Length + " - " + bytes2.Length);
        
    }

    [Test]
    public void newMap()
    {
        var ra3Map = Ra3Map.NewMap(40, 40, 0);
        
        var teams = ra3Map.Context.TeamsAsset;
        var bytes = teams.ToBytes(ra3Map.Context);
        teams.MarkModified();
        var bytes2 = teams.ToBytes(ra3Map.Context);
        Console.WriteLine(bytes.Length + " - " + bytes2.Length);
        
        ra3Map.SaveAs(newMapPath);
    }

    [Test]
    public void checkNewMap()
    {
        var ra3MapN = Ra3Map.NewMap(40, 40, 0);
        
        var ra3Map = Ra3Map.Open(newMapPath);
        
        // var teams = ra3Map.Context.TeamsAsset;
        // var bytes = teams.ToBytes(ra3Map.Context);
        // teams.MarkModified();
        // var bytes2 = teams.ToBytes(ra3Map.Context);
        //
        // for (int i = 0; i < bytes2.Length; i++)
        // {
        //     Console.WriteLine(i + ": " + bytes[i] + " - "  + bytes2[i]);
        // }
        //
        // Console.WriteLine("---------------");
        //
        // Console.WriteLine(bytes.Length + " - " + bytes2.Length);
        
        for(int i = 0; i < ra3Map.Context.TeamsAsset.TeamList.Count; i++)
        {
            var team = ra3Map.Context.TeamsAsset.TeamList[i].Properties;
            var bytes = team.ToBytes(ra3Map.Context); 
            team.MarkModified();
            var bytes2 = team.ToBytes(ra3Map.Context);
            var bytes3 = ra3MapN.Context.TeamsAsset.TeamList[i].Properties.ToBytes(ra3Map.Context);
            Console.WriteLine(i + " - " + bytes.Length + " - " + bytes2.Length + " - " + bytes3.Length);
        }
    }
}