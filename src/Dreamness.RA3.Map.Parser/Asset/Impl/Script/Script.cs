using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class Script: BaseAsset
{
    private string name;
    
    public string Name
    {
        get => name;
        set
        {
            if (name != value)
            {
                name = value;
                MarkModified();
            }
        }
    }

    private string comment;
    
    public string Comment
    {
        get => comment;
        set
        {
            if (comment != value)
            {
                comment = value;
                MarkModified();
            }
        }
    }

    private string ConditionComment;
    
    public string Condition_Comment
    {
        get => ConditionComment;
        set
        {
            if (ConditionComment != value)
            {
                ConditionComment = value;
                MarkModified();
            }
        }
    }

    private bool isActive;
    
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (isActive != value)
            {
                isActive = value;
                MarkModified();
            }
        }
    }

    private bool deactivateUponSuccess;
    
    public bool DeactivateUponSuccess
    {
        get => deactivateUponSuccess;
        set
        {
            if (deactivateUponSuccess != value)
            {
                deactivateUponSuccess = value;
                MarkModified();
            }
        }
    }

    private bool activeInEasy;
    
    public bool ActiveInEasy
    {
        get => activeInEasy;
        set
        {
            if (activeInEasy != value)
            {
                activeInEasy = value;
                MarkModified();
            }
        }
    }
    
    private bool activeInMedium;
    
    public bool ActiveInMedium
    {
        get => activeInMedium;
        set
        {
            if (activeInMedium != value)
            {
                activeInMedium = value;
                MarkModified();
            }
        }
    }
    
    private bool activeInHard;
    
    public bool ActiveInHard
    {
        get => activeInHard;
        set
        {
            if (activeInHard != value)
            {
                activeInHard = value;
                MarkModified();
            }
        }
    }
    
    private int evaluationInterval;
    
    public int EvaluationInterval
    {
        get => evaluationInterval;
        set
        {
            if (evaluationInterval != value)
            {
                evaluationInterval = value;
                MarkModified();
            }
        }
    }

    private bool actionsFireSequentially;
    
    public bool ActionsFireSequentially
    {
        get => actionsFireSequentially;
        set
        {
            if (actionsFireSequentially != value)
            {
                actionsFireSequentially = value;
                MarkModified();
            }
        }
    }

    private bool loopActions;
    
    public bool LoopActions
    {
        get => loopActions;
        set
        {
            if (loopActions != value)
            {
                loopActions = value;
                MarkModified();
            }
        }
    }

    private int loopCount;
    
    public int LoopCount
    {
        get => loopCount;
        set
        {
            if (loopCount != value)
            {
                loopCount = value;
                MarkModified();
            }
        }
    }

    private byte sequentialTargetType;

    public byte SequentialTargetType
    {
        get => sequentialTargetType;
        set
        {
            if (sequentialTargetType != value)
            {
                sequentialTargetType = value;
                MarkModified();
            }
        }
    }
    
    private string sequentialTargetName;
    public string SequentialTargetName
    {
        get => sequentialTargetName;
        set
        {
            if (sequentialTargetName != value)
            {
                sequentialTargetName = value;
                MarkModified();
            }
        }
    }
    
    
    
    
    
    
    public override short GetVersion()
    {
        return 4;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.Script;
    }

    protected override void _Parse(BaseContext context)
    {
        throw new NotImplementedException();
    }

    protected override byte[] Deparse(BaseContext context)
    {
        throw new NotImplementedException();
    }
}