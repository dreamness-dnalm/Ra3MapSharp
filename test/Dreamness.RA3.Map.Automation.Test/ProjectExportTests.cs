using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Session;

namespace Dreamness.RA3.Map.Automation.Test;

public class ProjectExportTests
{
    [Test]
    public async Task ExportedDirtyProjectHasIndependentIdentityCompanionsAndUsableHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var source = await MapSessionManager.CreateAsync(root, "Source", 16, 16, 4);
            var executor = CommandExecutor.CreateDefault();
            Task<CommandResult> Run(MapSession session, string command, object args) => executor.ExecuteAsync(new CommandRequest
            { SessionId = session.SessionId, ExpectedRevision = session.Revision, Command = command, Arguments = JsonSerializer.SerializeToElement(args) });
            await File.WriteAllTextAsync(Path.Combine(source.MapFolderPath, "map.str"), "strings");
            await File.WriteAllBytesAsync(Path.Combine(source.MapFolderPath, "Source.tga"), new byte[] { 1, 2, 3 });
            Directory.CreateDirectory(Path.Combine(source.MapFolderPath, "scripts"));
            await File.WriteAllTextAsync(Path.Combine(source.MapFolderPath, "scripts", "Source.lua"), "-- keep filename");
            await Run(source, "objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 });
            await Run(source, "protections.add", new { id = "base", region = new { x = 4, y = 4, width = 4, height = 4 }, layers = new[] { "objects" } });
            var originalSaved = await File.ReadAllBytesAsync(source.UserMapFilePath);
            var result = await Run(source, "map.export_project", new { parentPath = root, mapName = "Copy" });
            Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
            Assert.That(source.Dirty, Is.True);
            Assert.That(await File.ReadAllBytesAsync(source.UserMapFilePath), Is.EqualTo(originalSaved));
            using var copy = await MapSessionManager.OpenAsync(root, "Copy");
            Assert.That(copy.MapId, Is.Not.EqualTo(source.MapId));
            Assert.That(copy.Dirty, Is.False);
            Assert.That(copy.Revision, Is.EqualTo(source.Revision));
            Assert.That(copy.Facade.GetUnitObjects().Count, Is.EqualTo(1));
            Assert.That(await File.ReadAllTextAsync(Path.Combine(copy.MapFolderPath, "map.str")), Is.EqualTo("strings"));
            Assert.That(File.Exists(Path.Combine(copy.MapFolderPath, "Copy.tga")), Is.True);
            Assert.That(File.Exists(Path.Combine(copy.MapFolderPath, "scripts", "Source.lua")), Is.True);
            var edit = await Run(copy, "objects.place", new { typeName = "CC_Tree01", x = 5, y = 5 });
            Assert.That(edit.Error?.Code, Is.EqualTo("PROTECTED_REGION"));
            await Run(copy, "history.undo", new { });
            await Run(copy, "history.undo", new { });
            Assert.That(copy.Facade.GetUnitObjects(), Is.Empty);
            await Run(copy, "history.redo", new { });
            Assert.That(copy.Facade.GetUnitObjects().Count, Is.EqualTo(1));
            Assert.That(source.Facade.GetUnitObjects().Count, Is.EqualTo(1));
            result = await Run(source, "map.export_project", new { parentPath = root, mapName = "Copy" });
            Assert.That(result.Error?.Code, Is.EqualTo("MAP_EXISTS"));
            result = await Run(source, "map.export_package", new { parentPath = root, mapName = "Package" });
            Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
            Assert.That(Directory.Exists(Path.Combine(root, "Package", ".automation")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "Package", "map.str")), Is.True);
            Assert.That(Dreamness.Ra3.Map.Facade.Core.Ra3MapFacade.Open(Path.Combine(root, "Package", "Package.map"))
                .GetUnitObjects().Count, Is.EqualTo(1));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public async Task ExportRejectsNestedDestinationWithoutCreatingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var source = await MapSessionManager.CreateAsync(root, "Source", 16, 16, 4);
            var result = await CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest
            { SessionId = source.SessionId, ExpectedRevision = 0, Command = "map.export_project",
                Arguments = JsonSerializer.SerializeToElement(new { parentPath = source.MapFolderPath, mapName = "Nested" }) });
            Assert.That(result.Error?.Code, Is.EqualTo("INVALID_ARGUMENT"));
            Assert.That(Directory.Exists(Path.Combine(source.MapFolderPath, "Nested")), Is.False);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public async Task FilenameCollisionDoesNotPublishPartialExport()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var source = await MapSessionManager.CreateAsync(root, "Source", 16, 16, 4);
            await File.WriteAllTextAsync(Path.Combine(source.MapFolderPath, "Copy.map"), "existing companion");
            var result = await CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest
            { SessionId = source.SessionId, ExpectedRevision = 0, Command = "map.export_project",
                Arguments = JsonSerializer.SerializeToElement(new { parentPath = root, mapName = "Copy" }) });
            Assert.That(result.Error?.Code, Is.EqualTo("EXPORT_CONFLICT"));
            Assert.That(Directory.Exists(Path.Combine(root, "Copy")), Is.False);
            Assert.That(Directory.GetDirectories(root, ".ra3-export-*"), Is.Empty);
            Assert.That(await File.ReadAllTextAsync(Path.Combine(source.MapFolderPath, "Copy.map")), Is.EqualTo("existing companion"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
