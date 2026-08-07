using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

// The legacy namespace is retained to avoid breaking existing consumers.
namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;

/// <summary>
/// Per-side bookkeeping for WorldBuilder's "Skirmish AI Build List" feature.
/// This is not a unit production queue. The exact meaning of a non-zero count
/// has not yet been verified, so the value is intentionally preserved raw.
/// </summary>
public class BuildList : Ra3MapWritable
{
    private string _faction = string.Empty;
    private int _count;

    public string Faction
    {
        get => _faction;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_faction != value)
            {
                _faction = value;
                MarkModified();
            }
        }
    }

    public int Count
    {
        get => _count;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_count != value)
            {
                _count = value;
                MarkModified();
            }
        }
    }

    /// <summary>
    /// Gets or sets the raw WorldBuilder count. Prefer this name in new code;
    /// <see cref="Count"/> remains available for source compatibility.
    /// </summary>
    public int RawCount
    {
        get => Count;
        set => Count = value;
    }

    public static BuildList FromBinaryReader(BinaryReader reader)
    {
        var item = new BuildList
        {
            _faction = reader.ReadDefaultString(),
            _count = reader.ReadInt32()
        };

        if (item._count < 0)
        {
            throw new InvalidDataException($"Invalid BuildList count: {item._count}.");
        }

        return item;
    }

    public static BuildList Of(string faction, int count)
    {
        ArgumentNullException.ThrowIfNull(faction);
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var item = new BuildList { _faction = faction, _count = count };
        item.MarkModified();
        return item;
    }

    public override byte[] ToBytes(BaseContext context)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.WriteDefaultString(_faction);
        writer.Write(_count);
        writer.Flush();
        return stream.ToArray();
    }
}
