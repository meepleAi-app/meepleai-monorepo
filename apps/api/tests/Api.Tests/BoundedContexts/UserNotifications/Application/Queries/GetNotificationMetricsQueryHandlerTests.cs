using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
#pragma warning disable S3881
public class GetNotificationMetricsQueryHandlerTests : IDisposable
#pragma warning restore S3881
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetNotificationMetricsQueryHandler _handler;
    private bool _disposed;

    public GetNotificationMetricsQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetNotificationMetricsQueryHandler(_dbContext);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _dbContext.Dispose();
            _disposed = true;
        }
    }

    [Fact]
    public async Task Handle_EmptyQueue_ReturnsZeroCounts()
    {
        // Arrange
        var query = new GetNotificationMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(0, result.TotalPending);
        Assert.Equal(0, result.TotalSent);
        Assert.Equal(0, result.TotalFailed);
        Assert.Equal(0, result.TotalDeadLetter);
        Assert.Empty(result.PendingByChannel);
    }

    [Fact]
    public async Task Handle_WithMixedItems_ReturnsCorrectCounts()
    {
        // Arrange
        await SeedItem("email", "pending");
        await SeedItem("email", "pending");
        await SeedItem("slack_user", "pending");
        await SeedItem("email", "sent");
        await SeedItem("slack_user", "failed");
        await SeedItem("email", "dead_letter");

        var query = new GetNotificationMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalPending);
        Assert.Equal(1, result.TotalSent);
        Assert.Equal(1, result.TotalFailed);
        Assert.Equal(1, result.TotalDeadLetter);
        Assert.Equal(2, result.PendingByChannel["email"]);
        Assert.Equal(1, result.PendingByChannel["slack_user"]);
    }

    private async Task SeedItem(string channelType, string status)
    {
        await _dbContext.Set<NotificationQueueEntity>().AddAsync(new NotificationQueueEntity
        {
            Id = Guid.NewGuid(),
            ChannelType = channelType,
            NotificationType = "share_request_created",
            Payload = "{}",
            Status = status,
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
    }
}
