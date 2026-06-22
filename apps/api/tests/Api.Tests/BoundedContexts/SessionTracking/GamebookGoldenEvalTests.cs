using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public class GamebookGoldenEvalTests
{
    [Fact(Skip = "Aaron-validated locally — requires Nanolith Press Start + Rules indexed and Nanolith Tutor agent active. See design doc §0.1 (actionable) and §0.4 (confidence binning).")]
    public void GoldenSet_N1AndN2_MeetsConfidenceFloor()
    {
        // Validation pattern (manual, post-session):
        //   1. Aaron asks 5 N1 setup queries to Nanolith Tutor agent
        //   2. Aaron asks 5 N2 in-game rules queries
        //   3. Each response evaluated against §0.1 actionable criteria (3 boolean checks)
        //   4. DoD met if >=4/5 actionable per goal AND confidence >=0.7 per §0.4 binning
        //
        // Tracking sheet: nanolith-dogfood-eval.gsheet (created by Aaron post-Iter 1.A).
        // Wire to `make seed-index` CI job + remove skip gate in Iter 2 once stable.
    }
}
