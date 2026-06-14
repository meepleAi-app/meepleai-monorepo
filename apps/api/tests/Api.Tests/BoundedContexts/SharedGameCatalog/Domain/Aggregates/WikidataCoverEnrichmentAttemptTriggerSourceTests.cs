using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for <see cref="WikidataCoverEnrichmentAttempt"/> trigger-source
/// attribution — Issue #1823 Phase F F6. Verifies the optional
/// <c>TriggeredByAdminUserId</c> factory parameter (default <see langword="null"/>
/// for the M9 scheduler path; non-null for M12 single-trigger or F2 bulk-retry
/// admin invocations) and that <see cref="WikidataCoverEnrichmentAttempt.Reconstitute"/>
/// round-trips all Phase F state fields (<c>AcknowledgedAt</c>, <c>AcknowledgedBy</c>,
/// <c>TriggeredByAdminUserId</c>).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2255")]
public class WikidataCoverEnrichmentAttemptTriggerSourceTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime At = new(2026, 06, 13, 10, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void RecordDeadLetter_InitialState_AllPhaseFFieldsAreNull()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", null, 3, At);

        dl.AcknowledgedAt.Should().BeNull();
        dl.AcknowledgedBy.Should().BeNull();
        dl.TriggeredByAdminUserId.Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_DefaultSchedulerPath_TriggeredByAdminUserIdIsNull()
    {
        var a = WikidataCoverEnrichmentAttempt.RecordSuccess(GameId, 0, At);
        a.TriggeredByAdminUserId.Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_AdminTriggered_PersistsAdminId()
    {
        var a = WikidataCoverEnrichmentAttempt.RecordSuccess(
            GameId, 0, At, triggeredByAdminUserId: AdminId);
        a.TriggeredByAdminUserId.Should().Be(AdminId);
    }

    [Fact]
    public void Reconstitute_RoundTripsTriggeredByAdminUserId()
    {
        var hydrated = WikidataCoverEnrichmentAttempt.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: GameId,
            attemptedAt: At,
            outcome: WikidataCoverEnrichmentOutcome.Success,
            reason: "success",
            details: null,
            retryCount: 0,
            nextRetryAt: null,
            deadLetteredAt: null,
            acknowledgedAt: null,
            acknowledgedBy: null,
            triggeredByAdminUserId: AdminId);

        hydrated.TriggeredByAdminUserId.Should().Be(AdminId);
        hydrated.AcknowledgedAt.Should().BeNull();
        hydrated.AcknowledgedBy.Should().BeNull();
    }

    [Fact]
    public void RecordSkipped_AdminTriggered_PersistsAdminId()
    {
        var a = WikidataCoverEnrichmentAttempt.RecordSkipped(
            GameId, "qid-missing", 0, At, triggeredByAdminUserId: AdminId);
        a.TriggeredByAdminUserId.Should().Be(AdminId);
    }

    [Fact]
    public void RecordFailedWithRetry_AdminTriggered_PersistsAdminId()
    {
        var a = WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
            GameId, "r2-upload-error", null, 1, At, At.AddMinutes(5),
            triggeredByAdminUserId: AdminId);
        a.TriggeredByAdminUserId.Should().Be(AdminId);
    }
}
