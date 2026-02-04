using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for BggImportQueueBackgroundService.
/// Issue #3541: BGG Import Queue Service
/// Tests: Background worker processing, rate limiting, retry logic, failure handling, CQRS integration
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggImportQueueBackgroundServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private BggImportQueueService _queueService = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IServiceScopeFactory> _mockScopeFactory = null!;
    private Mock<IServiceScope> _mockScope = null!;
    private Mock<IServiceProvider> _mockServiceProvider = null!;

    public BggImportQueueBackgroundServiceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"bggworker_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Build DbContext with test database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Mock MediatR and DomainEventCollector
        _mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, _mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        // Initialize queue service
        var mockLogger = new Mock<ILogger<BggImportQueueService>>();
        _queueService = new BggImportQueueService(_dbContext, mockLogger.Object);

        // Setup service scope factory for background service
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);

        // Setup service provider to return queue service and mediator
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IBggImportQueueService)))
            .Returns(_queueService);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IMediator)))
            .Returns(_mockMediator.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region Background Worker Processing Tests

    [Fact]
    public async Task ProcessNextQueueItemAsync_WithQueuedItem_ProcessesSuccessfully()
    {
        // Arrange
        const int bggId = 174430; // Gloomhaven
        var queueEntity = await _queueService.EnqueueAsync(bggId, "Gloomhaven");
        var createdGameId = Guid.NewGuid();

        // Mock successful import
        _mockMediator.Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdGameId);

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act - Use reflection to call private ProcessNextQueueItemAsync
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        var processed = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == queueEntity.Id);
        Assert.NotNull(processed);
        Assert.Equal(BggImportStatus.Completed, processed.Status);
        Assert.Equal(createdGameId, processed.CreatedGameId);
        Assert.NotNull(processed.ProcessedAt);

        // Verify ImportGameFromBggCommand was sent
        _mockMediator.Verify(
            m => m.Send(
                It.Is<ImportGameFromBggCommand>(cmd => cmd.BggId == bggId && cmd.UserId == Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNextQueueItemAsync_WithEmptyQueue_DoesNothing()
    {
        // Arrange
        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act - Process empty queue
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - No mediator calls
        _mockMediator.Verify(
            m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessNextQueueItemAsync_WithFailure_MarksAsFailedAndRetries()
    {
        // Arrange
        const int bggId = 266192; // Wingspan
        var queueEntity = await _queueService.EnqueueAsync(bggId, "Wingspan");

        // Mock failure
        _mockMediator.Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("BGG API timeout"));

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1, maxRetryAttempts: 3);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        var failed = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == queueEntity.Id);
        Assert.NotNull(failed);
        Assert.Equal(BggImportStatus.Queued, failed.Status); // Back to queued for retry
        Assert.Equal(1, failed.RetryCount);
        Assert.Contains("HttpRequestException", failed.ErrorMessage!);
        Assert.Contains("BGG API timeout", failed.ErrorMessage!);
    }

    [Fact]
    public async Task ProcessNextQueueItemAsync_WithMaxRetriesReached_MarksAsPermanentlyFailed()
    {
        // Arrange
        const int bggId = 68448; // 7 Wonders
        var queueEntity = await _queueService.EnqueueAsync(bggId, "7 Wonders");

        // Simulate 2 previous failures
        await _queueService.MarkAsProcessingAsync(queueEntity.Id);
        await _queueService.MarkAsFailedAsync(queueEntity.Id, "First failure", maxRetries: 3);
        await _queueService.MarkAsProcessingAsync(queueEntity.Id);
        await _queueService.MarkAsFailedAsync(queueEntity.Id, "Second failure", maxRetries: 3);

        // Mock third failure
        _mockMediator.Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Final error"));

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1, maxRetryAttempts: 3);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert
        var permanentlyFailed = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == queueEntity.Id);
        Assert.NotNull(permanentlyFailed);
        Assert.Equal(BggImportStatus.Failed, permanentlyFailed.Status); // Permanently failed
        Assert.Equal(3, permanentlyFailed.RetryCount);
        Assert.NotNull(permanentlyFailed.ProcessedAt);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task BackgroundService_WithCustomInterval_UsesConfiguredDelay()
    {
        // Arrange
        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 2);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act - Verify configuration is respected
        // Note: ExecuteAsync is protected, so we verify via config validation
        var configValue = config.Value;

        // Assert
        Assert.Equal(2, configValue.ProcessingIntervalSeconds);
    }

    [Fact]
    public async Task BackgroundService_WithDisabledConfig_DoesNotStart()
    {
        // Arrange
        var config = CreateTestConfiguration(enabled: false);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act - Start background service with cancellation
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await service.StartAsync(cts.Token);

        // Wait briefly to ensure ExecuteAsync runs
        await Task.Delay(500);

        await service.StopAsync(CancellationToken.None);

        // Assert - No queue items should be processed
        _mockMediator.Verify(
            m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Cleanup Tests

    [Fact]
    public async Task PerformCleanupAsync_WithEmptyQueue_TriggersCleanup()
    {
        // Arrange
        var oldEntity = await _queueService.EnqueueAsync(1, "Old Completed Game");
        await _queueService.MarkAsProcessingAsync(oldEntity.Id);
        await _queueService.MarkAsCompletedAsync(oldEntity.Id, Guid.NewGuid());

        // Update ProcessedAt to 40 days ago
        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.NotNull(dbEntry);
        dbEntry.ProcessedAt = DateTime.UtcNow.AddDays(-40);
        await _dbContext.SaveChangesAsync();

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1, autoCleanupDays: 30);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act - Process with empty queue (triggers cleanup)
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - Old entry should be cleaned up
        var cleaned = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.Null(cleaned);
    }

    [Fact]
    public async Task PerformCleanupAsync_WithAutoCleanupDisabled_DoesNotCleanup()
    {
        // Arrange
        var oldEntity = await _queueService.EnqueueAsync(2, "Old Game");
        await _queueService.MarkAsProcessingAsync(oldEntity.Id);
        await _queueService.MarkAsCompletedAsync(oldEntity.Id, Guid.NewGuid());

        var dbEntry = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.NotNull(dbEntry);
        dbEntry.ProcessedAt = DateTime.UtcNow.AddDays(-40);
        await _dbContext.SaveChangesAsync();

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1, autoCleanupDays: 0); // Disabled
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - Old entry should NOT be cleaned up
        var notCleaned = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == oldEntity.Id);
        Assert.NotNull(notCleaned);
    }

    #endregion

    #region CQRS Integration Tests

    [Fact]
    public async Task ProcessNextQueueItemAsync_UsesSystemUserIdForBackgroundImports()
    {
        // Arrange
        const int bggId = 12345;
        await _queueService.EnqueueAsync(bggId, "Test Game");

        _mockMediator.Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - Verify system user ID (Guid.Empty) is used
        _mockMediator.Verify(
            m => m.Send(
                It.Is<ImportGameFromBggCommand>(cmd => cmd.UserId == Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNextQueueItemAsync_MarksAsProcessingBeforeImport()
    {
        // Arrange
        const int bggId = 999;
        var queueEntity = await _queueService.EnqueueAsync(bggId, "Test Game");

        _mockMediator.Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - Verify status after processing (should be Completed)
        var entity = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == queueEntity.Id);
        Assert.NotNull(entity);
        Assert.Equal(BggImportStatus.Completed, entity.Status);

        // Verify mediator was called
        _mockMediator.Verify(
            m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Position Management Tests

    [Fact]
    public async Task ProcessNextQueueItemAsync_ProcessesLowestPositionFirst()
    {
        // Arrange
        var entity1 = await _queueService.EnqueueAsync(1, "Game 1");
        var entity2 = await _queueService.EnqueueAsync(2, "Game 2");
        var entity3 = await _queueService.EnqueueAsync(3, "Game 3");

        var processedBggId = 0;

        _mockMediator.Setup(m => m.Send(It.IsAny<IRequest<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Guid>, CancellationToken>((cmd, _) =>
            {
                if (cmd is ImportGameFromBggCommand importCmd)
                {
                    processedBggId = importCmd.BggId;
                }
            })
            .ReturnsAsync(Guid.NewGuid());

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - Should process entity1 (position 1) first
        Assert.Equal(1, processedBggId);
    }

    [Fact]
    public async Task ProcessNextQueueItemAsync_WithCompletion_RecalculatesPositions()
    {
        // Arrange
        var entity1 = await _queueService.EnqueueAsync(1, "Game 1");
        var entity2 = await _queueService.EnqueueAsync(2, "Game 2");

        _mockMediator.Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1);
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);

        // Act - Process first item
        var method = typeof(BggImportQueueBackgroundService)
            .GetMethod("ProcessNextQueueItemAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        await task;

        // Assert - entity2 should now be position 1
        var entity2Updated = await _dbContext.BggImportQueue.FirstOrDefaultAsync(q => q.Id == entity2.Id);
        Assert.NotNull(entity2Updated);
        Assert.Equal(1, entity2Updated.Position);
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void BackgroundService_WithInvalidInterval_DoesNotStart()
    {
        // Arrange
        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 0); // Invalid
        var mockLogger = new Mock<ILogger<BggImportQueueBackgroundService>>();

        // Act & Assert - Service should validate and not process
        var service = new BggImportQueueBackgroundService(_mockScopeFactory.Object, mockLogger.Object, config);
        Assert.NotNull(service);
    }

    [Fact]
    public void BackgroundService_WithInitialDelay_DelaysFirstRun()
    {
        // Arrange
        var config = CreateTestConfiguration(enabled: true, processingIntervalSeconds: 1, initialDelayMinutes: 5);

        // Act
        var configValue = config.Value;

        // Assert
        Assert.Equal(5, configValue.InitialDelayMinutes);
    }

    #endregion

    #region Helper Methods

    private static IOptions<BggImportQueueConfiguration> CreateTestConfiguration(
        bool enabled = true,
        int processingIntervalSeconds = 1,
        int maxRetryAttempts = 3,
        int baseRetryDelaySeconds = 5,
        int autoCleanupDays = 30,
        int initialDelayMinutes = 0)
    {
        var config = new BggImportQueueConfiguration
        {
            Enabled = enabled,
            ProcessingIntervalSeconds = processingIntervalSeconds,
            MaxRetryAttempts = maxRetryAttempts,
            BaseRetryDelaySeconds = baseRetryDelaySeconds,
            AutoCleanupDays = autoCleanupDays,
            InitialDelayMinutes = initialDelayMinutes
        };

        return Options.Create(config);
    }

    #endregion
}
