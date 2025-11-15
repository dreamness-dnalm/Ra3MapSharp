// using Dreamness.Ra3.Map.Parser.Asset;
// using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
//
// namespace Dreamness.Ra3.Map.Parser.models;
//
//
// public class PlayerModel
// {
//
//     // {playerFaction:stringNameValueType=PlayerTemplate:Japan}, 
//     // {playerAllies:stringType=Player_3 Player_4}, 
//     // {playerEnemies:stringType=Player_1 Player_2}, 
//     // {playerColor:intType=-13481016}, 
//     // {aiPersonality:stringNameValueType=AIPersonalityDefinition:JapanCoopBasePersonality}, 
//     // {playerRadarColor:intType=-1698796}, 
//     // {playerFactionIcon:stringType=Allies}, 
//     // {aiBaseBuilder:intType=254}, 
//     // {aiUnitBuilder:intType=251}, 
//     // {aiTeamBuilder:intType=251}, 
//     // {aiEconomyBuilder:intType=253}, 
//     // {aiWallBuilder:intType=253}, 
//     // {aiUnitUpgrader:intType=247}, 
//     // {aiScienceUpgrader:intType=247}, 
//     // {aiTactical:intType=247}, 
//     // {aiOpeningMover:intType=247}}
//     
//     private PlayerData _playerData;
//
//     public String Name
//     {
//         get => _playerData.Properties.GetProperty<string>("PlayerName");
//         set
//         {
//             if (value == null)
//             {
//                 throw new NullReferenceException("PlayerName cannot be null");
//             }
//             _playerData.Properties.SetProperty("PlayerName", value);
//         }
//     }
//
//     public bool IsHuman
//     {
//         get => _playerData.Properties.GetProperty<bool>("playerIsHuman");
//         set
//         {
//             if (value == false)
//             {
//                 throw new NullReferenceException("PlayerIsHuman cannot be null");
//             }
//             _playerData.Properties.SetProperty("playerIsHuman", value);
//         }
//     }
//
//     public String DisplayName
//     {
//         get => _playerData.Properties.GetProperty<string>("playerDisplayName");
//         set
//         {
//             if (value == null)
//             {
//                 throw new NullReferenceException("playerDisplayName cannot be null");
//             }
//             _playerData.Properties.SetProperty("playerDisplayName", value);
//         }
//     }
//     
//     // public String Faction
// }
//
