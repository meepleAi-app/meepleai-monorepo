using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public sealed class GetEmailQueueStatsQueryHandlerTests
{
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly GetEmailQueueStatsQueryHandler _sut;

    public GetEmailQueueStatsQueryHandlerTests()
    {
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _sut = new GetEmailQueueStatsQueryHandler(_emailQueueRepo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectCountsPerStatus()
    {
        // Arrange
        var statusCounts = new Dictionary<string, int>
        {
            { "pending", 5 },
            { "processing", 2 },
            { "sent", 100 },
            { "failed", 3 },
            { "dead_letter", 1 }
        };
        _emailQueueRepo.Setup(r => r.GetCountsByStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusCounts);
        _emailQueueRepo.Setup(r => r.GetSentCountSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        // Act
        var result = await _sut.Handle(new GetEmailQueueStatsQuery(), CancellationToken.None);

        // Assert
        result.PendingCount.Should().Be(5);
        result.ProcessingCount.Should().Be(2);
        result.SentCount.Should().Be(100);
        result.FailedCount.Should().Be(3);
        result.DeadLetterCount.Should().Be(1);
        result.SentLastHour.Should().Be(10);
        result.SentLast24Hours.Should().Be(10);
    }

    [Fact]
    public async Task Handle_MissingStatusReturnsZero()
    {
        // Arrange: only "sent" status exists
        var statusCounts = new Dictionary<string, int>
        {
            { "sent", 50 }
        };
        _emailQueueRepo.Setup(r => r.GetCountsByStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusCounts);
        _emailQueueRepo.Setup(r => r.GetSentCountSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _sut.Handle(new GetEmailQueueStatsQuery(), CancellationToken.None);

        // Assert
        result.PendingCount.Should().Be(0);
        result.ProcessingCount.Should().Be(0);
        result.SentCount.Should().Be(50);
        result.FailedCount.Should().Be(0);
        result.DeadLetterCount.Should().Be(0);
    }
}
