using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Unit tests for <see cref="WikidataQuarterlyReVerificationJob"/>. Issue #1823
/// Wave 3 M15 (ADR DEC-3i). Functional behaviour (ExecuteUpdateAsync against
/// shared_games) is exercised by the Testcontainers integration test
/// <c>WikidataQuarterlyReVerificationJobIntegrationTests</c>; this class locks
/// the ADR-bound constants so any future change requires a deliberate ADR
/// update.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataQuarterlyReVerificationJobTests
{
    [Fact]
    public void ReVerificationWindow_IsExactly90Days()
    {
        // DEC-3i locks the quarterly re-verification window at 90 days — must
        // match the M8 freshness short-circuit so the M9 scheduler picks up
        // the reset rows on its very next tick.
        WikidataQuarterlyReVerificationJob.ReVerificationWindow
            .Should().Be(TimeSpan.FromDays(90),
                "DEC-3i locks the re-verification window at 90 days — any change requires a deliberate ADR update");
    }
}
