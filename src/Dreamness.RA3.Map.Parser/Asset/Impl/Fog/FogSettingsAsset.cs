using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Fog;

/// <summary>Rendering distance fog; unrelated to explored/unexplored fog of war.</summary>
public class FogSettingsAsset : BaseAsset
{
    private int enabled;
    private float rangeStart;
    private float rangeEnd;
    private ColorRgbF color = new(1, 1, 1);

    public bool Enabled { get => enabled != 0; set { if (Enabled != value) { enabled = value ? 1 : 0; MarkModified(); } } }
    public float RangeStart { get => rangeStart; set { if (rangeStart != value) { rangeStart = value; MarkModified(); } } }
    public float RangeEnd { get => rangeEnd; set { if (rangeEnd != value) { rangeEnd = value; MarkModified(); } } }
    public ColorRgbF Color { get => color; set { ArgumentNullException.ThrowIfNull(value); if (color != value) { color = value; MarkModified(); } } }

    public override short GetVersion() => 1;
    public override string GetAssetType() => AssetNameConst.FogSettings;
    protected override void _Parse(BaseContext context)
    {
        AssetReadUtility.RequireVersion(this, 1);
        using var reader = new BinaryReader(new MemoryStream(Data));
        enabled = reader.ReadInt32();
        rangeStart = reader.ReadSingle();
        rangeEnd = reader.ReadSingle();
        color = reader.ReadColorRgbF();
        AssetReadUtility.RequireEnd(reader);
    }
    protected override byte[] Deparse(BaseContext context)
    {
        AssetReadUtility.RequireWritable(this);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(enabled); writer.Write(rangeStart); writer.Write(rangeEnd); writer.WriteColorRgbF(color);
        return stream.ToArray();
    }
    public static FogSettingsAsset Default(BaseContext context)
    {
        var asset = new FogSettingsAsset();
        asset.ApplyBasicInfo(context); asset.MarkModified();
        return asset;
    }
}
