namespace Dreamness.RA3.Map.Automation.History;

internal sealed class HistoryIndex
{
    // 9 stores design entities as per-revision deltas instead of a complete snapshot per
    // revision. The bump is what makes an older reader reject the file instead of silently
    // reading every revision as having no design entities.
    public const int CurrentSchemaVersion = 9;

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
