using System.Runtime.InteropServices;
using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

/**
     * *********
     * *  2    *
     * * *     *
     * * 1*    *
     * *   *   *
     * *    *  *
     * *     * *
     * **********
 */

public class Symmetry6TwoPartsBackslash: SymmetryStrategy
{
    public Symmetry6TwoPartsBackslash(Ra3MapFacade sourceMapFacade, int templateAreaIndex) : base(sourceMapFacade, templateAreaIndex, 2)
    {
        if (sourceMapFacade.MapWidth != sourceMapFacade.MapHeight)
        {
            throw new ArgumentException("Symmetry6TwoPartsBackslash: source map must be square.");
        }
    }

    protected override int GetAreaIndex(int x, int y)
    {
        var t = height * x + width * y - width * height;
        return t <= 0 ? 1: 2;
    }

    protected override int GetAreaIndexByDetailAxis(float detailAxisX, float detailAxisY)
    {
        var t = (height * 10.0f) * detailAxisX + (width * 10.0f) * detailAxisY - (width * 10.0f) * (height * 10.0f);
        return t <= 0 ? 1: 2;
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
            return new Tuple<int, int>(height - y - 1, width - x -1);
        }
    }

    protected override Tuple<float, float> GetUnitPosition(int templateAreaIndex, int targetAreaIndex, Tuple<float, float> originPosition)
    {
        if (templateAreaIndex == targetAreaIndex)
        {
            return originPosition;
        }
        
        var (x, y) = originPosition;
        
        return new Tuple<float, float>(height * 10.0f - y, width * 10.0f - x);
    }

    protected override float GetUnitAngle(int templateAreaIndex, int targetAreaIndex, float originAngle)
    {
        if (templateAreaIndex == targetAreaIndex)
        {
            return originAngle;
        }
        else
        {
            float r = -90 - originAngle;
            r = (r + 180) % 360 - 180;
            if (r <= -180) r += 360;   // 保证落在 (-180, 180]
            return r;
        }
    }
}