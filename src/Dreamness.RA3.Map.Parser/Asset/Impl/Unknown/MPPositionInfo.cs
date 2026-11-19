using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Asset.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;

/// <summary>A multiplayer slot, serialized as a nested MPPositionInfo chunk.</summary>
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
        get => (string[])sideRestrictions.Clone();
        set
        {
            if (sideRestrictions != value)
            {
                ArgumentNullException.ThrowIfNull(value);
                if (value.Any(s => s == null)) throw new ArgumentException("Restrictions cannot contain null.", nameof(value));
                sideRestrictions = (string[])value.Clone();
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
        // Preserve the existing payload-reader API. Nested chunk headers are handled by AssetParser.
        var item = new MPPositionInfo();
        item.ApplyBasicInfo(context);
        item.ReadPayload(binaryReader);
        item.Data = item.Deparse(context);
        item.DataSize = item.Data.Length;
        item.Parsed = true;
        return item;
    }

    protected override void _Parse(BaseContext context)
    {
        AssetReadUtility.RequireVersion(this, 1);
        using var reader = new BinaryReader(new MemoryStream(Data));
        ReadPayload(reader);
        AssetReadUtility.RequireEnd(reader);
    }

    private void ReadPayload(BinaryReader reader)
    {
        isHuman = reader.ReadBoolean();
        isComputer = reader.ReadBoolean();
        loadAIScript = reader.ReadBoolean();
        team = reader.ReadUInt32();
        var count = AssetReadUtility.ReadCount(reader, 2);
        sideRestrictions = new string[count];
        for (var i = 0; i < count; i++) sideRestrictions[i] = AssetReadUtility.ReadString(reader);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        AssetReadUtility.RequireWritable(this);
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(IsHuman);
        binaryWriter.Write(IsComputer);
        binaryWriter.Write(LoadAIScript);
        binaryWriter.Write(Team);
        binaryWriter.Write(sideRestrictions.Length);
        foreach (var restriction in sideRestrictions)
        {
            AssetReadUtility.WriteString(binaryWriter, restriction);
        }
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
}
