using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.SubAsset.Impl.Unknown;

// TODO: ????
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

    private string[] sideRestrictions;
    public string[] SideRestrictions
    {
        get => sideRestrictions;
        set
        {
            if (sideRestrictions != value)
            {
                sideRestrictions = value;
                MarkModified();
            }
        }
    }
    
    public override short GetVersion()
    {
        return 0;
    }

    public override string GetName()
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
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        var mPositionInfo = new MPPositionInfo();
        
        var isHuman = binaryReader.ReadBoolean();
        binaryWriter.Write(isHuman);
        mPositionInfo.IsHuman = isHuman;
        
        var isComputer = binaryReader.ReadBoolean();
        binaryWriter.Write(isComputer);
        mPositionInfo.IsComputer = isComputer;
        
        var loadAIScript = binaryReader.ReadBoolean();
        binaryWriter.Write(loadAIScript);
        mPositionInfo.LoadAIScript = loadAIScript;
        
        var team = binaryReader.ReadUInt32();
        binaryWriter.Write(team);
        mPositionInfo.Team = team;
        
        var sideRestrictionCount = binaryReader.ReadInt32();
        binaryWriter.Write(sideRestrictionCount);
        var sideRestrictions = new string[sideRestrictionCount];
        for (var i = 0; i < sideRestrictionCount; i++)
        {
            var restriction = binaryReader.ReadDefaultString();
            binaryWriter.WriteDefaultString(restriction);
            sideRestrictions[i] = restriction;
        }
        
        binaryWriter.Flush();
        mPositionInfo.Data = memoryStream.ToArray();
        
        return mPositionInfo;
    }

    protected override void _Parse(BaseContext context)
    {
        throw new NotImplementedException();
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