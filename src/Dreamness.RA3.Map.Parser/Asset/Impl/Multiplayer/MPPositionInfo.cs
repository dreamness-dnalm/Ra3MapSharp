using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;

/// <summary>
/// WorldBuilder multiplayer-slot policy. Spatial spawn coordinates are stored
/// by player-start waypoints, not by this asset.
/// </summary>
public class MPPositionInfo: BaseAsset
{
    private bool isHuman;
    
    public bool IsHuman
    {
        get => isHuman;
        set
        {
            if (isHuman != value)
            {
                isHuman = value;
                MarkModified();
            }
        }
    }

    private bool isComputer;
    
    public bool IsComputer
    {
        get => isComputer;
        set
        {
            if (isComputer != value)
            {
                isComputer = value;
                MarkModified();
            }
        }
    }

    private bool loadAIScript;

    public bool LoadAIScript
    {
        get => loadAIScript;
        set
        {
            if (loadAIScript != value)
            {
                loadAIScript = value;
                MarkModified();
            }
        }
    }

    private uint team;
    
    public uint Team
    {
        get => team;
        set
        {
            if (team != value)
            {
                team = value;
                MarkModified();
            }
        }
    }

    private string[] sideRestrictions = Array.Empty<string>();
    public string[] SideRestrictions
    {
        get => sideRestrictions.ToArray();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!sideRestrictions.SequenceEqual(value))
            {
                sideRestrictions = value.ToArray();
                MarkModified();
            }
        }
    }
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.MPPositionInfo;
    }
    
    public static MPPositionInfo Of(bool isHuman, bool isComputer, bool loadAIScript, uint team, string[] sideRestrictions, BaseContext context)
    {
        var asset = new MPPositionInfo();
        asset.ApplyBasicInfo(context);
        
        asset.IsHuman = isHuman;
        asset.IsComputer = isComputer;
        asset.LoadAIScript = loadAIScript;
        asset.Team = team;
        asset.SideRestrictions = sideRestrictions ?? Array.Empty<string>();
        
        asset.MarkModified();
        return asset;
    }
    
    public static MPPositionInfo FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        return Dreamness.Ra3.Map.Parser.Asset.Util.AssetParser.FromBinaryReader(binaryReader, context) as MPPositionInfo
               ?? throw new InvalidDataException("Expected MPPositionInfo asset.");
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        isHuman = binaryReader.ReadBoolean();
        isComputer = binaryReader.ReadBoolean();
        loadAIScript = binaryReader.ReadBoolean();
        team = binaryReader.ReadUInt32();

        var sideRestrictionCount = binaryReader.ReadInt32();
        if (sideRestrictionCount < 0 || sideRestrictionCount > 1024)
        {
            throw new InvalidDataException($"Invalid side restriction count: {sideRestrictionCount}.");
        }

        sideRestrictions = new string[sideRestrictionCount];
        for (var i = 0; i < sideRestrictionCount; i++)
        {
            sideRestrictions[i] = binaryReader.ReadDefaultString();
        }

        if (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
        {
            throw new InvalidDataException("MPPositionInfo contains trailing data.");
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(IsHuman);
        binaryWriter.Write(IsComputer);
        binaryWriter.Write(LoadAIScript);
        binaryWriter.Write(Team);
        binaryWriter.Write(SideRestrictions.Length);
        foreach (var restriction in SideRestrictions)
        {
            binaryWriter.WriteDefaultString(restriction);
        }
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}
