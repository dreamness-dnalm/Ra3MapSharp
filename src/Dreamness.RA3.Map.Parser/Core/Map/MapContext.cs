using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.RA3.Map.Parser.Asset.Impl.MissionObjective;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Core.Map;

public class MapContext: BaseContext
{

    
    public string ExportSidesListAssetToJson()
    {
        return SideListAsset.ToJson();
    }
    
    public void ImportSidesListAssetFromJson(string json)
    {
        var asset = SidesListAsset.FromJson(json, this);
        OverrideAsset(asset);
    }
    
    public string ExportTeamsAssetToJson()
    {
        return TeamsAsset.ToJson();
    }
    
    public void ImportTeamsAssetFromJson(string json)
    {
        var asset = TeamsAsset.FromJson(json, this);
        OverrideAsset(asset);
    }
    
    // public void
    
    public HeightMapDataAsset HeightMapDataAsset => AssetDict[AssetNameConst.HeightMapData] as HeightMapDataAsset;
    
    public WorldInfoAsset WorldInfoAsset => AssetDict[AssetNameConst.WorldInfo] as WorldInfoAsset;
    
    public TeamsAsset TeamsAsset => AssetDict[AssetNameConst.Teams] as TeamsAsset;
    
    public SidesListAsset SideListAsset => AssetDict[AssetNameConst.SidesList] as SidesListAsset;
    
    public BlendTileDataAsset BlendTileDataAsset => AssetDict[AssetNameConst.BlendTileData] as BlendTileDataAsset;
    
    public ObjectsListAsset ObjectsListAsset => AssetDict[AssetNameConst.ObjectsList] as ObjectsListAsset;
    
    public MissionObjectivesAsset MissionObjectivesAsset
    {
        get
        {
            if (AssetDict.ContainsKey(AssetNameConst.MissionObjectives))
            {
                return AssetDict[AssetNameConst.MissionObjectives] as MissionObjectivesAsset;
            }
            else
            {
                return null;
            }
            
        }
    }

    // public ObjectsListAsset ObjectsListAsset => AssetDict[AssetConst.ObjectsList] as ObjectsListAsset;
}