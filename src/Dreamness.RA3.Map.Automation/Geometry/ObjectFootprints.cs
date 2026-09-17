using Dreamness.RA3.Map.Automation.Commands.Objects;

namespace Dreamness.RA3.Map.Automation.Geometry;

/// <summary>Explicit planning rectangles, in cells, relative to an object's unrotated origin.</summary>
public sealed class ObjectFootprint
{
    public string TypeName { get; set; } = "";
    public FootprintBox[] Boxes { get; set; } = Array.Empty<FootprintBox>();
}

public sealed class FootprintBox
{
    public string Label { get; set; } = "body";
    public double WidthCells { get; set; }
    public double DepthCells { get; set; }
    public double OffsetXCells { get; set; }
    public double OffsetYCells { get; set; }
    public bool BlocksMovement { get; set; } = true;
}

public readonly record struct FootprintPoint(double X, double Y);

public sealed class OrientedFootprintBox
{
    public string Label { get; }
    public FootprintPoint[] Corners { get; }
    public double MinX => Corners.Min(p => p.X);
    public double MaxX => Corners.Max(p => p.X);
    public double MinY => Corners.Min(p => p.Y);
    public double MaxY => Corners.Max(p => p.Y);

    public OrientedFootprintBox(FootprintBox box, double x, double y, double angleRadians)
    {
        Label = box.Label;
        var cos = Math.Cos(angleRadians); var sin = Math.Sin(angleRadians);
        Corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(sign =>
        {
            var localX = box.OffsetXCells + sign.Item1 * box.WidthCells / 2;
            var localY = box.OffsetYCells + sign.Item2 * box.DepthCells / 2;
            return new FootprintPoint(x + localX * cos - localY * sin, y + localX * sin + localY * cos);
        }).ToArray();
    }

    public bool Inside(double width, double height) => MinX >= -1e-7 && MinY >= -1e-7 && MaxX <= width + 1e-7 && MaxY <= height + 1e-7;

    public bool ContainsPoint(double x, double y)
    {
        var origin = Corners[0];
        foreach (var end in new[] { Corners[1], Corners[3] })
        {
            var dx = end.X - origin.X; var dy = end.Y - origin.Y;
            var projection = (x - origin.X) * dx + (y - origin.Y) * dy;
            if (projection < -1e-7 || projection > dx * dx + dy * dy + 1e-7) return false;
        }
        return true;
    }

    // Separating-axis theorem; mere edge contact is permitted. Use larger input boxes for clearance.
    public bool Overlaps(OrientedFootprintBox other)
    {
        if (MaxX <= other.MinX + 1e-7 || other.MaxX <= MinX + 1e-7 || MaxY <= other.MinY + 1e-7 || other.MaxY <= MinY + 1e-7) return false;
        foreach (var polygon in new[] { Corners, other.Corners })
        for (var i = 0; i < 2; i++)
        {
            var dx = polygon[i + 1].X - polygon[i].X;
            var dy = polygon[i + 1].Y - polygon[i].Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var ax = -dy / length; var ay = dx / length;
            var first = Corners.Select(p => p.X * ax + p.Y * ay).ToArray();
            var second = other.Corners.Select(p => p.X * ax + p.Y * ay).ToArray();
            if (first.Max() <= second.Min() + 1e-7 || second.Max() <= first.Min() + 1e-7) return false;
        }
        return true;
    }
}

public sealed class ObjectFootprintSet
{
    public static string ContentHash(ObjectFootprint[] profiles) => Storage.ContentHasher.HashBytes(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(profiles, Storage.AutomationJson.Options));
    private readonly Dictionary<string, ObjectFootprint> _types = new(StringComparer.Ordinal);
    public ObjectFootprintSet(ObjectFootprint[] profiles)
    {
        if (profiles == null || profiles.Length is < 1 or > 256) throw new AutomationException("INVALID_ARGUMENT", "footprints 需要1–256个类型。");
        foreach (var profile in profiles)
        {
            if (profile == null || !ObjectHandler.ValidTypeName(profile.TypeName) || !_types.TryAdd(profile.TypeName, profile)
                || profile.Boxes == null || profile.Boxes.Length is < 1 or > 8)
                throw new AutomationException("INVALID_ARGUMENT", "占地类型必须唯一，每类需要1–8个矩形。");
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var box in profile.Boxes)
                if (box == null || string.IsNullOrWhiteSpace(box.Label) || box.Label.Length > 64 || !labels.Add(box.Label)
                    || !double.IsFinite(box.WidthCells) || !double.IsFinite(box.DepthCells)
                    || !double.IsFinite(box.OffsetXCells) || !double.IsFinite(box.OffsetYCells)
                    || box.WidthCells <= 0 || box.DepthCells <= 0 || box.WidthCells > 4096 || box.DepthCells > 4096
                    || Math.Abs(box.OffsetXCells) > 4096 || Math.Abs(box.OffsetYCells) > 4096)
                    throw new AutomationException("INVALID_ARGUMENT", "占地矩形需要唯一标签、有限偏移及正的宽深，绝对值最多4096格。");
        }
    }
    public bool Contains(string type) => _types.ContainsKey(type);
    public OrientedFootprintBox[] At(string type, double x, double y, double angleRadians)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(angleRadians))
            throw new AutomationException("INVALID_ARGUMENT", "对象位置和朝向必须有限。");
        if (!_types.TryGetValue(type, out var profile)) throw new AutomationException("FOOTPRINT_MISSING", "缺少占地定义: " + type);
        return profile.Boxes.Select(b => new OrientedFootprintBox(b, x, y, angleRadians)).ToArray();
    }
    public OrientedFootprintBox[] MovementAt(string type, double x, double y, double angleRadians)
    {
        // At validates coordinates and requires a known profile even when all boxes are reservations.
        _ = At(type, x, y, angleRadians);
        return _types[type].Boxes.Where(b => b.BlocksMovement).Select(b => new OrientedFootprintBox(b, x, y, angleRadians)).ToArray();
    }
}
