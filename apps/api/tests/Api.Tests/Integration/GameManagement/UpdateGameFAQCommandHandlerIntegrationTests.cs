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
/// Comprehensive integration tests for UpdateGameFAQ workflow (Issue #2186).
/// Tests the complete FAQ update pipeline using Testcontainers for real PostgreSQL infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Update FAQ with valid data
/// 2. Validation Errors: Non-existent FAQ, empty question/answer
/// 3. Value Object Constraints: Max length validation
/// 4. UpdatedAt Timestamp: Proper timestamp updates
/// 5. Idempotency: Multiple updates
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for UpdateGameFAQCommandHandler
/// Execution Time Target: <30s per test
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Issue", "2186")]
[Trait("Category", TestCategories.Integration)]
public sealed class UpdateGameFAQCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testGameId;
    private Guid _testFaqId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UpdateGameFAQCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_updatefaq_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register repositories
        services.AddScoped<IGameFAQRepository, GameFAQRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(UpdateGameFAQCommandHandler).Assembly));
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<UpdateGameFAQCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

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

        // Seed FAQ
        _testFaqId = Guid.NewGuid();
        var faq = new GameFAQEntity
        {
            Id = _testFaqId,
            GameId = _testGameId,
            Question = "Original question?",
            Answer = "Original answer",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.GameFAQs.Add(faq);

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts with handler operations
        _dbContext.ChangeTracker.Clear();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithValidData_UpdatesFAQSuccessfully()
    {
        // Arrange
        var command = new UpdateGameFAQCommand(
            Id: _testFaqId,
            Question: "Updated question?",
            Answer: "Updated answer"
        );

        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(_testFaqId);
        result.Question.Should().Be("Updated question?");
        result.Answer.Should().Be("Updated answer");
        result.UpdatedAt.Should().NotBeNull();
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify persistence
        var persisted = await _dbContext!.GameFAQs.FindAsync([_testFaqId], TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Question.Should().Be("Updated question?");
        persisted.Answer.Should().Be("Updated answer");
        persisted.UpdatedAt.Should().NotBeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentFAQ_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new UpdateGameFAQCommand(
            Id: nonExistentId,
            Question: "Updated question?",
            Answer: "Updated answer"
        );

        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*FAQ with ID {nonExistentId} not found*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithEmptyQuestion_ThrowsArgumentException()
    {
        // Arrange
        var command = new UpdateGameFAQCommand(
            Id: _testFaqId,
            Question: "",
            Answer: "Valid answer"
        );

        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ question cannot be empty*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithEmptyAnswer_ThrowsArgumentException()
    {
        // Arrange
        var command = new UpdateGameFAQCommand(
            Id: _testFaqId,
            Question: "Valid question?",
            Answer: "   "
        );

        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ answer cannot be empty*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_MultipleUpdates_UpdatedAtChangesEachTime()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        var command1 = new UpdateGameFAQCommand(_testFaqId, "Update 1?", "Answer 1");
        var command2 = new UpdateGameFAQCommand(_testFaqId, "Update 2?", "Answer 2");

        // Act
        var result1 = await handler.Handle(command1, TestCancellationToken);
        await Task.Delay(100); // Ensure different timestamps
        var result2 = await handler.Handle(command2, TestCancellationToken);

        // Assert
        result2.UpdatedAt.Should().BeAfter(result1.UpdatedAt!.Value);
        result2.Question.Should().Be("Update 2?");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithQuestionExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longQuestion = new string('x', 501);
        var command = new UpdateGameFAQCommand(
            Id: _testFaqId,
            Question: longQuestion,
            Answer: "Valid answer"
        );

        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ question cannot exceed 500 characters*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithAnswerExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var longAnswer = new string('x', 5001);
        var command = new UpdateGameFAQCommand(
            Id: _testFaqId,
            Question: "Valid question?",
            Answer: longAnswer
        );

        var handler = _serviceProvider!.GetRequiredService<UpdateGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*FAQ answer cannot exceed 5000 characters*");
    }
}
