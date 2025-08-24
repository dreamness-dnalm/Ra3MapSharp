using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

public class BuildListsAsset: BaseAsset
{
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetName()
    {
        return AssetNameConst.BuildLists;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        // 写Data到文件
        using var fileStream = new FileStream(@"N:\Program Files (x86)\Red Alert 3(Incomplete)\RA3MapAnalyse\buildLists1.bin", FileMode.Create, FileAccess.Write);
        using var binaryWriter = new BinaryWriter(fileStream);
        binaryWriter.Write(Data);
        binaryWriter.Flush();
        binaryWriter.Close();
        fileStream.Close();
        
        

        var readInt32 = binaryReader.ReadInt32();
        Console.WriteLine($"ReadInt32: {readInt32}");
        var i2 = binaryReader.ReadDefaultString();
        Console.WriteLine($"ReadInt32: {i2}");
        
    }

    protected override byte[] Deparse(BaseContext context)
    {
        throw new NotImplementedException();
    }
}