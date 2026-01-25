using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class ScriptsSerTest
{
    [Test]
    public void TestExport()
    {
        var mapName = "NewMap203";
        // var mapName = "test02";
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);

        var str = ra3Map.ExportPlayerScriptsListToJsonStr();
        Console.WriteLine(str);
        var ra3Map2 = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "NewMap202");
        ra3Map2.ImportPlayerScriptsListFromJsonStr(str);
        //
        ra3Map2.SaveAs(Ra3PathUtil.RA3MapFolder, "ScriptTest_06");
    }

    [Test]
    public void check()
    {
        var mapName = "ScriptTest_06";
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);
        Console.WriteLine(ra3Map.ExportPlayerScriptsListToJsonStr());
    }

    [Test]
    public void test3()
    {
        var mapName = "[cor_samsara]ep11J_Kamikayama_demo1";
        // var mapName = "NewMap210";
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);

        var str = ra3Map.ExportPlayerScriptsListToJsonStr();
        Console.WriteLine(str);
        ra3Map.ImportPlayerScriptsListFromJsonStr(str);
        // //
        ra3Map.SaveAs(Ra3PathUtil.RA3MapFolder, "ScriptTest_06");
    }
    
    [Test]
    public void test4()
    {
        var mapName = "NewMap220";
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);

        var str = ra3Map.ExportPlayerScriptsListToJsonStr();
        Console.WriteLine(str);
        ra3Map.ImportPlayerScriptsListFromJsonStr(str);
        // //
        ra3Map.SaveAs(Ra3PathUtil.RA3MapFolder, "ScriptTest_06");
    }
    
    [Test]
    public void test5()
    {
        var ra3Map1 = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "NewMap220");
        var ra3Map2 = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "ScriptTest_06");


        var scriptList1 = ra3Map1.ra3Map.Context.PlayerScriptsListAsset.ScriptLists[0];
        var scriptList2 = ra3Map2.ra3Map.Context.PlayerScriptsListAsset.ScriptLists[0];
        
        Console.WriteLine("datasize: {0} vs {1}", scriptList1.DataSize, scriptList2.DataSize);

        for (int i = 0; i < scriptList2.DataSize; i++)
        {
            Console.WriteLine($"{i}th: {scriptList1.Data[i]} vs {scriptList2.Data[i]}");
        }
        
    }
    
}