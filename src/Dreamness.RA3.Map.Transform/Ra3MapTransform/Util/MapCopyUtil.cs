using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Util;

public static class MapCopyUtil
{
    public static void CopyTileAndTerrain(
        Ra3MapFacade sourceMapFacade, int sourceX, int sourceY,
        Ra3MapFacade destinationMapFacade, int destX, int destY
        )
    {
        destinationMapFacade.SetTerrainHeight(destX, destY, sourceMapFacade.GetTerrainHeight(sourceX, sourceY));
        destinationMapFacade.SetTileTexture(destX, destY, sourceMapFacade.GetTileTexture(sourceX, sourceY));
        destinationMapFacade.SetPassability(destX, destY, sourceMapFacade.GetPassability(sourceX, sourceY));
        destinationMapFacade.SetTileBlend(destX, destY, sourceMapFacade.GetTileBlend(sourceX, sourceY));
        destinationMapFacade.SetTileSingleEdgeBlend(destX, destY, sourceMapFacade.GetTileSingleEdgeBlend(sourceX, sourceY));
        destinationMapFacade.SetTileCliffBlend(destX, destY, sourceMapFacade.GetTileCliffBlend(sourceX, sourceY));
        destinationMapFacade.SetTilePassageWidth(destX, destY, sourceMapFacade.GetTilePassageWidth(sourceX, sourceY));
        destinationMapFacade.SetTileVisibility(destX, destY, sourceMapFacade.GetTileVisibility(sourceX, sourceY));
        destinationMapFacade.SetTileBuildability(destX, destY, sourceMapFacade.GetTileBuildability(sourceX, sourceY));
        destinationMapFacade.SetTileTiberiumGrowability(destX, destY, sourceMapFacade.GetTileTiberiumGrowability(sourceX, sourceY));
        destinationMapFacade.SetTileDynamicShrubbery(destX, destY, sourceMapFacade.GetTileDynamicShrubbery(sourceX, sourceY));
    }
}