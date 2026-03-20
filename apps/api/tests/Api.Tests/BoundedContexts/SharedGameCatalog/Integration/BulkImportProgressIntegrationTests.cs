using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for GetBulkImportProgressQuery with real database and queue service.
/// Issue #4353: Backend - Bulk Import SSE Progress Endpoint
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkImportProgressIntegrationTests : IAsyncLifetime
{
    private IServiceProvider _services = null!;
    private MeepleAiDbContext _dbContext = null!;

    public ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<IDomainEventCollector>(new Mock<IDomainEventCollector>().Object);
        services.AddScoped<IBggImportQueueService, BggImportQueueService>();
        services.AddLogging();

        _services = services.BuildServiceProvider();
        _dbContext = _services.GetRequiredService<MeepleAiDbContext>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }

        if (_services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task Handle_WithSeededQueueItems_ShouldReturnAggregatedProgress()
    {
        // Arrange - Seed queue items in various states
        var queueItems = new List<BggImportQueueEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BggId = 13,
                GameName = "Catan",
                Status = BggImportStatus.Completed,
                Position = 0,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                BggId = 266192,
                GameName = "Wingspan",
                Status = BggImportStatus.Processing,
                Position = 0,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                BggId = 230802,
                GameName = "Azul",
                Status = BggImportStatus.Queued,
                Position = 1,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                BggId = 174430,
                GameName = "Gloomhaven",
                Status = BggImportStatus.Queued,
                Position = 2,
                CreatedAt = DateTime.UtcNow
            }
        };

        await _dbContext.BggImportQueue.AddRangeAsync(queueItems);
        await _dbContext.SaveChangesAsync();

        var handler = new GetBulkImportProgressQueryHandler(
            _services.GetRequiredService<IBggImportQueueService>());

        // Act
        var result = await handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        result.Total.Should().Be(4);
        result.Queued.Should().Be(2);
        result.Processing.Should().Be(1);
        result.Completed.Should().Be(1);
        result.Failed.Should().Be(0);
        result.IsActive.Should().BeTrue();
        result.EstimatedSecondsRemaining.Should().Be(3);
        result.CurrentItem.Should().NotBeNull();
        result.CurrentItem.BggId.Should().Be(266192);
        result.CurrentItem.GameName.Should().Be("Wingspan");
    }

    [Fact]
    public async Task Handle_EmptyQueue_ShouldReturnInactiveProgress()
    {
        // Arrange - Empty queue
        var handler = new GetBulkImportProgressQueryHandler(
            _services.GetRequiredService<IBggImportQueueService>());

        // Act
        var result = await handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.IsActive.Should().BeFalse();
        result.EstimatedSecondsRemaining.Should().Be(0);
        result.CurrentItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AllItemsCompleted_ShouldBeInactive()
    {
        // Arrange
        var completedItems = Enumerable.Range(1, 5).Select(i => new BggImportQueueEntity
        {
            Id = Guid.NewGuid(),
            BggId = i * 100,
            GameName = $"Game {i}",
            Status = BggImportStatus.Completed,
            Position = 0,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        });

        await _dbContext.BggImportQueue.AddRangeAsync(completedItems);
        await _dbContext.SaveChangesAsync();

        var handler = new GetBulkImportProgressQueryHandler(
            _services.GetRequiredService<IBggImportQueueService>());

        // Act
        var result = await handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        result.Total.Should().Be(5);
        result.Completed.Should().Be(5);
        result.IsActive.Should().BeFalse();
        result.CurrentItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FailedItems_ShouldCountCorrectly()
    {
        // Arrange
        var items = new List<BggImportQueueEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                BggId = 100,
                GameName = "Failed Game",
                Status = BggImportStatus.Failed,
                Position = 0,
                RetryCount = 3,
                ErrorMessage = "BGG API timeout",
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                BggId = 200,
                GameName = "Success Game",
                Status = BggImportStatus.Completed,
                Position = 0,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            }
        };

        await _dbContext.BggImportQueue.AddRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        var handler = new GetBulkImportProgressQueryHandler(
            _services.GetRequiredService<IBggImportQueueService>());

        // Act
        var result = await handler.Handle(new GetBulkImportProgressQuery(), CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Completed.Should().Be(1);
        result.Failed.Should().Be(1);
        result.IsActive.Should().BeFalse();
    }
}
