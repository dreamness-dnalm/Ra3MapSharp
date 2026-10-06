using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Test;

public class RoadCompatibilityApiTests
{
    [Test]
    public void PairsReorderAndRoundtripPreserveIdsBitsZAndLaterEdits()
    {
        var map = Ra3MapFacade.NewMap(32, 32, 4, 0);
        var road = map.AddRoadSegment("YucatanDirtRoad01", new Vec3D(10,20,-35), new Vec3D(100,120,999),
            additionalOptions: 0x400, startAngle: 12, endAngle: 34);
        map.AddUnitObject("AlliedPowerPlant", 150,160);
        var bridge = map.AddBridgeSegment("HV_Bridge_01", new Vec3D(40,50,10), new Vec3D(200,50,20), 0x80);
        Assert.That(road.Start.Options.IsRoadStart, Is.True);
        Assert.That(bridge.End.Options.IsBridgeEnd, Is.True);
        var optionJson = System.Text.Json.JsonSerializer.Serialize(road.Start.Options);
        Assert.That(optionJson, Does.Not.Contain("IsRoadStart"));
        Assert.That(optionJson, Does.Contain("RawValue"));
        var order = new[] {bridge.Start, bridge.End, road.Start, road.End};
        map.ReorderRoadObjects(order);
        var path = Path.Combine(Path.GetTempPath(), "Ra3MapSharp-road-order-" + Guid.NewGuid() + ".map");
        try
        {
            map.SaveAs(path, compress: true);
            // Mutate after a save to check reordered collection change notifications too.
            road.End.Position.Z = 777;
            map.SaveAs(path, compress: true);
            var reopened = Ra3MapFacade.Open(path);
            Assert.That(reopened.GetRoadObjects().Select(r=>r.UniqueId), Is.EqualTo(order.Select(r=>r.UniqueId)));
            Assert.That(reopened.GetUnitObjects(), Has.Count.EqualTo(1));
            var saved = reopened.GetRoadObjects();
            Assert.That(saved[0].Options.RawValue, Is.EqualTo(0x80 | 16));
            Assert.That(saved[3].Options.RawValue, Is.EqualTo(0x400 | 4));
            Assert.That(saved[2].Position.Z, Is.EqualTo(-35));
            Assert.That(saved[3].Position.Z, Is.EqualTo(777));
            Assert.That(saved[2].Angle, Is.EqualTo(12));
            Assert.That(saved[3].Angle, Is.EqualTo(34));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void InvalidPairsAndPermutationsDoNotMutateTheMap()
    {
        var map = Ra3MapFacade.NewMap(32, 32, 4, 0);
        Assert.Throws<ArgumentException>(() => map.AddRoadSegment("Road",new Vec3D(0,0,0),new Vec3D(float.NaN,0,0)));
        Assert.Throws<ArgumentException>(() => map.AddBridgeSegment("Bridge",new Vec3D(0,0,0),new Vec3D(1,0,0),4));
        Assert.That(map.GetRoadObjects(), Is.Empty);
        var pair = map.AddRoadSegment("Road",new Vec3D(0,0,0),new Vec3D(1,0,0));
        var ids = map.GetRoadObjects().Select(r=>r.UniqueId).ToArray();
        Assert.Throws<ArgumentException>(()=>map.ReorderRoadObjects(new[]{pair.Start,pair.Start}));
        var other = Ra3MapFacade.NewMap(32,32,4,0).AddRoadObject("Other",1,2);
        Assert.Throws<ArgumentException>(()=>map.ReorderRoadObjects(new[]{pair.Start,other}));
        Assert.That(map.GetRoadObjects().Select(r=>r.UniqueId), Is.EqualTo(ids));
    }
}
