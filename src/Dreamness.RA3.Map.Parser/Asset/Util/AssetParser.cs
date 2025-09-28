using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Default;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Script;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Asset.Util;

public static class AssetParser
{
    public static BaseAsset FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        BaseAsset asset = new DefaultAsset();
        asset.Id = binaryReader.ReadInt32();
        asset.Version = binaryReader.ReadInt16();
        asset.DataSize = binaryReader.ReadInt32();
        asset.AssetType = context.GetDeclaredString(asset.Id);
        asset.Data = binaryReader.ReadBytes(asset.DataSize);

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
                (asset as PlayerScriptsList)?.ParseTolerance(context);
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
            
            case AssetNameConst.BlendTileData:
                asset = asset.Clone<BlendTileDataAsset>();
                (asset as BlendTileDataAsset)?.ParseTolerance(context);
                break;
            // case AssetConst.StandingWaterAreas:
            //     asset = asset.Clone<StandingWaterAreasAsset>();
            //     (asset as StandingWaterAreasAsset)?.Parse(context);
            //     break;
            // case AssetConst.GlobalLighting:
            //     asset = asset.Clone<GlobalLightingAsset>();
            //     (asset as GlobalLightingAsset)?.Parse(context);
            //     break;
                // case AssetConst.BuildLists:
                //     asset = asset.Clone<BuildListsAsset>();
                //     (asset as BuildListsAsset)?.Parse(context);
                //     break;
            default:
                (asset as DefaultAsset).ParseTolerance(context);
                break;
            
            // TODO rich ObjectsList  , BlendTileData, PlayerScriptsList
            // TODO test: AssetList  GlobalVersion,StandingWaterAreas,GlobalWaterSettings, LibraryMapLists, GlobalLighting,PostEffectsChunk,MPPositionList
            
        }
        
        return asset;
    }
}