using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.RA3.Map.Transform.Ra3MapTransform.Util;
using Microsoft.VisualBasic.CompilerServices;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

public class SymmetryTransformCommand: BaseTransformCommand
{
    public int DivideCount { get; private set; }
    public int TemplateAreaIndex { get; private set; }
    public HashSet<int> ExecuteAreaIndies { get; private set; }
    public int DivideType { get; private set; }
    
    /***************
     *
     * divide type
     * 1
     * ********
     * * 1    *
     * ********
     * * 2    *
     * ********
     *
     * 2
     * ********
     * * 1    *
     * ********
     * *    2 *
     * ********
     *
     * 3
     * *********
     * * 1 * 2 *
     * *   *   *
     * *   *   *
     * *********
     *
     * 4
     * *********
     * * 1 *   *
     * *   *   *
     * *   * 2 *
     * *********
     *
     * 5
     * *********
     * *       *
     * * *     *
     * * 1*    *
     * *   *   *
     * *    * 2*
     * *     * *
     * **********
     *
     * 6  (必须是正方形)
     * *********
     * *  2    *
     * * *     *
     * * 1*    *
     * *   *   *
     * *    *  *
     * *     * *
     * **********
     *
     * 7
     * *********
     * *       *
     * *      **
     * *     *2*
     * *    *  *
     * *   *   *
     * * 1*    *
     * * *     *
     * **********
     *
     * 8 (必须是正方形)
     * *********
     * *       *
     * *      **
     * *     * *
     * *    *  *
     * *   *   *
     * * 1* 2  *
     * * *     *
     * **********
     *
     * 9
     * *****
     * *1*2*
     * *****
     * *3*4*
     * *****
     *
     * 10 
     * ***********
     * * *     * *
     * *  * 1 *  *
     * *   * *   *
     * * 4  *  2 *
     * *  * 3 *  *
     * * *     * *
     * ***********
     *
     * 11
     * 8等分
     * 
     * *****
     * ** 2*
     * * * *
     * *1 **
     *     *
     * *
     */
    
    public SymmetryTransformCommand(Ra3MapFacade origin, int divideType, int templateAreaIndex) : base(origin)
    {
        if((divideType == 6 || divideType == 8) && origin.MapWidth != origin.MapHeight)
        {
            throw new ArgumentException("divideType 6 and 8 must be used on square maps.");
        }
        
        if(divideType < 1 || divideType > 10)
        {
            throw new ArgumentException("divideType must be in range [1, 10]");
        }
        
        // ExecuteAreaIndies = new HashSet<int>(executeAreaIndies);
        // ExecuteAreaIndies.Remove(templateAreaIndex);
        //
        // if (ExecuteAreaIndies.Count == 0)
        // {
        //     throw new ArgumentException("ExecuteAreaIndies must contain at least one element.");
        // }
        //
        // int maxIndex = ExecuteAreaIndies.Max();
        // int minIndex = ExecuteAreaIndies.Min();
        //
        // if (minIndex < 1)
        // {
        //     throw new ArgumentException("minIndex must at last 1");
        // }
        //
        // if (divideType <= 8)
        // {
        //     if (maxIndex > 2)
        //     {
        //         throw new ArgumentException("bad index: " + maxIndex);
        //     }
        //
        //     DivideCount = 2;
        // }
        // else if (divideType <= 10)
        // {
        //     if (maxIndex > 4)
        //     {
        //         throw new ArgumentException("bad index: " + maxIndex);
        //     }
        //     DivideCount = 4;
        // }
        // else if (divideType == 11)
        // {
        //     if (maxIndex > 8)
        //     {
        //         throw new ArgumentException("bad index: " + maxIndex);
        //     }
        //
        //     DivideCount = 8;
        // }
        TemplateAreaIndex = templateAreaIndex;
        DivideType = divideType;
    }

    public override void Transform()
    {
        DestinationRa3MapFacade = SymmetryStrategy.SymmetryStrategy.Of(DivideType, OriginMapFacade, TemplateAreaIndex).Transform();
    }
}