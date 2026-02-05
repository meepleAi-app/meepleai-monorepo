using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for BggImportQueueService with PostgreSQL persistence.
/// Issue #3541: BGG Import Queue Service
/// Tests: Enqueue single/batch, duplicate detection, cancel, retry, position recalculation, cleanup
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggImportQueueServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private BggImportQueueService _service = null!;
    private Mock<TimeProvider> _mockTimeProvider = null!;

    public BggImportQueueServiceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"bggqueue_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Build DbContext with test database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .Options;

        // Mock MediatR and DomainEventCollector
        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        // Mock TimeProvider for deterministic time testing
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(tp => tp.GetUtcNow())
            .Returns(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));

        // Initialize service
        var mockLogger = new Mock<ILogger<BggImportQueueService>>();
        _service = new BggImportQueueService(_dbContext, mockLogger.Object, _mockTimeProvider.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region EnqueueAsync Tests (Single)

    [Fact]
    public async Task EnqueueAsync_WithValidBggId_CreatesQueueEntry()
    {
        // Arrange
        const int bggId = 174430; // Gloomhaven

        // Act
        var result = await _service.EnqueueAsync(bggId, "Gloomhaven");

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(bggId, result.BggId);
        Assert.Equal("Gloomhaven", result.GameName);
        Assert.Equal(BggImportStatus.Queued, result.Status);
        Assert.Equal(1, result.Position);
        Assert.Equal(0, result.RetryCount);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ProcessedAt);

        // Verify database persistence
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.BggId == bggId);
        Assert.NotNull(dbEntry);
        Assert.Equal("Gloomhaven", dbEntry.GameName);
    }

    [Fact]
    public async Task EnqueueAsync_WithDuplicateBggId_ThrowsConflictException()
    {
        // Arrange
        const int bggId = 266192; // Wingspan
        await _service.EnqueueAsync(bggId, "Wingspan");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            async () => await _service.EnqueueAsync(bggId, "Wingspan Duplicate"));

        Assert.Contains("already queued or imported", exception.Message);
        Assert.Contains(bggId.ToString(), exception.Message);
    }

    [Fact]
    public async Task EnqueueAsync_AllowsRequeueAfterFailure()
    {
        // Arrange
        const int bggId = 68448; // 7 Wonders
        var entity = await _service.EnqueueAsync(bggId);
        await _service.MarkAsProcessingAsync(entity.Id);
        await _service.MarkAsFailedAsync(entity.Id, "Test failure", maxRetries: 1);

        // Act - Should allow re-enqueue after permanent failure
        var result = await _service.EnqueueAsync(bggId, "7 Wonders Retry");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bggId, result.BggId);
        Assert.Equal(BggImportStatus.Queued, result.Status);
        Assert.Equal(0, result.RetryCount); // Reset retry count
    }

    [Fact]
    public async Task EnqueueAsync_WithMultipleEntries_AssignsSequentialPositions()
    {
        // Arrange & Act
        var entity1 = await _service.EnqueueAsync(1, "Game 1");
        var entity2 = await _service.EnqueueAsync(2, "Game 2");
        var entity3 = await _service.EnqueueAsync(3, "Game 3");

        // Assert
        Assert.Equal(1, entity1.Position);
        Assert.Equal(2, entity2.Position);
        Assert.Equal(3, entity3.Position);
    }

    #endregion

    #region EnqueueBatchAsync Tests

    [Fact]
    public async Task EnqueueBatchAsync_WithMultipleBggIds_CreatesAllEntries()
    {
        // Arrange
        var bggIds = new List<int> { 174430, 266192, 68448 }; // Gloomhaven, Wingspan, 7 Wonders

        // Act
        var results = await _service.EnqueueBatchAsync(bggIds);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(BggImportStatus.Queued, r.Status));

        // Verify sequential positions
        Assert.Equal(1, results[0].Position);
        Assert.Equal(2, results[1].Position);
        Assert.Equal(3, results[2].Position);

        // Verify database persistence
        var dbCount = await _dbContext.BggImportQueue.CountAsync();
        Assert.Equal(3, dbCount);
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithDuplicateBggIds_FiltersDuplicates()
    {
        // Arrange
        var bggIds = new List<int> { 1, 1, 2, 2, 3 };

        // Act
        var results = await _service.EnqueueBatchAsync(bggIds);

        // Assert - Only unique BGG IDs should be enqueued
        Assert.Equal(3, results.Count);
        Assert.Equal(new[] { 1, 2, 3 }, results.Select(r => r.BggId).OrderBy(id => id));
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithSomeExistingBggIds_EnqueuesOnlyNew()
    {
        // Arrange
        await _service.EnqueueAsync(174430, "Gloomhaven"); // Pre-existing

        var bggIds = new List<int> { 174430, 266192, 68448 }; // 1 existing, 2 new

        // Act
        var results = await _service.EnqueueBatchAsync(bggIds);

        // Assert - Only 2 new entries should be created
        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, r => r.BggId == 174430);
        Assert.Contains(results, r => r.BggId == 266192);
        Assert.Contains(results, r => r.BggId == 68448);

        // Verify positions start after existing entry
        Assert.All(results, r => Assert.True(r.Position > 1));
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithEmptyList_ReturnsEmptyResult()
    {
        // Act
        var results = await _service.EnqueueBatchAsync(new List<int>());

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithNullList_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _service.EnqueueBatchAsync(null!));
    }

    [Fact]
    public async Task EnqueueBatchAsync_WithAllExisting_ReturnsEmpty()
    {
        // Arrange
        await _service.EnqueueAsync(1, "Game 1");
        await _service.EnqueueAsync(2, "Game 2");

        // Act
        var results = await _service.EnqueueBatchAsync(new List<int> { 1, 2 });

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region CancelAsync Tests

    [Fact]
    public async Task CancelAsync_WithQueuedEntry_RemovesAndRecalculatesPositions()
    {
        // Arrange
        var entity1 = await _service.EnqueueAsync(1, "Game 1");
        var entity2 = await _service.EnqueueAsync(2, "Game 2");
        var entity3 = await _service.EnqueueAsync(3, "Game 3");

        // Act - Cancel middle entry
        var cancelled = await _service.CancelAsync(entity2.Id);

        // Assert
        Assert.True(cancelled);

        // Verify removal from database
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity2.Id);
        Assert.Null(dbEntry);

        // Verify position recalculation (entity3 should move from position 3 to 2)
        var entity3Updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity3.Id);
        Assert.NotNull(entity3Updated);
        Assert.Equal(2, entity3Updated.Position);
    }

    [Fact]
    public async Task CancelAsync_WithNonQueuedStatus_ReturnsFalse()
    {
        // Arrange
        var entity = await _service.EnqueueAsync(123, "Test Game");
        await _service.MarkAsProcessingAsync(entity.Id);

        // Act
        var cancelled = await _service.CancelAsync(entity.Id);

        // Assert
        Assert.False(cancelled);

        // Verify entity still exists
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(dbEntry);
        Assert.Equal(BggImportStatus.Processing, dbEntry.Status);
    }

    [Fact]
    public async Task CancelAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var cancelled = await _service.CancelAsync(Guid.NewGuid());

        // Assert
        Assert.False(cancelled);
    }

    #endregion

    #region RetryFailedAsync Tests

    [Fact]
    public async Task RetryFailedAsync_WithFailedEntry_RequeuesWithResetRetryCount()
    {
        // Arrange
        var entity = await _service.EnqueueAsync(456, "Test Game");
        await _service.MarkAsProcessingAsync(entity.Id);
        await _service.MarkAsFailedAsync(entity.Id, "Test error", maxRetries: 1);

        // Act
        var retried = await _service.RetryFailedAsync(entity.Id);

        // Assert
        Assert.True(retried);

        // Verify database state
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(dbEntry);
        Assert.Equal(BggImportStatus.Queued, dbEntry.Status);
        Assert.Equal(0, dbEntry.RetryCount);
        Assert.Null(dbEntry.ErrorMessage);
        Assert.Null(dbEntry.ProcessedAt);
        Assert.Equal(1, dbEntry.Position); // Should be assigned next position
    }

    [Fact]
    public async Task RetryFailedAsync_WithNonFailedStatus_ReturnsFalse()
    {
        // Arrange
        var entity = await _service.EnqueueAsync(789, "Test Game");

        // Act
        var retried = await _service.RetryFailedAsync(entity.Id);

        // Assert
        Assert.False(retried);

        // Verify no state change
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(dbEntry);
        Assert.Equal(BggImportStatus.Queued, dbEntry.Status);
    }

    [Fact]
    public async Task RetryFailedAsync_WithNonExistentId_ReturnsFalse()
    {
        // Act
        var retried = await _service.RetryFailedAsync(Guid.NewGuid());

        // Assert
        Assert.False(retried);
    }

    [Fact]
    public async Task RetryFailedAsync_AssignsCorrectPosition()
    {
        // Arrange
        var entity1 = await _service.EnqueueAsync(1, "Game 1");
        var entity2 = await _service.EnqueueAsync(2, "Game 2");
        await _service.MarkAsProcessingAsync(entity2.Id);
        await _service.MarkAsFailedAsync(entity2.Id, "Error", maxRetries: 1);

        // Act
        await _service.RetryFailedAsync(entity2.Id);

        // Assert - Should be positioned after entity1
        var entity2Updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity2.Id);
        Assert.NotNull(entity2Updated);
        Assert.Equal(2, entity2Updated.Position);
    }

    #endregion

    #region CleanupOldJobsAsync Tests

    [Fact]
    public async Task CleanupOldJobsAsync_RemovesOldCompletedJobs()
    {
        // Arrange
        var oldEntity = await _service.EnqueueAsync(1, "Old Game");
        await _service.MarkAsProcessingAsync(oldEntity.Id);
        await _service.MarkAsCompletedAsync(oldEntity.Id, Guid.NewGuid());

        // Update ProcessedAt to 40 days ago using TimeProvider mock
        var oldDate = _mockTimeProvider.Object.GetUtcNow().UtcDateTime.AddDays(-40);
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.NotNull(dbEntry);
        dbEntry.ProcessedAt = oldDate;
        await _dbContext.SaveChangesAsync();

        // Act
        var deletedCount = await _service.CleanupOldJobsAsync(retentionDays: 30);

        // Assert
        Assert.Equal(1, deletedCount);

        // Verify removal
        var removed = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.Null(removed);
    }

    [Fact]
    public async Task CleanupOldJobsAsync_RemovesOldFailedJobs()
    {
        // Arrange
        var oldEntity = await _service.EnqueueAsync(2, "Old Failed Game");
        await _service.MarkAsProcessingAsync(oldEntity.Id);
        await _service.MarkAsFailedAsync(oldEntity.Id, "Error", maxRetries: 1);

        // Update ProcessedAt to 60 days ago using TimeProvider mock
        var oldDate = _mockTimeProvider.Object.GetUtcNow().UtcDateTime.AddDays(-60);
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.NotNull(dbEntry);
        dbEntry.ProcessedAt = oldDate;
        await _dbContext.SaveChangesAsync();

        // Act
        var deletedCount = await _service.CleanupOldJobsAsync(retentionDays: 30);

        // Assert
        Assert.Equal(1, deletedCount);
    }

    [Fact]
    public async Task CleanupOldJobsAsync_KeepsRecentJobs()
    {
        // Arrange
        var recentEntity = await _service.EnqueueAsync(3, "Recent Game");
        await _service.MarkAsProcessingAsync(recentEntity.Id);
        await _service.MarkAsCompletedAsync(recentEntity.Id, Guid.NewGuid());

        // ProcessedAt is set to current time by MarkAsCompletedAsync

        // Act
        var deletedCount = await _service.CleanupOldJobsAsync(retentionDays: 30);

        // Assert
        Assert.Equal(0, deletedCount);

        // Verify still exists
        var exists = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == recentEntity.Id);
        Assert.NotNull(exists);
    }

    [Fact]
    public async Task CleanupOldJobsAsync_DoesNotRemoveQueuedOrProcessing()
    {
        // Arrange
        var queuedEntity = await _service.EnqueueAsync(4, "Queued Game");
        var processingEntity = await _service.EnqueueAsync(5, "Processing Game");
        await _service.MarkAsProcessingAsync(processingEntity.Id);

        // Act
        var deletedCount = await _service.CleanupOldJobsAsync(retentionDays: 0);

        // Assert
        Assert.Equal(0, deletedCount);

        // Verify both still exist
        Assert.NotNull(await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == queuedEntity.Id));
        Assert.NotNull(await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == processingEntity.Id));
    }

    [Fact]
    public async Task CleanupOldJobsAsync_WithZeroRetention_ReturnsZero()
    {
        // Act
        var deletedCount = await _service.CleanupOldJobsAsync(retentionDays: 0);

        // Assert
        Assert.Equal(0, deletedCount);
    }

    [Fact]
    public async Task CleanupOldJobsAsync_WithNegativeRetention_ReturnsZero()
    {
        // Act
        var deletedCount = await _service.CleanupOldJobsAsync(retentionDays: -5);

        // Assert
        Assert.Equal(0, deletedCount);
    }

    #endregion

    #region RecalculatePositionsAsync Tests

    [Fact]
    public async Task RecalculatePositionsAsync_WithGaps_ReassignsSequentially()
    {
        // Arrange
        var entity1 = await _service.EnqueueAsync(1, "Game 1");
        var entity2 = await _service.EnqueueAsync(2, "Game 2");
        var entity3 = await _service.EnqueueAsync(3, "Game 3");

        // Manually create gaps by updating positions
        entity1.Position = 1;
        entity2.Position = 5; // Gap
        entity3.Position = 10; // Larger gap
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.RecalculatePositionsAsync();

        // Assert - Positions should be sequential 1, 2, 3
        var allEntries = await _dbContext.BggImportQueue
            .Where(q => q.Status == BggImportStatus.Queued)
            .OrderBy(q => q.Position)
            .ToListAsync();

        Assert.Equal(3, allEntries.Count);
        Assert.Equal(1, allEntries[0].Position);
        Assert.Equal(2, allEntries[1].Position);
        Assert.Equal(3, allEntries[2].Position);
    }

    [Fact]
    public async Task RecalculatePositionsAsync_IgnoresNonQueuedEntries()
    {
        // Arrange
        var queued = await _service.EnqueueAsync(1, "Queued");
        var processing = await _service.EnqueueAsync(2, "Processing");
        await _service.MarkAsProcessingAsync(processing.Id);

        // Act
        await _service.RecalculatePositionsAsync();

        // Assert - Processing entry position should not change
        var processingUpdated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == processing.Id);
        Assert.NotNull(processingUpdated);
        Assert.Equal(2, processingUpdated.Position); // Original position preserved
    }

    [Fact]
    public async Task RecalculatePositionsAsync_WithEmptyQueue_DoesNothing()
    {
        // Act
        await _service.RecalculatePositionsAsync();

        // Assert - Should not throw
        var count = await _dbContext.BggImportQueue.CountAsync();
        Assert.Equal(0, count);
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public async Task MarkAsProcessingAsync_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var entity = await _service.EnqueueAsync(100, "Test Game");

        // Act
        await _service.MarkAsProcessingAsync(entity.Id);

        // Assert
        var updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(updated);
        Assert.Equal(BggImportStatus.Processing, updated.Status);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task MarkAsCompletedAsync_UpdatesStatusAndRecalculatesPositions()
    {
        // Arrange
        var entity1 = await _service.EnqueueAsync(1, "Game 1");
        var entity2 = await _service.EnqueueAsync(2, "Game 2");
        await _service.MarkAsProcessingAsync(entity1.Id);

        var createdGameId = Guid.NewGuid();

        // Act
        await _service.MarkAsCompletedAsync(entity1.Id, createdGameId);

        // Assert
        var completed = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity1.Id);
        Assert.NotNull(completed);
        Assert.Equal(BggImportStatus.Completed, completed.Status);
        Assert.Equal(createdGameId, completed.CreatedGameId);
        Assert.NotNull(completed.ProcessedAt);
        Assert.Null(completed.ErrorMessage);

        // Verify position recalculation (entity2 should now be position 1)
        var entity2Updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity2.Id);
        Assert.NotNull(entity2Updated);
        Assert.Equal(1, entity2Updated.Position);
    }

    [Fact]
    public async Task MarkAsFailedAsync_WithRetriesRemaining_RequeuesWithIncrementedRetryCount()
    {
        // Arrange
        var entity = await _service.EnqueueAsync(200, "Test Game");
        await _service.MarkAsProcessingAsync(entity.Id);

        // Act
        await _service.MarkAsFailedAsync(entity.Id, "API timeout", maxRetries: 3);

        // Assert
        var updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(updated);
        Assert.Equal(BggImportStatus.Queued, updated.Status); // Back to queued
        Assert.Equal(1, updated.RetryCount);
        Assert.Equal("API timeout", updated.ErrorMessage);
        Assert.Null(updated.ProcessedAt); // Not permanently failed yet
    }

    [Fact]
    public async Task MarkAsFailedAsync_WithMaxRetriesReached_MarksAsPermanentlyFailed()
    {
        // Arrange
        var entity = await _service.EnqueueAsync(300, "Test Game");
        await _service.MarkAsProcessingAsync(entity.Id);

        // Simulate 2 previous failures
        entity.RetryCount = 2;
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.MarkAsFailedAsync(entity.Id, "Final failure", maxRetries: 3);

        // Assert
        var updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity.Id);
        Assert.NotNull(updated);
        Assert.Equal(BggImportStatus.Failed, updated.Status); // Permanently failed
        Assert.Equal(3, updated.RetryCount);
        Assert.Equal("Final failure", updated.ErrorMessage);
        Assert.NotNull(updated.ProcessedAt);
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetQueueStatusAsync_ReturnsOnlyQueuedAndProcessing()
    {
        // Arrange
        var queued = await _service.EnqueueAsync(1, "Queued");
        var processing = await _service.EnqueueAsync(2, "Processing");
        var completed = await _service.EnqueueAsync(3, "Completed");
        var failed = await _service.EnqueueAsync(4, "Failed");

        await _service.MarkAsProcessingAsync(processing.Id);
        await _service.MarkAsCompletedAsync(completed.Id, Guid.NewGuid());
        await _service.MarkAsProcessingAsync(failed.Id);
        await _service.MarkAsFailedAsync(failed.Id, "Error", maxRetries: 1);

        // Act
        var status = await _service.GetQueueStatusAsync();

        // Assert
        Assert.Equal(2, status.Count);
        Assert.Contains(status, s => s.Id == queued.Id);
        Assert.Contains(status, s => s.Id == processing.Id);
        Assert.DoesNotContain(status, s => s.Id == completed.Id);
        Assert.DoesNotContain(status, s => s.Id == failed.Id);
    }

    [Fact]
    public async Task GetAllQueueItemsAsync_ReturnsAllStatuses()
    {
        // Arrange
        await _service.EnqueueAsync(1, "Queued");
        var processing = await _service.EnqueueAsync(2, "Processing");
        var completed = await _service.EnqueueAsync(3, "Completed");
        var failed = await _service.EnqueueAsync(4, "Failed");

        await _service.MarkAsProcessingAsync(processing.Id);
        await _service.MarkAsCompletedAsync(completed.Id, Guid.NewGuid());
        await _service.MarkAsProcessingAsync(failed.Id);
        await _service.MarkAsFailedAsync(failed.Id, "Error", maxRetries: 1);

        // Act
        var allItems = await _service.GetAllQueueItemsAsync();

        // Assert
        Assert.Equal(4, allItems.Count);
        Assert.Contains(allItems, i => i.Status == BggImportStatus.Queued);
        Assert.Contains(allItems, i => i.Status == BggImportStatus.Processing);
        Assert.Contains(allItems, i => i.Status == BggImportStatus.Completed);
        Assert.Contains(allItems, i => i.Status == BggImportStatus.Failed);
    }

    [Fact]
    public async Task GetByBggIdAsync_WithExistingBggId_ReturnsEntity()
    {
        // Arrange
        const int bggId = 999;
        await _service.EnqueueAsync(bggId, "Test Game");

        // Act
        var result = await _service.GetByBggIdAsync(bggId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bggId, result.BggId);
    }

    [Fact]
    public async Task GetByBggIdAsync_WithNonExistentBggId_ReturnsNull()
    {
        // Act
        var result = await _service.GetByBggIdAsync(12345);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextQueuedItemAsync_ReturnsLowestPosition()
    {
        // Arrange
        var entity1 = await _service.EnqueueAsync(1, "Game 1");
        var entity2 = await _service.EnqueueAsync(2, "Game 2");
        var entity3 = await _service.EnqueueAsync(3, "Game 3");

        // Act
        var next = await _service.GetNextQueuedItemAsync();

        // Assert
        Assert.NotNull(next);
        Assert.Equal(entity1.Id, next.Id);
        Assert.Equal(1, next.Position);
    }

    [Fact]
    public async Task GetNextQueuedItemAsync_WithEmptyQueue_ReturnsNull()
    {
        // Act
        var next = await _service.GetNextQueuedItemAsync();

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public async Task GetNextQueuedItemAsync_IgnoresProcessingItems()
    {
        // Arrange
        var entity1 = await _service.EnqueueAsync(1, "Processing");
        var entity2 = await _service.EnqueueAsync(2, "Queued");

        await _service.MarkAsProcessingAsync(entity1.Id);

        // Act
        var next = await _service.GetNextQueuedItemAsync();

        // Assert
        Assert.NotNull(next);
        Assert.Equal(entity2.Id, next.Id);
    }

    #endregion
}
