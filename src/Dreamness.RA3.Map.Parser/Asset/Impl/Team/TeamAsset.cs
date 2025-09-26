using System.Text.Json;
using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Team;

public class TeamAsset: Ra3MapWritable
{
    [JsonIgnore] public AssetProperties Properties { get; private set; }

    public static TeamAsset FromJson(string json, BaseContext context)
    {
        var doc = JsonSerializer.Deserialize<JsonDocument>(json);

        var teamAsset = new TeamAsset();
        teamAsset.Properties = AssetProperties.Empty(context);

        teamAsset.FullName = JsonUtil.GetString(doc, "FullName");
        teamAsset.IsSingleton = JsonUtil.GetBool(doc, "IsSingleton");
        teamAsset.MaxInstances = JsonUtil.GetIntOrNull(doc, "MaxInstances");
        teamAsset.HomeWaypoint = JsonUtil.GetStringOrNull(doc, "HomeWaypoint");
        teamAsset.Destination = JsonUtil.GetStringOrNull(doc, "Destination");
        teamAsset.AiType = JsonUtil.GetStringOrNull(doc, "AiType");
        
        teamAsset.ProductionCondition = JsonUtil.GetStringOrNull(doc, "ProductionCondition");
        teamAsset.ProductionPriority = JsonUtil.GetIntOrNull(doc, "ProductionPriority");
        teamAsset.ProductionPrioritySuccessIncrease = JsonUtil.GetIntOrNull(doc, "ProductionPrioritySuccessIncrease");
        teamAsset.ProductionPriorityFailureDecrease = JsonUtil.GetIntOrNull(doc, "ProductionPriorityFailureDecrease");
        teamAsset.ProductionBuildDurationSeconds = JsonUtil.GetIntOrNull(doc, "ProductionBuildDurationSeconds");
        teamAsset.ProductionExecutesActionsOnCreate = JsonUtil.GetBoolOrNull(doc, "ProductionExecutesActionsOnCreate");
        
        teamAsset.TeamUnitConfig1 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig1");
        teamAsset.TeamUnitConfig2 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig2");
        teamAsset.TeamUnitConfig3 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig3");
        teamAsset.TeamUnitConfig4 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig4");
        teamAsset.TeamUnitConfig5 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig5");
        teamAsset.TeamUnitConfig6 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig6");
        teamAsset.TeamUnitConfig7 = JsonUtil.GetObjectOrNull<TeamUnitConfig>(doc, "TeamUnitConfig7");
        
        teamAsset.MicroManager = JsonUtil.GetStringOrNull(doc, "MicroManager");
        
        teamAsset.AutoReinforce = JsonUtil.GetBoolOrNull(doc, "AutoReinforce");
        teamAsset.IsAiRecruitable = JsonUtil.GetBoolOrNull(doc, "IsAiRecruitable");
        teamAsset.Description = JsonUtil.GetStringOrNull(doc, "Description");
        
        teamAsset.OnCreateScript = JsonUtil.GetStringOrNull(doc, "OnCreateScript");
        teamAsset.OnEnemyScript = JsonUtil.GetStringOrNull(doc, "OnEnemyScript");
        teamAsset.OnAllClearScript = JsonUtil.GetStringOrNull(doc, "OnAllClearScript");
        teamAsset.OnDestroyedPercentage = JsonUtil.GetIntOrNull(doc, "OnDestroyedPercentage");
        teamAsset.OnDestroyedScript = JsonUtil.GetStringOrNull(doc, "OnDestroyedScript");
        teamAsset.InitialAggressiveness = JsonUtil.GetStringOrNull(doc, "InitialAggressiveness");
        teamAsset.AttackSingleTargetWhileHardAndBrutal = JsonUtil.GetBoolOrNull(doc, "AttackSingleTargetWhileHardAndBrutal");
        
        teamAsset.EventsList = JsonUtil.GetStringOrNull(doc, "EventsList");
        
        teamAsset.GenericScripts = JsonUtil.GetStringList(doc, "GenericScripts");

        ObservableUtil.Subscribe(teamAsset.Properties, teamAsset);

        teamAsset.MarkModified();
        return teamAsset;
    }

    public static TeamAsset FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var teamAsset = new TeamAsset();

        teamAsset.Properties = AssetProperties.FromBinaryReader(binaryReader, context);
        ObservableUtil.Subscribe(teamAsset, teamAsset.Properties);
        binaryWriter.Write(teamAsset.Properties.ToBytes(context));

        binaryWriter.Flush();
        teamAsset.Data = memoryStream.ToArray();
        return teamAsset;
    }

    public static TeamAsset Of(string teamName, string ownerPlayerName, BaseContext context)
    {
        var teamAsset = new TeamAsset();

        var dict = new Dictionary<string, object>()
        {
            ["teamName"] = teamName,
            ["teamOwner"] = ownerPlayerName,
            ["teamIsSingleton"] = true
        };

        teamAsset.Properties = AssetProperties.FromDict(dict, context);
        ObservableUtil.Subscribe(teamAsset, teamAsset.Properties);
        teamAsset.MarkModified();
        return teamAsset;
    }

    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            return Properties.ToBytes(context);
        }
        else
        {
            return Data;
        }
    }
    
    // -------------------------------
    // ----------- Identify --------------------
    // -------------------------------
    [JsonIgnore]
    public string Name
    {
        get => Properties.GetProperty<string>("teamName");
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException("teamName cannot be null");
            }

            Properties.PutProperty("teamName", value);
        }
    }

    [JsonIgnore]
    public string OwnerPlayerName
    {
        get => Properties.GetProperty<string>("teamOwner");
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException("teamOwner cannot be null");
            }

            Properties.PutProperty("teamOwner", value);
        }
    }

    public bool IsSingleton
    {
        get => Properties.GetProperty<bool>("teamIsSingleton");
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException("teamIsSingleton cannot be null");
            }

            Properties.PutProperty("teamIsSingleton", value);
        }
    }

    public string FullName
    {
        get => OwnerPlayerName + "/" + Name;
        set
        {
            if (!value.Contains("/"))
            {
                throw new ArgumentException("Team name must contain one '/'.");
            }

            var parts = value.Split("/");
            if (parts.Length != 2)
            {
                throw new ArgumentException("Team name must contain one '/'.");
            }

            OwnerPlayerName = parts[0];
            Name = parts[1];
        }
    }

    public int? MaxInstances
    {
        get => Properties.GetProperty<int?>("teamMaxInstances");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamMaxInstances");
            }
            else
            {
                Properties.PutProperty("teamMaxInstances", value);
            }
        }
    }

    public string? HomeWaypoint
    {
        get => Properties.GetProperty<string?>("teamHome");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamHome");
            }
            else
            {
                Properties.PutProperty("teamHome", value);
            }
        }
    }

    public string? Destination
    {
        get => Properties.GetProperty<string?>("teamTargetThreatFinderName");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamTargetThreatFinderName");
            }
            else
            {
                Properties.PutProperty("teamTargetThreatFinderName", value);
            }
        }
    }

    public string? AiType
    {
        get
        {
            var originValue = Properties.GetProperty<int?>("teamType");
            if (originValue == null)
            {
                return null;
            }
            else
            {
                return InvetedAiTypeDict[originValue.Value];
            }
        }
        
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamType");
            }
            else
            {
                Properties.PutProperty("teamType", AiTypeDict[value]);
            }
        }
    }
    
    // ----------- Identify.Production --------------------
    
    public string? ProductionCondition
    {
        get => Properties.GetProperty<string?>("teamProductionCondition");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamProductionCondition");
            }
            else
            {
                Properties.PutProperty("teamProductionCondition", value);
            }
        }
    }
    
    public int? ProductionPriority
    {
        get => Properties.GetProperty<int?>("teamProductionPriority");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamProductionPriority");
            }
            else
            {
                Properties.PutProperty("teamProductionPriority", value);
            }
        }
    }
    
    public int? ProductionPrioritySuccessIncrease
    {
        get => Properties.GetProperty<int?>("teamProductionPrioritySuccessIncrease");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamProductionPrioritySuccessIncrease");
            }
            else
            {
                Properties.PutProperty("teamProductionPrioritySuccessIncrease", value);
            }
        }
    }
    
    public int? ProductionPriorityFailureDecrease
    {
        get => Properties.GetProperty<int?>("teamProductionPriorityFailureDecrease");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamProductionPriorityFailureDecrease");
            }
            else
            {
                Properties.PutProperty("teamProductionPriorityFailureDecrease", value);
            }
        }
    }
    
    public int? ProductionBuildDurationSeconds
    {
        get => Properties.GetProperty<int?>("teamInitialIdleSeconds");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamInitialIdleSeconds");
            }
            else
            {
                Properties.PutProperty("teamInitialIdleSeconds", value);
            }
        }
    }
    
    public bool? ProductionExecutesActionsOnCreate
    {
        get => Properties.GetProperty<bool?>("teamExecutesActionsOnCreate");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamExecutesActionsOnCreate");
            }
            else
            {
                Properties.PutProperty("teamExecutesActionsOnCreate", value);
            }
        }
    }

    
    // ----------- Identify.Units --------------------
    
    private TeamUnitConfig? getTeamUnitConfig(int ith)
    {
        var unitTypeOrigin = Properties.GetProperty<string?>($"teamUnitType{ith}");
        var minCount = Properties.GetProperty<int?>($"teamUnitMinCount{ith}");
        var maxCount = Properties.GetProperty<int?>($"teamUnitMaxCount{ith}");
        var upgradeStr = Properties.GetProperty<string?>($"teamUnitUpgradeList{ith}");
        var expLv = Properties.GetProperty<int?>($"teamUnitExperienceLevel{ith}");

        if (minCount == null || maxCount == null)
        {
            return null;
        }
        else
        {
            return TeamUnitConfig.Of(minCount.Value, maxCount.Value, unitTypeOrigin, upgradeStr, expLv);
        }
    }

    public void setTeamUnitConfig(int ith, TeamUnitConfig? config)
    {
        if (ith < 1 || ith > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(ith), "ith must be between 1 and 7.");
        }

        if (config == null)
        {
            Properties.RemoveProperty($"teamUnitType{ith}");
            Properties.RemoveProperty($"teamUnitMinCount{ith}");
            Properties.RemoveProperty($"teamUnitMaxCount{ith}");
            Properties.RemoveProperty($"teamUnitUpgradeList{ith}");
            Properties.RemoveProperty($"teamUnitExperienceLevel{ith}");
        }
        else
        {
            if (config.UnitTypeOrigin == null)
            {
                Properties.RemoveProperty($"teamUnitType{ith}");
            }
            else
            {
                Properties.PutProperty($"teamUnitType{ith}", AssetProperty.AssetPropertyType.stringNameValueType, config.UnitTypeOrigin);
            }
            
            
            Properties.PutProperty($"teamUnitMinCount{ith}", config.MinCount);
            Properties.PutProperty($"teamUnitMaxCount{ith}", config.MaxCount);

            if (config.ExpLv == null)
            {
                Properties.RemoveProperty($"teamUnitExperienceLevel{ith}");
            }
            else
            {
                Properties.PutProperty($"teamUnitExperienceLevel{ith}", config.ExpLv);
            }
            
            if (config.UpgradesStr == null)
            {
                Properties.RemoveProperty($"teamUnitUpgradeList{ith}");
            }
            else
            {
                Properties.PutProperty($"teamUnitUpgradeList{ith}", config.UpgradesStr);
            }
        }

        MarkModified();
    }

    public TeamUnitConfig? TeamUnitConfig1
    {
        get => getTeamUnitConfig(1);
        set => setTeamUnitConfig(1, value);
    }

    public TeamUnitConfig? TeamUnitConfig2
    {
        get => getTeamUnitConfig(2);
        set => setTeamUnitConfig(2, value);
    }

    public TeamUnitConfig? TeamUnitConfig3
    {
        get => getTeamUnitConfig(3);
        set => setTeamUnitConfig(3, value);
    }

    public TeamUnitConfig? TeamUnitConfig4
    {
        get => getTeamUnitConfig(4);
        set => setTeamUnitConfig(4, value);
    }

    public TeamUnitConfig? TeamUnitConfig5
    {
        get => getTeamUnitConfig(5);
        set => setTeamUnitConfig(5, value);
    }

    public TeamUnitConfig? TeamUnitConfig6
    {
        get => getTeamUnitConfig(6);
        set => setTeamUnitConfig(6, value);
    }

    public TeamUnitConfig? TeamUnitConfig7
    {
        get => getTeamUnitConfig(7);
        set => setTeamUnitConfig(7, value);
    }
    
    public string? MicroManager
    {
        get => Properties.GetProperty<string?>("microManagerIndex");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("microManagerIndex");
            }
            else
            {
                Properties.PutProperty("microManagerIndex", value);
            }
        }
    }





    // ----------- Identify.Recruitment_Options --------------------
    
    public bool? AutoReinforce
    {
        get => Properties.GetProperty<bool?>("teamAutoReinforce");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamAutoReinforce");
            }
            else
            {
                Properties.PutProperty("teamAutoReinforce", value);
            }
        }
    }
    
    public bool? IsAiRecruitable
    {
        get => Properties.GetProperty<bool?>("teamIsAIRecruitable");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamIsAIRecruitable");
            }
            else
            {
                Properties.PutProperty("teamIsAIRecruitable", value);
            }
        }
    }
    
    public string? Description
    {
        get => Properties.GetProperty<string?>("teamDescription");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamDescription");
            }
            else
            {
                Properties.PutProperty("teamDescription", value);
            }
        }
    }
    
    // -------------------------------
    // ----------- Behavior --------------------
    // -------------------------------
    
    // --------- Behavior Script Triggers ----------
    
    public string? OnCreateScript
    {
        get => Properties.GetProperty<string?>("teamOnCreateScript");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamOnCreateScript");
            }
            else
            {
                Properties.PutProperty("teamOnCreateScript", value);
            }
        }
    }
    
    public string? OnEnemyScript
    {
        get => Properties.GetProperty<string?>("teamEnemySightedScript");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamEnemySightedScript");
            }
            else
            {
                Properties.PutProperty("teamEnemySightedScript", value);
            }
        }
    }
    
    public string? OnAllClearScript
    {
        get => Properties.GetProperty<string?>("teamAllClearScript");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamAllClearScript");
            }
            else
            {
                Properties.PutProperty("teamAllClearScript", value);
            }
        }
    }

    public int? OnDestroyedPercentage
    {
        get
        {
            var originValue = Properties.GetProperty<float?>("teamDestroyedThreshold");
            if (originValue == null)
            {
                return null;
            }
            else
            {
                return (int)MathF.Round(originValue.Value * 100, 0);
            }
        }
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamDestroyedThreshold");
            }
            else
            {
                Properties.PutProperty("teamDestroyedThreshold", value / 100f);
            }
        }
    }
    
    public string? OnDestroyedScript
    {
        get => Properties.GetProperty<string?>("teamOnDestroyedScript");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamOnDestroyedScript");
            }
            else
            {
                Properties.PutProperty("teamOnDestroyedScript", value);
            }
        }
    }
    
    public string? InitialAggressiveness
    {
        get
        {
            var originValue = Properties.GetProperty<int?>("teamAggressiveness");
            if (originValue == null)
            {
                return null;
            }
            else
            {
                return InvetedAggressivenessDict[originValue.Value];
            }
        }
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamAggressiveness");
            }
            else
            {
                Properties.PutProperty("teamAggressiveness", AggressivenessDict[value]);
            }
        }
    }
    
    
    public bool? AttackSingleTargetWhileHardAndBrutal
    {
        get => Properties.GetProperty<bool?>("teamAttackCommonTarget");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamAttackCommonTarget");
            }
            else
            {
                Properties.PutProperty("teamAttackCommonTarget", value);
            }
        }
    }
    
    // ---------- Team Events List --------------
    
    public string? EventsList
    {
        get => Properties.GetProperty<string?>("teamEventsList");
        set
        {
            if (value == null)
            {
                Properties.RemoveProperty("teamEventsList");
            }
            else
            {
                Properties.PutProperty("teamEventsList", value);
            }
        }
    }
    
    // -------------------------------
    // ----------- Generic --------------------
    // -------------------------------
    
    public List<string?> GenericScripts
    {
        get
        {
            return Properties.PropertyNames
                .Where(s => s.StartsWith("teamGenericScriptHook"))
                .OrderBy(s => int.Parse(s.Replace("teamGenericScriptHook", "")))
                .Select(k => Properties.GetProperty<string?>(k))
                .ToList();
        }
        set
        {
            var originNames = Properties.PropertyNames
                .Where(s => s.StartsWith("teamGenericScriptHook"))
                .ToList();
            foreach (var originName in originNames)
            {
                Properties.RemoveProperty(originName);
            }

            if (value == null)
            {

            }
            else
            {
                for (int i = 0; i < value.Count; i++)
                {
                    var script = value[i];
                    if (script != null)
                    {
                        Properties.PutProperty($"teamGenericScriptHook{i}", script);
                    }
                }
            }
        }
    }
    
    
    private Dictionary<string, int> AiTypeDict = new Dictionary<string, int>()
    {
        ["ATTACK_TEAM"] = 0,
        ["DEFENSE_TEAM"] = 1,
        ["EXPLORE_TEAM"] = 2
    };
    private Dictionary<int, string> InvetedAiTypeDict = new Dictionary<int, string>()
    {
        [0] = "ATTACK_TEAM",
        [1] = "DEFENSE_TEAM",
        [2] = "EXPLORE_TEAM"
    };
    
    private Dictionary<string, int> AggressivenessDict = new Dictionary<string, int>()
    {
        ["Peaceful"] = -3,
        ["Sleep"] = -2,
        ["Passive"] = -1,
        ["Normal"] = 0,
        ["Alert"] = 1,
        ["Aggressive"] = 2
    };
    
    private Dictionary<int, string> InvetedAggressivenessDict = new Dictionary<int, string>()
    {
        [-3] = "Peaceful",
        [-2] = "Sleep",
        [-1] = "Passive",
        [0] = "Normal",
        [1] = "Alert",
        [2] = "Aggressive"
    };
}