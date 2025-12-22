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
/// Comprehensive integration tests for UpvoteGameFAQ workflow (Issue #2186).
/// Tests the complete FAQ upvoting pipeline using Testcontainers for real PostgreSQL infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Upvote FAQ successfully
/// 2. Validation Errors: Non-existent FAQ
/// 3. Concurrency: Multiple upvotes, race conditions
/// 4. Edge Cases: Maximum upvotes (int.MaxValue boundary)
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for UpvoteGameFAQCommandHandler
/// Execution Time Target: <30s per test
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Issue", "2186")]
[Trait("Category", TestCategories.Integration)]
public sealed class UpvoteGameFAQCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testGameId;
    private Guid _testFaqId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UpvoteGameFAQCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_upvotefaq_{Guid.NewGuid():N}";
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
            config.RegisterServicesFromAssembly(typeof(UpvoteGameFAQCommandHandler).Assembly));
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<UpvoteGameFAQCommandHandler>();

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
    public async Task Handle_WithValidId_IncrementsUpvoteCount()
    {
        // Arrange
        var command = new UpvoteGameFAQCommand(_testFaqId);
        var handler = _serviceProvider!.GetRequiredService<UpvoteGameFAQCommandHandler>();

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Upvotes.Should().Be(1);

        // Verify persistence
        var persisted = await _dbContext!.GameFAQs.FindAsync([_testFaqId], TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Upvotes.Should().Be(1);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithNonExistentFAQ_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new UpvoteGameFAQCommand(nonExistentId);
        var handler = _serviceProvider!.GetRequiredService<UpvoteGameFAQCommandHandler>();

        // Act & Assert
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*FAQ with ID {nonExistentId} not found*");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_MultipleUpvotes_IncrementsEachTime()
    {
        // Arrange
        var command = new UpvoteGameFAQCommand(_testFaqId);
        var handler = _serviceProvider!.GetRequiredService<UpvoteGameFAQCommandHandler>();

        // Act
        var result1 = await handler.Handle(command, TestCancellationToken);
        var result2 = await handler.Handle(command, TestCancellationToken);
        var result3 = await handler.Handle(command, TestCancellationToken);

        // Assert
        result1.Upvotes.Should().Be(1);
        result2.Upvotes.Should().Be(2);
        result3.Upvotes.Should().Be(3);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ConcurrentUpvotes_AllPersisted()
    {
        // Arrange - Each concurrent operation needs its own DbContext scope
        var command = new UpvoteGameFAQCommand(_testFaqId);

        // Act - Simulate 5 concurrent upvotes with separate scopes
        var tasks = Enumerable.Range(0, 5)
            .Select(async _ =>
            {
                using var scope = _serviceProvider!.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<UpvoteGameFAQCommandHandler>();
                return await handler.Handle(command, TestCancellationToken);
            })
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Final count should be 5 (using fresh DbContext)
        using var assertScope = _serviceProvider!.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var persisted = await assertDbContext.GameFAQs.FindAsync([_testFaqId], TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Upvotes.Should().Be(5);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_NearMaxUpvotes_ThrowsOnOverflow()
    {
        // Arrange - Manually set upvotes to near int.MaxValue
        var faq = await _dbContext!.GameFAQs.FindAsync([_testFaqId], TestCancellationToken);
        faq.Should().NotBeNull();

        // Directly set Upvotes to int.MaxValue - 1
        faq!.Upvotes = int.MaxValue - 1;
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var command = new UpvoteGameFAQCommand(_testFaqId);
        var handler = _serviceProvider!.GetRequiredService<UpvoteGameFAQCommandHandler>();

        // Act - First upvote should succeed (reaching int.MaxValue)
        var result = await handler.Handle(command, TestCancellationToken);
        result.Upvotes.Should().Be(int.MaxValue);

        // Act & Assert - Second upvote should throw
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum upvotes reached*");
    }
}
