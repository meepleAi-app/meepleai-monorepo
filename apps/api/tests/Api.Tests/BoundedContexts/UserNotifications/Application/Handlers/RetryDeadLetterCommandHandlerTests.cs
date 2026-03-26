using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.Commands;
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
public class RetryDeadLetterCommandHandlerTests : IDisposable
#pragma warning restore S3881
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly RetryDeadLetterCommandHandler _handler;
    private bool _disposed;

    public RetryDeadLetterCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new RetryDeadLetterCommandHandler(_dbContext);
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
    public async Task Handle_DeadLetterItem_ResetsStatusToPending()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var entity = new NotificationQueueEntity
        {
            Id = itemId,
            ChannelType = "slack_user",
            NotificationType = "share_request_created",
            Payload = "{}",
            Status = "dead_letter",
            RetryCount = 3,
            MaxRetries = 3,
            LastError = "Rate limited",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CorrelationId = Guid.NewGuid()
        };
        await _dbContext.Set<NotificationQueueEntity>().AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        var command = new RetryDeadLetterCommand(itemId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updated = await _dbContext.Set<NotificationQueueEntity>().FindAsync(itemId);
        updated.Should().NotBeNull();
        updated.Status.Should().Be("pending");
        updated.LastError.Should().BeNull();
        updated.RetryCount.Should().Be(0);
        updated.NextRetryAt.Should().BeNull();
        updated.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonExistentItem_ReturnsFalse()
    {
        // Arrange
        var command = new RetryDeadLetterCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonDeadLetterItem_ReturnsFalse()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var entity = new NotificationQueueEntity
        {
            Id = itemId,
            ChannelType = "email",
            NotificationType = "share_request_created",
            Payload = "{}",
            Status = "pending", // Not dead_letter
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid()
        };
        await _dbContext.Set<NotificationQueueEntity>().AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        var command = new RetryDeadLetterCommand(itemId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}
