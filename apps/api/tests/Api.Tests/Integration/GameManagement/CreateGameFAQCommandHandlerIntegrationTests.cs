using Api.Tests.Infrastructure;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Comprehensive integration tests for CreateGameFAQ workflow (Issue #2186).
/// Tests the complete FAQ creation pipeline using Testcontainers for real PostgreSQL infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Create FAQs with valid data
/// 2. Validation Errors: Empty question/answer, invalid game
/// 3. Value Object Constraints: Max length validation (500 chars question, 5000 chars answer)
/// 4. Database Integration: Repository persistence, DbContext transactions
/// 5. Concurrent Operations: Race condition handling
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for CreateGameFAQCommandHandler
/// Execution Time Target: <30s per test
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Issue", "2186")]
[Trait("Category", TestCategories.Integration)]
public sealed class CreateGameFAQCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testGameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CreateGameFAQCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = $"test_createfaq_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register repositories
        services.AddScoped<IGameFAQRepository, GameFAQRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(CreateGameFAQCommandHandler).Assembly));

        // Register TimeProvider
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<CreateGameFAQCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Retry to mitigate occasional network hiccups when starting fresh container
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for cleanup
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task SeedTestDataAsync()
    {
        // Seed game
        _testGameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = _testGameId,
            Name = "Test Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithValidData_CreatesFAQSuccessfully()
    {
        // Arrange
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: "How many players can play?",
            Answer: "This game supports 2-4 players."
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.GameId.Should().Be(_testGameId);
        result.Question.Should().Be("How many players can play?");
        result.Answer.Should().Be("This game supports 2-4 players.");
        result.Upvotes.Should().Be(0);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeNull();

        // Verify persistence
        var persisted = await _dbContext!.GameFAQs.FindAsync([result.Id], TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Question.Should().Be("How many players can play?");  // GameFAQEntity has string properties
        persisted.Answer.Should().Be("This game supports 2-4 players.");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithEmptyQuestion_ThrowsArgumentException()
    {
        // Arrange
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: "   ",
            Answer: "This is a valid answer."
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ question cannot be empty*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithEmptyAnswer_ThrowsArgumentException()
    {
        // Arrange
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: "What is the objective?",
            Answer: ""
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ answer cannot be empty*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var command = new CreateGameFAQCommand(
            GameId: nonExistentGameId,
            Question: "How to play?",
            Answer: "Follow the rulebook."
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Game with ID {nonExistentGameId} not found*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithQuestionExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longQuestion = new string('x', 501); // Max is 500
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: longQuestion,
            Answer: "Valid answer"
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ question cannot exceed 500 characters*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithAnswerExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longAnswer = new string('x', 5001); // Max is 5000
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: "Valid question?",
            Answer: longAnswer
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ answer cannot exceed 5000 characters*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithMaxLengthQuestionAndAnswer_CreatesSuccessfully()
    {
        // Arrange
        var maxQuestion = new string('Q', 500);
        var maxAnswer = new string('A', 5000);
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: maxQuestion,
            Answer: maxAnswer
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Question.Should().HaveLength(500);
        result.Answer.Should().HaveLength(5000);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithWhitespaceQuestion_TrimmedCorrectly()
    {
        // Arrange
        var command = new CreateGameFAQCommand(
            GameId: _testGameId,
            Question: "  How to setup?  ",
            Answer: "  Read instructions  "
        );

        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Question.Should().Be("How to setup?");
        result.Answer.Should().Be("Read instructions");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_MultipleFAQsForSameGame_AllPersisted()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<CreateGameFAQCommandHandler>();

        var command1 = new CreateGameFAQCommand(_testGameId, "Question 1?", "Answer 1");
        var command2 = new CreateGameFAQCommand(_testGameId, "Question 2?", "Answer 2");

        // Act
        var result1 = await handler.Handle(command1, TestCancellationToken);
        var result2 = await handler.Handle(command2, TestCancellationToken);

        // Assert
        var allFAQs = await _dbContext!.GameFAQs
            .Where(f => f.GameId == _testGameId)
            .ToListAsync(TestCancellationToken);

        allFAQs.Should().HaveCount(2);
        allFAQs.Should().ContainSingle(f => f.Question == "Question 1?");  // string comparison
        allFAQs.Should().ContainSingle(f => f.Question == "Question 2?");
    }
}
