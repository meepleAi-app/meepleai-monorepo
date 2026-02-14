using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Unit tests for EnqueueBggBatchFromJsonCommandHandler
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EnqueueBggBatchFromJsonCommandHandlerTests
{
    private readonly Mock<IBggImportQueueService> _mockQueueService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<EnqueueBggBatchFromJsonCommandHandler>> _mockLogger;
    private readonly EnqueueBggBatchFromJsonCommandHandler _handler;

    public EnqueueBggBatchFromJsonCommandHandlerTests()
    {
        _mockQueueService = new Mock<IBggImportQueueService>();
        _mockLogger = new Mock<ILogger<EnqueueBggBatchFromJsonCommandHandler>>();

        // In-memory database for testing
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new EnqueueBggBatchFromJsonCommandHandler(
            _mockQueueService.Object,
            _dbContext,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidJson_AllNewGames_ShouldEnqueueAll()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"bggId\": 123, \"name\": \"Catan\"}, {\"bggId\": 456, \"name\": \"Wingspan\"}]",
            UserId = Guid.NewGuid()
        };

        _mockQueueService
            .Setup(s => s.EnqueueAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggImportQueueEntity
            {
                BggId = It.IsAny<int>(),
                Status = BggImportStatus.Queued,
                Position = 1
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Enqueued);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);

        _mockQueueService.Verify(
            s => s.EnqueueAsync(123, "Catan", It.IsAny<CancellationToken>()),
            Times.Once);

        _mockQueueService.Verify(
            s => s.EnqueueAsync(456, "Wingspan", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateGames_ShouldSkipExisting()
    {
        // Arrange - Seed database with existing game entity
        var existingGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = 123,
            Title = "Catan",
            YearPublished = 1995,
            Description = "Trade and build",
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
            JsonContent = "[{\"bggId\": 123, \"name\": \"Catan\"}, {\"bggId\": 456, \"name\": \"Wingspan\"}]",
            UserId = Guid.NewGuid()
        };

        _mockQueueService
            .Setup(s => s.EnqueueAsync(456, "Wingspan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggImportQueueEntity
            {
                BggId = 456,
                Status = BggImportStatus.Queued,
                Position = 1
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Enqueued);  // Only Wingspan
        Assert.Equal(1, result.Skipped);   // Catan skipped
        Assert.Equal(0, result.Failed);
        Assert.Single(result.Errors);
        Assert.Equal("Duplicate", result.Errors[0].ErrorType);
        Assert.Equal(123, result.Errors[0].BggId);

        // Verify only Wingspan was enqueued
        _mockQueueService.Verify(
            s => s.EnqueueAsync(456, "Wingspan", It.IsAny<CancellationToken>()),
            Times.Once);

        _mockQueueService.Verify(
            s => s.EnqueueAsync(123, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidJson_ShouldReturnError()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "{ invalid json }",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Enqueued);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Equal("InvalidJson", result.Errors[0].ErrorType);
        Assert.Contains("JSON parsing failed", result.Errors[0].Reason);
    }

    [Fact]
    public async Task Handle_NegativeBggId_ShouldFailValidation()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"bggId\": -1, \"name\": \"Invalid Game\"}]",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.Total);
        Assert.Equal(0, result.Enqueued);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Equal("ValidationError", result.Errors[0].ErrorType);
        Assert.Contains("BGG ID must be positive", result.Errors[0].Reason);
    }

    [Fact]
    public async Task Handle_QueueServiceThrows_ShouldContinueWithBestEffort()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[{\"bggId\": 123, \"name\": \"Catan\"}, {\"bggId\": 456, \"name\": \"Wingspan\"}]",
            UserId = Guid.NewGuid()
        };

        // First game enqueues successfully, second throws
        _mockQueueService
            .Setup(s => s.EnqueueAsync(123, "Catan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggImportQueueEntity
            {
                BggId = 123,
                Status = BggImportStatus.Queued,
                Position = 1
            });

        _mockQueueService
            .Setup(s => s.EnqueueAsync(456, "Wingspan", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Queue service error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Best effort: first succeeded, second failed but processing continued
        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Enqueued);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Equal("ApiError", result.Errors[0].ErrorType);
        Assert.Equal(456, result.Errors[0].BggId);
        Assert.Contains("Failed to enqueue", result.Errors[0].Reason);
    }

    [Fact]
    public async Task Handle_MixedScenario_ShouldHandleAllCases()
    {
        // Arrange - Seed one duplicate
        var existingGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = 789,
            Title = "Azul",
            YearPublished = 2017,
            Description = "Tile laying",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.SharedGames.AddAsync(existingGame);
        await _dbContext.SaveChangesAsync();

        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = @"[
                {""bggId"": 123, ""name"": ""Catan""},
                {""bggId"": 789, ""name"": ""Azul""},
                {""bggId"": -1, ""name"": ""Invalid""},
                {""bggId"": 456, ""name"": ""Wingspan""}
            ]",
            UserId = Guid.NewGuid()
        };

        _mockQueueService
            .Setup(s => s.EnqueueAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggImportQueueEntity
            {
                BggId = It.IsAny<int>(),
                Status = BggImportStatus.Queued,
                Position = 1
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Total);
        Assert.Equal(2, result.Enqueued);    // Catan + Wingspan
        Assert.Equal(1, result.Skipped);     // Azul (duplicate)
        Assert.Equal(1, result.Failed);      // Invalid (negative ID)
        Assert.Equal(2, result.Errors.Count);

        var duplicateError = result.Errors.First(e => e.ErrorType == "Duplicate");
        Assert.Equal(789, duplicateError.BggId);

        var validationError = result.Errors.First(e => e.ErrorType == "ValidationError");
        Assert.Equal(-1, validationError.BggId);
    }

    [Fact]
    public async Task Handle_EmptyJsonArray_ShouldReturnEmptyResult()
    {
        // Arrange
        var command = new EnqueueBggBatchFromJsonCommand
        {
            JsonContent = "[]",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Should pass validator but return empty (validator will catch this normally)
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Enqueued);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
    }
}
