namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

/// <summary>
/// Domain wrapper for a road-tool node. RA3 stores these nodes in the normal
/// Object asset list, but a non-zero RoadOption distinguishes them from units
/// and static scenery objects.
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
