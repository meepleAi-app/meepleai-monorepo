using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentRetryPolicy"/>.
/// Issue #1823 Wave 3 M9 — DEC-3j retry + dead-letter policy.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataCoverEnrichmentRetryPolicyTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly WikidataCoverEnrichmentRetryPolicy _sut = new();

    [Fact]
    public void Classify_Success_Terminal()
    {
        var result = new EnrichCatalogCoverResult.Success("k", "CC0", null, "u");

        var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.Terminal>();
    }

    [Theory]
    [InlineData("qid-missing")]
    [InlineData("already-enriched-recent")]
    [InlineData("image-not-available-p18")]
    [InlineData("license-not-whitelisted")]
    [InlineData("image-bytes-not-available")]
    public void Classify_AnySkipped_Terminal(string reason)
    {
        var result = new EnrichCatalogCoverResult.Skipped(reason);

        var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.Terminal>(
            "Skipped outcomes are business conditions — retry within the freshness window won't change them");
    }

    [Fact]
    public void Classify_FailedImageProcessing_DeadLetterImmediately()
    {
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonImageProcessing,
            "corrupted bytes");

        var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.DeadLetter>(
            "corrupted image bytes cannot be 'unbroken' by a retry — surface to admin for triage");
    }

    [Theory]
    [InlineData(0, 1)]   // first attempt fails → schedule retry at +1m
    [InlineData(1, 5)]   // second attempt fails → schedule retry at +5m
    [InlineData(2, 15)]  // third attempt fails → schedule retry at +15m
    public void Classify_FailedR2Upload_ScheduleRetryWithExponentialBackoff(
        int currentRetryCount,
        int expectedBackoffMinutes)
    {
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonR2Upload,
            "503 service unavailable");

        var decision = _sut.Classify(result, currentRetryCount, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.ScheduleRetry>();
        var nextRetryAt = ((WikidataCoverEnrichmentRetryDecision.ScheduleRetry)decision).NextRetryAt;
        nextRetryAt.Should().BeOnOrAfter(FixedNow.AddMinutes(expectedBackoffMinutes),
            $"DEC-3j exponential backoff floor: retry #{currentRetryCount + 1} fires no earlier than +{expectedBackoffMinutes}m");
        nextRetryAt.Should().BeOnOrBefore(
            FixedNow.AddMinutes(expectedBackoffMinutes).AddSeconds(WikidataCoverEnrichmentRetryPolicy.MaxJitterSeconds),
            "additive anti-herd jitter is bounded at MaxJitterSeconds above the base backoff");
    }

    [Fact]
    public void Classify_FailedR2Upload_AppliesJitterSpreadingRetriesInsteadOfSchedulingThemAtTheSameInstant()
    {
        // A batch of games that all hit the same transient r2-upload-error must NOT
        // be rescheduled to the identical NextRetryAt, or they stampede Wikimedia in
        // lockstep on the next scheduler tick. Additive jitter spreads them.
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonR2Upload,
            "503 service unavailable");

        var scheduledTimes = new HashSet<DateTime>();
        for (var i = 0; i < 200; i++)
        {
            var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);
            scheduledTimes.Add(((WikidataCoverEnrichmentRetryDecision.ScheduleRetry)decision).NextRetryAt);
        }

        scheduledTimes.Count.Should().BeGreaterThan(1,
            "additive jitter must spread simultaneously-failed retries across a band, not schedule them all at one instant");
    }

    [Fact]
    public void Classify_FailedR2Upload_JitterStaysWithinAdditiveBandAboveTheBaseBackoff()
    {
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonR2Upload,
            "503 service unavailable");
        var floor = FixedNow.AddMinutes(1);
        var ceiling = floor.AddSeconds(WikidataCoverEnrichmentRetryPolicy.MaxJitterSeconds);

        for (var i = 0; i < 200; i++)
        {
            var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);
            var nextRetryAt = ((WikidataCoverEnrichmentRetryDecision.ScheduleRetry)decision).NextRetryAt;
            nextRetryAt.Should().BeOnOrAfter(floor, "jitter is additive — it never fires before the base backoff")
                .And.BeOnOrBefore(ceiling, "jitter never exceeds MaxJitterSeconds above the base backoff");
        }
    }

    [Fact]
    public void Classify_FailedR2Upload_AfterMaxRetries_DeadLetter()
    {
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonR2Upload,
            "503");

        // currentRetryCount = 3 means this is the 4th attempt total (original + 3 retries).
        var decision = _sut.Classify(result, currentRetryCount: WikidataCoverEnrichmentRetryPolicy.MaxRetryCount, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.DeadLetter>(
            "DEC-3j caps retries at 3 — the 4th failure dead-letters");
    }

    [Fact]
    public void Classify_FailedUnknownReason_DeadLetter()
    {
        var result = new EnrichCatalogCoverResult.Failed("unknown-reason", "?");

        var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.DeadLetter>(
            "fail-fast on unexpected failure reasons rather than silently retrying");
    }

    [Fact]
    public void Classify_NullResult_Throws()
    {
        var act = () => _sut.Classify(null!, currentRetryCount: 0, FixedNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Classify_CircuitOpen_SchedulesRetryAt6mPastNow()
    {
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonCircuitOpen,
            "circuit OPEN");

        var decision = _sut.Classify(result, currentRetryCount: 0, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.ScheduleRetry>(
            "circuit-open is an upstream-infra signal — schedule a single retry past the 5min DEC-3f BreakDuration");
        var nextRetryAt = ((WikidataCoverEnrichmentRetryDecision.ScheduleRetry)decision).NextRetryAt;
        nextRetryAt.Should().BeOnOrAfter(FixedNow.Add(WikidataCoverEnrichmentRetryPolicy.CircuitOpenBackoff),
            "the retry must still land past the 5min BreakDuration even with jitter");
        nextRetryAt.Should().BeOnOrBefore(
            FixedNow.Add(WikidataCoverEnrichmentRetryPolicy.CircuitOpenBackoff).AddSeconds(WikidataCoverEnrichmentRetryPolicy.MaxJitterSeconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void Classify_CircuitOpen_DoesNotEscalateToDeadLetterRegardlessOfRetryCount(int currentRetryCount)
    {
        // Unlike r2-upload-error which dead-letters after 3 retries, circuit-open
        // is NOT counted against the DEC-3j budget — it's an upstream signal so
        // the breaker MUST recover before we give up on the game.
        var result = new EnrichCatalogCoverResult.Failed(
            EnrichCatalogCoverCommandHandler.FailReasonCircuitOpen,
            "circuit OPEN");

        var decision = _sut.Classify(result, currentRetryCount, FixedNow);

        decision.Should().BeOfType<WikidataCoverEnrichmentRetryDecision.ScheduleRetry>(
            "circuit-open never escalates to dead-letter — the breaker decides when upstream is up again");
    }
}
