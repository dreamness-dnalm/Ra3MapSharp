using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.MissionHotSpot;

public class MissionHotSpotsAsset : BaseAsset
{
    public WritableList<MissionHotSpot> HotSpots { get; } = new();
    public MissionHotSpotsAsset() => ObservableUtil.Subscribe(HotSpots, this);
    public override short GetVersion() => 1;
    public override string GetAssetType() => AssetNameConst.MissionHotSpots;
    protected override void _Parse(BaseContext context)
    {
        AssetReadUtility.RequireVersion(this, 1);
        using var reader = new BinaryReader(new MemoryStream(Data));
        var count = AssetReadUtility.ReadCount(reader, 6);
        for (var i = 0; i < count; i++) HotSpots.Add(MissionHotSpot.FromBinaryReader(reader), ignoreModified: true);
        AssetReadUtility.RequireEnd(reader);
    }
    protected override byte[] Deparse(BaseContext context)
    {
        AssetReadUtility.RequireWritable(this);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(HotSpots.Count); writer.Write(HotSpots.ToBytes(context)); return stream.ToArray();
    }
    public static MissionHotSpotsAsset Default(BaseContext context)
    {
        var asset = new MissionHotSpotsAsset(); asset.ApplyBasicInfo(context); asset.MarkModified(); return asset;
    }
}
