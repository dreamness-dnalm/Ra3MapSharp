using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands.SymmetryStrategy;

/**
 ** ********
    * * 1    *
    * ********
    * *    2 *
    * ********
 */

public class Symmetry1TwoPartsH: SymmetryStrategy
{
    public Symmetry1TwoPartsH(Ra3MapFacade sourceMapFacade, int templateAreaIndex) : base(sourceMapFacade, templateAreaIndex, 2)
    {
    }

    protected override int GetAreaIndex(int x, int y)
    {
        if (height % 2 == 0)
        {
            int mid = height / 2 - 1;
            if (y <= mid)
            {
                return 2;
            }
            else
            {
                return 1;
            }
        }
        else
        {
            int mid = (height + 1) / 2 - 1;
            if (y == mid)
            {
                return 0;
            }else if (y < mid)
            {
                return 2;
            }
            else
            {
                return 1;
            }
            
        }
    }

    protected override int GetAreaIndexByDetailAxis(float detailAxisX, float detailAxisY)
    {
        var mid = (height * 10.0f) / 2;
        if (detailAxisY <= mid)
        {
            return 2;
        }
        else
        {
            return 1;
        }
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