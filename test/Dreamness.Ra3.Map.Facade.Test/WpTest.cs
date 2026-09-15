using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Facade.Util;
using Dreamness.Ra3.Map.Parser.Core.Map;

namespace Dreamness.Ra3.Map.Facade.Test;

public class WpTest
{
    [Test]
    public void test()
    {
        var ra3Map = Ra3MapFacade.Open(Ra3PathUtil.RA3MapFolder, "wp_test");

        var waypointWraps = ra3Map.GetWaypoints();


        foreach (var waypoint in waypointWraps)
        {
            Console.WriteLine(waypoint);
        }
    }
}
