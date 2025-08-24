using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

public class RotateTransformCommand: BaseTransformCommand
{
    public int ClockwiseAngle { get; private set; } = 0;
    
    public RotateTransformCommand(Ra3MapFacade origin, int clockwiseAngle) : base(origin)
    {
        if(clockwiseAngle != 90 && clockwiseAngle != 180 && clockwiseAngle != 270)
        {
            throw new ArgumentException("Clockwise angle must be one of the following values: 90, 180 or 270.");
        }
        
        ClockwiseAngle = clockwiseAngle;
    }


    public override void Transform()
    {
        var originWidth = OriginMapFacade.MapWidth;
        var originHeight = OriginMapFacade.MapHeight;
        var originBorderWidth = OriginMapFacade.MapBorderWidth;
        
        var newWidth = -1;
        var newHeight = -1;
        
        
        if (ClockwiseAngle == 180)
        {
            newWidth = originWidth;
            newHeight = originHeight;
        }
        else
        {
            newWidth = originHeight;
            newHeight = originWidth;
        }
        
        Ra3MapFacade newMapFacade = Ra3MapFacade.NewMap(newWidth, newHeight, 0, 0);

        for (int x = 0; x < originWidth; x++)
        {
            for (int y = 0; y < originHeight; y++)
            {
                var newX = -1;
                var newY = -1;
                
                if (ClockwiseAngle == 180)
                {
                    newX = originWidth - 1 - x;
                    newY = originHeight - 1 - y;
                }else if (ClockwiseAngle == 90)
                {
                    newX = y;
                    newY = originWidth - 1 - x;
                }
                else // ClockwiseAngle == 270
                {
                    newX = originHeight - 1 - y;
                    newY = x;
                }
                
                newMapFacade.SetTerrainHeight(newX, newY, OriginMapFacade.GetTerrainHeight(x, y));
                newMapFacade.SetTileTexture(newX, newY, OriginMapFacade.GetTileTexture(x, y));
                newMapFacade.SetPassability(newX, newY, OriginMapFacade.GetPassability(x, y));
                newMapFacade.SetTileBlend(newX, newY, OriginMapFacade.GetTileBlend(x, y));
                newMapFacade.SetTileSingleEdgeBlend(newX, newY, OriginMapFacade.GetTileSingleEdgeBlend(x, y));
                newMapFacade.SetTileCliffBlend(newX, newY, OriginMapFacade.GetTileCliffBlend(x, y));
                newMapFacade.SetTilePassageWidth(newX, newY, OriginMapFacade.GetTilePassageWidth(x, y));
                newMapFacade.SetTileVisibility(newX, newY, OriginMapFacade.GetTileVisibility(x, y));
                newMapFacade.SetTileBuildability(newX, newY, OriginMapFacade.GetTileBuildability(x, y));
                newMapFacade.SetTileTiberiumGrowability(newX, newY, OriginMapFacade.GetTileTiberiumGrowability(x, y));
                newMapFacade.SetTileDynamicShrubbery(newX, newY, OriginMapFacade.GetTileDynamicShrubbery(x, y));

            }
        }
        
        foreach (var o in OriginMapFacade.ra3Map.Context.ObjectsListAsset.MapObjectList)
        {
            var newO = o.Clone(newMapFacade.ra3Map.Context);

            var originX = newO.Position.X + originBorderWidth * 10;
            var originY = newO.Position.Y + originBorderWidth * 10;
            var originZ = newO.Position.Z;
            
            var newX = newO.Position.X;
            var newY = newO.Position.Y;
            
            var originAngle = newO.Angle;
            var newAngle = newO.Angle;
            

            if (ClockwiseAngle == 180)
            {
                newX = originWidth * 10 - originX;
                newY = originHeight * 10 - originY;
                
                // newAngle = originAngle + (originAngle >= 0 ? -180 : 180);
            }
            else if (ClockwiseAngle == 90)
            {
                newX = originY;
                newY = originWidth * 10 - originX;
            }
            else // ClockwiseAngle == 270
            {
                newX = originHeight * 10 - originY;
                newY = originX;
            }
            
            newO.Position = new Vec3D(newX, newY, originZ);
            
            newAngle = originAngle - ClockwiseAngle;
            if (newAngle < 0)
            {
                newAngle += 360;
            }
            
            if (!newO.IsWaypoint)
            {
                newO.Angle = newAngle;
            }
            
            newMapFacade.ra3Map.Context.ObjectsListAsset.Add(newO, true);
        }
        
        DestinationRa3MapFacade = newMapFacade;
    }
}