namespace Dreamness.RA3.Map.Automation.Session;

/// <summary>
/// 一张图唯一工作区的状态，保存在 <c>.automation/Workspace.json</c>。
/// </summary>
public sealed class WorkspaceInfo
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string MapId { get; set; } = "";

    public int Revision { get; set; }

    public int HistoryCursor { get; set; }

    public string LastSavedContentHash { get; set; } = "";

    /// <summary>Uncompressed, serialized map content; independent of the saved file's compression.</summary>
    public string LastSavedMapHash { get; set; } = "";
    public string? LastSavedProtectionHash { get; set; }
    public string? LastSavedDesignHash { get; set; }
    public bool DesignDirty { get; set; }

    public bool Dirty { get; set; }

    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Closed;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastOpenedAt { get; set; }
}

public enum WorkspaceStatus
{
    Closed = 0,
    Open = 1
}
