using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Default;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
using Dreamness.RA3.Map.Parser.Asset.Impl.MissionObjective;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Script;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Water;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Util;

public static class AssetParser
{
    public static BaseAsset FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        if (binaryReader.BaseStream.Length - binaryReader.BaseStream.Position < 10)
        {
            throw new InvalidDataException("Truncated asset header.");
        }

        BaseAsset asset = new DefaultAsset();
        asset.Id = binaryReader.ReadInt32();
        asset.Version = binaryReader.ReadInt16();
        asset.DataSize = binaryReader.ReadInt32();
        var remaining = binaryReader.BaseStream.Length - binaryReader.BaseStream.Position;
        if (asset.DataSize < 0 || asset.DataSize > remaining || asset.DataSize > MapFileCodec.MaxAssetDataSize)
        {
            throw new InvalidDataException(
                $"Invalid asset data size {asset.DataSize}; {remaining} bytes remain in the containing stream.");
        }

        asset.AssetType = context.GetDeclaredString(asset.Id);
        asset.Data = binaryReader.ReadBytesExactly(asset.DataSize, $"{asset.AssetType} asset payload");

        switch (asset.AssetType)
        {
            case AssetNameConst.WorldInfo:
                asset = asset.Clone<WorldInfoAsset>();
                // asset = WorldInfoAsset.FromBaseAsset(asset);
                (asset as WorldInfoAsset)?.Parse(context);
                break;
            case AssetNameConst.HeightMapData:
                asset = asset.Clone<HeightMapDataAsset>();
                (asset as HeightMapDataAsset)?.Parse(context);
                break;
            case AssetNameConst.SidesList:
                asset = asset.Clone<SidesListAsset>();
                (asset as SidesListAsset)?.Parse(context);
                break;
            case AssetNameConst.Teams:
                asset = asset.Clone<TeamsAsset>();
                (asset as TeamsAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.ObjectsList:
                asset = asset.Clone<ObjectsListAsset>();
                (asset as ObjectsListAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.Object:
                asset = asset.Clone<ObjectAsset>();
                (asset as ObjectAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.AssetList:
                asset = asset.Clone<AssetListAsset>();
                (asset as AssetListAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.GlobalVersion:
                asset = asset.Clone<GlobalVersionAsset>();
                (asset as GlobalVersionAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.PlayerScriptsList:
                asset = asset.Clone<PlayerScriptsList>();
                (asset as PlayerScriptsList)?.Parse(context);
                break;
            case AssetNameConst.ScriptList:
                asset = asset.Clone<ScriptList>();
                (asset as ScriptList)?.ParseTolerance(context);
                break;
            case AssetNameConst.Script:
                asset = asset.Clone<Script>();
                (asset as Script)?.ParseTolerance(context);
                break;
            case AssetNameConst.ScriptGroup:
                asset = asset.Clone<ScriptGroup>();
                (asset as ScriptGroup)?.ParseTolerance(context);
                break;
            case AssetNameConst.ScriptConditionContent:
                asset = asset.Clone<ScriptConditionContent>();
                (asset as ScriptConditionContent)?.ParseTolerance(context);
                break;
            case AssetNameConst.OrCondition:
                asset = asset.Clone<OrCondition>();
                (asset as OrCondition)?.ParseTolerance(context);
                break;
            case AssetNameConst.ScriptAction:
                asset = asset.Clone<ScriptAction>();
                (asset as ScriptAction)?.ParseTolerance(context);
                break;
            case AssetNameConst.ScriptActionFalse:
                asset = asset.Clone<ScriptActionFalse>();
                (asset as ScriptActionFalse)?.ParseTolerance(context);
                break;
            case AssetNameConst.MissionObjectives:
                asset = asset.Clone<MissionObjectivesAsset>();
                (asset as MissionObjectivesAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.MPPositionList:
                asset = asset.Clone<MPPositionListAsset>();
                (asset as MPPositionListAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.MPPositionInfo:
                asset = asset.Clone<MPPositionInfo>();
                (asset as MPPositionInfo)?.ParseTolerance(context);
                break;
            case AssetNameConst.LibraryMapLists:
                asset = asset.Clone<LibraryMapListsAsset>();
                (asset as LibraryMapListsAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.LibraryMaps:
                asset = asset.Clone<LibraryMaps>();
                (asset as LibraryMaps)?.ParseTolerance(context);
                break;
            case AssetNameConst.BuildLists:
                asset = asset.Clone<BuildListsAsset>();
                (asset as BuildListsAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.StandingWaterAreas:
                asset = asset.Clone<StandingWaterAreasAsset>();
                (asset as StandingWaterAreasAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.GlobalWaterSettings:
                asset = asset.Clone<GlobalWaterSettingsAsset>();
                (asset as GlobalWaterSettingsAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.GlobalLighting:
                asset = asset.Clone<GlobalLightingAsset>();
                (asset as GlobalLightingAsset)?.ParseTolerance(context);
                break;
            case AssetNameConst.PostEffectsChunk:
                asset = asset.Clone<PostEffectsChunkAsset>();
                (asset as PostEffectsChunkAsset)?.ParseTolerance(context);
                break;
            
            case AssetNameConst.BlendTileData:
                asset = asset.Clone<BlendTileDataAsset>();
                (asset as BlendTileDataAsset)?.ParseTolerance(context);
                break;
            default:
                ((DefaultAsset)asset).ParseTolerance(context);
                break;
            
            // TODO rich ObjectsList  , BlendTileData, PlayerScriptsList
            // TODO test: AssetList  GlobalVersion,StandingWaterAreas,GlobalWaterSettings, LibraryMapLists, GlobalLighting,PostEffectsChunk,MPPositionList
            
        }
        
        return asset;
    }
}
