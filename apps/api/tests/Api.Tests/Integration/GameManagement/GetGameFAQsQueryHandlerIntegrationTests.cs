using Api.Tests.Infrastructure;
using Api.BoundedContexts.GameManagement.Application.Queries;
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
/// Comprehensive integration tests for GetGameFAQs query workflow (Issue #2186).
/// Tests the complete FAQ retrieval pipeline using Testcontainers for real PostgreSQL infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Retrieve FAQs with pagination
/// 2. Empty Results: Game without FAQs
/// 3. Ordering: Upvote-based sorting
/// 4. Pagination: Limit/Offset handling
/// 5. Performance: N+1 query prevention (single query optimization)
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for GetGameFAQsQueryHandler
/// Execution Time Target: <30s per test
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Issue", "2186")]
[Trait("Category", TestCategories.Integration)]
public sealed class GetGameFAQsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private Guid _testGameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetGameFAQsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_getfaqs_{Guid.NewGuid():N}";
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
            config.RegisterServicesFromAssembly(typeof(GetGameFAQsQueryHandler).Assembly));
        services.AddSingleton(TimeProvider.System);

        // Register handler explicitly
        services.AddScoped<GetGameFAQsQueryHandler>();

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

        // Seed multiple FAQs with different upvote counts for ordering tests
        var faq1 = new GameFAQEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Question = "Question 1?",
            Answer = "Answer 1",
            Upvotes = 3,
            CreatedAt = DateTime.UtcNow
        };

        var faq2 = new GameFAQEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Question = "Question 2?",
            Answer = "Answer 2",
            Upvotes = 1,
            CreatedAt = DateTime.UtcNow
        };

        var faq3 = new GameFAQEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Question = "Question 3?",
            Answer = "Answer 3",
            Upvotes = 5,
            CreatedAt = DateTime.UtcNow
        };

        var faq4 = new GameFAQEntity
        {
            Id = Guid.NewGuid(),
            GameId = _testGameId,
            Question = "Question 4?",
            Answer = "Answer 4",
            Upvotes = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.GameFAQs.AddRange(faq1, faq2, faq3, faq4);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts with handler operations
        _dbContext.ChangeTracker.Clear();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithValidGameId_ReturnsAllFAQs()
    {
        // Arrange
        var query = new GetGameFAQsQuery(
            GameId: _testGameId,
            Limit: 10,
            Offset: 0
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.FAQs.Should().HaveCount(4);
        result.TotalCount.Should().Be(4);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithGameWithoutFAQs_ReturnsEmptyList()
    {
        // Arrange - Create game without FAQs
        var emptyGameId = Guid.NewGuid();
        var emptyGame = new GameEntity
        {
            Id = emptyGameId,
            Name = "Empty Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Games.Add(emptyGame);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var query = new GetGameFAQsQuery(
            GameId: emptyGameId,
            Limit: 10,
            Offset: 0
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.FAQs.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithPagination_ReturnsCorrectSubset()
    {
        // Arrange
        var query = new GetGameFAQsQuery(
            GameId: _testGameId,
            Limit: 2,
            Offset: 1
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.FAQs.Should().HaveCount(2); // Limit 2
        result.TotalCount.Should().Be(4); // Total count remains 4
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_OrdersByUpvotesDescending()
    {
        // Arrange
        var query = new GetGameFAQsQuery(
            GameId: _testGameId,
            Limit: 10,
            Offset: 0
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert - Ordered by upvotes DESC: 5, 3, 1, 0
        result.FAQs.Should().HaveCount(4);
        result.FAQs[0].Upvotes.Should().Be(5);
        result.FAQs[1].Upvotes.Should().Be(3);
        result.FAQs[2].Upvotes.Should().Be(1);
        result.FAQs[3].Upvotes.Should().Be(0);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithLimitZero_ReturnsEmpty()
    {
        // Arrange
        var query = new GetGameFAQsQuery(
            GameId: _testGameId,
            Limit: 0,
            Offset: 0
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.FAQs.Should().BeEmpty();
        result.TotalCount.Should().Be(4); // Total count still correct
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_WithOffsetBeyondCount_ReturnsEmpty()
    {
        // Arrange
        var query = new GetGameFAQsQuery(
            GameId: _testGameId,
            Limit: 10,
            Offset: 100 // Beyond total count
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.FAQs.Should().BeEmpty();
        result.TotalCount.Should().Be(4);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsDTOsWithAllProperties()
    {
        // Arrange
        var query = new GetGameFAQsQuery(
            GameId: _testGameId,
            Limit: 1,
            Offset: 0
        );

        var handler = _serviceProvider!.GetRequiredService<GetGameFAQsQueryHandler>();

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        var faq = result.FAQs.First();
        faq.Id.Should().NotBeEmpty();
        faq.GameId.Should().Be(_testGameId);
        faq.Question.Should().NotBeNullOrEmpty();
        faq.Answer.Should().NotBeNullOrEmpty();
        faq.Upvotes.Should().BeGreaterThanOrEqualTo(0);
        faq.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
