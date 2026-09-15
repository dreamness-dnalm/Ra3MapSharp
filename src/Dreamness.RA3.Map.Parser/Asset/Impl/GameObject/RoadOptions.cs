namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

/// <summary>
/// World Builder 路径节点上保存的原始 option 位。
/// 各位含义尚未对照 World Builder 完全核实，因此先按原始整数保留。
/// </summary>
public readonly struct RoadOptions : IEquatable<RoadOptions>
{
    public RoadOptions(int rawValue)
    {
        RawValue = rawValue;
    }

    public int RawValue { get; }

    public bool IsRoad => RawValue != 0;

    public bool ContainsBits(int bits) => (RawValue & bits) == bits;

    public static RoadOptions Default => new(2);

    public bool Equals(RoadOptions other) => RawValue == other.RawValue;

    public override bool Equals(object? obj) => obj is RoadOptions other && Equals(other);

    public override int GetHashCode() => RawValue;

    public override string ToString() => $"0x{RawValue:X8}";

    public static bool operator ==(RoadOptions left, RoadOptions right) => left.Equals(right);

    public static bool operator !=(RoadOptions left, RoadOptions right) => !left.Equals(right);
}
