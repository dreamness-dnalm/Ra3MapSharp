using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class ScriptsSerTest
{
    [Test]
    public void TestExport()
    {
        var mapName = "[cor_samsara]ep17S_SANDSTORM_demo1";
        // var mapName = "test02";
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);

        var str = ra3Map.ExportPlayerScriptsListToJsonStr();
        Console.WriteLine(str);
        var ra3Map2 = Ra3MapFacade.NewMap(400, 400, 0);
        ra3Map2.ImportPlayerScriptsListFromJsonStr(str);
        //
        ra3Map2.SaveAs(Ra3PathUtil.RA3MapFolder, "ScriptTest_03");
    }

    [Test]
    public void check()
    {
        var mapName = "ScriptTest_03";
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, mapName);
        Console.WriteLine(ra3Map.ExportPlayerScriptsListToJsonStr());
    }
}