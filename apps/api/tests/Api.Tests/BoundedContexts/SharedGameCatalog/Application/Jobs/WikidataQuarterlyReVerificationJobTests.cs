using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using FluentAssertions;
using Quartz;
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

    /// <summary>
    /// The cron string registered in the DI extension (<c>0 0 4 1 */3 ?</c>)
    /// is a Quartz 6-field expression (seconds + minutes + hours + day-of-month
    /// + month + day-of-week). Quartz.NET requires exactly one of day-of-month /
    /// day-of-week to be the <c>?</c> wildcard. This test guards against a
    /// typo silently breaking the schedule at runtime — a parse failure would
    /// only surface when Quartz boots in staging.
    /// </summary>
    [Fact]
    public void CronExpression_QuarterlySchedule_ParsesAndFiresOnExpectedMonths()
    {
        const string cron = "0 0 4 1 */3 ?";
        var act = () => new CronExpression(cron);
        act.Should().NotThrow("the M15 schedule MUST parse — a typo here would only be caught at staging boot");

        // Pin CronExpression to UTC explicitly — the default is
        // TimeZoneInfo.Local, which would make this test machine-dependent.
        // The DI registration uses Quartz's default scheduler TZ (UTC in prod).
        var expression = new CronExpression(cron) { TimeZone = TimeZoneInfo.Utc };

        // Probe the four expected quarters from a Jan 1st anchor BEFORE the
        // fire-time. Quartz's GetNextValidTimeAfter returns the NEXT fire
        // strictly after the anchor, so anchoring at 2026-01-01T00:00:00Z
        // walks us through Jan/Apr/Jul/Oct at 04:00 UTC, day-of-month=1,
        // confirming both the cron parses AND fires on the documented cadence.
        var anchor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedMonths = new[] { 1, 4, 7, 10 };

        foreach (var month in expectedMonths)
        {
            var next = expression.GetNextValidTimeAfter(anchor);
            next.Should().NotBeNull($"the schedule MUST yield a next fire-time after {anchor:O}");
            var utc = next!.Value.ToUniversalTime();
            utc.Day.Should().Be(1, "DEC-3i locks the 1st-of-month firing day");
            utc.Hour.Should().Be(4, "DEC-3i locks the 04:00 UTC firing hour");
            utc.Minute.Should().Be(0);
            utc.Month.Should().Be(month,
                "the M15 cron fires on Jan/Apr/Jul/Oct — verifying step traversal");
            anchor = next.Value;
        }
    }
}
