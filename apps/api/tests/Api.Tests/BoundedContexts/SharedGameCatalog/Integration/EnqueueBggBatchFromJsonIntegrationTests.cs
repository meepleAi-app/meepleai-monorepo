using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
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
/// Integration tests for EnqueueBggBatchFromJsonCommand with real database and queue service
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class EnqueueBggBatchFromJsonIntegrationTests : IAsyncLifetime
{
    private IServiceProvider _services = null!;
    private MeepleAiDbContext _dbContext = null!;

    public ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();

        // In-memory database
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

        // MeepleAiDbContext dependencies
        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<IDomainEventCollector>(new Mock<IDomainEventCollector>().Object);

        // Real queue service
        services.AddScoped<IBggImportQueueService, BggImportQueueService>();

        // Logging
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
    public async Task Handle_FullWorkflow_WithDuplicates_ShouldWorkCorrectly()
    {
        // Arrange - Seed existing game entity with BGG ID
        var existingGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = 13,  // Catan's real BGG ID
            Title = "Catan",
            YearPublished = 1995,
            Description = "Settle the island",
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.SharedGames.AddAsync(existingGame);
        await _dbContext.SaveChangesAsync();

        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = @"[
                {""bggId"": 13, ""name"": ""Catan""},
                {""bggId"": 266192, ""name"": ""Wingspan""},
                {""bggId"": 230802, ""name"": ""Azul""}
            ]",
            UserId = Guid.NewGuid()
        };

        var handler = new EnqueueBggBatchFromJsonCommandHandler(
            _services.GetRequiredService<IBggImportQueueService>(),
            _dbContext,
            _services.GetRequiredService<ILogger<EnqueueBggBatchFromJsonCommandHandler>>());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(3);
        result.Enqueued.Should().Be(2);   // Wingspan + Azul
        result.Skipped.Should().Be(1);    // Catan (duplicate)
        result.Failed.Should().Be(0);
        result.Errors.Should().ContainSingle();

        var duplicateError = result.Errors[0];
        duplicateError.ErrorType.Should().Be("Duplicate");
        duplicateError.BggId.Should().Be(13);
        duplicateError.GameName.Should().Be("Catan");

        // Verify queue entities created
        var queuedItems = await _dbContext.BggImportQueue.ToListAsync();
        queuedItems.Count.Should().Be(2);
        queuedItems.Should().Contain(q => q.BggId == 266192); // Wingspan
        queuedItems.Should().Contain(q => q.BggId == 230802); // Azul
    }

    [Fact]
    public async Task Handle_LargeJsonBatch_ShouldQueryDuplicatesEfficiently()
    {
        // Arrange - Seed 50 existing games
        var existingGames = Enumerable.Range(1, 50).Select(i => new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = i,
            Title = $"Game {i}",
            YearPublished = 2020,
            Description = "Test",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SharedGames.AddRangeAsync(existingGames);
        await _dbContext.SaveChangesAsync();

        // JSON with 100 games (50 duplicates + 50 new)
        var jsonGames = Enumerable.Range(1, 100)
            .Select(i => $"{{\"bggId\": {i}, \"name\": \"Game {i}\"}}")
            .ToList();

        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[" + string.Join(",", jsonGames) + "]",
            UserId = Guid.NewGuid()
        };

        var handler = new EnqueueBggBatchFromJsonCommandHandler(
            _services.GetRequiredService<IBggImportQueueService>(),
            _dbContext,
            _services.GetRequiredService<ILogger<EnqueueBggBatchFromJsonCommandHandler>>());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(100);
        result.Enqueued.Should().Be(50);  // Games 51-100
        result.Skipped.Should().Be(50);   // Games 1-50 (duplicates)
        result.Failed.Should().Be(0);
        result.Errors.Count.Should().Be(50);  // 50 duplicate errors

        // Verify all duplicate errors are correct type
        result.Errors.Should().AllSatisfy(error => error.ErrorType.Should().Be("Duplicate"));

        // Verify queue has 50 new items
        var queuedItems = await _dbContext.BggImportQueue.ToListAsync();
        queuedItems.Count.Should().Be(50);
    }

    [Fact]
    public async Task Handle_EmptyJson_ShouldReturnEmptyResult()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[]",
            UserId = Guid.NewGuid()
        };

        var handler = new EnqueueBggBatchFromJsonCommandHandler(
            _services.GetRequiredService<IBggImportQueueService>(),
            _dbContext,
            _services.GetRequiredService<ILogger<EnqueueBggBatchFromJsonCommandHandler>>());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.Enqueued.Should().Be(0);
        result.Skipped.Should().Be(0);
        result.Failed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }
}