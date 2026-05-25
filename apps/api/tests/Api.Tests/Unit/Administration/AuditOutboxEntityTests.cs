using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Administration;

/// <summary>
/// Unit tests for AuditOutboxEntity domain logic — state machine guards (SP5 S1 T1 review).
/// Validates idempotent MarkSent, Failed→Sent retry path, and Sent→Failed data-integrity throw.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class AuditOutboxEntityTests
{
    private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 5, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddSeconds(5);
    private static readonly DateTimeOffset T2 = T0.AddSeconds(10);
    private const string SamplePayload = "{\"action\":\"UserCreated\"}";

    #region CreatePending Tests

    [Fact]
    public void CreatePending_ProducesPendingRow_WithZeroRetries()
    {
        // Act
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.PayloadJson.Should().Be(SamplePayload);
        entity.Status.Should().Be(OutboxStatus.Pending);
        entity.RetryCount.Should().Be(0);
        entity.LastError.Should().BeNull();
        entity.CreatedAt.Should().Be(T0);
        entity.ProcessedAt.Should().BeNull();
    }

    #endregion

    #region MarkSent Tests

    [Fact]
    public void MarkSent_FromPending_TransitionsToSent_AndSetsProcessedAt()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);

        // Act
        entity.MarkSent(T1);

        // Assert
        entity.Status.Should().Be(OutboxStatus.Sent);
        entity.ProcessedAt.Should().Be(T1);
    }

    [Fact]
    public void MarkSent_OnAlreadySentRow_IsIdempotent_AndDoesNotResetProcessedAt()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);
        entity.MarkSent(T1); // first call sets ProcessedAt = T1

        // Act — at-least-once delivery re-calls MarkSent with a later timestamp
        entity.MarkSent(T2);

        // Assert — status still Sent, ProcessedAt NOT updated to T2
        entity.Status.Should().Be(OutboxStatus.Sent);
        entity.ProcessedAt.Should().Be(T1, because: "idempotent guard must return early and preserve original ProcessedAt");
    }

    [Fact]
    public void MarkSent_FromFailed_AllowsTransition_AndPreservesLastErrorAndRetryCount()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);
        entity.MarkFailed("transient error", T1);
        var retryCountAfterFail = entity.RetryCount;
        var lastErrorAfterFail = entity.LastError;

        // Act — retry processor retries and succeeds
        entity.MarkSent(T2);

        // Assert — Failed→Sent allowed; forensic data preserved
        entity.Status.Should().Be(OutboxStatus.Sent);
        entity.ProcessedAt.Should().Be(T2);
        entity.RetryCount.Should().Be(retryCountAfterFail, because: "RetryCount must not change on successful retry");
        entity.LastError.Should().Be(lastErrorAfterFail, because: "LastError preserved so operator can see the row failed before succeeding");
    }

    #endregion

    #region MarkFailed Tests

    [Fact]
    public void MarkFailed_FromPending_TransitionsToFailed_IncrementsRetryCount_SetsLastError()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);

        // Act
        entity.MarkFailed("delivery timeout", T1);

        // Assert
        entity.Status.Should().Be(OutboxStatus.Failed);
        entity.RetryCount.Should().Be(1);
        entity.LastError.Should().Be("delivery timeout");
        entity.ProcessedAt.Should().Be(T1);
    }

    [Fact]
    public void MarkFailed_FromFailed_IncrementsRetryCount()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);
        entity.MarkFailed("first attempt", T1);

        // Act — re-failed
        entity.MarkFailed("second attempt", T2);

        // Assert
        entity.Status.Should().Be(OutboxStatus.Failed);
        entity.RetryCount.Should().Be(2);
        entity.LastError.Should().Be("second attempt");
    }

    [Fact]
    public void MarkFailed_OnSentRow_Throws_InvalidOperationException()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);
        entity.MarkSent(T1);

        // Act
        var act = () => entity.MarkFailed("race condition error", T2);

        // Assert — Sent→Failed is forbidden; would regress a successfully-processed audit row
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{entity.Id}*");
    }

    [Fact]
    public void MarkFailed_WithErrorMessageOver2048Chars_TruncatesAndAppendsEllipsis()
    {
        // Arrange
        var entity = AuditOutboxEntity.CreatePending(SamplePayload, T0);
        var longError = new string('x', 3000);

        // Act
        entity.MarkFailed(longError, T1);

        // Assert
        entity.LastError.Should().HaveLength(2048);
        entity.LastError!.Should().EndWith("...");
    }

    #endregion
}
