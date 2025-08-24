

using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private SidesListAsset _sideListAsset;
    
    public WritableList<PlayerData> GetPlayers()
    {
        return _sideListAsset.PlayerDataList;
    }
    
    public string ExportPlayersToJsonStr()
    {
        return ra3Map.Context.ExportSidesListAssetToJson();
    }
    
    public void ImportPlayersFromJsonStr(string jsonStr)
    {
        ra3Map.Context.ImportSidesListAssetFromJson(jsonStr);
        LoadPlayer();
    }
    
    // --------- init -----------
    private void LoadPlayer()
    {
        _sideListAsset = ra3Map.Context.SideListAsset;
    }
}

//// {playerName:stringType=player0002}, 
//// {playerIsHuman:boolType=False}, 
//// {playerDisplayName:stringUnicodeType=Player 0002's Display Name}, 
//// {playerFaction:stringNameValueType=PlayerTemplate:Japan}, 
//// {playerAllies:stringType=Player_3 Player_4}, 
//// {playerEnemies:stringType=Player_1 Player_2}, 
//// {playerColor:intType=-13481016}, 
//// {aiPersonality:stringNameValueType=AIPersonalityDefinition:JapanCoopBasePersonality}, 
//// {playerRadarColor:intType=-1698796}, 
//// {playerFactionIcon:stringType=Allies}, 
// {aiBaseBuilder:intType=254}, 
// {aiUnitBuilder:intType=251}, 
// {aiTeamBuilder:intType=251}, 
// {aiEconomyBuilder:intType=253}, 
// {aiWallBuilder:intType=253}, 
// {aiUnitUpgrader:intType=247}, 
// {aiScienceUpgrader:intType=247}, 
// {aiTactical:intType=247}, 
// {aiOpeningMover:intType=247}}
// , PlayerBulidListItems=[Count=0, Assets=]