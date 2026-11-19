using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Trigger;

public class TriggerAreasAsset : BaseAsset
{
    public WritableList<TriggerArea> Areas { get; } = new();
    public TriggerAreasAsset() => ObservableUtil.Subscribe(Areas, this);
    public override short GetVersion() => 1;
    public override string GetAssetType() => AssetNameConst.TriggerAreas;
    protected override void _Parse(BaseContext context)
    {
        AssetReadUtility.RequireVersion(this, 1);
        using var reader = new BinaryReader(new MemoryStream(Data));
        var count = AssetReadUtility.ReadCount(reader, 16);
        for (var i = 0; i < count; i++) Areas.Add(TriggerArea.FromBinaryReader(reader, context), ignoreModified: true);
        AssetReadUtility.RequireEnd(reader);
    }
    protected override byte[] Deparse(BaseContext context)
    {
        AssetReadUtility.RequireWritable(this);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(Areas.Count); writer.Write(Areas.ToBytes(context)); return stream.ToArray();
    }
    public static TriggerAreasAsset Default(BaseContext context)
    {
        var asset = new TriggerAreasAsset(); asset.ApplyBasicInfo(context); asset.MarkModified(); return asset;
    }
}
