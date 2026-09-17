using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class PlayerStartTests
{
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(7)]
    public void InvalidSlotCannotCreateWaypoint(int slot)
    {
        var objects = new ObjectsListAsset();
        Assert.Throws<ArgumentOutOfRangeException>(() => objects.AddPlayerStartWaypoint(slot, new Vec3D(0, 0, 0), new MapContext()));
        Assert.That(objects.MapObjectList, Is.Empty);
    }
}
