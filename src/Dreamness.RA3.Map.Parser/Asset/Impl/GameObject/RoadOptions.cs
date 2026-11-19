using System.Text.Json.Serialization;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

/// <summary>
/// World Builder 路径节点上保存的原始 option 位。
/// 端点与拐角/尾部位已对照 Generals MapObject 标志，并用 Brighton/Cannes/Leningrad
/// 官方战役图直方图复核（仅见 0x2/4/8/10/20/40/80 组合）。其余位仍按原始整数保留。
/// </summary>
public readonly struct RoadOptions : IEquatable<RoadOptions>
{
    public const int RoadStartBit = 2;
    public const int RoadEndBit = 4;
    public const int CornerAngledBit = 8;
    public const int BridgeStartBit = 16;
    public const int BridgeEndBit = 32;
    public const int CornerTightBit = 0x40;
    public const int JoinBit = 0x80;

    /// <summary>拐角样式位：直角 0x8，小弧 0x40；皆无则大弧（默认）。</summary>
    public const int CornerStyleMask = CornerAngledBit | CornerTightBit;

    /// <summary>不可作为 additionalOptions 传入的起终点/桥梁端点位。</summary>
    public const int EndpointBitsMask = RoadStartBit | RoadEndBit | BridgeStartBit | BridgeEndBit;

    public int RawValue { get; }

    [JsonIgnore] public bool IsRoadStart => ContainsBits(RoadStartBit);
    [JsonIgnore] public bool IsRoadEnd => ContainsBits(RoadEndBit);
    [JsonIgnore] public bool IsBridgeStart => ContainsBits(BridgeStartBit);
    [JsonIgnore] public bool IsBridgeEnd => ContainsBits(BridgeEndBit);
    [JsonIgnore] public bool IsCornerAngled => ContainsBits(CornerAngledBit);
    [JsonIgnore] public bool IsCornerTight => ContainsBits(CornerTightBit);
    [JsonIgnore] public bool IsJoin => ContainsBits(JoinBit);

    /// <summary>大弧：未设直角/小弧位。直角优先于小弧（与地编互斥写入一致）。</summary>
    [JsonIgnore]
    public bool IsCornerBroad => (RawValue & CornerStyleMask) == 0;

    public bool IsRoad => RawValue != 0;
    public static RoadOptions Default => new(RoadStartBit);

    public RoadOptions(int rawValue) => RawValue = rawValue;

    public bool ContainsBits(int bits) => (RawValue & bits) == bits;

    /// <summary>替换拐角样式位，保留端点与尾部等其它位。</summary>
    public RoadOptions WithCornerStyle(bool angled, bool tight)
    {
        var value = RawValue & ~CornerStyleMask;
        if (angled) value |= CornerAngledBit;
        else if (tight) value |= CornerTightBit;
        return new RoadOptions(value);
    }

    /// <summary>设置或清除尾部/异种路面接合位。</summary>
    public RoadOptions WithJoin(bool join) =>
        new(join ? RawValue | JoinBit : RawValue & ~JoinBit);

    public bool Equals(RoadOptions other) => RawValue == other.RawValue;
    public override bool Equals(object? obj) => obj is RoadOptions other && Equals(other);
    public override int GetHashCode() => RawValue;
    public override string ToString() => $"0x{RawValue:X8}";
    public static bool operator ==(RoadOptions left, RoadOptions right) => left.Equals(right);
    public static bool operator !=(RoadOptions left, RoadOptions right) => !left.Equals(right);
}
