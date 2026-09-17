using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    internal async Task<object> ExportProjectAsync(string parentPath, string mapName, bool includeHistory, CancellationToken token)
    {
        var target = AutomationLayout.Resolve(parentPath, mapName);
        if (target.MapFolderPath.StartsWith(MapFolderPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target.MapFolderPath, MapFolderPath, StringComparison.OrdinalIgnoreCase))
            throw new AutomationException("INVALID_ARGUMENT", "导出目标不能位于源工程内。");
        if (Directory.Exists(target.MapFolderPath) || File.Exists(target.MapFolderPath))
            throw new AutomationException("MAP_EXISTS", "完整工程导出需要不存在的目标目录。");
        await EnsureUserMapUnchangedAsync(token);
        var parent = Path.GetDirectoryName(target.MapFolderPath)!;
        Directory.CreateDirectory(parent);
        var staging = AutomationLayout.Resolve(parent, ".ra3-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging.MapFolderPath);
        var manifest = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var companionHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mapId = Guid.NewGuid().ToString("D");
        try
        {
            async Task Write(string path, byte[] bytes)
            {
                var relative = Path.GetRelativePath(staging.MapFolderPath, path);
                if (File.Exists(path)) throw new AutomationException("EXPORT_CONFLICT", $"导出文件名冲突: {relative}");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, bytes, token);
                manifest[relative] = ContentHasher.HashBytes(bytes);
            }
            var mapBytes = SerializeMap(true, out var normalized);
            await Write(Path.Combine(staging.MapFolderPath, mapName + ".map"), mapBytes);
            var companions = EnumerateCompanions().ToArray();
            foreach (var source in companions)
            {
                token.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(MapFolderPath, source);
                if (string.Equals(relative, MapName + ".tga", StringComparison.OrdinalIgnoreCase))
                    relative = mapName + ".tga";
                var bytes = await File.ReadAllBytesAsync(source, token);
                companionHashes[source] = ContentHasher.HashBytes(bytes);
                await Write(Path.Combine(staging.MapFolderPath, relative), bytes);
            }
            if (includeHistory)
            {
                staging.EnsureDirectories();
                await Write(staging.WorkingMapFilePath, mapBytes);
                foreach (var entry in _history.Entries)
                    await Write(staging.SnapshotFilePath(entry.Index), await File.ReadAllBytesAsync(_layout.SnapshotFilePath(entry.Index), token));
                var workspace = Clone(_workspace);
                workspace.MapId = mapId;
                workspace.Status = WorkspaceStatus.Closed;
                workspace.LastSavedContentHash = ContentHasher.HashBytes(mapBytes);
                workspace.LastSavedMapHash = ContentHasher.HashMap(normalized);
                workspace.LastSavedProtectionHash = ProtectionHash();
                workspace.LastSavedDesignHash = DesignHash();
                workspace.DesignDirty = false;
                workspace.Dirty = false;
                await AutomationJson.WriteAsync(staging.MapInfoFilePath, new MapInfo { MapId = mapId }, token);
                await AutomationJson.WriteAsync(staging.WorkspaceFilePath, workspace, token);
                await HistoryIndexStore.WriteAsync(staging.HistoryIndexFilePath, _history, token);
                if (File.Exists(_layout.HistoryLogFilePath))
                    await Write(staging.HistoryLogFilePath, await File.ReadAllBytesAsync(_layout.HistoryLogFilePath, token));
                foreach (var path in new[] { staging.MapInfoFilePath, staging.WorkspaceFilePath, staging.HistoryIndexFilePath })
                    manifest[Path.GetRelativePath(staging.MapFolderPath, path)] = await ContentHasher.HashFileAsync(path, token);
            }
            // Companion edits happen outside our session API; reject a changing source set.
            if (!companions.SequenceEqual(EnumerateCompanions()))
                throw new AutomationException("WORKSPACE_CONFLICT", "导出期间附属文件列表发生变化。");
            foreach (var pair in companionHashes)
                if (await ContentHasher.HashFileAsync(pair.Key, token) != pair.Value)
                    throw new AutomationException("WORKSPACE_CONFLICT", "导出期间附属文件发生变化。");
            await EnsureUserMapUnchangedAsync(token);
            if (includeHistory)
                await AutomationJson.WriteAsync(Path.Combine(staging.AutomationDirectoryPath, "export.json"),
                    new { sourceMapId = MapId, sourceRevision = Revision, exportedMapId = mapId, files = manifest }, token);
            token.ThrowIfCancellationRequested();
            Directory.Move(staging.MapFolderPath, target.MapFolderPath);
            return new { mapFolderPath = target.MapFolderPath, mapId = includeHistory ? mapId : null, sourceMapId = MapId, revision = Revision,
                fileCount = manifest.Count + (includeHistory ? 1 : 0), files = manifest,
                historyPreserved = includeHistory, protectionsPreserved = includeHistory };
        }
        finally
        {
            // The staging path is an independently generated direct child of the verified parent.
            if (Directory.Exists(staging.MapFolderPath)) Directory.Delete(staging.MapFolderPath, true);
        }
    }

    private IEnumerable<string> EnumerateCompanions()
    {
        var files = new List<string>();
        void Visit(string directory)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (string.Equals(path, AutomationDirectoryPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, UserMapFilePath, StringComparison.OrdinalIgnoreCase)) continue;
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new AutomationException("INVALID_ARGUMENT", "工程导出不跟随符号链接或联接点。");
                if ((attributes & FileAttributes.Directory) != 0) Visit(path);
                else files.Add(path);
            }
        }
        Visit(MapFolderPath);
        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }
}
