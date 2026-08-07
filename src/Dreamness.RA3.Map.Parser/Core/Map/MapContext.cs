using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.RA3.Map.Parser.Asset.Impl.MissionObjective;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Script;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Water;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
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
        SynchronizeSideMetadata();
    }

    /// <summary>
    /// Synchronizes WorldBuilder's index-based library and skirmish-build-list
    /// metadata with the current SidesList while preserving retained entries.
    /// </summary>
    public void SynchronizeSideMetadata()
    {
        if (!AssetDict.TryGetValue(AssetNameConst.SidesList, out var sidesAsset) ||
            sidesAsset is not SidesListAsset sides)
        {
            return;
        }

        if (AssetDict.TryGetValue(AssetNameConst.LibraryMapLists, out var libraryAsset) &&
            libraryAsset is LibraryMapListsAsset libraries)
        {
            libraries.SynchronizeWithSideCount(sides.PlayerDataList.Count, this);
        }

        if (AssetDict.TryGetValue(AssetNameConst.BuildLists, out var buildAsset) &&
            buildAsset is BuildListsAsset builds)
        {
            builds.SynchronizeWithSides(sides);
        }
    }

    /// <summary>
    /// Appends a side and its corresponding index-based WorldBuilder metadata.
    /// </summary>
    public void AddSide(PlayerData player)
    {
        ArgumentNullException.ThrowIfNull(player);
        SideListAsset.PlayerDataList.Add(player);
        SynchronizeSideMetadata();
    }

    /// <summary>
    /// Removes a side and metadata at the same index so library imports and raw
    /// skirmish-build-list counts stay attached to the following players.
    /// </summary>
    public bool RemoveSide(PlayerData player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var index = SideListAsset.PlayerDataList.ToList().IndexOf(player);
        if (index < 0)
        {
            return false;
        }

        SideListAsset.PlayerDataList.Remove(player);

        if (AssetDict.TryGetValue(AssetNameConst.LibraryMapLists, out var libraryAsset) &&
            libraryAsset is LibraryMapListsAsset libraries && index < libraries.LibraryMapsList.Count)
        {
            libraries.LibraryMapsList.Remove(libraries.LibraryMapsList[index]);
        }

        if (AssetDict.TryGetValue(AssetNameConst.BuildLists, out var buildAsset) &&
            buildAsset is BuildListsAsset builds && index < builds.BuildLists.Count)
        {
            builds.BuildLists.Remove(builds.BuildLists[index]);
        }

        SynchronizeSideMetadata();
        return true;
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
    
    public void ImportPlayerScriptsListFromJson(string json)
    {
        var asset = PlayerScriptsListAsset.FromJson(json, this);
        OverrideAsset(asset);
    }
    
    public string ExportPlayerScriptsListToJson()
    {
        return PlayerScriptsListAsset.ToJson(this);
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
    
    public PlayerScriptsList PlayerScriptsListAsset => AssetDict[AssetNameConst.PlayerScriptsList] as PlayerScriptsList;

    public MPPositionListAsset MPPositionListAsset => (MPPositionListAsset)AssetDict[AssetNameConst.MPPositionList];

    public MPPositionListAsset MultiplayerPositions => MPPositionListAsset;

    public LibraryMapListsAsset LibraryMapListsAsset => (LibraryMapListsAsset)AssetDict[AssetNameConst.LibraryMapLists];

    public LibraryMapListsAsset ImportedLibraryMaps => LibraryMapListsAsset;

    public BuildListsAsset BuildListsAsset => (BuildListsAsset)AssetDict[AssetNameConst.BuildLists];

    public BuildListsAsset SkirmishBuildListMetadata => BuildListsAsset;

    public AssetListAsset AssetDependencies => (AssetListAsset)AssetDict[AssetNameConst.AssetList];

    public GlobalWaterSettingsAsset GlobalWaterSettingsAsset =>
        (GlobalWaterSettingsAsset)AssetDict[AssetNameConst.GlobalWaterSettings];

    public StandingWaterAreasAsset StandingWaterAreasAsset =>
        (StandingWaterAreasAsset)AssetDict[AssetNameConst.StandingWaterAreas];

    public GlobalLightingAsset GlobalLightingAsset => (GlobalLightingAsset)AssetDict[AssetNameConst.GlobalLighting];

    public PostEffectsChunkAsset PostEffectsChunkAsset =>
        (PostEffectsChunkAsset)AssetDict[AssetNameConst.PostEffectsChunk];
    
}
