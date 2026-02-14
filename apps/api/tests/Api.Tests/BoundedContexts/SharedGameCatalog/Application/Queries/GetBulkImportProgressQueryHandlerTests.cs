using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for GetBulkImportProgressQueryHandler
/// Issue #4353: Backend - Bulk Import SSE Progress Endpoint
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetBulkImportProgressQueryHandlerTests
{
    private readonly Mock<IBggImportQueueService> _mockQueueService;
    private readonly GetBulkImportProgressQueryHandler _handler;

    public GetBulkImportProgressQueryHandlerTests()
    {
        _mockQueueService = new Mock<IBggImportQueueService>();
        _handler = new GetBulkImportProgressQueryHandler(_mockQueueService.Object);
    }

    [Fact]
    public async Task Handle_EmptyQueue_ShouldReturnZeroCounts()
    {
        // Arrange
        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggImportQueueEntity>());

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Queued);
        Assert.Equal(0, result.Processing);
        Assert.Equal(0, result.Completed);
        Assert.Equal(0, result.Failed);
        Assert.False(result.IsActive);
        Assert.Equal(0, result.EstimatedSecondsRemaining);
        Assert.Null(result.CurrentItem);
    }

    [Fact]
    public async Task Handle_AllQueued_ShouldReturnCorrectCounts()
    {
        // Arrange
        var items = new List<BggImportQueueEntity>
        {
            CreateQueueItem(1, "Catan", BggImportStatus.Queued),
            CreateQueueItem(2, "Wingspan", BggImportStatus.Queued),
            CreateQueueItem(3, "Azul", BggImportStatus.Queued)
        };

        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Queued);
        Assert.Equal(0, result.Processing);
        Assert.Equal(0, result.Completed);
        Assert.Equal(0, result.Failed);
        Assert.True(result.IsActive);
        Assert.Equal(3, result.EstimatedSecondsRemaining);
        Assert.Null(result.CurrentItem);
    }

    [Fact]
    public async Task Handle_WithProcessingItem_ShouldReturnCurrentItem()
    {
        // Arrange
        var items = new List<BggImportQueueEntity>
        {
            CreateQueueItem(1, "Catan", BggImportStatus.Processing),
            CreateQueueItem(2, "Wingspan", BggImportStatus.Queued),
            CreateQueueItem(3, "Azul", BggImportStatus.Queued)
        };

        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Queued);
        Assert.Equal(1, result.Processing);
        Assert.True(result.IsActive);
        Assert.Equal(3, result.EstimatedSecondsRemaining); // 2 queued + 1 processing
        Assert.NotNull(result.CurrentItem);
        Assert.Equal(1, result.CurrentItem.BggId);
        Assert.Equal("Catan", result.CurrentItem.GameName);
    }

    [Fact]
    public async Task Handle_MixedStatuses_ShouldAggregateCorrectly()
    {
        // Arrange
        var items = new List<BggImportQueueEntity>
        {
            CreateQueueItem(1, "Catan", BggImportStatus.Completed),
            CreateQueueItem(2, "Wingspan", BggImportStatus.Processing),
            CreateQueueItem(3, "Azul", BggImportStatus.Queued),
            CreateQueueItem(4, "Gloomhaven", BggImportStatus.Failed),
            CreateQueueItem(5, "Terraforming Mars", BggImportStatus.Completed)
        };

        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(5, result.Total);
        Assert.Equal(1, result.Queued);
        Assert.Equal(1, result.Processing);
        Assert.Equal(2, result.Completed);
        Assert.Equal(1, result.Failed);
        Assert.True(result.IsActive);
        Assert.Equal(2, result.EstimatedSecondsRemaining); // 1 queued + 1 processing
        Assert.NotNull(result.CurrentItem);
        Assert.Equal(2, result.CurrentItem.BggId);
        Assert.Equal("Wingspan", result.CurrentItem.GameName);
    }

    [Fact]
    public async Task Handle_AllCompleted_ShouldBeInactive()
    {
        // Arrange
        var items = new List<BggImportQueueEntity>
        {
            CreateQueueItem(1, "Catan", BggImportStatus.Completed),
            CreateQueueItem(2, "Wingspan", BggImportStatus.Completed),
            CreateQueueItem(3, "Azul", BggImportStatus.Failed)
        };

        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(0, result.Queued);
        Assert.Equal(0, result.Processing);
        Assert.Equal(2, result.Completed);
        Assert.Equal(1, result.Failed);
        Assert.False(result.IsActive);
        Assert.Equal(0, result.EstimatedSecondsRemaining);
        Assert.Null(result.CurrentItem);
    }

    [Fact]
    public async Task Handle_ProcessingItemWithRetries_ShouldIncludeRetryCount()
    {
        // Arrange
        var processingItem = CreateQueueItem(1, "Catan", BggImportStatus.Processing);
        processingItem.RetryCount = 2;

        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggImportQueueEntity> { processingItem });

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(result.CurrentItem);
        Assert.Equal(2, result.CurrentItem.RetryCount);
    }

    [Fact]
    public async Task Handle_ShouldSetTimestamp()
    {
        // Arrange
        _mockQueueService
            .Setup(s => s.GetAllQueueItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggImportQueueEntity>());

        var before = DateTime.UtcNow;

        // Act
        var result = await _handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(result.Timestamp, before, after);
    }

    private static BggImportQueueEntity CreateQueueItem(int bggId, string name, BggImportStatus status)
    {
        return new BggImportQueueEntity
        {
            Id = Guid.NewGuid(),
            BggId = bggId,
            GameName = name,
            Status = status,
            Position = bggId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
