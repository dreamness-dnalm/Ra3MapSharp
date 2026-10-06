using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Agent.Rendering;

/// <summary>
/// A rendered album tile. Implements <see cref="IAgentImage"/> so the MCP layer attaches the
/// PNG itself instead of only describing it.
/// </summary>
public sealed record AlbumImage(string ImagePath, string ImageHash, string TypeName, string AlbumHash,
    int GridX, int GridY, int Batch, long Bytes) : IAgentImage;
