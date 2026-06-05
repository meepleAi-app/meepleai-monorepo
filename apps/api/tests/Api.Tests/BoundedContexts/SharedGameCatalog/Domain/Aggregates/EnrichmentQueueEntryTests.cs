using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Domain unit tests for <see cref="EnrichmentQueueEntry"/> (#1874).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1874")]
public sealed class EnrichmentQueueEntryTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Enqueue_CreatesEntryWithExpectedState()
    {
        var entry = EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Normal, "manual enqueue", UserId);

        entry.Id.Should().NotBeEmpty();
        entry.SharedGameId.Should().Be(GameId);
        entry.Priority.Should().Be(EnrichmentPriority.Normal);
        entry.Reason.Should().Be("manual enqueue");
        entry.QueuedByUserId.Should().Be(UserId);
        entry.QueuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(2));
        entry.IsProcessed.Should().BeFalse();
        entry.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void Enqueue_AllowsNullQueuedBy_ForCronOrSystem()
    {
        var entry = EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Stale, "stale skeletons batch", queuedBy: null);

        entry.QueuedByUserId.Should().BeNull();
    }

    [Fact]
    public void Enqueue_ThrowsOnEmptySharedGameId()
    {
        var act = () => EnrichmentQueueEntry.Enqueue(
            Guid.Empty, EnrichmentPriority.Normal, "reason", UserId);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*SharedGameId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Enqueue_ThrowsOnEmptyOrWhitespaceReason(string reason)
    {
        var act = () => EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Normal, reason, UserId);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Reason*");
    }

    [Fact]
    public void Enqueue_ThrowsOnReasonOver200Chars()
    {
        var act = () => EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Normal, new string('x', 201), UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enqueue_ThrowsOnEmptyQueuedByGuid()
    {
        var act = () => EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Normal, "reason", queuedBy: Guid.Empty);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*QueuedByUserId*");
    }

    [Fact]
    public void MarkProcessed_FlipsFlagAndStampsTimestamp()
    {
        var entry = EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Normal, "manual enqueue", UserId);

        entry.MarkProcessed();

        entry.IsProcessed.Should().BeTrue();
        entry.ProcessedAt.Should().NotBeNull();
        entry.ProcessedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MarkProcessed_IsIdempotent()
    {
        var entry = EnrichmentQueueEntry.Enqueue(
            GameId, EnrichmentPriority.Normal, "reason", UserId);

        entry.MarkProcessed();
        var firstTimestamp = entry.ProcessedAt;

        entry.MarkProcessed();

        entry.ProcessedAt.Should().Be(firstTimestamp, "re-calling must be a no-op");
    }

    [Fact]
    public void Reconstitute_RestoresStateWithoutInvariantsRunning()
    {
        var queuedAt = DateTimeOffset.UtcNow.AddHours(-3);
        var processedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var entry = EnrichmentQueueEntry.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: GameId,
            priority: EnrichmentPriority.High,
            reason: "hist",
            queuedByUserId: UserId,
            queuedAt: queuedAt,
            isProcessed: true,
            processedAt: processedAt);

        entry.Priority.Should().Be(EnrichmentPriority.High);
        entry.IsProcessed.Should().BeTrue();
        entry.ProcessedAt.Should().Be(processedAt);
        entry.QueuedAt.Should().Be(queuedAt);
    }
}
