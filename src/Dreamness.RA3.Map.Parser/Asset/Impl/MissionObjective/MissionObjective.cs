using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Parser.Asset.Impl.MissionObjective;

public class MissionObjective: Ra3MapWritable
{
    // public AssetProperties Properties { get; private set; }

    private string id;

    public string Id
    {
        get => id;
        set
        {
            if (value != id)
            {
                id = value;
                MarkModified();
            }
        }
    }

    private string text;

    public string Text
    {
        get => text;
        set
        {
            if (value != text)
            {
                text = value;
                MarkModified();
            }
        }
    }
    

    private string description;

    public string Description
    {
        get => description;
        set
        {
            if (value != description)
            {
                description = value;
                MarkModified();
            }
        }
    }
    
    private byte isBonus;

    public bool IsBonus
    {
        get => isBonus == 1;
        set
        {
            byte _value = value? (byte)1: (byte)0;
            if (_value != isBonus)
            {
                isBonus = _value;
                MarkModified();
            }
        }
    }

    private int type;

    public string Type
    {
        get => ((MissionObjectiveType)type).ToString();
        set
        {
            var _value = (int)Enum.Parse<MissionObjectiveType>(value);
            if (_value != type)
            {
                type = _value;
                MarkModified();
            }
        }
    }


    
    public override byte[] ToBytes(BaseContext context)
    {
        if (_modified)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);
            
            binaryWriter.WriteDefaultString(id);
            binaryWriter.WriteDefaultString(text);
            binaryWriter.WriteDefaultString(description);
            binaryWriter.Write(isBonus);
            binaryWriter.Write(type);
            
            binaryWriter.Flush();
            return memoryStream.ToArray();
        }
        else
        {
            return Data;
        }
    }
    
    
    public static MissionObjective FromBinaryReader(BinaryReader binaryReader, BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        var missionObjective = new MissionObjective();
        
        missionObjective.id = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(missionObjective.id);
        missionObjective.text = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(missionObjective.text);
        missionObjective.description = binaryReader.ReadDefaultString();
        binaryWriter.WriteDefaultString(missionObjective.description);
        missionObjective.isBonus = binaryReader.ReadByte();
        binaryWriter.Write(missionObjective.isBonus);
        missionObjective.type = binaryReader.ReadInt32();
        binaryWriter.Write(missionObjective.type);
        
        binaryWriter.Flush();
        missionObjective.Data = memoryStream.ToArray();
        
        missionObjective.MarkModified();
        return missionObjective;
    }
    
    public static MissionObjective Of(string id, string text, string description, bool isBonus, string type)
    {
        var missionObjective = new MissionObjective();
        missionObjective.Id = id;
        missionObjective.Text = text;
        missionObjective.Description = description;
        missionObjective.IsBonus = isBonus;
        missionObjective.Type = type;
        missionObjective.MarkModified();
        return missionObjective;
    }
    
    public enum MissionObjectiveType
    {
        Attack = 0,
        Build = 3,
        Capture = 2,
        Move = 4,
        Protect = 1
    }
}