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
        ((WikidataCoverEnrichmentRetryDecision.ScheduleRetry)decision).NextRetryAt
            .Should().Be(FixedNow.AddMinutes(expectedBackoffMinutes),
                $"DEC-3j exponential backoff: retry #{currentRetryCount + 1} fires at +{expectedBackoffMinutes}m");
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
}
