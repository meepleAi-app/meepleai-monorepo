using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentAttempt.Acknowledge"/> — Issue #1823 Phase F F5.
/// Verifies the dead-letter acknowledgement mutator: only-on-dead-letter guard,
/// non-empty user id guard, and idempotency on repeated calls.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2254")]
public class WikidataCoverEnrichmentAttemptAcknowledgeTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime AttemptedAt = new(2026, 06, 13, 10, 00, 00, DateTimeKind.Utc);
    private static readonly DateTime AckedAt = new(2026, 06, 13, 11, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void Acknowledge_OnDeadLetter_PersistsAtAndBy()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", details: null, retryCount: 3, attemptedAt: AttemptedAt);

        dl.Acknowledge(UserId, AckedAt);

        dl.AcknowledgedAt.Should().Be(AckedAt);
        dl.AcknowledgedBy.Should().Be(UserId);
    }

    [Fact]
    public void Acknowledge_TwiceIsIdempotent_PreservesFirstAck()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", null, 3, AttemptedAt);

        dl.Acknowledge(UserId, AckedAt);

        var laterUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var laterAt = AckedAt.AddHours(2);
        dl.Acknowledge(laterUserId, laterAt);

        dl.AcknowledgedAt.Should().Be(AckedAt);  // preserved
        dl.AcknowledgedBy.Should().Be(UserId);    // preserved
    }

    [Fact]
    public void Acknowledge_GuidEmpty_ThrowsArgumentException()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", null, 3, AttemptedAt);

        var act = () => dl.Acknowledge(Guid.Empty, AckedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(WikidataCoverEnrichmentOutcome.Success)]
    [InlineData(WikidataCoverEnrichmentOutcome.Skipped)]
    [InlineData(WikidataCoverEnrichmentOutcome.Failed)]
    public void Acknowledge_NonDeadLetterState_ThrowsInvalidOperationException(
        WikidataCoverEnrichmentOutcome outcome)
    {
        WikidataCoverEnrichmentAttempt attempt = outcome switch
        {
            WikidataCoverEnrichmentOutcome.Success =>
                WikidataCoverEnrichmentAttempt.RecordSuccess(GameId, 0, AttemptedAt),
            WikidataCoverEnrichmentOutcome.Skipped =>
                WikidataCoverEnrichmentAttempt.RecordSkipped(GameId, "qid-missing", 0, AttemptedAt),
            WikidataCoverEnrichmentOutcome.Failed =>
                WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
                    GameId, "r2-upload-error", null, 1, AttemptedAt, AttemptedAt.AddMinutes(5)),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        var act = () => attempt.Acknowledge(UserId, AckedAt);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{outcome}*");
    }
}
