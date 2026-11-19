using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Waypoint;

/// <summary>Waypoint connections. Waypoint names/positions live in ObjectsList.</summary>
public class WaypointsListAsset : BaseAsset
{
    public WritableList<WaypointConnection> Connections { get; } = new();
    public WaypointsListAsset() => ObservableUtil.Subscribe(Connections, this);
    public override short GetVersion() => 1;
    public override string GetAssetType() => AssetNameConst.WaypointsList;
    protected override void _Parse(BaseContext context)
    {
        AssetReadUtility.RequireVersion(this, 1);
        using var reader = new BinaryReader(new MemoryStream(Data));
        var count = AssetReadUtility.ReadCount(reader, 8);
        for (var i = 0; i < count; i++) Connections.Add(WaypointConnection.FromBinaryReader(reader), ignoreModified: true);
        AssetReadUtility.RequireEnd(reader);
    }
    protected override byte[] Deparse(BaseContext context)
    {
        AssetReadUtility.RequireWritable(this);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(Connections.Count); writer.Write(Connections.ToBytes(context)); return stream.ToArray();
    }
    public static WaypointsListAsset Default(BaseContext context)
    {
        var asset = new WaypointsListAsset(); asset.ApplyBasicInfo(context); asset.MarkModified(); return asset;
    }
}
