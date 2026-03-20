using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;
using FluentAssertions;

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
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
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
        result.Items.Count.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
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
        result.Items.Count.Should().Be(2);
        Assert.All(result.Items, item => item.ChannelType.Should().Be("slack_user"));
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
        result.Items.Count.Should().Be(2);
        Assert.All(result.Items, item => item.Status.Should().Be("sent"));
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
        result.Items.Count.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
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
