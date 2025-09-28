using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;


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

    private string conditionComment;
    
    public string ConditionComment
    {
        get => conditionComment;
        set
        {
            if (conditionComment != value)
            {
                conditionComment = value;
                MarkModified();
            }
        }
    }

    private string actionComment;

    public string ActionComment
    {
        get => actionComment;
        set
        {
            if (actionComment != value)
            {
                actionComment = value;
                MarkModified();
            }
        }
    }
    
    private bool isSubroutine;
    public bool IsSubroutine
    {
        get => isSubroutine;
        set
        {
            if (isSubroutine != value)
            {
                isSubroutine = value;
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

    private string unknown = "";
    public string Unknown
    {
        get => unknown;
        set
        {
            if (unknown != value)
            {
                unknown = value;
                MarkModified();
            }
        }
    }

    public WritableList<OrCondition> ScriptOrConditions = new WritableList<OrCondition>();

    public WritableList<ScriptAction> ScriptActionOnTrue = new WritableList<ScriptAction>();

    public WritableList<ScriptActionFalse> ScriptActionOnFalse = new WritableList<ScriptActionFalse>();
    
    
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
        var memoryStream = new MemoryStream(Data);
        var binaryReader = new BinaryReader(memoryStream);

        name = binaryReader.ReadDefaultString();
        comment = binaryReader.ReadDefaultString();
        conditionComment = binaryReader.ReadDefaultString();
        actionComment = binaryReader.ReadDefaultString();
        isActive = binaryReader.ReadBoolean();
        deactivateUponSuccess = binaryReader.ReadBoolean();
        activeInEasy = binaryReader.ReadBoolean();
        activeInMedium = binaryReader.ReadBoolean();
        activeInHard = binaryReader.ReadBoolean();
        isSubroutine = binaryReader.ReadBoolean();
        evaluationInterval = binaryReader.ReadInt32();
        actionsFireSequentially = binaryReader.ReadBoolean();
        loopActions = binaryReader.ReadBoolean();
        loopCount = binaryReader.ReadInt32();
        sequentialTargetType = binaryReader.ReadByte();
        sequentialTargetName = binaryReader.ReadDefaultString();
        unknown = binaryReader.ReadDefaultString();
        
        while (binaryReader.BaseStream.Position < DataSize)
        {
            var asset = AssetParser.FromBinaryReader(binaryReader, context);
            if (asset is OrCondition orCondition)
            {
                ScriptOrConditions.Add(orCondition, ignoreModified: true);
            }
            else if (asset is ScriptActionFalse scriptActionFalse)
            {
                ScriptActionOnFalse.Add(scriptActionFalse, ignoreModified: true);
            }
            else if (asset is ScriptAction scriptAction)
            {
                ScriptActionOnTrue.Add(scriptAction, ignoreModified: true);
            }

            else
            {
                throw new InvalidDataException(
                    $"Unexpected asset type in Script: {asset.GetType().Name}. Expected OrCondition, ScriptAction or ScriptActionFalse.");
            }
        }
        
        ObservableUtil.Subscribe(ScriptOrConditions, this);
        ObservableUtil.Subscribe(ScriptActionOnTrue, this);
        ObservableUtil.Subscribe(ScriptActionOnFalse, this);
        
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.WriteDefaultString(name);
        binaryWriter.WriteDefaultString(comment);
        binaryWriter.WriteDefaultString(conditionComment);
        binaryWriter.WriteDefaultString(actionComment);
        binaryWriter.Write(isActive);
        binaryWriter.Write(deactivateUponSuccess);
        binaryWriter.Write(activeInEasy);
        binaryWriter.Write(activeInMedium);
        binaryWriter.Write(activeInHard);
        binaryWriter.Write(isSubroutine);
        binaryWriter.Write(evaluationInterval);
        binaryWriter.Write(actionsFireSequentially);
        binaryWriter.Write(loopActions);
        binaryWriter.Write(loopCount);
        binaryWriter.Write(sequentialTargetType);
        binaryWriter.WriteDefaultString(sequentialTargetName);
        binaryWriter.WriteDefaultString(unknown);

        binaryWriter.Write(ScriptOrConditions.ToBytes(context));
        
        binaryWriter.Write(ScriptActionOnTrue.ToBytes(context));
        
        binaryWriter.Write(ScriptActionOnFalse.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    public JsonNode ToJsonNode()
    {
        var jsonObj = new JsonObject();
        
        jsonObj["Type"] = "Script";
        
        jsonObj["Name"] = name;
        jsonObj["Comment"] = comment;
        jsonObj["ConditionComment"] = conditionComment;
        jsonObj["ActionComment"] = actionComment;
        jsonObj["IsActive"] = isActive;
        jsonObj["DeactivateUponSuccess"] = deactivateUponSuccess;
        jsonObj["ActiveInEasy"] = activeInEasy;
        jsonObj["ActiveInMedium"] = activeInMedium;
        jsonObj["ActiveInHard"] = activeInHard;
        jsonObj["IsSubroutine"] = isSubroutine;
        jsonObj["EvaluationInterval"] = evaluationInterval;
        jsonObj["ActionsFireSequentially"] = actionsFireSequentially;
        jsonObj["LoopActions"] = loopActions;
        jsonObj["LoopCount"] = loopCount;
        jsonObj["SequentialTargetType"] = sequentialTargetType;
        jsonObj["SequentialTargetName"] = sequentialTargetName;
        // jsonObj["Unknown"] = unknown;

        var ifJsonArr = new JsonArray();
        foreach (var o in ScriptOrConditions)
        {
            ifJsonArr.Add(o.ToJsonNode());
        }
        jsonObj["If"] = ifJsonArr;
        
        var thenJsonArr = new JsonArray();
        foreach (var o in ScriptActionOnTrue)
        {
            thenJsonArr.Add(o.ToJsonNode());
        }
        jsonObj["Then"] = thenJsonArr;
        
        var elseJsonArr = new JsonArray();
        foreach (var o in ScriptActionOnFalse)
        {
            elseJsonArr.Add(o.ToJsonNode());
        }
        jsonObj["Else"] = elseJsonArr;


        return jsonObj;
    }

    public static Script FromJsonNode(JsonNode node)
    {
        throw new NotImplementedException();
    }
}