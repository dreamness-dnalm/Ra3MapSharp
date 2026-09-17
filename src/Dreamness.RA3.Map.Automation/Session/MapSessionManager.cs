using System.Diagnostics.CodeAnalysis;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

/// <summary>
/// 打开与查找地图工作区。一张图同时只允许一个写入工作区。
/// </summary>
public static class MapSessionManager
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, MapSession> OpenSessions =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, MapSession> OpenSessionsById =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 创建新地图并打开唯一工作区。出生点数量固定为 0，由后续命令显式放置。
    /// </summary>
    public static async Task<MapSession> CreateAsync(
        string parentPath,
        string mapName,
        int playableWidth,
        int playableHeight,
        int border = 8,
        string defaultTexture = "Dirt_Yucatan03",
        bool compress = true,
        CancellationToken cancellationToken = default)
    {
        if (playableWidth <= 0 || playableHeight <= 0)
        {
            throw new AutomationException("INVALID_ARGUMENT", "可玩区域尺寸必须为正数。");
        }

        if (border < 0)
        {
            throw new AutomationException("INVALID_ARGUMENT", "边界不能为负数。");
        }

        var layout = AutomationLayout.Resolve(parentPath, mapName);
        if (File.Exists(layout.UserMapFilePath))
        {
            throw new AutomationException(
                "MAP_EXISTS",
                $"地图已存在: {layout.UserMapFilePath}",
                details: new Dictionary<string, string>
                {
                    ["userMapFilePath"] = layout.UserMapFilePath
                });
        }

        try
        {
            var facade = Ra3MapFacade.NewMap(
                playableWidth,
                playableHeight,
                border,
                initPlayerStartWaypointCnt: 0,
                defaultTexture);
            facade.SaveAs(parentPath, mapName, compress);
        }
        catch (Exception ex) when (ex is not AutomationException)
        {
            throw new AutomationException("IO_ERROR", "创建地图失败。", innerException: ex);
        }

        return await OpenAsync(parentPath, mapName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 打开地图的唯一工作区：解析或引导元数据，隔离工作副本，并加载地图。
    /// </summary>
    public static async Task<MapSession> OpenAsync(
        string parentPath,
        string mapName,
        CancellationToken cancellationToken = default)
    {
        var layout = AutomationLayout.Resolve(parentPath, mapName);
        if (!File.Exists(layout.UserMapFilePath))
        {
            throw new AutomationException(
                "MAP_NOT_FOUND",
                $"未找到地图文件: {layout.UserMapFilePath}",
                details: new Dictionary<string, string>
                {
                    ["userMapFilePath"] = layout.UserMapFilePath
                });
        }

        lock (Sync)
        {
            if (OpenSessions.ContainsKey(layout.MapFolderPath))
            {
                throw AlreadyOpen(layout);
            }
        }

        layout.EnsureDirectories();

        FileStream? lockStream = null;
        var registered = false;
        try
        {
            lockStream = AcquireLock(layout);
            var mapInfo = await LoadOrCreateMapInfoAsync(layout, cancellationToken)
                .ConfigureAwait(false);
            var workspace = await PrepareWorkspaceAsync(layout, mapInfo, cancellationToken)
                .ConfigureAwait(false);

            Ra3MapFacade facade;
            try
            {
                facade = Ra3MapFacade.Open(layout.WorkingMapFilePath);
            }
            catch (Exception ex) when (ex is not AutomationException)
            {
                throw new AutomationException(
                    "VALIDATION_FAILED",
                    $"无法打开工作副本: {layout.WorkingMapFilePath}",
                    innerException: ex);
            }

            var session = new MapSession(
                mapInfo,
                workspace,
                layout,
                facade,
                lockStream,
                Guid.NewGuid().ToString("D"),
                Unregister);

            await session.InitializeHistoryAsync(cancellationToken).ConfigureAwait(false);

            lock (Sync)
            {
                if (OpenSessions.ContainsKey(layout.MapFolderPath))
                {
                    throw AlreadyOpen(layout);
                }

                OpenSessions[layout.MapFolderPath] = session;
                OpenSessionsById[session.SessionId] = session;
                registered = true;
            }

            return session;
        }
        catch
        {
            if (!registered)
            {
                lockStream?.Dispose();
            }

            throw;
        }
    }

    public static bool TryGetOpen(
        string parentPath,
        string mapName,
        [NotNullWhen(true)] out MapSession? session)
    {
        var layout = AutomationLayout.Resolve(parentPath, mapName);
        lock (Sync)
        {
            return OpenSessions.TryGetValue(layout.MapFolderPath, out session);
        }
    }

    public static bool TryGetBySessionId(string sessionId, [NotNullWhen(true)] out MapSession? session)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            session = null;
            return false;
        }

        lock (Sync)
        {
            return OpenSessionsById.TryGetValue(sessionId, out session);
        }
    }

    internal static FileStream AcquireLock(AutomationLayout layout)
    {
        try
        {
            return new FileStream(
                layout.LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException ex)
        {
            throw new AutomationException(
                "SESSION_ALREADY_OPEN",
                $"地图工作区已被占用: {layout.MapFolderPath}",
                details: new Dictionary<string, string>
                {
                    ["mapFolderPath"] = layout.MapFolderPath
                },
                innerException: ex);
        }
    }

    private static async Task<MapInfo> LoadOrCreateMapInfoAsync(
        AutomationLayout layout,
        CancellationToken cancellationToken)
    {
        var mapInfo = await AutomationJson.ReadAsync<MapInfo>(layout.MapInfoFilePath, cancellationToken)
            .ConfigureAwait(false);

        if (mapInfo == null)
        {
            mapInfo = new MapInfo
            {
                SchemaVersion = MapInfo.CurrentSchemaVersion,
                MapId = Guid.NewGuid().ToString("D"),
            };
            await AutomationJson.WriteAsync(layout.MapInfoFilePath, mapInfo, cancellationToken)
                .ConfigureAwait(false);
            return mapInfo;
        }

        ValidateSchema(mapInfo.SchemaVersion, layout.MapInfoFilePath, MapInfo.CurrentSchemaVersion);
        if (string.IsNullOrWhiteSpace(mapInfo.MapId))
        {
            throw new AutomationException(
                "VALIDATION_FAILED",
                $"MapInfo.json 缺少 mapId: {layout.MapInfoFilePath}");
        }

        return mapInfo;
    }

    private static async Task<WorkspaceInfo> PrepareWorkspaceAsync(
        AutomationLayout layout,
        MapInfo mapInfo,
        CancellationToken cancellationToken)
    {
        var workspace = await AutomationJson.ReadAsync<WorkspaceInfo>(
                layout.WorkspaceFilePath,
                cancellationToken)
            .ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var userHash = await ContentHasher.HashFileAsync(layout.UserMapFilePath, cancellationToken)
            .ConfigureAwait(false);

        if (workspace != null)
        {
            ValidateSchema(
                workspace.SchemaVersion,
                layout.WorkspaceFilePath,
                WorkspaceInfo.CurrentSchemaVersion);

            if (!string.IsNullOrWhiteSpace(workspace.MapId)
                && !string.Equals(workspace.MapId, mapInfo.MapId, StringComparison.Ordinal))
            {
                throw new AutomationException(
                    "VALIDATION_FAILED",
                    "Workspace.json 的 mapId 与 MapInfo.json 不一致。",
                    details: new Dictionary<string, string>
                    {
                        ["mapInfoMapId"] = mapInfo.MapId,
                        ["workspaceMapId"] = workspace.MapId
                    });
            }
        }

        if (workspace is { Dirty: true } or { DesignDirty: true })
        {
            return await ResumeDirtyWorkspaceAsync(layout, mapInfo, workspace, userHash, now, cancellationToken)
                .ConfigureAwait(false);
        }

        if (workspace != null && workspace.LastSavedContentHash == userHash
            && File.Exists(layout.HistoryIndexFilePath) && File.Exists(layout.WorkingMapFilePath))
        {
            // Preserve the authoring history and handles across a normal save/close.
            // A changed working copy must not silently replace a clean saved map.
            var working = Ra3MapFacade.Open(layout.WorkingMapFilePath);
            var saved = Ra3MapFacade.Open(layout.UserMapFilePath);
            if (ContentHasher.HashMap(working) != ContentHasher.HashMap(saved))
                throw new AutomationException("WORKSPACE_CONFLICT", "已保存工作区的副本与用户地图不一致。");
            workspace.Status = WorkspaceStatus.Open;
            workspace.LastOpenedAt = now;
            await AutomationJson.WriteAsync(layout.WorkspaceFilePath, workspace, cancellationToken);
            return workspace;
        }

        return await StartFreshWorkspaceAsync(layout, mapInfo, workspace, userHash, now, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<WorkspaceInfo> ResumeDirtyWorkspaceAsync(
        AutomationLayout layout,
        MapInfo mapInfo,
        WorkspaceInfo workspace,
        string userHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.WorkingMapFilePath))
        {
            throw new AutomationException(
                "VALIDATION_FAILED",
                "工作区标记为 dirty，但缺少工作副本。",
                details: new Dictionary<string, string>
                {
                    ["workingMapFilePath"] = layout.WorkingMapFilePath
                });
        }

        if (string.IsNullOrWhiteSpace(workspace.LastSavedContentHash))
        {
            throw new AutomationException(
                "VALIDATION_FAILED",
                "工作区标记为 dirty，但缺少 lastSavedContentHash。",
                details: new Dictionary<string, string>
                {
                    ["workspaceFilePath"] = layout.WorkspaceFilePath
                });
        }

        if (!string.Equals(workspace.LastSavedContentHash, userHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationException(
                "WORKSPACE_CONFLICT",
                "用户地图文件已被外部修改，与未保存工作区冲突。",
                details: new Dictionary<string, string>
                {
                    ["expectedHash"] = workspace.LastSavedContentHash,
                    ["actualHash"] = userHash,
                    ["userMapFilePath"] = layout.UserMapFilePath
                });
        }

        workspace.MapId = mapInfo.MapId;
        workspace.Status = WorkspaceStatus.Open;
        workspace.LastOpenedAt = now;
        await AutomationJson.WriteAsync(layout.WorkspaceFilePath, workspace, cancellationToken)
            .ConfigureAwait(false);
        return workspace;
    }

    private static async Task<WorkspaceInfo> StartFreshWorkspaceAsync(
        AutomationLayout layout,
        MapInfo mapInfo,
        WorkspaceInfo? previous,
        string userHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        layout.ClearHistoryArtifacts();
        await ContentHasher.CopyAndHashAsync(
                layout.UserMapFilePath,
                layout.WorkingMapFilePath,
                cancellationToken)
            .ConfigureAwait(false);

        var workspace = new WorkspaceInfo
        {
            SchemaVersion = WorkspaceInfo.CurrentSchemaVersion,
            MapId = mapInfo.MapId,
            Revision = 0,
            HistoryCursor = 0,
            LastSavedContentHash = userHash,
            Dirty = false,
            Status = WorkspaceStatus.Open,
            CreatedAt = previous?.CreatedAt ?? now,
            LastOpenedAt = now
        };

        await AutomationJson.WriteAsync(layout.WorkspaceFilePath, workspace, cancellationToken)
            .ConfigureAwait(false);
        return workspace;
    }

    private static void ValidateSchema(int schemaVersion, string path, int currentVersion)
    {
        if (schemaVersion == currentVersion)
        {
            return;
        }

        throw new AutomationException(
            "UNSUPPORTED_VERSION",
            $"不支持的元数据版本 {schemaVersion}: {path}",
            details: new Dictionary<string, string>
            {
                ["path"] = path,
                ["schemaVersion"] = schemaVersion.ToString(),
                ["supportedVersion"] = currentVersion.ToString()
            });
    }

    private static AutomationException AlreadyOpen(AutomationLayout layout)
    {
        return new AutomationException(
            "SESSION_ALREADY_OPEN",
            $"地图工作区已打开: {layout.MapFolderPath}",
            details: new Dictionary<string, string>
            {
                ["mapFolderPath"] = layout.MapFolderPath
            });
    }

    private static void Unregister(MapSession session)
    {
        lock (Sync)
        {
            if (OpenSessions.TryGetValue(session.MapFolderPath, out var current)
                && ReferenceEquals(current, session))
            {
                OpenSessions.Remove(session.MapFolderPath);
            }

            OpenSessionsById.Remove(session.SessionId);
        }
    }
}
