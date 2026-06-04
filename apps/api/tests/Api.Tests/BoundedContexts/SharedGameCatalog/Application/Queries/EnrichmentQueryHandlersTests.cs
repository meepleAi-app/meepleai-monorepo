using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for #1874 enrichment query handlers (with mocked repositories).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1874")]
public sealed class EnrichmentQueryHandlersTests
{
    [Fact]
    public async Task GetEnrichmentQueueQueryHandler_MapsRepoOutputToDto()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entry = EnrichmentQueueEntry.Enqueue(gameId, EnrichmentPriority.High, "errata", userId);

        var repoMock = new Mock<IEnrichmentQueueRepository>();
        repoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<EnrichmentPriority?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<EnrichmentQueueEntryWithTitle>)new[]
            {
                new EnrichmentQueueEntryWithTitle(entry, "Twilight Imperium 4E"),
            }, 1));

        var handler = new GetEnrichmentQueueQueryHandler(repoMock.Object);

        var result = await handler.Handle(
            new GetEnrichmentQueueQuery(EnrichmentPriority.High, 25),
            CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].SharedGameId.Should().Be(gameId);
        result.Items[0].Title.Should().Be("Twilight Imperium 4E");
        result.Items[0].Priority.Should().Be(EnrichmentPriority.High);
        result.Items[0].Reason.Should().Be("errata");
        result.Items[0].QueuedBy.Should().Be(userId);

        repoMock.Verify(
            r => r.GetPendingAsync(EnrichmentPriority.High, 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEnrichmentQueueQueryHandler_EmptyResult_ReturnsEmptyList()
    {
        var repoMock = new Mock<IEnrichmentQueueRepository>();
        repoMock
            .Setup(r => r.GetPendingAsync(It.IsAny<EnrichmentPriority?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<EnrichmentQueueEntryWithTitle>(), 0));

        var handler = new GetEnrichmentQueueQueryHandler(repoMock.Object);

        var result = await handler.Handle(
            new GetEnrichmentQueueQuery(null, 25),
            CancellationToken.None);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFailedItemsQueryHandler_MapsRepoAggregatesToDto()
    {
        var gameId = Guid.NewGuid();
        var when = DateTimeOffset.UtcNow.AddDays(-2);

        var repoMock = new Mock<IEnrichmentAttemptRepository>();
        repoMock
            .Setup(r => r.GetFailedAggregatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<FailedItemAggregate>)new[]
            {
                new FailedItemAggregate(
                    gameId,
                    "Gloomhaven JOTL",
                    "SCHEMA_MISMATCH",
                    "Field 'designer' missing",
                    when,
                    1),
            }, 1));

        var handler = new GetFailedItemsQueryHandler(repoMock.Object);

        var result = await handler.Handle(
            new GetFailedItemsQuery(30, 50),
            CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("Gloomhaven JOTL");
        result.Items[0].ErrorCode.Should().Be("SCHEMA_MISMATCH");
        result.Items[0].RetryCount.Should().Be(1);
        result.Items[0].LastAttemptAt.Should().Be(when);
    }

    [Fact]
    public void GetEnrichmentQueueQueryValidator_RejectsLimitOutOfRange()
    {
        var validator = new GetEnrichmentQueueQueryValidator();

        validator.Validate(new GetEnrichmentQueueQuery(null, 0)).IsValid.Should().BeFalse();
        validator.Validate(new GetEnrichmentQueueQuery(null, 101)).IsValid.Should().BeFalse();
        validator.Validate(new GetEnrichmentQueueQuery(null, 25)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetFailedItemsQueryValidator_RejectsBoundsOutOfRange()
    {
        var validator = new GetFailedItemsQueryValidator();

        validator.Validate(new GetFailedItemsQuery(0, 50)).IsValid.Should().BeFalse();
        validator.Validate(new GetFailedItemsQuery(366, 50)).IsValid.Should().BeFalse();
        validator.Validate(new GetFailedItemsQuery(30, 0)).IsValid.Should().BeFalse();
        validator.Validate(new GetFailedItemsQuery(30, 101)).IsValid.Should().BeFalse();
        validator.Validate(new GetFailedItemsQuery(30, 50)).IsValid.Should().BeTrue();
    }
}
