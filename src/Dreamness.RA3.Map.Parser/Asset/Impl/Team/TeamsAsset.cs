using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Team;

public class TeamsAsset: BaseAsset
{

    public WritableList<TeamAsset> TeamList { get; private set; }  = new WritableList<TeamAsset>();

    protected override void _Parse(BaseContext context)
    {
        var memoryStream = new MemoryStream(Data);
        var binaryReader = new BinaryReader(memoryStream);

        var cnt = binaryReader.ReadInt32();

        for (var i = 0; i < cnt; i++)
        {
            TeamList.Add(TeamAsset.FromBinaryReader(binaryReader, context), ignoreModified:true);
        }
        ObservableUtil.Subscribe(TeamList, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(TeamList.Count);
        binaryWriter.Write(TeamList.ToBytes(context));
        binaryWriter.Flush();
        
        return memoryStream.ToArray();
    }

    public static TeamsAsset Default(BaseContext context)
    {
        var asset = new TeamsAsset();
        
        asset.ApplyBasicInfo(context);
        //
        // string[] playerNames =
        // {
        //     "",
        //     "Player_1",
        //     "Player_2",
        //     "Player_3",
        //     "Player_4",
        //     "Player_5",
        //     "Player_6",
        //     "PlyrCivilian",
        //     "PlyrCreeps",
        //     "PlyrNeutral",
        //     "SkirmishCivilian",
        //     "SkirmishRandom",
        //     "SkirmishSoviet",
        //     "SkirmishAllies",
        //     "SkirmishJapan",
        //     "SkirmishNull",
        //     "SkirmishObserver",
        //     
        // };

        foreach (var playerName in SidesListAsset.DefaultPlayerNames)
        {
            var teamName = "team" + playerName;
            asset.TeamList.Add(TeamAsset.Of(teamName, playerName, context));
        }
        ObservableUtil.Subscribe(asset.TeamList, asset);
        asset.MarkModified();
        return asset;
    }
    
    public override short GetVersion()
    {
        return 1;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.Teams;
    }

    public static TeamsAsset FromJson(string json, BaseContext context)
    {
        var JsonDocumentList = JsonSerializer.Deserialize<List<JsonDocument>>(json);
        if (JsonDocumentList == null)
        {
            throw new InvalidDataException("Failed to deserialize SidesListAsset from JSON.");
        }
        
        var teamsAsset = new TeamsAsset();
        teamsAsset.AssetType = AssetNameConst.Teams;
        teamsAsset.Id = context.RegisterStringDeclare(teamsAsset.AssetType);
        teamsAsset.Version = teamsAsset.GetVersion();
        
        foreach (var jsonDocument in JsonDocumentList)
        {
            var teamAsset = TeamAsset.FromJson(JsonUtil.Serialize(jsonDocument), context);
            teamsAsset.TeamList.Add(teamAsset);
        }
        
        ObservableUtil.Subscribe(teamsAsset.TeamList, teamsAsset);
        teamsAsset.MarkModified();
        return teamsAsset;
    }
    
    public string ToJson()
    {
        return JsonUtil.Serialize(TeamList);
    }
}