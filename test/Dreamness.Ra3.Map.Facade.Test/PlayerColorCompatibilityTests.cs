using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.Ra3.Map.Facade.Test;

public class PlayerColorCompatibilityTests
{
    [TestCase(0xFF123456u)]
    [TestCase(0x00123456u)]
    [TestCase(0xFFFFFFFFu)]
    [TestCase(0u)]
    public void CustomArgbSurvivesNamesJsonAndBinary(uint color)
    {
        var map = Ra3MapFacade.NewMap(32, 32, 4, 0);
        var player = map.AddPlayer("CustomColorOwner");
        player.ColorArgb = color;
        player.RadarColorArgb = color ^ 0x0055AA55u;
        Assert.That(player.Color, Is.EqualTo("#" + color.ToString("X8")));
        var json = map.ExportPlayersToJsonStr();
        Assert.That(json, Does.Not.Contain("ColorArgb"));
        var restored = Ra3MapFacade.NewMap(32, 32, 4, 0);
        restored.ImportPlayersFromJsonStr(json);
        Assert.That(restored.GetPlayer(player.Name).ColorArgb, Is.EqualTo(color));
        Assert.That(restored.GetPlayer(player.Name).RadarColorArgb, Is.EqualTo(player.RadarColorArgb));
        var path = Path.Combine(Path.GetTempPath(), "Ra3MapSharp-color-" + Guid.NewGuid() + ".map");
        try
        {
            restored.SaveAs(path, compress: true);
            var reopened = Ra3MapFacade.Open(path).GetPlayer(player.Name);
            Assert.That(reopened.ColorArgb, Is.EqualTo(color));
            Assert.That(reopened.RadarColorArgb, Is.EqualTo(player.RadarColorArgb));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void LegacyNamesGoldAliasAndNullKeepTheirContracts()
    {
        var player = Ra3MapFacade.NewMap(32, 32, 4, 0).AddPlayer("ColorNames");
        player.Color = "Blue";
        Assert.That(player.Color, Is.EqualTo("Blue"));
        player.Color = "Gold";
        Assert.That(player.Color, Is.EqualTo("Glod"));
        player.RadarColor = "Glod";
        Assert.That(player.RadarColorArgb, Is.EqualTo(player.ColorArgb));
        var previous = player.ColorArgb;
        Assert.Throws<KeyNotFoundException>(() => player.Color = "UnknownColor");
        Assert.That(player.ColorArgb, Is.EqualTo(previous));
        player.ColorArgb = null;
        player.RadarColor = null;
        Assert.That(player.Color, Is.Null);
        Assert.That(player.RadarColorArgb, Is.Null);
    }
}
