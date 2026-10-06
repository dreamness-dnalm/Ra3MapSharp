namespace Dreamness.RA3.Map.Automation.Commands.Abstractions;

public sealed class SessionOpenedData
{
    public bool DesignDirty { get; set; }
    public bool HasUnexportedChanges { get; set; }
    public string SessionId { get; set; } = "";

    public string MapId { get; set; } = "";

    public string MapName { get; set; } = "";

    public int Revision { get; set; }

    public bool Dirty { get; set; }

    public string UserMapFilePath { get; set; } = "";
}

public sealed class MapInfoData
{
    public bool DesignDirty { get; set; }
    public bool HasUnexportedChanges { get; set; }
    public string SessionId { get; set; } = "";

    public string MapId { get; set; } = "";

    public string MapName { get; set; } = "";

    public int Revision { get; set; }

    public int HistoryCursor { get; set; }

    public bool Dirty { get; set; }

    public bool CanUndo { get; set; }

    public bool CanRedo { get; set; }

    public int PlayableWidth { get; set; }

    public int PlayableHeight { get; set; }

    public int Border { get; set; }

    public int MapWidth { get; set; }

    public int MapHeight { get; set; }

    public int WaypointCount { get; set; }
}

public sealed class SetHeightData
{
    public int AffectedCells { get; set; }

    public float Height { get; set; }
}

public sealed class HeightQueryData
{
    public int MapX { get; set; }

    public int MapY { get; set; }

    public float Height { get; set; }
}

public sealed class WaypointData
{
    public string ObjectId { get; set; } = "";

    public string Name { get; set; } = "";

    public string UniqueId { get; set; } = "";

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }
}

public sealed class BatchResultData
{
    public int Count { get; set; }

    public List<object?> Results { get; set; } = new();
}

public sealed class HistoryData
{
    public int HistoryCursor { get; set; }

    public bool CanUndo { get; set; }

    public bool CanRedo { get; set; }
}

public sealed class SaveMapData
{
    public string UserMapFilePath { get; set; } = "";

    public string ContentHash { get; set; } = "";
}
