using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

public class ResizeTransformCommand: BaseTransformCommand
{
    public int NewWidth { get; private set; }
    public int NewHeight { get; private set; }
    public int NewPositionX { get; private set; }
    public int NewPositionY { get; private set; }
    public float DefaultTerrainHeight { get; private set; }
    public string DefaultTexture { get; private set; }
    
    public ResizeTransformCommand(Ra3MapFacade origin, int newWidth, int newHeight, int newPositionX, int newPositionY, float defaultTerrainHeight=200f, string defaultTexture="Dirt_Yucatan03") : base(origin)
    {
        if (newWidth <= 0 || newHeight <= 0)
        {
            throw new ArgumentException("New width and height must be greater than zero.");
        }
        
        NewWidth = newWidth;
        NewHeight = newHeight;
        NewPositionX = newPositionX;
        NewPositionY = newPositionY;
        DefaultTerrainHeight = defaultTerrainHeight;
        DefaultTexture = defaultTexture;
    }

    public override void Transform()
    {
        Ra3MapFacade newMapFacade = Ra3MapFacade.NewMap(NewWidth, NewHeight, 0, 0, DefaultTexture);

        var commonAreaLeftBottomX = Math.Max(0, NewPositionX);
        var commonAreaLeftBottomY = Math.Max(0, NewPositionY);
        
        var commonAreaRightTopX = Math.Min(OriginMapFacade.MapWidth, NewPositionX + NewWidth);
        var commonAreaRightTopY = Math.Min(OriginMapFacade.MapHeight, NewPositionY + NewHeight);
        
        for(int x = 0; x < NewWidth; x++)
        {
            for(int y = 0; y < NewHeight; y++)
            {
                newMapFacade.SetTerrainHeight(x, y, DefaultTerrainHeight);
            }
        }

        for (var x = commonAreaLeftBottomX; x < commonAreaRightTopX; x++)
        {
            for (var y = commonAreaLeftBottomY; y < commonAreaRightTopY; y++)
            {
                var newX = x - NewPositionX;
                var newY = y - NewPositionY;
                
                newMapFacade.SetTerrainHeight(newX, newY, OriginMapFacade.GetTerrainHeight(x, y));
                
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
            var originX = o.Position.X + OriginMapFacade.MapBorderWidth * 10;
            var originY = o.Position.Y + OriginMapFacade.MapBorderWidth * 10;
            
            if(originX >= commonAreaLeftBottomX * 10 && originX <= commonAreaRightTopX * 10 &&
               originY >= commonAreaLeftBottomY * 10 && originY <= commonAreaRightTopY * 10)
            {
                var newX = originX - NewPositionX * 10;
                var newY = originY - NewPositionY * 10;

                var newO = o.Clone(newMapFacade.ra3Map.Context);
                newO.Position = new Vec3D(newX, newY, o.Position.Z);
                
                newMapFacade.ra3Map.Context.ObjectsListAsset.Add(newO, true);
            }
        }
        
        DestinationRa3MapFacade = newMapFacade;
    }
}