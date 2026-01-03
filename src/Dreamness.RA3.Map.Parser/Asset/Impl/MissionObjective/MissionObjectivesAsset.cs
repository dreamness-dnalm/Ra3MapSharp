using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.RA3.Map.Parser.Asset.Impl.MissionObjective;

public class MissionObjectivesAsset: BaseAsset
{
    public WritableList<MissionObjective> Objectives { get; set; } = new WritableList<MissionObjective>();
    
    public override short GetVersion()
    {
        return 3;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.MissionObjectives;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        var objective_cnt = binaryReader.ReadInt32();
        for (int i = 0; i < objective_cnt; i++)
        {
            var objective = MissionObjective.FromBinaryReader(binaryReader, context);
            Objectives.Add(objective);
        }
        ObservableUtil.Subscribe(Objectives, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(Objectives.Count);
        binaryWriter.Write(Objectives.ToBytes(context));
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static MissionObjectivesAsset Default(BaseContext context)
    {
        var asset = new MissionObjectivesAsset();
        asset.ApplyBasicInfo(context);
        ObservableUtil.Subscribe(asset.Objectives, asset);
        asset.MarkModified();
        return asset;
    }

    public void Remove(MissionObjective missionObjective)
    {
        Objectives.Remove(missionObjective);
        MarkModified();
    }
    
    public void Add(MissionObjective missionObjective)
    {
        Objectives.Add(missionObjective);
        MarkModified();
    }
}