using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for Arbitro Validation Feedback system.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4328")]
public sealed class ArbitroValidationFeedbackTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IArbitroValidationFeedbackRepository? _repository;
    private IMediator? _mediator;
    private IServiceProvider? _serviceProvider;
    private Guid _testGameId;
    private Guid _testUserId;

    public ArbitroValidationFeedbackTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_arbitrofeedback_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IArbitroValidationFeedbackRepository, ArbitroValidationFeedbackRepository>();

        // Override the bare IGameSessionRepository mock from CreateBase() with one that returns a valid GameSession.
        // SubmitValidationFeedbackCommandHandler calls GetByIdAsync and throws NotFoundException if null.
        var gameSessionMock = new Mock<IGameSessionRepository>();
        gameSessionMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                new GameSession(id, Guid.NewGuid(), new[] { new SessionPlayer("Player1", 1) }));
        services.AddScoped<IGameSessionRepository>(_ => gameSessionMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IArbitroValidationFeedbackRepository>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Create database schema
        await _dbContext.Database.MigrateAsync();

        // Seed a test user (FK requirement for feedback)
        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = "arbitro-test@test.com",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        });

        // Seed a test game (FK requirement for GameSession)
        _testGameId = Guid.NewGuid();
        _dbContext.Games.Add(new GameEntity
        {
            Id = _testGameId,
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a GameSession entity in the DB for FK constraint satisfaction.
    /// </summary>
    private async Task SeedGameSessionAsync(Guid gameSessionId)
    {
        _dbContext!.Set<GameSessionEntity>().Add(new GameSessionEntity
        {
            Id = gameSessionId,
            GameId = _testGameId,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            PlayersJson = "[{\"PlayerName\":\"Player1\",\"PlayerOrder\":1}]"
        });
        await _dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task SubmitFeedback_ValidRequest_SavesSuccessfully()
    {
        // Arrange
        var validationId = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();

        // Seed GameSession in DB for FK constraint
        await SeedGameSessionAsync(gameSessionId);

        var command = new SubmitValidationFeedbackCommand
        {
            ValidationId = validationId,
            GameSessionId = gameSessionId,
            UserId = _testUserId,
            Rating = 5,
            Accuracy = "Correct",
            Comment = "Great validation, very accurate!",
            AiDecision = "VALID",
            AiConfidence = 0.95,
            HadConflicts = false
        };

        // Act
        var feedbackId = await _mediator!.Send(command);

        // Assert
        feedbackId.Should().NotBeEmpty();

        var savedFeedback = await _repository!.GetByIdAsync(feedbackId);
        savedFeedback.Should().NotBeNull();
        savedFeedback!.ValidationId.Should().Be(validationId);
        savedFeedback.GameSessionId.Should().Be(gameSessionId);
        savedFeedback.UserId.Should().Be(_testUserId);
        savedFeedback.Rating.Should().Be(5);
        savedFeedback.Accuracy.Should().Be(AccuracyAssessment.Correct);
        savedFeedback.Comment.Should().Be("Great validation, very accurate!");
        savedFeedback.AiDecision.Should().Be("VALID");
        savedFeedback.AiConfidence.Should().Be(0.95);
        savedFeedback.HadConflicts.Should().BeFalse();
        savedFeedback.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SubmitFeedback_DuplicateValidationId_ThrowsConflict()
    {
        // Arrange
        var validationId = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();

        // Seed GameSession in DB for FK constraint
        await SeedGameSessionAsync(gameSessionId);

        // Submit first feedback
        var command1 = new SubmitValidationFeedbackCommand
        {
            ValidationId = validationId,
            GameSessionId = gameSessionId,
            UserId = _testUserId,
            Rating = 4,
            Accuracy = "Correct",
            AiDecision = "VALID",
            AiConfidence = 0.85,
            HadConflicts = false
        };
        await _mediator!.Send(command1);

        // Attempt to submit duplicate
        var command2 = new SubmitValidationFeedbackCommand
        {
            ValidationId = validationId, // Same validation ID
            GameSessionId = gameSessionId,
            UserId = _testUserId,
            Rating = 3,
            Accuracy = "Incorrect",
            AiDecision = "INVALID",
            AiConfidence = 0.70,
            HadConflicts = true
        };

        // Act & Assert
        var act = async () => await _mediator!.Send(command2);
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*already submitted*");
    }

    [Fact]
    public async Task GetAccuracyMetrics_MultipleSubmissions_ReturnsCorrectAggregation()
    {
        // Arrange
        var gameSessionId = Guid.NewGuid();

        // Seed GameSession in DB for FK constraint
        await SeedGameSessionAsync(gameSessionId);

        // Submit 3 feedbacks: 2 Correct, 1 Incorrect
        var feedbacks = new[]
        {
            new { Rating = 5, Accuracy = "Correct", Confidence = 0.95 },
            new { Rating = 4, Accuracy = "Correct", Confidence = 0.88 },
            new { Rating = 2, Accuracy = "Incorrect", Confidence = 0.65 }
        };

        foreach (var fb in feedbacks)
        {
            var command = new SubmitValidationFeedbackCommand
            {
                ValidationId = Guid.NewGuid(),
                GameSessionId = gameSessionId,
                UserId = _testUserId,
                Rating = fb.Rating,
                Accuracy = fb.Accuracy,
                AiDecision = "VALID",
                AiConfidence = fb.Confidence,
                HadConflicts = false
            };
            await _mediator!.Send(command);
        }

        // Act
        var metrics = await _repository!.GetAccuracyMetricsAsync(gameSessionId);

        // Assert
        metrics.total.Should().Be(3);
        metrics.correct.Should().Be(2);
        metrics.incorrect.Should().Be(1);
        metrics.uncertain.Should().Be(0);
        metrics.avgRating.Should().BeApproximately((5 + 4 + 2) / 3.0, 0.01);
    }

    [Fact]
    public async Task GetConflictFeedback_OnlyConflictCases_ReturnsFiltered()
    {
        // Arrange
        // Submit 2 feedbacks: 1 with conflicts, 1 without
        var sessionWithConflict = Guid.NewGuid();
        var sessionNoConflict = Guid.NewGuid();

        // Seed GameSessions in DB for FK constraint
        await SeedGameSessionAsync(sessionWithConflict);
        await SeedGameSessionAsync(sessionNoConflict);

        await _mediator!.Send(new SubmitValidationFeedbackCommand
        {
            ValidationId = Guid.NewGuid(),
            GameSessionId = sessionWithConflict,
            UserId = _testUserId,
            Rating = 3,
            Accuracy = "Uncertain",
            AiDecision = "UNCERTAIN",
            AiConfidence = 0.55,
            HadConflicts = true // With conflicts
        });

        await _mediator!.Send(new SubmitValidationFeedbackCommand
        {
            ValidationId = Guid.NewGuid(),
            GameSessionId = sessionNoConflict,
            UserId = _testUserId,
            Rating = 5,
            Accuracy = "Correct",
            AiDecision = "VALID",
            AiConfidence = 0.98,
            HadConflicts = false // No conflicts
        });

        // Act
        var conflictFeedback = await _repository!.GetConflictFeedbackAsync();

        // Assert
        conflictFeedback.Should().HaveCount(1);
        conflictFeedback.First().HadConflicts.Should().BeTrue();
        conflictFeedback.First().GameSessionId.Should().Be(sessionWithConflict);
    }
}
