using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.CatalogIngestion;

/// <summary>
/// Integration tests for the BGG enrichment queue with real PostgreSQL database.
/// Validates: atomic claiming, stale recovery, enrichment batch operations, position management.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class EnrichmentQueueIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private BggImportQueueService _service = null!;
    private Mock<TimeProvider> _mockTimeProvider = null!;

    private static readonly Guid AdminUserId = Guid.NewGuid();

    public EnrichmentQueueIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"enrichqueue_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(tp => tp.GetUtcNow())
            .Returns(new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero));

        _service = new BggImportQueueService(
            _dbContext,
            Mock.Of<ILogger<BggImportQueueService>>(),
            _mockTimeProvider.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region Enrichment Enqueue Tests

    [Fact]
    public async Task EnqueueEnrichment_SingleItem_CreatesEnrichmentJob()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        // Act
        var entity = await _service.EnqueueEnrichmentAsync(
            sharedGameId, bggId: 13, "Catan", AdminUserId, batchId);

        // Assert
        entity.Should().NotBeNull();
        entity.JobType.Should().Be(BggQueueJobType.Enrichment);
        entity.SharedGameId.Should().Be(sharedGameId);
        entity.BggId.Should().Be(13);
        entity.BatchId.Should().Be(batchId);
        entity.Status.Should().Be(BggImportStatus.Queued);
        entity.GameName.Should().Be("Catan");
        entity.RequestedByUserId.Should().Be(AdminUserId);

        // Verify in DB
        var dbEntity = await _dbContext.BggImportQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == entity.Id);
        dbEntity.Should().NotBeNull();
        dbEntity!.JobType.Should().Be(BggQueueJobType.Enrichment);
    }

    [Fact]
    public async Task EnqueueEnrichment_NullBggId_AllowedForAutoMatch()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        // Act
        var entity = await _service.EnqueueEnrichmentAsync(
            sharedGameId, bggId: null, "Unknown Game", AdminUserId, batchId);

        // Assert
        entity.BggId.Should().BeNull();
        entity.GameName.Should().Be("Unknown Game");
    }

    [Fact]
    public async Task EnqueueEnrichmentBatch_MultipleItems_CreatesAllJobs()
    {
        // Arrange
        var items = new[]
        {
            (Guid.NewGuid(), (int?)13, "Catan"),
            (Guid.NewGuid(), (int?)266192, "Wingspan"),
            (Guid.NewGuid(), (int?)null, "Unknown Game")
        };

        // Act
        var (batchId, enqueued) = await _service.EnqueueEnrichmentBatchAsync(
            items.Select(i => (i.Item1, i.Item2, i.Item3)), AdminUserId);

        // Assert
        batchId.Should().NotBeEmpty();
        enqueued.Should().Be(3);

        var dbEntities = await _dbContext.BggImportQueue
            .AsNoTracking()
            .Where(q => q.BatchId == batchId)
            .OrderBy(q => q.Position)
            .ToListAsync();

        dbEntities.Should().HaveCount(3);
        dbEntities.Should().OnlyContain(e => e.JobType == BggQueueJobType.Enrichment);
        dbEntities.Should().OnlyContain(e => e.BatchId == batchId);

        // Positions should be sequential
        dbEntities[0].Position.Should().BeLessThan(dbEntities[1].Position);
        dbEntities[1].Position.Should().BeLessThan(dbEntities[2].Position);
    }

    [Fact]
    public async Task EnqueueEnrichmentBatch_EmptyList_ReturnsZero()
    {
        // Act
        var (batchId, enqueued) = await _service.EnqueueEnrichmentBatchAsync(
            Array.Empty<(Guid, int?, string)>(), AdminUserId);

        // Assert
        batchId.Should().NotBeEmpty();
        enqueued.Should().Be(0);
    }

    #endregion

    #region Atomic Claim Tests

    [Fact]
    public async Task TryClaimItem_FirstClaimSucceeds()
    {
        // Arrange
        var entity = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 13, "Catan", AdminUserId, Guid.NewGuid());

        // Act
        var claimed = await _service.TryClaimItemAsync(entity.Id);

        // Assert
        claimed.Should().BeTrue();

        var dbEntity = await _dbContext.BggImportQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == entity.Id);
        dbEntity!.Status.Should().Be(BggImportStatus.Processing);
    }

    [Fact]
    public async Task TryClaimItem_SecondClaimFails()
    {
        // Arrange
        var entity = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 13, "Catan", AdminUserId, Guid.NewGuid());

        // Act
        var claim1 = await _service.TryClaimItemAsync(entity.Id);
        var claim2 = await _service.TryClaimItemAsync(entity.Id);

        // Assert
        claim1.Should().BeTrue();
        claim2.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimItem_NonExistentId_ReturnsFalse()
    {
        // Act
        var claimed = await _service.TryClaimItemAsync(Guid.NewGuid());

        // Assert
        claimed.Should().BeFalse();
    }

    #endregion

    #region Stale Recovery Tests

    [Fact]
    public async Task RecoverStaleItems_StuckProcessing_ResetsToQueued()
    {
        // Arrange: Create an item and mark it as processing
        var entity = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 13, "Catan", AdminUserId, Guid.NewGuid());

        await _service.MarkAsProcessingAsync(entity.Id);

        // Simulate time passing: advance clock by 10 minutes
        _mockTimeProvider.Setup(tp => tp.GetUtcNow())
            .Returns(new DateTimeOffset(2026, 3, 14, 12, 10, 0, TimeSpan.Zero));

        // Act
        var recovered = await _service.RecoverStaleItemsAsync(TimeSpan.FromMinutes(5));

        // Assert
        recovered.Should().Be(1);

        var dbEntity = await _dbContext.BggImportQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == entity.Id);
        dbEntity!.Status.Should().Be(BggImportStatus.Queued);
        dbEntity.RetryCount.Should().Be(1); // Incremented
    }

    [Fact]
    public async Task RecoverStaleItems_RecentProcessing_DoesNotReset()
    {
        // Arrange: Create an item and mark it as processing
        var entity = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 13, "Catan", AdminUserId, Guid.NewGuid());

        await _service.MarkAsProcessingAsync(entity.Id);

        // Time has NOT advanced beyond threshold

        // Act
        var recovered = await _service.RecoverStaleItemsAsync(TimeSpan.FromMinutes(5));

        // Assert
        recovered.Should().Be(0);

        var dbEntity = await _dbContext.BggImportQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == entity.Id);
        dbEntity!.Status.Should().Be(BggImportStatus.Processing);
    }

    #endregion

    #region Queue Ordering Tests

    [Fact]
    public async Task GetNextQueuedItem_ReturnsLowestPosition()
    {
        // Arrange
        var entity1 = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 1, "Game1", AdminUserId, Guid.NewGuid());
        var entity2 = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 2, "Game2", AdminUserId, Guid.NewGuid());

        // Act
        var next = await _service.GetNextQueuedItemAsync();

        // Assert
        next.Should().NotBeNull();
        next!.Id.Should().Be(entity1.Id);
        next.Position.Should().BeLessThanOrEqualTo(entity2.Position);
    }

    [Fact]
    public async Task Completion_RecalculatesPositions()
    {
        // Arrange
        var entity1 = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 1, "Game1", AdminUserId, Guid.NewGuid());
        var entity2 = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 2, "Game2", AdminUserId, Guid.NewGuid());
        var entity3 = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 3, "Game3", AdminUserId, Guid.NewGuid());

        // Act: Complete first item
        await _service.MarkAsProcessingAsync(entity1.Id);
        await _service.MarkAsCompletedAsync(entity1.Id, Guid.NewGuid());

        // Assert: Remaining items should have positions recalculated
        var remaining = await _dbContext.BggImportQueue
            .AsNoTracking()
            .Where(q => q.Status == BggImportStatus.Queued)
            .OrderBy(q => q.Position)
            .ToListAsync();

        remaining.Should().HaveCount(2);
        remaining[0].Position.Should().Be(1);
        remaining[1].Position.Should().Be(2);
    }

    #endregion

    #region Mixed Job Type Tests

    [Fact]
    public async Task Queue_MixedJobTypes_ProcessesInOrder()
    {
        // Arrange: Mix import and enrichment jobs
        var importEntity = await _service.EnqueueAsync(174430, "Gloomhaven", AdminUserId);
        var enrichEntity = await _service.EnqueueEnrichmentAsync(
            Guid.NewGuid(), 266192, "Wingspan", AdminUserId, Guid.NewGuid());

        // Act
        var next = await _service.GetNextQueuedItemAsync();

        // Assert: First queued item should come first (import)
        next.Should().NotBeNull();
        next!.Id.Should().Be(importEntity.Id);
        next.JobType.Should().Be(BggQueueJobType.Import);
    }

    [Fact]
    public async Task EnrichmentJob_MarkedCompleted_StoresSharedGameId()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var entity = await _service.EnqueueEnrichmentAsync(
            sharedGameId, 13, "Catan", AdminUserId, Guid.NewGuid());

        await _service.MarkAsProcessingAsync(entity.Id);

        // Act: Mark as completed with the SharedGameId as the created game ID
        await _service.MarkAsCompletedAsync(entity.Id, sharedGameId);

        // Assert
        var completed = await _dbContext.BggImportQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == entity.Id);

        completed.Should().NotBeNull();
        completed!.Status.Should().Be(BggImportStatus.Completed);
        completed.CreatedGameId.Should().Be(sharedGameId);
        completed.ProcessedAt.Should().NotBeNull();
    }

    #endregion
}
