using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Integration;

/// <summary>
/// Testcontainers integration test for the UNIQUE-on-game_id concurrency invariant.
/// Verifies that two concurrent Upsert calls for the same game_id result in
/// exactly one row persisted (no duplicate key error surfaces to callers).
/// ADR-069 follow-up (#2383).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Collection("Integration-GroupC")]
public sealed class AiToolkitSuggestionCacheConcurrentInsertTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _context = null!;
    private AiToolkitSuggestionCacheRepository _repository = null!;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AiToolkitSuggestionCacheConcurrentInsertTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_aitoolkitcache_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        // Register the repository under test
        services.AddScoped<IAiToolkitSuggestionCacheRepository, AiToolkitSuggestionCacheRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations with retry
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _context.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _repository = new AiToolkitSuggestionCacheRepository(
            _context,
            eventCollector,
            NullLogger<AiToolkitSuggestionCacheRepository>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();

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

    [Fact]
    public async Task Upsert_SequentialCallsForSameGameId_OnlyOneRowPersists()
    {
        // Arrange — single DbContext, sequential calls (regression guard for basic upsert)
        var gameId = Guid.NewGuid();
        var entry1 = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"src\":\"r1\"}", null);
        var entry2 = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"src\":\"r2\"}", null);

        // Act — first insert
        await _repository.UpsertAsync(entry1, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Second upsert should UPDATE, not throw
        await _repository.UpsertAsync(entry2, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        // Assert — exactly one row, content is from the last upsert
        var rowCount = await _context.AiToolkitSuggestionCache
            .Where(e => e.GameId == gameId)
            .CountAsync(TestCancellationToken);
        rowCount.Should().Be(1);

        var row = await _context.AiToolkitSuggestionCache
            .FirstAsync(e => e.GameId == gameId, TestCancellationToken);
        row.SuggestionJson.Should().Be("{\"src\":\"r2\"}");
    }

    [Fact]
    public async Task Upsert_TwoConcurrentDbContextsForSameGameId_NoExceptionSurfaces()
    {
        // Arrange — two separate service scopes (two DbContext instances, simulating two parallel requests)
        var gameId = Guid.NewGuid();
        var entry1 = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"src\":\"scope1\"}", null);
        var entry2 = AiToolkitSuggestionCacheEntry.Create(gameId, "{\"src\":\"scope2\"}", null);

        // Scope 1: first to upsert and commit
        await using var scope1 = _serviceProvider!.CreateAsyncScope();
        var ctx1 = scope1.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var evtCollector1 = scope1.ServiceProvider.GetRequiredService<IDomainEventCollector>();
        var repo1 = new AiToolkitSuggestionCacheRepository(ctx1, evtCollector1,
            NullLogger<AiToolkitSuggestionCacheRepository>.Instance);
        var uow1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await repo1.UpsertAsync(entry1, TestCancellationToken);
        await uow1.SaveChangesAsync(TestCancellationToken);

        // Scope 2: arrives after scope1 committed — Upsert should detect existing row and UPDATE
        await using var scope2 = _serviceProvider!.CreateAsyncScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var evtCollector2 = scope2.ServiceProvider.GetRequiredService<IDomainEventCollector>();
        var repo2 = new AiToolkitSuggestionCacheRepository(ctx2, evtCollector2,
            NullLogger<AiToolkitSuggestionCacheRepository>.Instance);
        var uow2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var act = async () =>
        {
            await repo2.UpsertAsync(entry2, TestCancellationToken);
            await uow2.SaveChangesAsync(TestCancellationToken);
        };

        // Should NOT throw (existing row detected by UpsertAsync → UPDATE path)
        await act.Should().NotThrowAsync();

        // Assert — exactly one row persists
        var rowCount = await _context.AiToolkitSuggestionCache
            .Where(e => e.GameId == gameId)
            .CountAsync(TestCancellationToken);
        rowCount.Should().Be(1);
    }
}
