using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest7
{
    [Test]
    public void DebugScript()
    {
        // test_script_01
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "test_script_01");
        
        // Console.WriteLine(ra3Map);

        var json = ra3Map.ra3Map.Context.PlayerScriptsListAsset.ToJson(ra3Map.ra3Map.Context);
        Console.WriteLine(json);
    }
}