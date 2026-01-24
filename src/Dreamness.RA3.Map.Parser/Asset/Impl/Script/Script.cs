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

    private SequentialTargetTypeEnum sequentialTargetType;

    public SequentialTargetTypeEnum SequentialTargetType
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
        loopCount = binaryReader.ReadInt32() + 1;
        sequentialTargetType = (SequentialTargetTypeEnum)binaryReader.ReadByte();
        sequentialTargetName = binaryReader.ReadDefaultString();
        unknown = binaryReader.ReadDefaultString();
        
        ObservableUtil.Subscribe(ScriptOrConditions, this);
        ObservableUtil.Subscribe(ScriptActionOnTrue, this);
        ObservableUtil.Subscribe(ScriptActionOnFalse, this);
        
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
        binaryWriter.Write(loopCount - 1);
        binaryWriter.Write((byte)sequentialTargetType);
        binaryWriter.WriteDefaultString(sequentialTargetName);
        binaryWriter.WriteDefaultString(unknown);
        
        foreach (var o in ScriptOrConditions)
        {
            binaryWriter.Write(o.ToBytes(context));
        }
        foreach (var o in ScriptActionOnTrue)
        {
            binaryWriter.Write(o.ToBytes(context));
        }
        foreach (var o in ScriptActionOnFalse)
        {
            binaryWriter.Write(o.ToBytes(context));
        }

        // binaryWriter.Write(ScriptOrConditions.ToBytes(context));
        //
        // binaryWriter.Write(ScriptActionOnTrue.ToBytes(context));
        //
        // binaryWriter.Write(ScriptActionOnFalse.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    public static Script Default(string name, BaseContext context)
    {
        var script = new Script();
        script.Name = name;
        script.Comment = "";
        script.ConditionComment = "";
        script.ActionComment = "";
        script.IsActive = true;
        script.DeactivateUponSuccess = false;
        script.ActiveInEasy = true;
        script.ActiveInMedium = true;
        script.ActiveInHard = true;
        script.IsSubroutine = false;
        script.EvaluationInterval = 0;
        script.ActionsFireSequentially = false;
        script.SequentialTargetType = SequentialTargetTypeEnum.Unit;
        script.SequentialTargetName = "";
        script.LoopActions = false;
        script.LoopCount = 1;
        ObservableUtil.Subscribe(script.ScriptOrConditions, script);
        ObservableUtil.Subscribe(script.ScriptActionOnTrue, script);
        ObservableUtil.Subscribe(script.ScriptActionOnFalse, script);
        script.ApplyBasicInfo(context);
        script.MarkModified();

        return script;
    }


    public JsonNode ToJsonNode()
    {
        var jsonObj = new JsonObject();
        
        jsonObj["Type"] = "Script";
        
        jsonObj["Name"] = name;

        if (comment != "")
        {
            jsonObj["Comment"] = comment;
        }

        if (conditionComment != "")
        {
            jsonObj["ConditionComment"] = conditionComment;
        }

        if (actionComment != "")
        {
            jsonObj["ActionComment"] = actionComment;
        }

        if (isActive == false)
        {
            jsonObj["IsActive"] = isActive;
        }

        if (IsSubroutine)
        {
            jsonObj["IsSubroutine"] = isSubroutine;
        }
        
        jsonObj["DeactivateUponSuccess"] = deactivateUponSuccess;

        if (!(activeInEasy && activeInMedium && activeInHard))
        {
            var jsonArray = new JsonArray();
            jsonArray.Add(activeInEasy);
            jsonArray.Add(activeInMedium);
            jsonArray.Add(activeInHard);

            jsonObj["ActiveInDifficulties"] = jsonArray;
        }
        
        jsonObj["EvaluationInterval"] = evaluationInterval;

        if (!(actionsFireSequentially == false && sequentialTargetType == SequentialTargetTypeEnum.Unit &&
              sequentialTargetName == "" && loopActions == false && loopCount == 1))
        {
            var sequentialScript = new JsonObject();
            sequentialScript["ActionsFireSequentially"] = actionsFireSequentially;
            sequentialScript["SequentialTargetType"] = sequentialTargetType.ToString();
            sequentialScript["SequentialTargetName"] = sequentialTargetName;
            sequentialScript["LoopActions"] = loopActions;
            sequentialScript["LoopCount"] = loopCount;
        
            jsonObj["SequentialScript"] = sequentialScript;
        }
        

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

    public static Script FromJsonNode(JsonNode node, BaseContext context)
    {
        var jsonObject = node as JsonObject;
        if (!jsonObject.ContainsKey("Name"))
        {
            throw new InvalidDataException("Bad Script Json: Missing Name field.");
        }

        var script = Default(node["Name"].ToString(), context);
        if (jsonObject.ContainsKey("Comment"))
        {
            script.Comment = node["Comment"].ToString();
        }
        if (jsonObject.ContainsKey("ConditionComment"))
        {
            script.ConditionComment = node["ConditionComment"].ToString();
        }
        if (jsonObject.ContainsKey("ActionComment"))
        {
            script.ActionComment = node["ActionComment"].ToString();
        }
        if (jsonObject.ContainsKey("IsActive"))
        {
            script.IsActive = node["IsActive"].GetValue<bool>();
        }
        if (jsonObject.ContainsKey("IsSubroutine"))
        {
            script.IsSubroutine = node["IsSubroutine"].GetValue<bool>();
        }
        if (jsonObject.ContainsKey("DeactivateUponSuccess"))
        {
            script.DeactivateUponSuccess = node["DeactivateUponSuccess"].GetValue<bool>();
        }
        if (jsonObject.ContainsKey("ActiveInDifficulties"))
        {
            var difArr = node["ActiveInDifficulties"] as JsonArray;
            script.ActiveInEasy = difArr[0].GetValue<bool>();
            script.ActiveInMedium = difArr[1].GetValue<bool>();
            script.ActiveInHard = difArr[2].GetValue<bool>();
        }
        if (jsonObject.ContainsKey("EvaluationInterval"))
        {
            script.EvaluationInterval = node["EvaluationInterval"].GetValue<int>();
        }
        if (jsonObject.ContainsKey("SequentialScript"))
        {
            var seqObj = node["SequentialScript"] as JsonObject;
            script.ActionsFireSequentially = seqObj["ActionsFireSequentially"].GetValue<bool>();
            script.SequentialTargetType = Enum.Parse<SequentialTargetTypeEnum>(seqObj["SequentialTargetType"].ToString());
            script.SequentialTargetName = seqObj["SequentialTargetName"].ToString();
            script.LoopActions = seqObj["LoopActions"].GetValue<bool>();
            script.LoopCount = seqObj["LoopCount"].GetValue<int>();
        }
        ObservableUtil.Subscribe(script.ScriptOrConditions, script);
        ObservableUtil.Subscribe(script.ScriptActionOnTrue, script);
        ObservableUtil.Subscribe(script.ScriptActionOnFalse, script);
        
        if (jsonObject.ContainsKey("If"))
        {
            var ifArr = node["If"] as JsonArray;
            foreach (var o in ifArr)
            {
                var orCondition = OrCondition.FromJsonNode(o, context);
                script.ScriptOrConditions.Add(orCondition, ignoreModified: true);
            }
        }
        if (jsonObject.ContainsKey("Then"))
        {
            var thenArr = node["Then"] as JsonArray;
            foreach (var o in thenArr)
            {
                var scriptAction = ScriptAction.FromJsonNode(o, context);
                script.ScriptActionOnTrue.Add(scriptAction, ignoreModified: true);
            }
        }
        if (jsonObject.ContainsKey("Else"))
        {
            var elseArr = node["Else"] as JsonArray;
            foreach (var o in elseArr)
            {                
                var scriptActionFalse = ScriptActionFalse.FromJsonNode(o, context);
                script.ScriptActionOnFalse.Add(scriptActionFalse, ignoreModified: true);
            }
        }
        script.ScriptOrConditions.MarkModified();
        script.ScriptActionOnTrue.MarkModified();
        script.ScriptActionOnFalse.MarkModified();
        return script;
    }
}

public enum SequentialTargetTypeEnum: byte
{
    Team = (byte)0,
    Unit = (byte)1
}