using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Core.Map;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Test;

public class UnitObjectSettingsTests
{
    [Test]
    public void ReadSkipsUniqueIdAndApplyKeepsItsType()
    {
        var context = new MapContext();
        var properties = AssetProperties.FromDict(new Dictionary<string, object>
        {
            ["uniqueID"] = "abc",
            ["objectInitialHealth"] = 80,
            ["objectPrototypeScale"] = 1.5f,
            ["objectEnabled"] = true
        }, context);
        var read = UnitObjectSettings.Read(properties);
        Assert.That(read.ContainsKey("uniqueID"), Is.False);
        Assert.That(read["objectInitialHealth"], Is.EqualTo(80));
        Assert.That(read["objectPrototypeScale"], Is.EqualTo(1.5f));

        read.Remove("objectPrototypeScale");
        read["objectInitialHealth"] = 80;
        UnitObjectSettings.Apply(properties, read);

        Assert.That(properties.PropertiesDict.ContainsKey("objectPrototypeScale"), Is.False);
        Assert.That(properties.GetProperty<int>("objectInitialHealth"), Is.EqualTo(80));
        Assert.That(properties.GetProperty<string>("uniqueID"), Is.EqualTo("abc"));
        Assert.That(properties.GetProperty<bool>("objectEnabled"), Is.True);
    }

    [Test]
    public void CoerceKnownFloatFromWholeNumber()
    {
        var coerced = UnitObjectSettings.Coerce(UnitObjectSettings.PrototypeScale, 2);
        Assert.That(coerced, Is.TypeOf<float>());
        Assert.That(coerced, Is.EqualTo(2f));
    }

    [Test]
    public void InitialHealthUsesOfficialKey()
    {
        var context = new MapContext();
        var asset = ObjectAsset.OfObj("id", "AlliedPowerPlant", new Vec3D(1, 2, 3), 10, "Plant", "P1/team", context);
        var wrap = new UnitObjectWrap(asset);
        Assert.That(wrap.InitialHealth, Is.EqualTo(100));
        wrap.InitialHealth = 40;
        Assert.That(asset.Properties.GetProperty<int>(UnitObjectSettings.InitialHealth), Is.EqualTo(40));
        Assert.That(asset.Properties.PropertiesDict.ContainsKey("initialHealth"), Is.False);
    }
}
