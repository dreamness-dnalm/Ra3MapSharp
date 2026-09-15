using Dreamness.RA3.Map.Parser.Core.MapScb;
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Test;

public class Ra3MapScbSaveTests
{
    [Test]
    public void SaveAs_ShouldCreateDirectorySetPathAndRoundTrip()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var scb = Ra3MapScb.FromBytes(BuildMinimalScb());
            var outputPath = Path.Combine(tempDir, "nested", "output.scb");

            scb.SaveAs(outputPath);
            var reopened = Ra3MapScb.FromFile(outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(outputPath), Is.True);
                Assert.That(ReadHeader(outputPath), Is.EqualTo(CompressConst.UnCompressFlag));
                Assert.That(scb.ScbFilePath, Is.EqualTo(Path.GetFullPath(outputPath)));
                Assert.That(reopened.ScbFilePath, Is.EqualTo(Path.GetFullPath(outputPath)));
            });
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void Save_ShouldOverwriteCurrentFile()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(tempDir, "source.scb");
            File.WriteAllBytes(filePath, BuildMinimalScb());
            var scb = Ra3MapScb.FromFile(filePath);

            File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });
            scb.Save();

            Assert.That(ReadHeader(filePath), Is.EqualTo(CompressConst.UnCompressFlag));
            Assert.DoesNotThrow(() => Ra3MapScb.FromFile(filePath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void Save_FromBytesWithoutSaveAs_ShouldThrow()
    {
        var scb = Ra3MapScb.FromBytes(BuildMinimalScb());

        var exception = Assert.Throws<System.Exception>(() => scb.Save());

        Assert.That(exception!.Message, Does.Contain("SaveAs"));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Ra3MapSharp_ScbSaveTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] BuildMinimalScb()
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
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);
        return reader.ReadUInt32();
    }
}
