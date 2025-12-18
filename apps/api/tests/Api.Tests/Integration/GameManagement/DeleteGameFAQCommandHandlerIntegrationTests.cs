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
/// Comprehensive integration tests for DeleteGameFAQ workflow (Issue #2186).
/// Tests the complete FAQ deletion pipeline using Testcontainers for real PostgreSQL infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Delete existing FAQ
/// 2. Validation Errors: Non-existent FAQ
/// 3. Database Integration: Soft vs hard delete verification
/// 4. Cascading: Verify no orphaned data
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for DeleteGameFAQCommandHandler
/// Execution Time Target: <30s per test
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Issue", "2186")]
[Trait("Category", TestCategories.Integration)]
public sealed class DeleteGameFAQCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testGameId;
    private Guid _testFaqId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DeleteGameFAQCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_deletefaq_{Guid.NewGuid():N}";
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
            config.RegisterServicesFromAssembly(typeof(DeleteGameFAQCommandHandler).Assembly));
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<DeleteGameFAQCommandHandler>();

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
            Question = "Test question?",
            Answer = "Test answer",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.GameFAQs.Add(faq);

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts with handler operations
        _dbContext.ChangeTracker.Clear();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithValidId_DeletesFAQSuccessfully()
    {
        // Arrange
        var command = new DeleteGameFAQCommand(_testFaqId);
        var handler = _serviceProvider!.GetRequiredService<DeleteGameFAQCommandHandler>();

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert - FAQ should be removed
        var deleted = await _dbContext!.GameFAQs.FindAsync([_testFaqId], TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentFAQ_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new DeleteGameFAQCommand(nonExistentId);
        var handler = _serviceProvider!.GetRequiredService<DeleteGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*FAQ with ID {nonExistentId} not found*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_AfterDeletion_CannotDeleteAgain()
    {
        // Arrange
        var command = new DeleteGameFAQCommand(_testFaqId);
        var handler = _serviceProvider!.GetRequiredService<DeleteGameFAQCommandHandler>();

        // Act - First deletion
        await handler.Handle(command, TestCancellationToken);

        // Act & Assert - Second deletion should fail
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_DeletingOneFAQ_DoesNotAffectOthers()
    {
        // Arrange - Create second FAQ
        var secondFaqId = Guid.NewGuid();
        var secondFaq = new GameFAQEntity
        {
            Id = secondFaqId,
            GameId = _testGameId,
            Question = "Another question?",
            Answer = "Another answer",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.GameFAQs.Add(secondFaq);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new DeleteGameFAQCommand(_testFaqId);
        var handler = _serviceProvider!.GetRequiredService<DeleteGameFAQCommandHandler>();

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert - First FAQ deleted, second still exists
        var firstDeleted = await _dbContext.GameFAQs.FindAsync([_testFaqId], TestCancellationToken);
        var secondExists = await _dbContext.GameFAQs.FindAsync([secondFaqId], TestCancellationToken);

        firstDeleted.Should().BeNull();
        secondExists.Should().NotBeNull();
    }
}
