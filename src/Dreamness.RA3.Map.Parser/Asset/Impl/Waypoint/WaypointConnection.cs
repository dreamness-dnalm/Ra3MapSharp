using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Waypoint;

/// <summary>An ordered pair of waypoint IDs stored in WaypointsList.</summary>
public class WaypointConnection : Ra3MapWritable
{
    private int fromWaypointId;
    private int toWaypointId;
    public int FromWaypointId { get => fromWaypointId; set { if (fromWaypointId != value) { fromWaypointId = value; MarkModified(); } } }
    public int ToWaypointId { get => toWaypointId; set { if (toWaypointId != value) { toWaypointId = value; MarkModified(); } } }
    public static WaypointConnection Of(int fromWaypointId, int toWaypointId)
    {
        var connection = new WaypointConnection { fromWaypointId = fromWaypointId, toWaypointId = toWaypointId };
        connection.MarkModified(); return connection;
    }
    public static WaypointConnection FromBinaryReader(BinaryReader reader) => new()
    { fromWaypointId = reader.ReadInt32(), toWaypointId = reader.ReadInt32() };
    public override byte[] ToBytes(BaseContext context)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(fromWaypointId); writer.Write(toWaypointId); return stream.ToArray();
    }
}
