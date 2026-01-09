using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class UnitTest6
{
    [Test]
    public void TeamExportTest()
    {
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "A-00");

        var teamJsonStr = ra3Map.ExportTeamsToJsonStr();


        var ra3Map2 = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "NewMap23");

        ra3Map2.ImportTeamsFromJsonStr(teamJsonStr);
        
        ra3Map2.SaveAs(Ra3PathUtil.RA3MapFolder, "NewMap23_4");


    }

    [Test]
    public void TestWriteStream()
    {
        string str = "天蓝帝国空军支援1";

        using var stream = new MemoryStream();

        var writer = new BinaryWriter(stream);
        
        writer.WriteDefaultString(str);
        writer.Flush();
        

        stream.Position = 0; // 关键：回到开头再读

        using var reader = new BinaryReader(stream);
        var str2 = reader.ReadDefaultString();

        Console.WriteLine(str2);
    }
}