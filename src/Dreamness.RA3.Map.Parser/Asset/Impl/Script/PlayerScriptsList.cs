using System.Text.Json.Nodes;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class PlayerScriptsList: BaseAsset
{
    public WritableList<ScriptList> ScriptLists { get; } = new WritableList<ScriptList>();
    
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.PlayerScriptsList;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        while (binaryReader.BaseStream.Position < DataSize)
        {
            var asset = AssetParser.FromBinaryReader(binaryReader, context) as ScriptList;
            if (asset.Errored)
            {
                throw asset.ErrorException;
            }
            ScriptLists.Add(asset, ignoreModified: true);
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(ScriptLists.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static PlayerScriptsList Default(BaseContext context)
    {
        var asset = new PlayerScriptsList();
        
        asset.ApplyBasicInfo(context);

        for (int i = 0; i < SidesListAsset.DefaultPlayerNames.Length; i++)
        {
            asset.ScriptLists.Add(ScriptList.Empty(context));
        }
        
        ObservableUtil.Subscribe(asset.ScriptLists, asset);
        
        asset.MarkModified();
        
        return asset;
    }

    public string ToJson(BaseContext context)
    {
        var sidesListAsset = (SidesListAsset)context.AssetDict[AssetNameConst.SidesList];

        var playerNames = sidesListAsset.PlayerDataList.Select(p => p.Name).ToArray();

        if (ScriptLists.Count > playerNames.Length)
        {
            throw new System.Exception("ScriptLists length should equal to player cnt.");
        }

        var jsonArray = new JsonArray();

        for (int i = 0; i < ScriptLists.Count; i++)
        {
            var o = new JsonObject();
            
            // var content = ScriptLists[i].ToJsonNode();
            // var playerName = playerNames[i];
            o.Add("PlayerName", playerNames[i]);
            o.Add("Content", ScriptLists[i].ToJsonNode());
            
            jsonArray.Add(o);
        }

        return JsonUtil.Serialize(jsonArray);
        // return jsonArray.ToJsonString();
    }

    public PlayerScriptsList FromJson(string json, BaseContext context)
    {
        

        var jsonArr = (JsonArray)JsonArray.Parse(json);
        
        var sidesListAsset = (SidesListAsset)context.AssetDict[AssetNameConst.SidesList];
        var playerNames = sidesListAsset.PlayerDataList.Select(p => p.Name).ToList();

        var scriptLists = new ScriptList[playerNames.Count];

        foreach (var o in jsonArr)
        {
            var scriptListObj = o as JsonObject;
            var playerName = scriptListObj["PlayerName"].ToString();
            if (playerName == "(neutral)")
            {
                playerName = "";
            }

            var indexOf = playerNames.IndexOf(playerName);
            if (indexOf < 0)
            {
                throw new System.Exception($"Bad Script Json: Player name ({playerName}) not exists.");
            }

            ScriptList scriptList = null;
            if (scriptListObj.ContainsKey("Content"))
            {
                scriptList = ScriptList.FromJsonNode(scriptListObj["Content"], context);
            }
            else
            {
                scriptList = ScriptList.Empty(context);
            }
            
            scriptLists[indexOf] = scriptList;
        }
        
        for (int i = 0; i < scriptLists.Length; i++)
        {
            if (scriptLists[i] == null)
            {
                scriptLists[i] = ScriptList.Empty(context);
            }
        }
        
        
        ScriptLists.Clear();
        for(int i = 0; i < scriptLists.Length; i++)
        {
            ScriptLists.Add(scriptLists[i]);
        }

        return this;
    }
}