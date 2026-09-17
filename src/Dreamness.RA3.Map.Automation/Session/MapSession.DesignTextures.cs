using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Terrain;
using Dreamness.RA3.Map.Automation.Design;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    private TextureCellValue ReadTextureCell(int x, int y)
    {
        var asset = Facade.ra3Map.Context.BlendTileDataAsset;
        var blend = asset.Blends[x, y];
        if (blend > asset.BlendInfos.Count) throw new AutomationException("VALIDATION_FAILED", "纹理格引用无效混合项。");
        var definition = new { texture = Facade.GetTileTexture(x, y),
            blend = blend == 0 ? Array.Empty<byte>() : asset.BlendInfos[blend - 1].ToBytes(Facade.ra3Map.Context) };
        return new(asset.Tiles[x, y], blend, asset.SingleEdgeBlends[x, y], asset.CliffBlends[x, y],
            ContentHasher.HashBytes(JsonSerializer.SerializeToUtf8Bytes(definition, AutomationJson.Options)));
    }

    private void RestoreTextureCell(int x, int y, TextureCellValue value)
    {
        // Painting only appends/reuses table entries; preserve original tile variants and blend references.
        var asset = Facade.ra3Map.Context.BlendTileDataAsset;
        asset.Tiles[x, y] = value.Tile;
        asset.Blends[x, y] = value.Blend;
        asset.SingleEdgeBlends[x, y] = value.SingleEdgeBlend;
        asset.CliffBlends[x, y] = value.CliffBlend;
    }

    private object GenerateTextureEntity(DesignEntityState entity, HashSet<string> ancestors, CancellationToken token)
    {
        var sourceSpec = entity.Spec;
        var rotate = false;
        while (sourceSpec.Kind == "derived")
        {
            if (sourceSpec.Parameters.GetProperty("transform").GetString() != "rotate180")
                throw new AutomationException("INVALID_ARGUMENT", "仅支持 rotate180。");
            rotate = !rotate;
            sourceSpec = DesignEntities[sourceSpec.Parameters.GetProperty("sourceEntityId").GetString()!].Spec;
        }
        if (sourceSpec.Kind != "texture") throw new AutomationException("INVALID_REFERENCE", "纹理来源必须为纹理实体。");
        var args = AutomationJson.Deserialize<PaintTextureArgs>(sourceSpec.Parameters);
        if (args.Region == null) throw new AutomationException("INVALID_ARGUMENT", "纹理实体 region 必填。");
        var painted = args.Region.GetCells(Facade).Select(c => rotate
            ? (X: Facade.MapWidth - 1 - c.X, Y: Facade.MapHeight - 1 - c.Y) : c).ToHashSet();
        var cells = painted.ToHashSet();
        if (args.AutoBlend)
            foreach (var (x, y) in cells.ToArray())
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    if (x + dx >= 0 && y + dy >= 0 && x + dx < Facade.MapWidth && y + dy < Facade.MapHeight)
                        cells.Add((x + dx, y + dy));
        if (entity.Spec.Kind == "derived")
        {
            var source = DesignEntities[entity.Spec.Parameters.GetProperty("sourceEntityId").GetString()!];
            if (cells.Overlaps((source.Textures ?? new()).Select(c => (c.X, c.Y))))
                throw new AutomationException("SYMMETRY_OVERLAP", "派生纹理的区域或混合边界与源实体重叠。");
        }
        var claimed = DesignEntities.Where(e => !ancestors.Contains(e.Key))
            .SelectMany(e => e.Value.Textures ?? new()).Select(c => (c.X, c.Y)).ToHashSet();
        if (cells.Overlaps(claimed)) throw new AutomationException("ENTITY_OVERLAP", "纹理区域及混合边界与非依赖实体重叠。");
        var before = cells.ToDictionary(c => c, c => ReadTextureCell(c.X, c.Y));
        var result = PaintTextureHandler.PaintCells(Facade, painted.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray(),
            args.Texture ?? "", args.AutoBlend, token);
        entity.Textures = new();
        foreach (var cell in cells.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            token.ThrowIfCancellationRequested();
            entity.Textures.Add(new(cell.X, cell.Y, before[cell], ReadTextureCell(cell.X, cell.Y)));
        }
        return result;
    }
}
