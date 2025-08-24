using Dreamness.Ra3.Map.Parser.Core.Map;

namespace Dreamness.Ra3.Map.Parser.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var ra3Map1 = Ra3Map.Open(@"C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\seal_test_mix\seal_test_mix.map");
        var ra3Map2 = Ra3Map.Open(@"C:\Users\mmmmm\AppData\Roaming\Red Alert 3\Maps\seal_test_mix2\seal_test_mix2.map");

        // var asetKeys = ra3Map1.Context.AssetDict.Keys.ToList();

        // foreach (var key in asetKeys)
        // {
        //     var v1Size = ra3Map1.Context.AssetDict[key].DataSize;
        //     var v2Size = ra3Map2.Context.AssetDict[key].DataSize;
        //     // var v1Size = 0;
        //     if (v1Size != v2Size)
        //     {
        //         Console.WriteLine($"{key} - {v1Size} - {v2Size}");
        //     }
        //     
        // }

        // var playerDataList1 = ra3Map1.Context.SideListAsset.PlayerDataList;
        // var playerDataList2 = ra3Map2.Context.SideListAsset.PlayerDataList;

        var d1 = ra3Map1.Context.SideListAsset;
        var d2 = ra3Map2.Context.SideListAsset;
        
        Console.WriteLine("------------ d1 -----------");
        Console.WriteLine(d1.Data);


        // Console.WriteLine("playerCnt: " + playerDataList1.Count + " - " + playerDataList2.Count);
        Console.WriteLine(0);
        // for(int)
    }
}