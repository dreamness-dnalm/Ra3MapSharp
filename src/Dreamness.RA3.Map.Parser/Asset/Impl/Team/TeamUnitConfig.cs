using System.Text.Json.Serialization;

namespace Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Team;

public class TeamUnitConfig
{
    public int MinCount { get; private set; }
    public int MaxCount { get; private set; }
    public string? UnitType { get; private set; }
    public List<string>? Upgrades { get; private set; }
    public int? ExpLv { get; private set; }

    
    [JsonConstructor]
    public TeamUnitConfig(int minCount, int maxCount, string unitType, List<string>? upgrades, int? expLv)
    {
        MinCount = minCount;
        MaxCount = maxCount;
        UnitType = unitType;
        Upgrades = upgrades ?? new List<string>();
        ExpLv = expLv;
    }

    public static TeamUnitConfig Of(int minCount, int maxCount, string? unitType, List<string>? upgrades, int? expLv)
    {
        return new TeamUnitConfig(minCount, maxCount, unitType, upgrades, expLv);
    }

    public static TeamUnitConfig Of(int minCount, int maxCount, string? unitTypeOrigin, string? upgrades, int? expLv)
    {
        var upgradeList = new List<string>();
        if (upgrades != null && upgrades.Length > 0)
        {
            upgradeList.AddRange(upgrades.Split(' ').Select(u => u.Trim()));
        }

        return Of(minCount, maxCount, unitTypeOrigin != null ? unitTypeOrigin.Replace("GameObject:", ""): null, upgradeList, expLv);
    }

    [JsonIgnore]
    public string? UpgradesStr
    {
        get
        {
            if (Upgrades == null)
            {
                return null;
            }
            else
            {
                return string.Join(" ", Upgrades);
            }
        }
    }
    
    [JsonIgnore]
    public string? UnitTypeOrigin
    {
        get
        {
            if (UnitType == null)
            {
                return null;
            }
            else
            {
                return "GameObject:" + UnitType;
            }
        }
    }
    
    
}