using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.RA3.Map.Automation.Storage;

namespace Dreamness.RA3.Map.Automation.Session;

public sealed partial class MapSession
{
    // Invoked under the executor's lock. No live session state is replaced.
    internal async Task<object> ReadAnalysisMapAsync(string? planId, string? planHash,
        Func<Ra3MapFacade, int, string, object> analyze, CancellationToken token)
    {
        if ((planId == null) != (planHash == null)) throw new AutomationException("INVALID_ARGUMENT", "preparedPlanId 和 planHash 必须成对提供。");
        if (planId == null) return analyze(Facade, Revision, await ContentHasher.HashFileAsync(WorkingMapFilePath, token));
        var plan = RequirePrepared(planId, planHash);
        var path = Path.Combine(_layout.WorkingDirectoryPath, Guid.NewGuid().ToString("N") + ".map");
        try
        {
            await File.WriteAllBytesAsync(path, plan.Bytes, token);
            return analyze(Ra3MapFacade.Open(path), plan.Info.BaseRevision, plan.Info.CandidateContentHash);
        }
        finally { FileTransaction.TryDelete(path); }
    }
}
