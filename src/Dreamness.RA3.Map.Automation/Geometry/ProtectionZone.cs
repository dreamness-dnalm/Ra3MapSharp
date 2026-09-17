using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Geometry;

public sealed class ProtectionZone
{
    public string Id { get; set; } = "";
    public GridRegion Region { get; set; } = new();
    public string[] Layers { get; set; } = Array.Empty<string>();

    internal void Validate(Ra3MapFacade map)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 128 || Region == null || Layers == null || Layers.Length == 0
            || Layers.Any(layer => layer is not ("terrain" or "textures" or "passability" or "objects")))
            throw new AutomationException("INVALID_ARGUMENT", "保护区需要 id、region 和 layers（terrain/textures/passability/objects）。");
        Region.Cells(map);
    }

    internal void EnsureUnchanged(Ra3MapFacade before, Ra3MapFacade after)
    {
        var cells = Region.Cells(before);
        var a = before.ra3Map.Context.BlendTileDataAsset;
        var b = after.ra3Map.Context.BlendTileDataAsset;
        static byte[] Blend(Ra3MapFacade map, ushort index)
        {
            if (index == 0) return Array.Empty<byte>();
            var infos = map.ra3Map.Context.BlendTileDataAsset.BlendInfos;
            if (index > infos.Count) throw new AutomationException("VALIDATION_FAILED", "保护区引用无效纹理混合项。");
            return infos[index - 1].ToBytes(map.ra3Map.Context);
        }
        foreach (var layer in Layers)
        {
            var changed = false;
            if (layer == "objects")
            {
                var mask = cells.ToHashSet();
                string[] Fingerprints(Ra3MapFacade map) => map.ra3Map.Context.ObjectsListAsset.MapObjectList
                    .Where(obj => mask.Contains(((int)Math.Floor(obj.Position.X / 10) + map.MapBorderWidth,
                        (int)Math.Floor(obj.Position.Y / 10) + map.MapBorderWidth)))
                    .Select(obj => ContentHasher.HashBytes(obj.ToBytes(map.ra3Map.Context)))
                    .OrderBy(hash => hash, StringComparer.Ordinal).ToArray();
                changed = !Fingerprints(before).SequenceEqual(Fingerprints(after));
            }
            else foreach (var (x, y) in cells)
            {
                changed = layer switch
                {
                    "terrain" => before.GetTerrainHeight(x, y) != after.GetTerrainHeight(x, y),
                    "passability" => a.Passabilities[x, y] != b.Passabilities[x, y],
                    "textures" => before.GetTileTexture(x, y) != after.GetTileTexture(x, y)
                        || a.Tiles[x, y] != b.Tiles[x, y] || a.Blends[x, y] != b.Blends[x, y]
                        || !Blend(before, a.Blends[x, y]).SequenceEqual(Blend(after, b.Blends[x, y]))
                        || a.SingleEdgeBlends[x, y] != b.SingleEdgeBlends[x, y] || a.CliffBlends[x, y] != b.CliffBlends[x, y],
                    _ => throw new AutomationException("VALIDATION_FAILED", "未知保护图层。")
                };
                if (changed) break;
            }
            if (changed) throw new AutomationException("PROTECTED_REGION", $"修改触及保护区 {Id} 的 {layer} 图层。",
                details: new Dictionary<string, string> { ["protectionId"] = Id, ["layer"] = layer });
        }
    }
}
