using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Commands.Objects;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    // The executor holds ExecutionLock. Candidate reads never replace live editing state.
    internal async Task<object> AnalyzeObjectSpaceAsync(AnalyzeObjectSpaceArgs args, CancellationToken token)
    {
        if (args.Limit is < 1 or > 1000 || (args.PreparedPlanId == null) != (args.PlanHash == null))
            throw new AutomationException("INVALID_ARGUMENT", "limit 为1–1000；preparedPlanId 和 planHash 必须成对提供。");
        var profiles = new ObjectFootprintSet(args.Footprints);
        string? temporaryPath = null;
        try
        {
            var map = Facade;
            IReadOnlyList<string> ids = Handles.UnitObjectIds;
            var revision = Revision;
            string contentHash;
            if (args.PreparedPlanId != null)
            {
                var plan = RequirePrepared(args.PreparedPlanId, args.PlanHash);
                temporaryPath = Path.Combine(_layout.WorkingDirectoryPath, Guid.NewGuid().ToString("N") + ".map");
                await File.WriteAllBytesAsync(temporaryPath, plan.Bytes, token);
                map = Ra3MapFacade.Open(temporaryPath);
                ids = plan.Objects;
                revision = plan.Info.BaseRevision;
                contentHash = plan.Info.CandidateContentHash;
            }
            else contentHash = await ContentHasher.HashFileAsync(WorkingMapFilePath, token);
            var objects = map.GetUnitObjects();
            if (objects.Count > 2000) throw new AutomationException("LIMIT_EXCEEDED", "占地检查当前最多2000个普通对象。");
            var unknown = objects.Where(o => !profiles.Contains(o.TypeName)).GroupBy(o => o.TypeName)
                .Select(g => new { typeName = g.Key, count = g.Count() }).OrderBy(p => p.typeName, StringComparer.Ordinal).ToArray();
            var boxes = objects.Select((o, index) => (Id: ids[index], Boxes: profiles.Contains(o.TypeName)
                ? profiles.At(o.TypeName, o.Position.X / 10d, o.Position.Y / 10d, MapAngles.ToRadians(o.Angle)) : Array.Empty<OrientedFootprintBox>())).ToArray();
            var overlaps = new List<object>(); var outside = new List<object>();
            var overlapCount = 0; var outsideCount = 0;
            for (var i = 0; i < boxes.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                foreach (var box in boxes[i].Boxes)
                    if (!box.Inside(map.MapPlayableWidth, map.MapPlayableHeight))
                    {
                        outsideCount++;
                        if (outside.Count < args.Limit) outside.Add(new { objectId = boxes[i].Id, box = box.Label, box.Corners });
                    }
                for (var j = i + 1; j < boxes.Length; j++)
                foreach (var first in boxes[i].Boxes)
                foreach (var second in boxes[j].Boxes)
                    if (first.Overlaps(second))
                    {
                        overlapCount++;
                        if (overlaps.Count < args.Limit) overlaps.Add(new { firstObjectId = boxes[i].Id, firstBox = first.Label,
                            secondObjectId = boxes[j].Id, secondBox = second.Label });
                    }
            }
            return new { revision, mapContentHash = contentHash, args.PreparedPlanId, args.PlanHash,
                model = "explicit-oriented-rectangles-v1", space = "playableGrid", profileSource = "caller-supplied",
                profileHash = ContentHasher.HashBytes(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(args.Footprints, AutomationJson.Options)),
                objectCount = objects.Count, unknownTypes = unknown, coverageComplete = unknown.Length == 0,
                clearUnderProfile = unknown.Length == 0 && overlapCount == 0 && outsideCount == 0,
                overlapCount, overlaps, outsideCount, outside, truncated = overlaps.Count < overlapCount || outside.Count < outsideCount,
                notEvaluated = new[] { "engineCollision", "terrainSuitability", "waypointsAndRoads", "miningBehavior", "buildability", "profileAccuracy" } };
        }
        finally { if (temporaryPath != null) FileTransaction.TryDelete(temporaryPath); }
    }
}
