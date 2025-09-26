using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Asset.Base;

namespace Dreamness.Ra3.Map.Facade.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var ra3MapFacade = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "csharp_script_test");

        var baseAsset = ra3MapFacade.ra3Map.Context.AssetDict[AssetNameConst.PlayerScriptsList];
        
        Console.WriteLine(baseAsset);
    }
}