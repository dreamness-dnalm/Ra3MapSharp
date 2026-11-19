namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

public class UnitObjectWrap: ObjectWrap
{
    public UnitObjectWrap(ObjectAsset obj) : base(obj)
    {
    }
    
    /// <summary>
    /// 角度
    /// </summary>
    public float Angle
    {
        get => Obj.Angle;
        set
        {
            Obj.Angle = value;
        }
    }

    /// <summary>
    /// 单位类型名称 (对应Thing)
    /// </summary>
    public string TypeName
    {
        get => Obj.TypeName;
        set
        {
            Obj.TypeName = value;
        }
    }

    /// <summary>
    /// 生命值
    /// </summary>
    public int InitialHealth
    {
        get => Properties.GetProperty<int>(UnitObjectSettings.InitialHealth);
        set => Properties.PutProperty(UnitObjectSettings.InitialHealth, value);
    }

    public int ExperienceLevel
    {
        get => Properties.GetProperty(UnitObjectSettings.ExperienceLevel, 1);
        set => Properties.PutProperty(UnitObjectSettings.ExperienceLevel, value);
    }

    public int InitialStance
    {
        get => Properties.GetProperty<int>(UnitObjectSettings.InitialStance);
        set => Properties.PutProperty(UnitObjectSettings.InitialStance, value);
    }

    public bool AlignToTerrain
    {
        get => Properties.GetProperty<bool>(UnitObjectSettings.AlignToTerrain);
        set => Properties.PutProperty(UnitObjectSettings.AlignToTerrain, value);
    }

    public float? PrototypeScale
    {
        get => Properties.PropertiesDict.ContainsKey(UnitObjectSettings.PrototypeScale)
            ? Properties.GetProperty<float>(UnitObjectSettings.PrototypeScale)
            : null;
        set
        {
            if (value is null) Properties.RemoveProperty(UnitObjectSettings.PrototypeScale);
            else Properties.PutProperty(UnitObjectSettings.PrototypeScale, value.Value);
        }
    }

    /// <summary>
    /// 可用
    /// </summary>
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

    /// <summary>
    /// 不可售卖
    /// </summary>
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
    
    /// <summary>
    /// 为有效目标
    /// </summary>
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
    
    /// <summary>
    /// 单位名称
    /// </summary>
    public string ObjName
    {
        get => Properties.GetProperty<string>("objectName", "");
        set => Properties.PutProperty("objectName", value);
    }

    // Remaining World Builder keys are read and written through UnitObjectSettings.
    
}