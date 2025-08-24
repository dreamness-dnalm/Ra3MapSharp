using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Player;

public class SidesListAsset: BaseAsset
{

    public WritableList<PlayerData> PlayerDataList { get; private set; } = new WritableList<PlayerData>();
    
    public byte magic = 1; 
    
    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);
        magic = binaryReader.ReadByte(); // must be 1
        // if (mustbeOne != 1)
        // {
        //     throw new InvalidDataException("Invalid SidesList data, expected first byte to be 1.");
        // }
        var playerCount = binaryReader.ReadInt32();
        for (int i = 0; i < playerCount; i++)
        {
            var playerData = PlayerData.FromBinaryReader(binaryReader, context);
            PlayerDataList.Add(playerData, ignoreModified:true);
        }
        
        ObservableUtil.Subscribe(PlayerDataList, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        binaryWriter.Write(magic); // must be 1
        binaryWriter.Write(PlayerDataList.Count);
        binaryWriter.Write(PlayerDataList.ToBytes(context));
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static string[] DefaultPlayerNames =
    {
        "",
        "Player_1",
        "Player_2",
        "Player_3",
        "Player_4",
        "Player_5",
        "Player_6",
        "PlyrCivilian",
        "PlyrCreeps",
        "PlyrNeutral",
        "SkirmishCivilian",
        "SkirmishRandom",
        "SkirmishSoviet",
        "SkirmishAllies",
        "SkirmishJapan",
        "SkirmishNull",
        "SkirmishObserver"
    };

    public static SidesListAsset Default(BaseContext context)
    {
        var asset = new SidesListAsset();

        asset.ApplyBasicInfo(context);



        foreach (var playerName in DefaultPlayerNames)
        {
            asset.PlayerDataList.Add(PlayerData.OfForDefault(playerName, context));
        }
        ObservableUtil.Subscribe(asset.PlayerDataList, asset);
        
        asset.MarkModified();
        return asset;
    }
    
    public override short GetVersion()
    {
        return 6;
    }

    public override string GetName()
    {
        return AssetNameConst.SidesList;
    }

    // public override string ToString()
    // {
    //     return $"SidesListAsset(Name: {Name}, Id: {Id}, Version: {Version}, Content: {PlayerDataList})";
    // }

    public string ToJson()
    {
        return JsonUtil.Serialize(PlayerDataList);
    }

    public static SidesListAsset FromJson(string json, BaseContext context)
    {
        var JsonDocumentList = JsonSerializer.Deserialize<List<JsonDocument>>(json);
        if (JsonDocumentList == null)
        {
            throw new InvalidDataException("Failed to deserialize SidesListAsset from JSON.");
        }

        var sidesListAsset = new SidesListAsset();
        sidesListAsset.Name = AssetNameConst.SidesList;
        sidesListAsset.Id = context.RegisterStringDeclare(sidesListAsset.Name);
        sidesListAsset.Version = sidesListAsset.GetVersion();
        
        foreach (var jsonDocument in JsonDocumentList)
        {
            var playerData = PlayerData.FromJson(JsonUtil.Serialize(jsonDocument), context);
            sidesListAsset.PlayerDataList.Add(playerData, ignoreModified:true);
        }
        
        ObservableUtil.Subscribe(sidesListAsset.PlayerDataList, sidesListAsset);
        sidesListAsset.MarkModified();
        return sidesListAsset;
    }
}