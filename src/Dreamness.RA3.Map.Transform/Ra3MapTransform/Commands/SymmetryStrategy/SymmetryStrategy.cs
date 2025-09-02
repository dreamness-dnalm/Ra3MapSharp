using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.RA3.Map.Transform.Ra3MapTransform.Util;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

public abstract class SymmetryStrategy
{
    protected Ra3MapFacade sourceMapFacade { get; private set; }
    
    protected int width { get; private set; }
    
    protected int height { get; private set; }
    
    protected int borderWidth { get; private set; }
    
    protected int templateAreaIndex { get; private set; }
    
    protected int maxAreaIndex { get; private set; }
    
    public SymmetryStrategy(Ra3MapFacade sourceMapFacade, int templateAreaIndex, int maxAreaIndex)
    {
        this.sourceMapFacade = sourceMapFacade;
        this.templateAreaIndex = templateAreaIndex;

        this.width = sourceMapFacade.MapWidth;
        this.height = sourceMapFacade.MapHeight;
        this.borderWidth = sourceMapFacade.MapBorderWidth;
        this.maxAreaIndex = maxAreaIndex;
    }
    
    public Ra3MapFacade Transform()
    {
        var targetMapFacade = Ra3MapFacade.NewMap(width, height, 0, 0);
        
        // 地形和纹理
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int areaIndex = GetAreaIndex(x, y);
                if (areaIndex == 0)
                {
                    // areaIndex == 0, 不属于任何区域, 直接复制
                    MapCopyUtil.CopyTileAndTerrain(sourceMapFacade, x, y, targetMapFacade, x, y);
                }
                else
                {
                    var (templateX, templateY) = GetTemplateAreaPosPosition(x, y);
                    MapCopyUtil.CopyTileAndTerrain(sourceMapFacade, templateX, templateY, targetMapFacade, x, y);
                }
            }
        }
        
        // 物体
        var objectAssets = sourceMapFacade.ra3Map.Context.ObjectsListAsset.MapObjectList;
        
        foreach (var objectAsset in objectAssets)
        {
            // var detailAxisX = objectAsset.Position.X + borderWidth * 10;
            // var detailAxisY = objectAsset.Position.Y + borderWidth * 10;
            // var detailAxisZ = objectAsset.Position.Z;
            //
            // var x = Math.Floor(detailAxisX / 10.0);
            // var y = Math.Floor(detailAxisY / 10.0);
            
            var newObjectAssets = CloneTemplateAreaObjectAsset(objectAsset, targetMapFacade.ra3Map.Context);

            foreach (var newObjectAsset in newObjectAssets)
            {
                targetMapFacade.ra3Map.Context.ObjectsListAsset.Add(newObjectAsset, true);
            }
        }

        return targetMapFacade;
    }
    
    protected abstract int GetAreaIndex(int x, int y);
    
    protected abstract int GetAreaIndexByDetailAxis(float detailAxisX, float detailAxisY);
    
    // protected abstract List<Tuple<float, float>> GetDetailAxisPositionsByTemplateDetailAxisPosition(int templateAreaIndex, float templateDetailAxisX, float templateDetailAxisY);
    protected List<ObjectAsset> CloneTemplateAreaObjectAsset(ObjectAsset asset, BaseContext context)
    {
        var ret = new List<ObjectAsset>();

        var detailAxisX = asset.Position.X + borderWidth * 10;
        var detailAxisY = asset.Position.Y + borderWidth * 10;
        var detailAxisZ = asset.Position.Z;
        
        var x = Math.Floor(detailAxisX / 10.0);
        var y = Math.Floor(detailAxisY / 10.0);
        
        if(GetAreaIndexByDetailAxis(detailAxisX, detailAxisY) == templateAreaIndex)
        {
            for (int i = 1; i <= maxAreaIndex; i++)
            {
                var newAsset = asset.Clone(context);

                var wrap = ObjectWrap.Of(newAsset);
                var originPosition = wrap.Position;

                var (newX, newY) = GetUnitPosition(templateAreaIndex, i, new Tuple<float, float>(detailAxisX, detailAxisY));
                wrap.Position = new Vec3D(newX, newY, detailAxisZ);

                if (wrap is WaypointWrap waypointWrap)
                {
                    waypointWrap.WaypointName += $"_clone_{i}";
                    waypointWrap.Properties.PutProperty("uniqueID", waypointWrap.WaypointName);
                }
                else if (wrap is UnitObjectWrap unitObjectWrap)
                {
                    unitObjectWrap.Angle = GetUnitAngle(templateAreaIndex, i, unitObjectWrap.Angle);
                    if(unitObjectWrap.ObjName != null && unitObjectWrap.ObjName.Length > 0)
                    {
                        unitObjectWrap.ObjName += $"_clone_{i}";
                    }
                }

                ret.Add(newAsset);
            }
        }
        
        return ret;
    }
    
    
    protected abstract Tuple<int, int> GetTemplateAreaPosPosition(int x, int y);

    protected abstract Tuple<float, float> GetUnitPosition(int templateAreaIndex, int targetAreaIndex,
        Tuple<float, float> originPosition);
    
    protected abstract float GetUnitAngle(int templateAreaIndex, int targetAreaIndex, float originAngle);
    
    public static SymmetryStrategy Of(int symmetryType, Ra3MapFacade map, int templateAreaIndex)
    {
        switch (symmetryType)
        {
            // case 0:
            //     return new HorizontalSymmetryStrategy();
            case 1:
                return new Symmetry1TwoPartsH(map, templateAreaIndex);
            case 2:
                return new Symmetry2TwoPartsHCenter(map, templateAreaIndex);
            case 3:
                return new Symmetry3TwoPartsV(map, templateAreaIndex);
            case 4:
                return new  Symmetry4TwoPartsVCenter(map, templateAreaIndex);
            case 5:
                return new Symmetry5TwoPartsBackslashCenter(map, templateAreaIndex);
            case 6:
                return new Symmetry6TwoPartsBackslash(map, templateAreaIndex);
            case 7:
                return new Symmetry7TwoPartsSlashCenter(map, templateAreaIndex);
            case 8:
                return new Symmetry8TwoPartsSlash(map, templateAreaIndex);
            // case 9:
            // case 10:
            // case 11:
            default:
                throw new ArgumentException("Invalid symmetry type");
        }
    }
}