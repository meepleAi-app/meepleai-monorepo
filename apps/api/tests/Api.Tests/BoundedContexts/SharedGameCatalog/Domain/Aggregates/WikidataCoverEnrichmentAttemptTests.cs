using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentAttempt"/> — Issue #1823 Wave 3 M9.
/// Verifies aggregate invariants + the four factory variants
/// (Success / Skipped / FailedWithRetry / DeadLetter).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikidataCoverEnrichmentAttemptTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RecordSuccess_SetsOutcomeSuccessAndClearsRetryFields()
    {
        var attempt = WikidataCoverEnrichmentAttempt.RecordSuccess(
            sharedGameId: Guid.NewGuid(),
            retryCount: 2,
            attemptedAt: FixedNow);

        attempt.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Success);
        attempt.Reason.Should().Be("success");
        attempt.Details.Should().BeNull();
        attempt.RetryCount.Should().Be(2);
        attempt.NextRetryAt.Should().BeNull("success is terminal");
        attempt.DeadLetteredAt.Should().BeNull();
        attempt.AttemptedAt.Should().Be(FixedNow);
    }

    [Fact]
    public void RecordSkipped_TerminalNoRetry()
    {
        var attempt = WikidataCoverEnrichmentAttempt.RecordSkipped(
            sharedGameId: Guid.NewGuid(),
            reason: "qid-missing",
            retryCount: 0,
            attemptedAt: FixedNow);

        attempt.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Skipped);
        attempt.Reason.Should().Be("qid-missing");
        attempt.NextRetryAt.Should().BeNull("skipped is terminal");
        attempt.DeadLetteredAt.Should().BeNull("skipped is NOT dead-lettered");
    }

    [Fact]
    public void RecordFailedWithRetry_SetsNextRetryAt()
    {
        var nextRetry = FixedNow.AddMinutes(5);

        var attempt = WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            sharedGameId: Guid.NewGuid(),
            reason: "r2-upload-error",
            details: "503 service unavailable",
            retryCount: 1,
            attemptedAt: FixedNow,
            nextRetryAt: nextRetry);

        attempt.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.Failed);
        attempt.Reason.Should().Be("r2-upload-error");
        attempt.Details.Should().Be("503 service unavailable");
        attempt.RetryCount.Should().Be(1);
        attempt.NextRetryAt.Should().Be(nextRetry,
            "failed-with-retry MUST schedule the next attempt window so the scheduler can pick it up");
        attempt.DeadLetteredAt.Should().BeNull();
    }

    [Fact]
    public void RecordDeadLetter_StampsDeadLetteredAtAndClearsNextRetry()
    {
        var attempt = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            sharedGameId: Guid.NewGuid(),
            reason: "r2-upload-error",
            details: "exhausted retries",
            retryCount: 3,
            attemptedAt: FixedNow);

        attempt.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.DeadLetter);
        attempt.NextRetryAt.Should().BeNull("dead-letter is terminal");
        attempt.DeadLetteredAt.Should().Be(FixedNow,
            "dead-letter timestamp is needed for the 7-day retention sweep + M13 admin page");
        attempt.RetryCount.Should().Be(3);
    }

    [Fact]
    public void Factory_RejectsEmptySharedGameId()
    {
        var act = () => WikidataCoverEnrichmentAttempt.RecordSuccess(
            sharedGameId: Guid.Empty,
            retryCount: 0,
            attemptedAt: FixedNow);

        act.Should().Throw<ArgumentException>().WithMessage("*SharedGameId*");
    }

    [Fact]
    public void Factory_RejectsEmptyReason()
    {
        var act = () => WikidataCoverEnrichmentAttempt.RecordSkipped(
            sharedGameId: Guid.NewGuid(),
            reason: "  ",
            retryCount: 0,
            attemptedAt: FixedNow);

        act.Should().Throw<ArgumentException>().WithMessage("*Reason*");
    }

    [Fact]
    public void Factory_RejectsTooLongReason()
    {
        var act = () => WikidataCoverEnrichmentAttempt.RecordSkipped(
            sharedGameId: Guid.NewGuid(),
            reason: new string('x', 65),
            retryCount: 0,
            attemptedAt: FixedNow);

        act.Should().Throw<ArgumentException>().WithMessage("*Reason*");
    }

    [Fact]
    public void Factory_RejectsTooLongDetails()
    {
        var act = () => WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            sharedGameId: Guid.NewGuid(),
            reason: "r2-upload-error",
            details: new string('x', 1025),
            retryCount: 0,
            attemptedAt: FixedNow,
            nextRetryAt: FixedNow.AddMinutes(1));

        act.Should().Throw<ArgumentException>().WithMessage("*Details*");
    }

    [Fact]
    public void Factory_RejectsNegativeRetryCount()
    {
        var act = () => WikidataCoverEnrichmentAttempt.RecordSuccess(
            sharedGameId: Guid.NewGuid(),
            retryCount: -1,
            attemptedAt: FixedNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Reconstitute_BypassesInvariants()
    {
        var id = Guid.NewGuid();
        var sgid = Guid.NewGuid();

        var attempt = WikidataCoverEnrichmentAttempt.Reconstitute(
            id: id,
            sharedGameId: sgid,
            attemptedAt: FixedNow,
            outcome: WikidataCoverEnrichmentOutcome.DeadLetter,
            reason: "license-not-whitelisted",
            details: null,
            retryCount: 0,
            nextRetryAt: null,
            deadLetteredAt: FixedNow);

        attempt.Id.Should().Be(id);
        attempt.SharedGameId.Should().Be(sgid);
        attempt.Outcome.Should().Be(WikidataCoverEnrichmentOutcome.DeadLetter);
    }
}
