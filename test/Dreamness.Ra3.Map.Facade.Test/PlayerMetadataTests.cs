using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;

namespace Dreamness.Ra3.Map.Facade.Test;

public class PlayerMetadataTests
{
    [Test]
    public void AddAndRemovePlayer_ShouldKeepWorldBuilderMetadataAligned()
    {
        var map = Ra3MapFacade.NewMap(32, 32, 4, 0);
        var originalCount = map.GetPlayers().Count;

        var added = PlayerData.Of("MetadataTestPlayer", map.ra3Map.Context);
        added.Faction = "Soviet";
        map.AddPlayer(added);

        Assert.Multiple(() =>
        {
            Assert.That(map.ra3Map.Context.ImportedLibraryMaps.LibraryMapsList.Count,
                Is.EqualTo(originalCount + 1));
            Assert.That(map.ra3Map.Context.SkirmishBuildListMetadata.BuildLists.Count,
                Is.EqualTo(originalCount + 1));
            Assert.That(map.ra3Map.Context.SkirmishBuildListMetadata.BuildLists[originalCount].Faction,
                Is.EqualTo("PlayerTemplate:Soviet"));
        });

        map.Remove(added);

        Assert.Multiple(() =>
        {
            Assert.That(map.ra3Map.Context.ImportedLibraryMaps.LibraryMapsList.Count,
                Is.EqualTo(originalCount));
            Assert.That(map.ra3Map.Context.SkirmishBuildListMetadata.BuildLists.Count,
                Is.EqualTo(originalCount));
        });
    }
}
