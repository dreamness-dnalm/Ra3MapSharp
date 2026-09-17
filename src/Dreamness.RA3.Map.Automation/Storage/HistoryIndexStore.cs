using System.Text.Json;
using Dreamness.RA3.Map.Automation.Design;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.History;

namespace Dreamness.RA3.Map.Automation.Storage;

/// <summary>
/// Persists the history index in a compact, delta form.
/// <para>
/// The in-memory model keeps a complete state per revision, so undo/redo stays a direct
/// lookup and every consumer keeps working on full snapshots. On disk that model is
/// wasteful: a single design entity owns every height cell it touched (a 200x200 platform
/// is 40000 cells), and re-serializing all of them for every revision made the index grow
/// linearly with session length. Measured before this change: 22.4 MB after 4 revisions on
/// a 256x256 map where the dominant entity never changed after the first revision.
/// </para>
/// <para>
/// Each revision therefore stores only the entities whose payload actually changed, plus
/// the ids removed since the previous revision; reading forward-fills them back into
/// complete per-revision states.
/// </para>
/// </summary>
internal static class HistoryIndexStore
{
    public static async Task<HistoryIndex?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var storage = await AutomationJson.ReadAsync<HistoryIndexStorage>(path, cancellationToken)
            .ConfigureAwait(false);
        return storage == null ? null : FromStorage(storage);
    }

    public static async Task WriteAsync(string path, HistoryIndex index, CancellationToken cancellationToken)
    {
        await AutomationJson.WriteBytesAsync(path, Serialize(index), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Serializes the compact on-disk form. Callers stage it into a file transaction.</summary>
    public static byte[] Serialize(HistoryIndex index) =>
        JsonSerializer.SerializeToUtf8Bytes(ToStorage(index), AutomationJson.CompactOptions);

    internal static HistoryIndexStorage ToStorage(HistoryIndex index)
    {
        var storage = new HistoryIndexStorage
        {
            SchemaVersion = HistoryIndex.CurrentSchemaVersion,
            Cursor = index.Cursor,
            NextHandle = index.NextHandle
        };
        SortedDictionary<string, byte[]>? previous = null;
        foreach (var entry in index.Entries)
        {
            var current = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var changes = new SortedDictionary<string, DesignEntityState>(StringComparer.Ordinal);
            foreach (var pair in entry.DesignEntities)
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(pair.Value, AutomationJson.CompactOptions);
                current[pair.Key] = payload;
                // Comparing serialized payloads is what "unchanged" means on disk, so it
                // decides exactly what may be omitted: anything else risks dropping an edit.
                if (previous == null
                    || !previous.TryGetValue(pair.Key, out var before)
                    || !before.AsSpan().SequenceEqual(payload))
                {
                    changes[pair.Key] = pair.Value;
                }
            }
            var removed = previous == null
                ? new List<string>()
                : previous.Keys.Where(id => !current.ContainsKey(id)).ToList();
            storage.Entries.Add(new HistoryEntryStorage
            {
                Index = entry.Index,
                Revision = entry.Revision,
                Command = entry.Command,
                WaypointObjectIds = entry.WaypointObjectIds,
                UnitObjectIds = entry.UnitObjectIds,
                ProtectionZones = entry.ProtectionZones,
                DesignEntityChanges = changes,
                RemovedDesignEntityIds = removed
            });
            previous = current;
        }
        return storage;
    }

    internal static HistoryIndex FromStorage(HistoryIndexStorage storage)
    {
        var index = new HistoryIndex
        {
            SchemaVersion = storage.SchemaVersion,
            Cursor = storage.Cursor,
            NextHandle = storage.NextHandle
        };
        var state = new SortedDictionary<string, DesignEntityState>(StringComparer.Ordinal);
        foreach (var entry in storage.Entries)
        {
            if (entry.DesignEntities != null)
            {
                // schemaVersion <= 8 stored a complete snapshot per revision.
                state = new SortedDictionary<string, DesignEntityState>(StringComparer.Ordinal);
                foreach (var pair in entry.DesignEntities) state[pair.Key] = pair.Value;
            }
            else
            {
                if (entry.RemovedDesignEntityIds != null)
                {
                    foreach (var id in entry.RemovedDesignEntityIds) state.Remove(id);
                }
                if (entry.DesignEntityChanges != null)
                {
                    foreach (var pair in entry.DesignEntityChanges) state[pair.Key] = pair.Value;
                }
            }
            index.Entries.Add(new HistoryEntry
            {
                Index = entry.Index,
                Revision = entry.Revision,
                Command = entry.Command,
                WaypointObjectIds = entry.WaypointObjectIds ?? new List<string>(),
                UnitObjectIds = entry.UnitObjectIds ?? new List<string>(),
                ProtectionZones = entry.ProtectionZones ?? new List<ProtectionZone>(),
                // Each revision keeps its own copy: entries are independently readable and
                // must not alias each other through a shared payload instance.
                DesignEntities = DeepCopy(state)
            });
        }
        return index;
    }

    private static SortedDictionary<string, DesignEntityState> DeepCopy(
        SortedDictionary<string, DesignEntityState> state)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, AutomationJson.CompactOptions);
        var plain = JsonSerializer.Deserialize<Dictionary<string, DesignEntityState>>(
            bytes, AutomationJson.CompactOptions) ?? new Dictionary<string, DesignEntityState>();
        var copy = new SortedDictionary<string, DesignEntityState>(StringComparer.Ordinal);
        foreach (var pair in plain) copy[pair.Key] = pair.Value;
        return copy;
    }
}

/// <summary>On-disk shape of the history index. Kept out of the runtime model on purpose.</summary>
internal sealed class HistoryIndexStorage
{
    public int SchemaVersion { get; set; } = HistoryIndex.CurrentSchemaVersion;

    public int Cursor { get; set; }

    public int NextHandle { get; set; } = 1;

    public List<HistoryEntryStorage> Entries { get; set; } = new();
}

internal sealed class HistoryEntryStorage
{
    public int Index { get; set; }

    public int Revision { get; set; }

    public string Command { get; set; } = "";

    public List<string>? WaypointObjectIds { get; set; }

    public List<string>? UnitObjectIds { get; set; }

    public List<ProtectionZone>? ProtectionZones { get; set; }

    /// <summary>Entities whose payload differs from the previous revision (schemaVersion 9).</summary>
    public SortedDictionary<string, DesignEntityState>? DesignEntityChanges { get; set; }

    /// <summary>Entity ids present in the previous revision but gone here (schemaVersion 9).</summary>
    public List<string>? RemovedDesignEntityIds { get; set; }

    /// <summary>Complete snapshot written by schemaVersion 8 and older. Read-only compatibility.</summary>
    public SortedDictionary<string, DesignEntityState>? DesignEntities { get; set; }
}
