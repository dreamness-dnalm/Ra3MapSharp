using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

/// <summary>A detached, serialized map revision. Mutating this buffer cannot modify the session.</summary>
public sealed record MapSnapshot(string MapId, string SessionId, string MapName, int Revision,
    string ContentHash, int PlayableWidth, int PlayableHeight, int Border, byte[] MapBytes)
{
    public IReadOnlyList<Geometry.ProtectionZone> ProtectionZones { get; init; } = Array.Empty<Geometry.ProtectionZone>();
    public string ProtectionHash { get; init; } = "";
    public string? PreparedPlanId { get; init; }
    public string? PlanHash { get; init; }
    public string DesignHash { get; init; } = "";
    public void VerifyIntegrity()
    {
        if (ContentHasher.HashBytes(MapBytes) != ContentHash
            || (ProtectionHash != "" && ContentHasher.HashBytes(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ProtectionZones, AutomationJson.Options)) != ProtectionHash))
            throw new AutomationException("SNAPSHOT_CHANGED", "地图或保护信息快照哈希不一致。");
    }
}

public sealed partial class MapSession
{
    /// <summary>Capture under the session lock; callers render/analyze outside that lock.</summary>
    public async Task<MapSnapshot> CaptureSnapshotAsync(int? requiredRevision = null,
        CancellationToken cancellationToken = default)
    {
        await ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfClosed();
            if (requiredRevision.HasValue && requiredRevision.Value != Revision)
                throw new AutomationException("REVISION_CONFLICT",
                    $"requiredRevision 为 {requiredRevision}，当前为 {Revision}。");
            var bytes = await File.ReadAllBytesAsync(WorkingMapFilePath, cancellationToken).ConfigureAwait(false);
            return new MapSnapshot(MapId, SessionId, MapName, Revision, ContentHasher.HashBytes(bytes),
                Facade.MapPlayableWidth, Facade.MapPlayableHeight, Facade.MapBorderWidth, bytes)
            { ProtectionZones = Clone(ProtectionZones).AsReadOnly(), ProtectionHash = ProtectionHash(), DesignHash = DesignHash() };
        }
        finally { ExecutionLock.Release(); }
    }
}
