namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class UnitObjectWrap: ObjectWrap
{
    public UnitObjectWrap(ObjectAsset obj) : base(obj)
    {
    }
    
    
    public float Angle
    {
        get => Obj.Angle;
        set
        {
            Obj.Angle = value;
        }
    }

    public string TypeName
    {
        get => Obj.TypeName;
        set
        {
            Obj.TypeName = value;
        }
    }

    public int InitialHealth
    {
        get => Properties.GetProperty<int>("initialHealth");
        set =>Properties.PutProperty("initialHealth", value);
    }

    public bool Enable
    {
        get => Properties.GetProperty<bool>("objectEnabled");
        set => Properties.PutProperty("objectEnabled", value);
    }
    
    public bool Indestructible
    {
        get => Properties.GetProperty<bool>("objectIndestructible");
        set => Properties.PutProperty("objectIndestructible", value);
    }

    public bool Unsellable
    {
        get => Properties.GetProperty<bool>("objectUnsellable");
        set => Properties.PutProperty("objectUnsellable", value);
    }

    public bool Powered
    {
        get => Properties.GetProperty<bool>("objectPowered");
        set => Properties.PutProperty("objectPowered", value);
    }
    
    public bool RecruitableAI
    {
        get => Properties.GetProperty<bool>("objectRecruitableAI");
        set => Properties.PutProperty("objectRecruitableAI", value);
    }
    
    public bool Targetable
    {
        get => Properties.GetProperty<bool>("objectTargetable");
        set => Properties.PutProperty("objectTargetable", value);
    }
    
    public bool Sleeping
    {
        get => Properties.GetProperty<bool>("objectSleeping");
        set => Properties.PutProperty("objectSleeping", value);
    }
    
    public int BasePriority
    {
        get => Properties.GetProperty<int>("objectBasePriority");
        set => Properties.PutProperty("objectBasePriority", value);
    }

    public int BasePhase
    {
        get => Properties.GetProperty<int>("objectBasePhase");
        set => Properties.PutProperty("objectBasePhase", value);
    }
    
    public string BelongToTeam
    {
        get => Properties.GetProperty<string>("originalOwner");
        set => Properties.PutProperty("originalOwner", value);
    }
    
    public string ObjName
    {
        get => Properties.GetProperty<string>("objectName", "");
        set => Properties.PutProperty("objectName", value);
    }

    // TODO: 解析更多
    
}