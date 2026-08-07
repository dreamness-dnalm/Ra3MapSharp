using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

/// <summary>
/// Index-aligned Skirmish AI Build List bookkeeping for SidesList entries.
/// </summary>
public class BuildListsAsset : BaseAsset
{
    public WritableList<BuildList> BuildLists { get; } = new();

    public override short GetVersion() => 1;

    public override string GetAssetType() => AssetNameConst.BuildLists;

    protected override void _Parse(BaseContext context)
    {
        using var stream = new MemoryStream(Data);
        using var reader = new BinaryReader(stream);
        var count = reader.ReadInt32();
        if (count < 0 || count > 100_000)
        {
            throw new InvalidDataException($"Invalid BuildLists entry count: {count}.");
        }

        for (var i = 0; i < count; i++)
        {
            BuildLists.Add(BuildList.FromBinaryReader(reader), ignoreModified: true);
        }

        if (reader.BaseStream.Position != reader.BaseStream.Length)
        {
            throw new InvalidDataException("BuildLists contains trailing data.");
        }

        ObservableUtil.Subscribe(BuildLists, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(BuildLists.Count);
        writer.Write(BuildLists.ToBytes(context));
        writer.Flush();
        return stream.ToArray();
    }

    public static BuildListsAsset Default(BaseContext context)
    {
        var factions = SidesListAsset.DefaultPlayerNames.Select(GetDefaultBuildListFaction);
        return FromFactions(factions, context);
    }

    public static BuildListsAsset Default(SidesListAsset sides, BaseContext context)
    {
        ArgumentNullException.ThrowIfNull(sides);
        return FromFactions(sides.PlayerDataList.Select(GetBuildListFaction), context);
    }

    /// <summary>
    /// Keeps this index-based metadata aligned with the current side list.
    /// Existing raw counts are preserved for entries that remain at the same index.
    /// </summary>
    public void SynchronizeWithSides(SidesListAsset sides)
    {
        ArgumentNullException.ThrowIfNull(sides);

        for (var i = 0; i < sides.PlayerDataList.Count; i++)
        {
            var faction = GetBuildListFaction(sides.PlayerDataList[i]);
            if (i < BuildLists.Count)
            {
                BuildLists[i].Faction = faction;
            }
            else
            {
                BuildLists.Add(BuildList.Of(faction, 0));
            }
        }

        while (BuildLists.Count > sides.PlayerDataList.Count)
        {
            BuildLists.Remove(BuildLists[BuildLists.Count - 1]);
        }
    }

    private static BuildListsAsset FromFactions(IEnumerable<string> factions, BaseContext context)
    {
        var asset = new BuildListsAsset();
        asset.ApplyBasicInfo(context);

        foreach (var faction in factions)
        {
            asset.BuildLists.Add(BuildList.Of(faction, 0), ignoreModified: true);
        }

        ObservableUtil.Subscribe(asset.BuildLists, asset);
        asset.MarkModified();
        return asset;
    }

    private static string GetBuildListFaction(PlayerData player)
    {
        var faction = player.Faction;
        if (string.IsNullOrEmpty(faction))
        {
            faction = "Null";
        }
        else if (faction == "FactionCivilian")
        {
            faction = "Civilian";
        }

        return "PlayerTemplate:" + faction;
    }

    private static string GetDefaultBuildListFaction(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            return "PlayerTemplate:Null";
        }

        if (playerName.StartsWith("Skirmish", StringComparison.Ordinal))
        {
            return "PlayerTemplate:" + playerName["Skirmish".Length..];
        }

        return "PlayerTemplate:Civilian";
    }
}
