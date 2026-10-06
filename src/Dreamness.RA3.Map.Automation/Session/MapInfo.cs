namespace Dreamness.RA3.Map.Automation.Session;

/// <summary>
/// 地图级元数据，保存在 <c>.automation/MapInfo.json</c>。
/// </summary>
public sealed class MapInfo
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// 稳定的地图身份，不随文件夹名变化。
    /// </summary>
    public string MapId { get; set; } = "";
    
}
