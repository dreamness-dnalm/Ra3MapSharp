using System.Text.Json.Serialization;

namespace Dreamness.RA3.Map.Automation.Geometry;

internal interface ITerrainFootprint
{
    IReadOnlyCollection<(int X, int Y)> Cells { get; }
}

internal sealed record SculptResult(int AffectedCells, bool PassabilityUpdated) : ITerrainFootprint
{
    [JsonIgnore] public IReadOnlyCollection<(int X, int Y)> Cells { get; init; } = Array.Empty<(int, int)>();
}

internal sealed record RampResult(int AffectedCells, int CoreCells, double LengthCells, double MaximumCoreSlopeDegrees,
    bool PassabilityUpdated, string[] NotEvaluated) : ITerrainFootprint
{
    [JsonIgnore] public IReadOnlyCollection<(int X, int Y)> Cells { get; init; } = Array.Empty<(int, int)>();
}
