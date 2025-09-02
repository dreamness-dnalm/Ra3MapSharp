using System.Runtime.InteropServices;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

/**
     * *********
     * *       *
     * *      **
     * *     *2*
     * *    *  *
     * *   *   *
     * * 1*    *
     * * *     *
     * **********
 */

public class Symmetry7TwoPartsSlashCenter: SymmetryStrategy
{
    public Symmetry7TwoPartsSlashCenter(Ra3MapFacade sourceMapFacade, int templateAreaIndex) : base(sourceMapFacade, templateAreaIndex, 2)
    {

    }

    protected override int GetAreaIndex(int x, int y)
    {
        double d = x - y;
        return d > 0 ? 1 : 2;
    }

    protected override int GetAreaIndexByDetailAxis(float detailAxisX, float detailAxisY)
    {
        double d = detailAxisX - detailAxisY;
        return d > 0 ? 1 : 2;
    }

    protected override Tuple<int, int> GetTemplateAreaPosPosition(int x, int y)
    {
        var areaIndex = GetAreaIndex(x, y);
        if (areaIndex == templateAreaIndex)
        {
            return new Tuple<int, int>(x, y);
        }
        else
        {
            return new Tuple<int, int>(width - x - 1, height - y - 1);
        }
    }

    protected override Tuple<float, float> GetUnitPosition(int templateAreaIndex, int targetAreaIndex, Tuple<float, float> originPosition)
    {
        if (templateAreaIndex == targetAreaIndex)
        {
            return originPosition;
        }
        
        var (x, y) = originPosition;
        
        return new Tuple<float, float>(width * 10.0f - x, height * 10.0f - y);
    }

    protected override float GetUnitAngle(int templateAreaIndex, int targetAreaIndex, float originAngle)
    {
        if (templateAreaIndex == targetAreaIndex)
        {
            return originAngle;
        }
        else
        {
            return originAngle >= 0 ? originAngle - 180 : originAngle + 180;
        }
    }
}