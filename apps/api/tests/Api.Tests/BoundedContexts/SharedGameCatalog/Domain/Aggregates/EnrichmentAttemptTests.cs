using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Domain unit tests for <see cref="EnrichmentAttempt"/> (#1874).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1874")]
public sealed class EnrichmentAttemptTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RunId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void RecordSuccess_BuildsCleanAttempt()
    {
        var attempt = EnrichmentAttempt.RecordSuccess(GameId, RunId, retryCount: 0);

        attempt.Id.Should().NotBeEmpty();
        attempt.SharedGameId.Should().Be(GameId);
        attempt.CatalogSyncRunId.Should().Be(RunId);
        attempt.Success.Should().BeTrue();
        attempt.ErrorCode.Should().BeNull();
        attempt.ErrorDetail.Should().BeNull();
        attempt.RetryCount.Should().Be(0);
        attempt.AttemptedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordSuccess_AllowsNullRunId_ForManualOneOff()
    {
        var attempt = EnrichmentAttempt.RecordSuccess(GameId, catalogSyncRunId: null, retryCount: 2);

        attempt.CatalogSyncRunId.Should().BeNull();
        attempt.RetryCount.Should().Be(2);
    }

    [Fact]
    public void RecordFailure_BuildsAttemptWithErrorDetails()
    {
        var attempt = EnrichmentAttempt.RecordFailure(
            GameId, RunId, "BGG_API_RATE_LIMIT_429", "4 retry esauriti", retryCount: 3);

        attempt.Success.Should().BeFalse();
        attempt.ErrorCode.Should().Be("BGG_API_RATE_LIMIT_429");
        attempt.ErrorDetail.Should().Be("4 retry esauriti");
        attempt.RetryCount.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordFailure_RejectsBlankErrorCode(string errorCode)
    {
        var act = () => EnrichmentAttempt.RecordFailure(
            GameId, RunId, errorCode, "detail", retryCount: 0);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Error code*");
    }

    [Fact]
    public void RecordFailure_RejectsErrorCodeOver100Chars()
    {
        var act = () => EnrichmentAttempt.RecordFailure(
            GameId, RunId, new string('x', 101), "detail", retryCount: 0);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordFailure_RejectsBlankErrorDetail(string detail)
    {
        var act = () => EnrichmentAttempt.RecordFailure(
            GameId, RunId, "ERR", detail, retryCount: 0);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Error detail*");
    }

    [Fact]
    public void RecordSuccess_ThrowsOnEmptySharedGameId()
    {
        var act = () => EnrichmentAttempt.RecordSuccess(Guid.Empty, RunId, retryCount: 0);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*SharedGameId*");
    }

    [Fact]
    public void RecordSuccess_ThrowsOnEmptyRunGuid()
    {
        var act = () => EnrichmentAttempt.RecordSuccess(GameId, catalogSyncRunId: Guid.Empty, retryCount: 0);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*CatalogSyncRunId*");
    }

    [Fact]
    public void RecordSuccess_ThrowsOnNegativeRetryCount()
    {
        var act = () => EnrichmentAttempt.RecordSuccess(GameId, RunId, retryCount: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Reconstitute_RestoresFailureSnapshot()
    {
        var when = DateTimeOffset.UtcNow.AddDays(-1);
        var attempt = EnrichmentAttempt.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: GameId,
            catalogSyncRunId: RunId,
            attemptedAt: when,
            success: false,
            errorCode: "SCHEMA_MISMATCH",
            errorDetail: "Field 'designer' missing",
            retryCount: 1);

        attempt.Success.Should().BeFalse();
        attempt.AttemptedAt.Should().Be(when);
        attempt.ErrorCode.Should().Be("SCHEMA_MISMATCH");
    }
}
