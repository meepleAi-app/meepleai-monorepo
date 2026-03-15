using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
#pragma warning disable S3881
public class GetNotificationQueueQueryHandlerTests : IDisposable
#pragma warning restore S3881
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetNotificationQueueQueryHandler _handler;
    private bool _disposed;

    public GetNotificationQueueQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetNotificationQueueQueryHandler(_dbContext);
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
    public async Task Handle_EmptyQueue_ReturnsEmptyResult()
    {
        // Arrange
        var query = new GetNotificationQueueQuery(1, 20, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task Handle_WithItems_ReturnsPaginatedResult()
    {
        // Arrange
        await SeedItems(5);
        var query = new GetNotificationQueueQuery(1, 2, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task Handle_WithChannelFilter_FiltersCorrectly()
    {
        // Arrange
        await SeedItems(3, channelType: "email");
        await SeedItems(2, channelType: "slack_user");
        var query = new GetNotificationQueueQuery(1, 20, "slack_user", null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("slack_user", item.ChannelType));
    }

    [Fact]
    public async Task Handle_WithStatusFilter_FiltersCorrectly()
    {
        // Arrange
        await SeedItems(3, status: "pending");
        await SeedItems(2, status: "sent");
        var query = new GetNotificationQueueQuery(1, 20, null, "sent");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("sent", item.Status));
    }

    [Fact]
    public async Task Handle_Page2_ReturnsCorrectOffset()
    {
        // Arrange
        await SeedItems(5);
        var query = new GetNotificationQueueQuery(2, 2, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
    }

    private async Task SeedItems(int count, string channelType = "email", string status = "pending")
    {
        for (var i = 0; i < count; i++)
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
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                CorrelationId = Guid.NewGuid()
            });
        }

        await _dbContext.SaveChangesAsync();
    }
}
