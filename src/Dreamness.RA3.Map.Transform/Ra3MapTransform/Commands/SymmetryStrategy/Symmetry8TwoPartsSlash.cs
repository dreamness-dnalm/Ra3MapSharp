using System.Runtime.InteropServices;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

/**
     * *********
     * *       *
     * *      **
     * *     * *
     * *    *  *
     * *   *   *
     * * 1* 2  *
     * * *     *
     * **********
 */

public class Symmetry8TwoPartsSlash: SymmetryStrategy
{
    public Symmetry8TwoPartsSlash(Ra3MapFacade sourceMapFacade, int templateAreaIndex) : base(sourceMapFacade, templateAreaIndex, 2)
    {
        if (sourceMapFacade.MapWidth != sourceMapFacade.MapHeight)
        {
            throw new ArgumentException("Symmetry8TwoPartsSlash: source map must be square.");
        }
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
            return new Tuple<int, int>(y, x);
        }
    }

    protected override Tuple<float, float> GetUnitPosition(int templateAreaIndex, int targetAreaIndex, Tuple<float, float> originPosition)
    {
        if (templateAreaIndex == targetAreaIndex)
        {
            return originPosition;
        }
        
        var (x, y) = originPosition;
        
        return new Tuple<float, float>(y, x);
    }

    protected override float GetUnitAngle(int templateAreaIndex, int targetAreaIndex, float originAngle)
    {
        if (templateAreaIndex == targetAreaIndex)
        {
            return originAngle;
        }
        else
        {
            float r = 90 - originAngle;
            r = (r + 180) % 360 - 180; // wrap to (-180, 180]
            if (r <= -180) r += 360;
            return r;
        }
    }
}