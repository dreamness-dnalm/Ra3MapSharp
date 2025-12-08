using System.Text;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest3
{
    [Test]
    public void DeleteObj()
    {
        var mapName = "the_train_0_1_dev";
        
        var map1 = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "the_train_0_1_dev");
        var map2 = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "the_train_0_1_dev_2");

        // Console.WriteLine("map1 asset cnt: " + map1.ra3Map.Context.AssetDict.Count);
        // Console.WriteLine("map2 asset cnt: " + map1.ra3Map.Context.AssetDict.Count);

        var map1_assetListAsset = map1.ra3Map.Context.AssetDict[AssetNameConst.AssetList] as AssetListAsset;
        var map2_assetListAsset = map2.ra3Map.Context.AssetDict[AssetNameConst.AssetList] as AssetListAsset;

        var map2Objs = map2.GetUnitObjects();

        foreach (var map2Obj in map2Objs)
        {
            if (map2Obj.UniqueId.StartsWith("AlliedPowerPlant"))
            {
                continue;
            }
            Console.WriteLine(map2Obj.UniqueId + "|" + map2Obj.Position);
            
        }


        // foreach (var assetName in map2.ra3Map.Context.AssetDict.Keys)
        // {
        //     // Console.WriteLine("------------ assetName: " + assetName + " -----------------");
        //     var map1Data = map1.ra3Map.Context.AssetDict[assetName].Data;
        //     var map2Data = map2.ra3Map.Context.AssetDict[assetName].Data;
        //     // Console.WriteLine("map1DataLen: " + map1Data.Length);
        //     // Console.WriteLine("map2DataLen: " + map2Data.Length);
        //     if (map1Data.Length != map2Data.Length)
        //     {
        //         Console.WriteLine("Data Length Different: " + assetName + ", map1 length: " + map1Data.Length + ", map2 length: " + map2Data.Length);
        //         continue;
        //     }
        // }

        // foreach (var o in unitObjectWraps)
        // {
        //     map.Remove(o);
        // }

        // map.AddUnitObject("AlliedPowerPlant", 200, 200);

        // map.Remove(map.GetPlayer("Player_1"));
        //
        // map.SaveAs(Ra3PathUtil.RA3MapFolder, mapName + "_2");
    }

    [Test]
    public void t2()
    {
        uint a = 3516764358;

        var aBytes = BitConverter.GetBytes(a);
        
        Console.WriteLine(Encoding.GetEncoding("GBK").GetString(aBytes));
    }
}