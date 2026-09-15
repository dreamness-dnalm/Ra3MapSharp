namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

/// <summary>
/// 路径工具节点的领域包装。RA3 把这些节点存在普通 Object 列表里，
/// 但非零的 RoadOption 把它们和单位、场景物体区分开。
/// </summary>
public sealed class RoadObjectWrap : ObjectWrap
{
    public RoadObjectWrap(ObjectAsset obj) : base(obj)
    {
        if (!obj.IsRoad)
        {
            throw new ArgumentException("The object does not contain a non-zero road option.", nameof(obj));
        }
    }

    public float Angle
    {
        get => Obj.Angle;
        set => Obj.Angle = value;
    }

    public string TypeName
    {
        get => Obj.TypeName;
        set => Obj.TypeName = value;
    }

    public RoadOptions Options
    {
        get => new(Obj.RoadOption);
        set
        {
            if (!value.IsRoad)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A road node must have a non-zero option value.");
            }

            Obj.RoadOption = value.RawValue;
        }
    }

    public new static RoadObjectWrap Of(ObjectAsset obj) => new(obj);
}
