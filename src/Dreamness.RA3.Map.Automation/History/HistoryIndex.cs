namespace Dreamness.RA3.Map.Automation.History;

internal sealed class HistoryIndex
{
    public const int CurrentSchemaVersion = 8;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public int Cursor { get; set; }

    public int NextHandle { get; set; } = 1;

    public List<HistoryEntry> Entries { get; set; } = new();
}

internal sealed class HistoryEntry
{
    public int Index { get; set; }

    public int Revision { get; set; }

    public string Command { get; set; } = "";

    public List<string> WaypointObjectIds { get; set; } = new();
    public List<string> UnitObjectIds { get; set; } = new();
    public List<Geometry.ProtectionZone> ProtectionZones { get; set; } = new();
    public SortedDictionary<string, Design.DesignEntityState> DesignEntities { get; set; } = new(StringComparer.Ordinal);
}
