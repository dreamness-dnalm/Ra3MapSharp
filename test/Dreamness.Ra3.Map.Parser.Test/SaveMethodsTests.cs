using Dreamness.RA3.Map.Parser.Core.MapScb;
using Dreamness.Ra3.Map.Parser.Core.ClipBoard;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Test;

public class SaveMethodsTests
{
    [Test]
    public void Ra3MapScb_FromFile_Save_DefaultShouldWriteUncompressedHeader()
    {
        var tempDir = CreateTempDir();
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.scb");
            File.WriteAllBytes(sourcePath, BuildMinimalUncompressedBytes());

            var scb = Ra3MapScb.FromFile(sourcePath);
            scb.Save();

            Assert.That(ReadHeader(sourcePath), Is.EqualTo(CompressConst.UnCompressFlag));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Ra3MapScb_SaveAs_DefaultShouldWriteUncompressedHeader()
    {
        var tempDir = CreateTempDir();
        try
        {
            var scb = Ra3MapScb.FromBytes(BuildMinimalUncompressedBytes());
            var outputPath = Path.Combine(tempDir, "output.scb");

            scb.SaveAs(outputPath);

            Assert.That(ReadHeader(outputPath), Is.EqualTo(CompressConst.UnCompressFlag));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Ra3MapScb_FromBytes_Save_ShouldThrowWhenPathIsNull()
    {
        var scb = Ra3MapScb.FromBytes(BuildMinimalUncompressedBytes());

        var ex = Assert.Throws<System.Exception>(() => scb.Save());

        Assert.That(ex!.Message, Does.Contain("SaveAs method"));
    }

    [Test]
    public void Ra3MapClipboard_FromFile_Save_DefaultShouldWriteUncompressedHeader()
    {
        var tempDir = CreateTempDir();
        try
        {
            var sourcePath = Path.Combine(tempDir, "source.paste");
            File.WriteAllBytes(sourcePath, BuildMinimalUncompressedBytes());

            var clipboard = Ra3MapClipboard.FromFile(sourcePath);
            clipboard.Save();

            Assert.That(ReadHeader(sourcePath), Is.EqualTo(CompressConst.UnCompressFlag));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Ra3MapClipboard_SaveAs_DefaultShouldWriteUncompressedHeader()
    {
        var tempDir = CreateTempDir();
        try
        {
            var clipboard = Ra3MapClipboard.FromBytes(BuildMinimalUncompressedBytes());
            var outputPath = Path.Combine(tempDir, "output.paste");

            clipboard.SaveAs(outputPath);

            Assert.That(ReadHeader(outputPath), Is.EqualTo(CompressConst.UnCompressFlag));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Ra3MapClipboard_FromBytes_Save_ShouldThrowWhenPathIsNull()
    {
        var clipboard = Ra3MapClipboard.FromBytes(BuildMinimalUncompressedBytes());

        var ex = Assert.Throws<System.Exception>(() => clipboard.Save());

        Assert.That(ex!.Message, Does.Contain("SaveAs method"));
    }

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Ra3MapSharp_SaveTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static byte[] BuildMinimalUncompressedBytes()
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        binaryWriter.Write(CompressConst.UnCompressFlag);
        binaryWriter.Write(0);
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    private static uint ReadHeader(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        using var binaryReader = new BinaryReader(fileStream);
        return binaryReader.ReadUInt32();
    }
}
