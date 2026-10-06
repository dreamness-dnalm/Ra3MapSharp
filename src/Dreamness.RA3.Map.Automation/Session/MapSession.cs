using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.History;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

/// <summary>
/// 一张地图的运行时编辑工作区。同一张图同时只允许一个实例。
/// </summary>
public sealed partial class MapSession : IDisposable
{
    private readonly AutomationLayout _layout;
    private readonly FileStream _lockStream;
    private readonly Action<MapSession> _unregister;
    private readonly object _gate = new();
    private readonly Dictionary<string, DedupRecord> _dedup = new(StringComparer.Ordinal);
    private WorkspaceInfo _workspace;
    private HistoryIndex _history = new();
    private bool _closed;

    internal MapSession(
        MapInfo mapInfo,
        WorkspaceInfo workspace,
        AutomationLayout layout,
        Ra3MapFacade facade,
        FileStream lockStream,
        string sessionId,
        Action<MapSession> unregister)
    {
        MapInfo = mapInfo;
        _workspace = workspace;
        _layout = layout;
        Facade = facade;
        _lockStream = lockStream;
        SessionId = sessionId;
        _unregister = unregister;
        ExecutionLock = new SemaphoreSlim(1, 1);
        Handles = new ObjectHandleTable();
    }

    public MapInfo MapInfo { get; }

    public string MapId => MapInfo.MapId;

    /// <summary>
    /// 本次打开的运行时标识。关闭后再次打开会生成新值。
    /// </summary>
    public string SessionId { get; }

    public string MapName => _layout.MapName;

    public string MapFolderPath => _layout.MapFolderPath;

    public string UserMapFilePath => _layout.UserMapFilePath;

    public string WorkingMapFilePath => _layout.WorkingMapFilePath;

    public string AutomationDirectoryPath => _layout.AutomationDirectoryPath;

    public int Revision => _workspace.Revision;

    public int HistoryCursor => _history.Cursor;

    public string LastSavedContentHash => _workspace.LastSavedContentHash;

    public bool Dirty => _workspace.Dirty;
    public bool DesignDirty => _workspace.DesignDirty;
    public bool HasUnexportedChanges => Dirty || DesignDirty;

    public WorkspaceStatus Status => _workspace.Status;

    public bool CanUndo => _history.Cursor > 0;

    public bool CanRedo => _history.Cursor < _history.Entries.Count - 1;

    internal Ra3MapFacade Facade { get; private set; }

    internal AutomationLayout Layout => _layout;

    internal SemaphoreSlim ExecutionLock { get; }

    internal ObjectHandleTable Handles { get; }

    /// <summary>
    /// 关闭工作区并释放锁。未丢弃时会保留 dirty 工作副本以便下次恢复。
    /// </summary>
    public void Close(bool discard = false)
    {
        CloseAsync(discard).GetAwaiter().GetResult();
    }

    public async Task CloseAsync(bool discard = false, CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _prepared.Clear();
            }

            try
            {
                if (discard)
                {
                    _layout.DeleteWorkingMap();
                    _layout.ClearHistoryArtifacts();
                    _workspace.Dirty = false;
                    _workspace.DesignDirty = false;
                    _workspace.Revision = 0;
                    _workspace.HistoryCursor = 0;
                    _history = new HistoryIndex();
                }

                _workspace.Status = WorkspaceStatus.Closed;
                _workspace.HistoryCursor = _history.Cursor;
                await AutomationJson.WriteAsync(_layout.WorkspaceFilePath, _workspace, cancellationToken)
                    .ConfigureAwait(false);
                if (!discard)
                {
                    await SaveHistoryIndexAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _unregister(this);
                _lockStream.Dispose();
            }
        }
        finally
        {
            ExecutionLock.Release();
        }
    }

    public void Dispose()
    {
        Close(discard: false);
    }

    internal void ThrowIfClosed()
    {
        lock (_gate)
        {
            if (_closed)
            {
                throw new AutomationException("SESSION_NOT_FOUND", "工作区已关闭。");
            }
        }
    }

    internal bool TryGetDedup(string requestId, string fingerprint, out CommandResult? result)
    {
        if (_dedup.TryGetValue(requestId, out var record))
        {
            if (!string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new AutomationException(
                    "REQUEST_ID_CONFLICT",
                    $"requestId 已使用且参数不同: {requestId}");
            }

            result = record.Result;
            return true;
        }

        result = null;
        return false;
    }

    internal void RememberDedup(string requestId, string fingerprint, CommandResult result)
    {
        // A conflicting request must never replace the original receipt.
        _dedup.TryAdd(requestId, new DedupRecord(fingerprint, result));
    }

    internal async Task MarkDirtyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();
        _workspace.Dirty = true;
        _workspace.Status = WorkspaceStatus.Open;
        await AutomationJson.WriteAsync(_layout.WorkspaceFilePath, _workspace, cancellationToken)
            .ConfigureAwait(false);
    }

    internal MapInfoData ToMapInfoData()
    {
        return new MapInfoData
        {
            SessionId = SessionId,
            MapId = MapId,
            MapName = MapName,
            Revision = Revision,
            HistoryCursor = HistoryCursor,
            Dirty = Dirty,
            DesignDirty = DesignDirty, HasUnexportedChanges = HasUnexportedChanges,
            CanUndo = CanUndo,
            CanRedo = CanRedo,
            PlayableWidth = Facade.MapPlayableWidth,
            PlayableHeight = Facade.MapPlayableHeight,
            Border = Facade.MapBorderWidth,
            MapWidth = Facade.MapWidth,
            MapHeight = Facade.MapHeight,
            WaypointCount = Facade.GetWaypoints().Count
        };
    }

    internal SessionOpenedData ToOpenedData()
    {
        return new SessionOpenedData
        {
            SessionId = SessionId,
            MapId = MapId,
            MapName = MapName,
            Revision = Revision,
            Dirty = Dirty,
            UserMapFilePath = UserMapFilePath,
            DesignDirty = DesignDirty, HasUnexportedChanges = HasUnexportedChanges
        };
    }

    private sealed record DedupRecord(string Fingerprint, CommandResult Result);
}
