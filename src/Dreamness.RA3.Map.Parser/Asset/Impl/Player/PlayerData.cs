using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Player;

public class PlayerData: Ra3MapWritable
{

    [JsonIgnore]
    public AssetProperties Properties { get; private set; }

    public static PlayerData FromJson(string json, BaseContext context)
    {
        var playerDataDoc = JsonSerializer.Deserialize<JsonDocument>(json);

        var playerData = new PlayerData();

        playerData.Properties = AssetProperties.Empty(context);
        
        playerData.Name = JsonUtil.GetString(playerDataDoc, "Name");
        playerData.IsHuman = JsonUtil.GetBool(playerDataDoc, "IsHuman");
        playerData.DisplayName = JsonUtil.GetString(playerDataDoc, "DisplayName");
        playerData.Faction = JsonUtil.GetString(playerDataDoc, "Faction");
        playerData.AllyPlayerNames = JsonUtil.GetStringList(playerDataDoc, "AllyPlayerNames");
        playerData.EnemyPlayerNames = JsonUtil.GetStringList(playerDataDoc, "EnemyPlayerNames");

        playerData.Personality = JsonUtil.GetStringOrNull(playerDataDoc, "Personality");
        playerData.FactionIcon = JsonUtil.GetStringOrNull(playerDataDoc, "FactionIcon");
        playerData.BaseBuilder = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "BaseBuilder");
        playerData.UnitBuilder = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "UnitBuilder");
        playerData.TeamBuilder = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "TeamBuilder");
        playerData.EconomyBuilder = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "EconomyBuilder");
        playerData.WallBuilder = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "WallBuilder");
        playerData.UnitUpgrader = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "UnitUpgrader");
        playerData.ScienceUpgrader = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "ScienceUpgrader");
        playerData.Tactical = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "Tactical");
        playerData.OpeningMover = JsonUtil.GetObjectOrNull<PlayerDifficultyConfig>(playerDataDoc, "OpeningMover");
        
        playerData.Color = JsonUtil.GetStringOrNull(playerDataDoc, "Color");
        playerData.RadarColor = JsonUtil.GetStringOrNull(playerDataDoc, "RadarColor");
        
        
        ObservableUtil.Subscribe(playerData.Properties, playerData);
        
        playerData.MarkModified();
        return playerData;

    }
    
    public static PlayerData FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var playerData = new PlayerData();
        
        playerData.Properties = AssetProperties.FromBinaryReader(binaryReader, context);
        ObservableUtil.Subscribe(playerData.Properties, playerData);
        binaryWriter.Write(playerData.Properties.ToBytes(context));
        
        var magic = binaryReader.ReadInt32();
        binaryWriter.Write(magic);
        
        binaryWriter.Flush();
        playerData.Data = memoryStream.ToArray();

        return playerData;
    }

    public static PlayerData Of(string playerName, BaseContext context)
    {
        var playerData = new PlayerData();

        var playerPropertiesDict = new Dictionary<string, object>()
        {
            ["playerName"] = playerName,
            ["playerIsHuman"] = false,
            ["playerDisplayName"] = playerName,
            ["playerFaction"] = "PlayerTemplate:FactionCivilian",
            ["playerAllies"] = "",
            ["playerEnemies"] = "",
        };
        

        playerData.Properties = AssetProperties.FromDict(playerPropertiesDict, context);
        ObservableUtil.Subscribe(playerData.Properties, playerData);
        
        playerData.MarkModified();
        return playerData;
        
    }

    public static PlayerData OfForDefault(string playerName, BaseContext context)
    {
        // TODO: review code
        string faction = "";
        if (playerName == "")
        {
            faction = "";
        }else if (playerName.Contains("Skirmish"))
        {
            faction = "PlayerTemplate:" + playerName.Substring("Skirmish".Length);
        }
        else
        {
            faction = "PlayerTemplate:FactionCivilian";
        }
        
        // AIDifficulty ai = AIDifficulty.Easy | AIDifficulty.Normal | AIDifficulty.Hard | AIDifficulty.Brutal;

        var playerData = new PlayerData();

        var playerPropertiesDict = new Dictionary<string, object>()
        {
            ["playerName"] = playerName,
            ["playerIsHuman"] = false,
            ["playerDisplayName"] = playerName == "" ? "Neutral" : playerName,
            ["playerFaction"] = faction,
            ["playerAllies"] = "",
            ["playerEnemies"] = "",
        };
        
        playerData.Properties = AssetProperties.FromDict(playerPropertiesDict, context);
        ObservableUtil.Subscribe(playerData.Properties, playerData);
        
        playerData.MarkModified();
        return playerData;
    }
    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            binaryWriter.Write(Properties.ToBytes(context));
            var magic = 0;
            binaryWriter.Write(magic);
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
    
    [Flags]
    public enum AIDifficulty
    {
        Easy = 0x1,
        Normal = 0x2,
        Hard = 0x4,
        Brutal = 0x8,
        Unused0 = 0x10,
        Unused1 = 0x20,
        Unused2 = 0x40,
        Unused3 = 0x80
    }

    public override string ToString()
    {
        return "PlayerData{" +
               "Properties=" + Properties + "\n" +
               '}';
    }
    
    [JsonInclude]
    public string Name
    {
        get => Properties.GetProperty<string>("playerName");
        set
        {
            if (value == null)
            {
                throw new NullReferenceException("PlayerName cannot be null");
            }
            Properties.PutProperty("playerName", value);
        }
    }
    
    [JsonInclude]
    public bool IsHuman
    {
        get => Properties.GetProperty<bool>("playerIsHuman");
        set
        {
            if (value == null)
            {
                throw new NullReferenceException("PlayerIsHuman cannot be null");
            }
            Properties.PutProperty("playerIsHuman", value);
        }
    }
    
    [JsonInclude]
    public string DisplayName
    {
        get => Properties.GetProperty<string>("playerDisplayName");
        set
        {
            if (value == null)
            {
                throw new NullReferenceException("playerDisplayName cannot be null");
            }
            Properties.PutProperty("playerDisplayName", value);
        }
    }

    [JsonInclude]
    public string Faction
    {
        get => Properties.GetProperty<string>("playerFaction").Replace("PlayerTemplate:", "");
        set 
        {
            if (value == null)
            {
                throw new NullReferenceException("playerFaction cannot be null");
            }
            Properties.PutProperty("playerFaction", "PlayerTemplate:" + value);
        }
    }
    
    [JsonInclude]
    public List<string> AllyPlayerNames
    {
        get
        {
            var alliesStr = Properties.GetProperty<string>("playerAllies");
            if (alliesStr == null || alliesStr.Trim() == "")
            {
                return new List<string>();
            }
            else
            {
                return alliesStr.Split(' ').ToList();
            }
        }
        set
        {
            if (value == null)
            {
                throw new NullReferenceException("playerAllies cannot be null");
            }
            Properties.PutProperty("playerAllies", string.Join(" ", value));
        }
    }
    
    [JsonInclude]
    public List<string> EnemyPlayerNames
    {
        get
        {
            var enemiesStr = Properties.GetProperty<string>("playerEnemies");
            if (enemiesStr == null || enemiesStr.Trim() == "")
            {
                return new List<string>();
            }
            else
            {
                return enemiesStr.Split(' ').ToList();
            }
        }
        set
        {
            if (value == null)
            {
                throw new NullReferenceException("playerEnemies cannot be null");
            }
            Properties.PutProperty("playerEnemies", string.Join(" ", value));
        }
    }
    
    [JsonInclude]
    public string? Personality
    {
        get
        {
            var s = Properties.GetProperty<string?>("aiPersonality");
            if (s == null)
            {
                return null;
            }
            else
            {
                return s.Replace("AIPersonalityDefinition:", "");
            }
        }
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiPersonality");
            }
            else
            {
                Properties.PutProperty("aiPersonality", "AIPersonalityDefinition:" + value);
            }
            
        }
    }
    
    [JsonInclude]
    public string? FactionIcon
    {
        get => Properties.GetProperty<string?>("playerFactionIcon");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("playerFactionIcon");
            }
            else
            {
                Properties.PutProperty("playerFactionIcon", value);
            }
        }
    }
    
    /// <summary>
    /// 基地建造
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? BaseBuilder
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiBaseBuilder"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiBaseBuilder");
            }
            else
            {
                Properties.PutProperty("aiBaseBuilder", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 单位建造
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? UnitBuilder
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiUnitBuilder"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiUnitBuilder");
            }
            else
            {
                Properties.PutProperty("aiUnitBuilder", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 队伍建造 (Party Builder)
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? TeamBuilder
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiTeamBuilder"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiTeamBuilder");
            }
            else
            {
                Properties.PutProperty("aiTeamBuilder", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 经济建造
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? EconomyBuilder
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiEconomyBuilder"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiEconomyBuilder");
            }
            else
            {
                Properties.PutProperty("aiEconomyBuilder", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 围墙建造
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? WallBuilder
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiWallBuilder"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiWallBuilder");
            }
            else
            {
                Properties.PutProperty("aiWallBuilder", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 单位改善 (单位升级)
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? UnitUpgrader
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiUnitUpgrader"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiUnitUpgrader");
            }
            else
            {
                Properties.PutProperty("aiUnitUpgrader", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 科技改善 (科技升级)
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? ScienceUpgrader
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiScienceUpgrader"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiScienceUpgrader");
            }
            else
            {
                Properties.PutProperty("aiScienceUpgrader", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 策略AI
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? Tactical
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiTactical"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiTactical");
            }
            else
            {
                Properties.PutProperty("aiTactical", value.ToValue());
            }
        }
    }
    
    /// <summary>
    /// 开局移动
    /// </summary>
    [JsonInclude]
    public PlayerDifficultyConfig? OpeningMover
    {
        get => PlayerDifficultyConfig.Of(Properties.GetProperty<int>("aiOpeningMover"));
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("aiOpeningMover");
            }
            else
            {
                Properties.PutProperty("aiOpeningMover", value.ToValue());
            }
        }
    }
    
    [JsonInclude]
    public String? Color
    {
        get
        {
            var color = Properties.GetProperty<int?>("playerColor");
            if (color == null)
            {
                return null;
            }

            return InvertedPlayerColor[color.Value];
        }
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("playerColor");
            }
            else
            {
                Properties.PutProperty("playerColor", PlayerColor[value]);
            }
        }
    }
    
    [JsonInclude]
    public String? RadarColor
    {
        get
        {
            var color = Properties.GetProperty<int?>("playerRadarColor");
            if (color == null)
            {
                return null;
            }

            return InvertedPlayerColor[color.Value];
        }
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("playerRadarColor");
            }
            else
            {
                Properties.PutProperty("playerRadarColor", PlayerColor[value]);
            }
        }
    }

    public static Dictionary<string, int> PlayerColor = new Dictionary<string, int>()
    {
        ["Blue"] = -13481016,
        ["Glod"] = -1186001,
        ["Green"] = -15439556,
        ["Orange"] = -2002156,
        ["Purple"] = -8578616,
        ["Red"] = -1698796,
        ["SkyBlue"] = -10039304
    };
    
    public static Dictionary<int, string> InvertedPlayerColor = new Dictionary<int, string>()
    {
        [-13481016] = "Blue",
        [-1186001] = "Glod",
        [-15439556] = "Green",
        [-2002156] = "Orange",
        [-8578616] = "Purple",
        [-1698796] = "Red",
        [-10039304] = "SkyBlue"
    };

}