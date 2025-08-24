// using Dreamness.RA3.Map.Parser.Core;
// using Dreamness.RA3.Map.Parser.Core.Asset;
//
// namespace Dreamness.RA3.Map.Facade.model;
//
// public class TeamModel
// {
//     private Team _team;
//     
//     private MapDataContext _mapContext;
//     
//     public TeamModel(Team team, MapDataContext context)
//     {
//         _team = team;
//         _mapContext = context;
//     }
//     
//     public string team_name
//     {
//         get => _team.propertyCollection.getProperty("teamName").data.ToString();
//         set => _team.propertyCollection.getProperty("teamName").data = value;
//     }
//     
//     public string belong_to_player_name
//     {
//         get => _team.propertyCollection.getProperty("teamOwner").data.ToString();
//         set => _team.propertyCollection.getProperty("teamOwner").data = value;
//     }
//     
//     public string team_full_name
//     {
//         get => belong_to_player_name + "/" + team_name;
//     }
//
//     public void set_team_unit(int index, int min, int max, string type_name, List<string> upgrades,
//         int experience_level)
//     {
//         if (index < 1 || index > 7)
//         {
//             throw new IndexOutOfRangeException("index must be between 1 and 7");
//         }
//
//         if (min == null)
//         {
//             throw new NullReferenceException("min cannot be null");
//         }
//         if (max == null)
//         {
//             throw new NullReferenceException("max cannot be null");
//         }
//         
//         _team.propertyCollection.addOrSetProperty("teamUnitMinCount" + index, min, _mapContext);
//         _team.propertyCollection.addOrSetProperty("teamUnitMaxCount" + index, max, _mapContext);
//
//
//         var typeKey = "teamUnitType" + index;
//         var experienceKey = "teamUnitExperienceLevel" + index;
//         var upgradeKey = "teamUnitUpgrade" + index;
//
//         if (type_name == null)
//         {
//             _team.propertyCollection.removeProperty(typeKey);
//             _team.propertyCollection.removeProperty(upgradeKey);
//             _team.propertyCollection.removeProperty(experienceKey);
//         }
//         else
//         {
//             _team.propertyCollection.addOrSetProperty(typeKey, "GameObject:" + type_name, _mapContext);
//             
//             if (experience_level == null || experience_level < 1 || experience_level > 4)
//             {
//                 throw new ArgumentException("Experience level must be between 1 and 4");
//             }
//             _team.propertyCollection.addOrSetProperty(experienceKey, experience_level, _mapContext);
//
//             if (upgrades == null)
//             {
//                 throw new NullReferenceException("upgrades cannot be null");
//             }
//             _team.propertyCollection.addOrSetProperty("teamUnitUpgradeList" + index, string.Join(' ', upgrades), _mapContext);
//         }
//         
//     }
//     
//     public int? get_team_unit_min(int index)
//     {
//         if (index < 1 || index > 7)
//         {
//             throw new IndexOutOfRangeException("index must be between 1 and 7");
//         }
//         var assetProperty = _team.propertyCollection.getProperty("teamUnitMinCount" + index);
//         if (assetProperty == null)
//         {
//             return null;
//         }
//         else
//         {
//             return (int)assetProperty.data;
//         }
//     }
//     
//     public int? get_team_unit_max(int index)
//     {
//         if (index < 1 || index > 7)
//         {
//             throw new IndexOutOfRangeException("index must be between 1 and 7");
//         }
//         var assetProperty = _team.propertyCollection.getProperty("teamUnitMaxCount" + index);
//         if (assetProperty == null)
//         {
//             return null;
//         }
//         else
//         {
//             return (int)assetProperty.data;
//         }
//     }
//     
//     public string? get_team_unit_type(int index)
//     {
//         if (index < 1 || index > 7)
//         {
//             throw new IndexOutOfRangeException("index must be between 1 and 7");
//         }
//         var assetProperty = _team.propertyCollection.getProperty("teamUnitType" + index);
//         if (assetProperty == null)
//         {
//             return null;
//         }
//         else
//         {
//             return assetProperty.data.ToString().Replace("GameObject:", "");
//         }
//     }
//     
//     public int? get_team_unit_experience_level(int index)
//     {
//         if (index < 1 || index > 7)
//         {
//             throw new IndexOutOfRangeException("index must be between 1 and 7");
//         }
//         var assetProperty = _team.propertyCollection.getProperty("teamUnitExperienceLevel" + index);
//         if (assetProperty == null)
//         {
//             return null;
//         }
//         else
//         {
//             return (int)assetProperty.data;
//         }
//     }
//     
//     public List<string> get_team_unit_upgrades(int index)
//     {
//         if (index < 1 || index > 7)
//         {
//             throw new IndexOutOfRangeException("index must be between 1 and 7");
//         }
//         var assetProperty = _team.propertyCollection.getProperty("teamUnitUpgradeList" + index);
//         if (assetProperty == null)
//         {
//             return new List<string>();
//         }
//         else
//         {
//             return ((string)assetProperty.data).Split(' ').ToList();
//         }
//     }
//     
//     public int? production_priority
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamProductionPriority");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (int)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamProductionPriority", value, _mapContext);
//     }
//     
//     public int? production_priority_success_increase
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamProductionPrioritySuccessIncrease");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (int)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamProductionPrioritySuccessIncrease", value, _mapContext);
//     }
//     
//     public int? production_priority_failure_decrease
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamProductionPriorityFailureDecrease");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (int)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamProductionPriorityFailureDecrease", value, _mapContext);
//     }
//     
//     public int? production_initial_idle_seconds
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamInitialIdleSeconds");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (int)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamInitialIdleSeconds", value, _mapContext);
//     }
//     
//     public bool? build_from_old_build_system
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamBuildFromOldBuildSystem");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (bool)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamBuildFromOldBuildSystem", value, _mapContext);
//     }
//     
//     public bool? auto_reinforce
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamAutoReinforce");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (bool)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamAutoReinforce", value, _mapContext);
//     }
//     
//     public bool? is_ai_recruitable
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamIsAIRecruitable");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (bool)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamIsAIRecruitable", value, _mapContext);
//     }
//     
//     public bool? attack_common_target
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("teamAttackCommonTarget");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return (bool)assetProperty.data;
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("teamAttackCommonTarget", value, _mapContext);
//     }
//     
//     public string? micro_manager
//     {
//         get
//         {
//             var assetProperty = _team.propertyCollection.getProperty("microManagerIndex");
//             if (assetProperty == null)
//             {
//                 return null;
//             }
//             else
//             {
//                 return assetProperty.data.ToString();
//             }
//         }
//         set => _team.propertyCollection.addOrSetProperty("microManagerIndex", value, _mapContext);
//     }
//
//     public override string ToString()
//     {
//         // return "TeamModel{" +
//         //        "team_name='" + team_name + '\'' +
//         //        ", belong_to_player_name='" + belong_to_player_name + '\'' +
//         //        '}';
//         return _team.propertyCollection.ToString();
//     }
// }
//
//
// // teamDescription : [propertyType=stringType, id=53, name='teamDescription', data=]
// // teamMaxInstances : [propertyType=intType, id=54, name='teamMaxInstances', data=1]
//
// // teamExecutesActionsOnCreate : [propertyType=boolType, id=58, name='teamExecutesActionsOnCreate', data=False]
// // teamHome : [propertyType=stringType, id=61, name='teamHome', data=Player_1_Start]
// // teamOnCreateScript : [propertyType=stringType, id=62, name='teamOnCreateScript', data=/Script 1]
// // teamProductionCondition : [propertyType=stringType, id=63, name='teamProductionCondition', data=/Script 1]
// // teamAggressiveness : [propertyType=intType, id=64, name='teamAggressiveness', data=-3]
// // teamTargetThreatFinderName : [propertyType=stringType, id=65, name='teamTargetThreatFinderName', data=<This Object>]
// // teamEnemySightedScript : [propertyType=stringType, id=77, name='teamEnemySightedScript', data=/Script 1]
// // teamOnDestroyedScript : [propertyType=stringType, id=78, name='teamOnDestroyedScript', data=/Script 1]
// // teamAllClearScript : [propertyType=stringType, id=79, name='teamAllClearScript', data=/Script 1]
// // teamEventsList : [propertyType=stringType, id=80, name='teamEventsList', data=AlienMCVUnpackingEventsList]
// // teamDestroyedThreshold : [propertyType=floatType, id=82, name='teamDestroyedThreshold', data=0.59999996]
// // teamGenericScriptHook0 : [propertyType=stringType, id=83, name='teamGenericScriptHook0', data=/Script 1]
// // teamGenericScriptHook1 : [propertyType=stringType, id=84, name='teamGenericScriptHook1', data=/Script 1]