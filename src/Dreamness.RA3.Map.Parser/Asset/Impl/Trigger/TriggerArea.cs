using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Trigger;

/// <summary>A named polygon in world coordinates with a trigger layer and numeric ID.</summary>
public class TriggerArea : Ra3MapWritable
{
    private string name = "";
    private string layerName = "";
    private int id;
    private int height;
    public string Name { get => name; set { ArgumentNullException.ThrowIfNull(value); if (name != value) { name = value; MarkModified(); } } }
    public string LayerName { get => layerName; set { ArgumentNullException.ThrowIfNull(value); if (layerName != value) { layerName = value; MarkModified(); } } }
    public int Id { get => id; set { if (id != value) { id = value; MarkModified(); } } }
    /// <summary>The final int32 polygon field. Its editor height semantics need manual confirmation.</summary>
    public int Height { get => height; set { if (height != value) { height = value; MarkModified(); } } }
    public WritableList<Vec2D> Points { get; } = new();
    public TriggerArea() => ObservableUtil.Subscribe(Points, this);
    public static TriggerArea Of(string name, string layerName, int id, IEnumerable<Vec2D> points, int height = 0)
    {
        ArgumentNullException.ThrowIfNull(points);
        var area = new TriggerArea { Name = name, LayerName = layerName, Id = id, Height = height };
        foreach (var point in points) { ArgumentNullException.ThrowIfNull(point); area.Points.Add(point); }
        area.MarkModified(); return area;
    }
    public static TriggerArea FromBinaryReader(BinaryReader reader, BaseContext context)
    {
        var area = new TriggerArea { name = AssetReadUtility.ReadString(reader), layerName = AssetReadUtility.ReadString(reader), id = reader.ReadInt32() };
        var count = AssetReadUtility.ReadCount(reader, 8);
        for (var i = 0; i < count; i++) area.Points.Add(Vec2D.FromBinaryReader(reader, context), ignoreModified: true);
        area.height = reader.ReadInt32(); return area;
    }
    public override byte[] ToBytes(BaseContext context)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        AssetReadUtility.WriteString(writer, name); AssetReadUtility.WriteString(writer, layerName);
        writer.Write(id); writer.Write(Points.Count); writer.Write(Points.ToBytes(context)); writer.Write(height);
        return stream.ToArray();
    }
}
