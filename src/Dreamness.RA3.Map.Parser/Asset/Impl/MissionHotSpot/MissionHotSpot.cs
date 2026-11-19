using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.MissionHotSpot;

/// <summary>A mission hotspot ID and its title/description string-table keys.</summary>
public class MissionHotSpot : Ra3MapWritable
{
    private string id = "";
    private string title = "";
    private string description = "";
    public string Id { get => id; set { ArgumentNullException.ThrowIfNull(value); if (id != value) { id = value; MarkModified(); } } }
    public string Title { get => title; set { ArgumentNullException.ThrowIfNull(value); if (title != value) { title = value; MarkModified(); } } }
    public string Description { get => description; set { ArgumentNullException.ThrowIfNull(value); if (description != value) { description = value; MarkModified(); } } }
    public static MissionHotSpot Of(string id, string title, string description)
    {
        var item = new MissionHotSpot { Id = id, Title = title, Description = description };
        item.MarkModified(); return item;
    }
    public static MissionHotSpot FromBinaryReader(BinaryReader reader) => new()
    { id = AssetReadUtility.ReadString(reader), title = AssetReadUtility.ReadString(reader), description = AssetReadUtility.ReadString(reader) };
    public override byte[] ToBytes(BaseContext context)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        AssetReadUtility.WriteString(writer, id); AssetReadUtility.WriteString(writer, title); AssetReadUtility.WriteString(writer, description);
        return stream.ToArray();
    }
}
