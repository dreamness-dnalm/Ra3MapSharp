namespace Dreamness.RA3.Map.Automation.Session;

/// <summary>
/// 一张地图对应的 Automation 磁盘布局。
/// </summary>
public sealed class AutomationLayout
{
    public const string AutomationDirectoryName = ".automation";
    public const string MapInfoFileName = "MapInfo.json";
    public const string WorkspaceFileName = "Workspace.json";
    public const string WorkingDirectoryName = "Working";
    public const string WorkingMapFileName = "current.map";
    public const string SnapshotsDirectoryName = "Snapshots";
    public const string HistoryDirectoryName = "History";
    public const string HistoryLogFileName = "history.jsonl";
    public const string HistoryIndexFileName = "index.json";
    public const string LockFileName = "workspace.lock";

    private AutomationLayout(
        string mapName,
        string mapFolderPath,
        string userMapFilePath)
    {
        MapName = mapName;
        MapFolderPath = mapFolderPath;
        UserMapFilePath = userMapFilePath;
        AutomationDirectoryPath = Path.Combine(mapFolderPath, AutomationDirectoryName);
        MapInfoFilePath = Path.Combine(AutomationDirectoryPath, MapInfoFileName);
        WorkspaceFilePath = Path.Combine(AutomationDirectoryPath, WorkspaceFileName);
        WorkingDirectoryPath = Path.Combine(AutomationDirectoryPath, WorkingDirectoryName);
        WorkingMapFilePath = Path.Combine(WorkingDirectoryPath, WorkingMapFileName);
        SnapshotsDirectoryPath = Path.Combine(AutomationDirectoryPath, SnapshotsDirectoryName);
        HistoryDirectoryPath = Path.Combine(AutomationDirectoryPath, HistoryDirectoryName);
        HistoryLogFilePath = Path.Combine(HistoryDirectoryPath, HistoryLogFileName);
        HistoryIndexFilePath = Path.Combine(HistoryDirectoryPath, HistoryIndexFileName);
        LockFilePath = Path.Combine(AutomationDirectoryPath, LockFileName);
    }

    public string MapName { get; }

    public string MapFolderPath { get; }

    public string UserMapFilePath { get; }

    public string AutomationDirectoryPath { get; }

    public string MapInfoFilePath { get; }

    public string WorkspaceFilePath { get; }

    public string WorkingDirectoryPath { get; }

    public string WorkingMapFilePath { get; }

    public string SnapshotsDirectoryPath { get; }

    public string HistoryDirectoryPath { get; }

    public string HistoryLogFilePath { get; }

    public string HistoryIndexFilePath { get; }

    public string LockFilePath { get; }

    public string SnapshotFilePath(int historyIndex)
    {
        return Path.Combine(SnapshotsDirectoryPath, historyIndex + ".map");
    }

    /// <summary>
    /// 根据父目录与地图名解析布局，并拒绝目录穿越。
    /// </summary>
    public static AutomationLayout Resolve(string parentPath, string mapName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "parentPath 不能为空。");
        }

        ValidateMapName(mapName);

        var parentFullPath = Path.GetFullPath(parentPath);
        var mapFolderPath = Path.GetFullPath(Path.Combine(parentFullPath, mapName));
        var parentPrefix = AppendDirectorySeparator(parentFullPath);

        if (!mapFolderPath.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mapFolderPath, parentFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "地图路径超出允许的父目录。",
                details: new Dictionary<string, string>
                {
                    ["parentPath"] = parentFullPath,
                    ["mapName"] = mapName
                });
        }

        return new AutomationLayout(
            mapName,
            mapFolderPath,
            Path.Combine(mapFolderPath, mapName + ".map"));
    }

    internal void EnsureDirectories()
    {
        Directory.CreateDirectory(AutomationDirectoryPath);
        TryHideDirectory(AutomationDirectoryPath);
        Directory.CreateDirectory(WorkingDirectoryPath);
        Directory.CreateDirectory(SnapshotsDirectoryPath);
        Directory.CreateDirectory(HistoryDirectoryPath);
    }

    internal void ClearHistoryArtifacts()
    {
        ClearDirectoryContents(SnapshotsDirectoryPath);
        ClearDirectoryContents(HistoryDirectoryPath);
    }

    internal void DeleteWorkingMap()
    {
        if (File.Exists(WorkingMapFilePath))
        {
            File.Delete(WorkingMapFilePath);
        }
    }

    private static void ValidateMapName(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            throw new AutomationException("INVALID_ARGUMENT", "mapName 不能为空。");
        }

        if (mapName != mapName.Trim())
        {
            throw new AutomationException("INVALID_ARGUMENT", "mapName 不能包含前后空白。");
        }

        if (mapName is "." or "..")
        {
            throw new AutomationException("INVALID_ARGUMENT", "mapName 非法。");
        }

        if (mapName != Path.GetFileName(mapName))
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "mapName 不能包含路径分隔符。",
                details: new Dictionary<string, string> { ["mapName"] = mapName });
        }

        if (mapName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new AutomationException(
                "INVALID_ARGUMENT",
                "mapName 包含非法字符。",
                details: new Dictionary<string, string> { ["mapName"] = mapName });
        }
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static void TryHideDirectory(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            directory.Attributes |= FileAttributes.Hidden;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    internal static void ClearDirectoryContents(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            return;
        }

        foreach (var file in Directory.GetFiles(path))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.GetDirectories(path))
        {
            Directory.Delete(directory, true);
        }
    }
}
