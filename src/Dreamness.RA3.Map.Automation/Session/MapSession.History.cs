using System.Text;
using System.Text.Json;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.History;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    internal List<Geometry.ProtectionZone> ProtectionZones { get; private set; } = new();
    internal SortedDictionary<string, Design.DesignEntityState> DesignEntities { get; private set; } = new(StringComparer.Ordinal);
    private string DesignHash() => ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(
        DesignEntities.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value), AutomationJson.Options));
    private string ProtectionHash() => ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(ProtectionZones, AutomationJson.Options));
    internal async Task InitializeHistoryAsync(CancellationToken cancellationToken)
    {
        _workspace.LastSavedProtectionHash ??= ProtectionHash();
        _workspace.LastSavedDesignHash ??= DesignHash();
        // Older workspaces only have the physical file hash. The manager has already
        // checked that the saved file has not changed before resuming a dirty workspace.
        if (string.IsNullOrEmpty(_workspace.LastSavedMapHash))
            _workspace.LastSavedMapHash = ContentHasher.HashMap(Ra3MapFacade.Open(UserMapFilePath));
        var loaded = await AutomationJson.ReadAsync<HistoryIndex>(_layout.HistoryIndexFilePath, cancellationToken)
            .ConfigureAwait(false);
        if (loaded == null || loaded.Entries.Count == 0)
        {
            Handles.RebuildFromMap(Facade);
            _history = new HistoryIndex
            {
                Cursor = 0,
                NextHandle = Handles.NextHandle,
                Entries = { Entry(0, "baseline") }
            };
            _workspace.HistoryCursor = 0;
            var files = new FileTransaction();
            files.Add(_layout.SnapshotFilePath(0), await File.ReadAllBytesAsync(WorkingMapFilePath, cancellationToken));
            files.AddJson(_layout.HistoryIndexFilePath, _history);
            files.AddJson(_layout.WorkspaceFilePath, _workspace);
            await files.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        if (loaded.Cursor < 0 || loaded.Cursor >= loaded.Entries.Count
            || loaded.Entries.Where((entry, index) => entry.Index != index).Any())
            throw new AutomationException("VALIDATION_FAILED", "历史游标或快照索引无效。");
        var currentSnapshot = Ra3MapFacade.Open(_layout.SnapshotFilePath(loaded.Cursor));
        if (ContentHasher.HashMap(currentSnapshot) != ContentHasher.HashMap(Facade))
            throw new AutomationException("WORKSPACE_CONFLICT", "历史快照与当前工作副本不一致。");
        if (loaded.SchemaVersion == 1) await MigrateObjectHandlesAsync(loaded, cancellationToken);
        if (loaded.SchemaVersion is 2 or 3 or 4 or 5 or 6 or 7)
        {
            loaded.SchemaVersion = HistoryIndex.CurrentSchemaVersion;
            await AutomationJson.WriteAsync(_layout.HistoryIndexFilePath, loaded, cancellationToken);
        }
        if (loaded.SchemaVersion != HistoryIndex.CurrentSchemaVersion)
            throw new AutomationException("UNSUPPORTED_VERSION", "不支持的历史版本。");
        _history = loaded;
        _workspace.HistoryCursor = _history.Cursor;
        if (_history.Cursor < 0 || _history.Cursor >= _history.Entries.Count)
            throw new AutomationException("VALIDATION_FAILED", "历史游标无效。");
        Handles.Import(_history.Entries[_history.Cursor].WaypointObjectIds, _history.NextHandle, _history.Entries[_history.Cursor].UnitObjectIds);
        Handles.EnsureMatches(Facade);
        ProtectionZones = Clone(_history.Entries[_history.Cursor].ProtectionZones);
        DesignEntities = Clone(_history.Entries[_history.Cursor].DesignEntities);
        foreach (var zone in ProtectionZones) zone.Validate(Facade);
    }

    private async Task MigrateObjectHandlesAsync(HistoryIndex history, CancellationToken cancellationToken)
    {
        // Version 1 commands cannot edit ordinary objects. Verify every snapshot rather
        // than guessing identities from names or IDs, which may legitimately repeat.
        static string[] Fingerprints(Ra3MapFacade map) => map.GetUnitObjects()
            .Select(o => ContentHasher.HashBytes(o.Obj.ToBytes(map.ra3Map.Context))).ToArray();
        var expected = Fingerprints(Facade);
        foreach (var entry in history.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var map = Ra3MapFacade.Open(_layout.SnapshotFilePath(entry.Index));
            if (!expected.SequenceEqual(Fingerprints(map)))
                throw new AutomationException("HISTORY_MIGRATION_REQUIRED", "旧历史中的普通对象不一致，无法安全分配跨版本标识。");
        }
        var next = Math.Max(1, history.NextHandle);
        var ids = expected.Select(_ => "obj-" + next++).ToList();
        foreach (var entry in history.Entries) entry.UnitObjectIds = ids.ToList();
        history.NextHandle = next;
        history.SchemaVersion = HistoryIndex.CurrentSchemaVersion;
        await AutomationJson.WriteAsync(_layout.HistoryIndexFilePath, history, cancellationToken);
    }

    internal Task CommitMutationAsync(string command, Func<Task> mutate, CancellationToken cancellationToken) =>
        ChangeStateAsync(command, mutate, null, cancellationToken);

    internal Task UndoAsync(CancellationToken cancellationToken)
    {
        if (!CanUndo) throw new AutomationException("VALIDATION_FAILED", "没有可回滚的历史。");
        return ChangeStateAsync("history.undo", null, _history.Cursor - 1, cancellationToken);
    }

    internal Task RedoAsync(CancellationToken cancellationToken)
    {
        if (!CanRedo) throw new AutomationException("VALIDATION_FAILED", "没有可重做的历史。");
        return ChangeStateAsync("history.redo", null, _history.Cursor + 1, cancellationToken);
    }

    private async Task ChangeStateAsync(string command, Func<Task>? mutate, int? cursor, CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        var previousFacade = Facade;
        var previousHistory = _history;
        var previousWorkspace = _workspace;
        var previousHandles = Handles.WaypointObjectIds.ToArray();
        var previousUnits = Handles.UnitObjectIds.ToArray();
        var previousNext = Handles.NextHandle;
        var previousZones = ProtectionZones;
        var previousDesign = DesignEntities;
        DesignEntities = Clone(previousDesign);
        ProtectionZones = Clone(previousZones);
        _history = Clone(previousHistory);
        _workspace = Clone(previousWorkspace);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] mapBytes;
            if (cursor.HasValue)
            {
                var path = _layout.SnapshotFilePath(cursor.Value);
                mapBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                Facade = Ra3MapFacade.Open(path);
                Handles.Import(_history.Entries[cursor.Value].WaypointObjectIds, _history.NextHandle, _history.Entries[cursor.Value].UnitObjectIds);
                _history.Cursor = cursor.Value;
                ProtectionZones = Clone(_history.Entries[cursor.Value].ProtectionZones);
                DesignEntities = Clone(_history.Entries[cursor.Value].DesignEntities);
            }
            else
            {
                // Handlers see an isolated map. Failed edits never mutate the original Facade.
                Facade = Ra3MapFacade.Open(WorkingMapFilePath);
                await mutate!().ConfigureAwait(false);
                Handles.EnsureMatches(Facade);
                mapBytes = SerializeMap(true, out var normalized);
                Facade = normalized;
                foreach (var zone in previousZones.Concat(ProtectionZones)) zone.EnsureUnchanged(previousFacade, Facade);
                var next = _history.Cursor + 1;
                // Only the candidate history is truncated. Existing snapshot files remain
                // available for rollback until the whole file transaction succeeds.
                _history.Entries.RemoveRange(next, _history.Entries.Count - next);
                _history.Cursor = next;
            }
            Handles.EnsureMatches(Facade);
            _workspace.Revision = checked(_workspace.Revision + 1);
            _workspace.HistoryCursor = _history.Cursor;
            _workspace.Dirty = ContentHasher.HashMap(Facade) != _workspace.LastSavedMapHash
                || ProtectionHash() != _workspace.LastSavedProtectionHash;
            _workspace.DesignDirty = DesignHash() != _workspace.LastSavedDesignHash;
            _history.NextHandle = Handles.NextHandle;
            if (!cursor.HasValue) _history.Entries.Add(Entry(_history.Cursor, command));

            var files = new FileTransaction();
            files.Add(WorkingMapFilePath, mapBytes);
            if (!cursor.HasValue) files.Add(_layout.SnapshotFilePath(_history.Cursor), mapBytes);
            files.AddJson(_layout.HistoryIndexFilePath, _history);
            files.AddJson(_layout.WorkspaceFilePath, _workspace);
            var log = File.Exists(_layout.HistoryLogFilePath)
                ? await File.ReadAllTextAsync(_layout.HistoryLogFilePath, cancellationToken).ConfigureAwait(false) : "";
            var record = JsonSerializer.Serialize(new { revision = Revision, historyCursor = HistoryCursor, command });
            files.Add(_layout.HistoryLogFilePath, Encoding.UTF8.GetBytes(log + record + Environment.NewLine));
            await files.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Facade = previousFacade;
            _history = previousHistory;
            _workspace = previousWorkspace;
            Handles.Import(previousHandles, previousNext, previousUnits);
            ProtectionZones = previousZones;
            DesignEntities = previousDesign;
            throw;
        }
        // Cleanup is not part of the commit. An inaccessible obsolete snapshot may be
        // retained on disk, but it is no longer reachable through the history index.
        for (var i = _history.Entries.Count; i < previousHistory.Entries.Count; i++)
            FileTransaction.TryDelete(_layout.SnapshotFilePath(i));
    }

    internal async Task SaveUserMapAsync(bool compress, CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        await EnsureUserMapUnchangedAsync(cancellationToken).ConfigureAwait(false);
        var bytes = SerializeMap(compress, out var normalized);
        var saved = Clone(_workspace);
        saved.LastSavedContentHash = ContentHasher.HashBytes(bytes);
        saved.LastSavedMapHash = ContentHasher.HashMap(normalized);
        saved.Dirty = false;
        saved.LastSavedProtectionHash = ProtectionHash();
        saved.LastSavedDesignHash = DesignHash();
        saved.DesignDirty = false;
        var files = new FileTransaction();
        files.AddChecked(UserMapFilePath, bytes, _workspace.LastSavedContentHash);
        files.AddJson(_layout.WorkspaceFilePath, saved);
        await EnsureUserMapUnchangedAsync(cancellationToken).ConfigureAwait(false);
        await files.CommitAsync(cancellationToken).ConfigureAwait(false);
        _workspace = saved;
    }

    internal async Task SaveUserMapAsAsync(string parentPath, string mapName, bool compress,
        bool overwrite, CancellationToken cancellationToken)
    {
        ThrowIfClosed();
        var target = AutomationLayout.Resolve(parentPath, mapName);
        if (string.Equals(target.UserMapFilePath, UserMapFilePath, StringComparison.OrdinalIgnoreCase))
        {
            if (!overwrite) throw new AutomationException("MAP_EXISTS", "目标地图已存在。");
            await SaveUserMapAsync(compress, cancellationToken).ConfigureAwait(false);
            return;
        }
        // Use the same per-map lock as OpenAsync so exporting cannot overwrite an
        // active workspace, including one opened in another process.
        target.EnsureDirectories();
        using var targetLock = MapSessionManager.AcquireLock(target);
        if (File.Exists(target.UserMapFilePath) && !overwrite)
            throw new AutomationException("MAP_EXISTS", $"目标地图已存在: {target.UserMapFilePath}");
        var expectedHash = File.Exists(target.UserMapFilePath)
            ? await ContentHasher.HashFileAsync(target.UserMapFilePath, cancellationToken).ConfigureAwait(false) : null;
        var bytes = SerializeMap(compress, out _);
        var currentHash = File.Exists(target.UserMapFilePath)
            ? await ContentHasher.HashFileAsync(target.UserMapFilePath, cancellationToken).ConfigureAwait(false) : null;
        if (expectedHash != currentHash)
            throw new AutomationException("WORKSPACE_CONFLICT", "导出目标已被外部修改。");
        var files = new FileTransaction();
        files.AddChecked(target.UserMapFilePath, bytes, expectedHash);
        await files.CommitAsync(cancellationToken).ConfigureAwait(false);
        // Exporting a copy intentionally does not change this session's save target.
    }

    private async Task EnsureUserMapUnchangedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(UserMapFilePath) ||
            await ContentHasher.HashFileAsync(UserMapFilePath, cancellationToken).ConfigureAwait(false) != _workspace.LastSavedContentHash)
            throw new AutomationException("WORKSPACE_CONFLICT", "用户地图文件已被外部修改或删除，请重新加载或另存副本。");
    }

    private byte[] SerializeMap(bool compress, out Ra3MapFacade normalized)
    {
        var path = Path.Combine(_layout.WorkingDirectoryPath, Guid.NewGuid().ToString("N") + ".map");
        try
        {
            Facade.SaveAs(path, compress);
            normalized = Ra3MapFacade.Open(path);
            return File.ReadAllBytes(path);
        }
        finally { FileTransaction.TryDelete(path); }
    }

    private HistoryEntry Entry(int index, string command) => new()
    {
        Index = index, Revision = Revision, Command = command,
        WaypointObjectIds = Handles.WaypointObjectIds.ToList(),
        UnitObjectIds = Handles.UnitObjectIds.ToList(),
        ProtectionZones = Clone(ProtectionZones), DesignEntities = Clone(DesignEntities)
    };

    private static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value, AutomationJson.Options), AutomationJson.Options)!;

    private Task SaveHistoryIndexAsync(CancellationToken cancellationToken)
    {
        _history.NextHandle = Handles.NextHandle;
        return AutomationJson.WriteAsync(_layout.HistoryIndexFilePath, _history, cancellationToken);
    }
}
