using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Integration tests for <see cref="EnrichmentAttemptRepository"/> (#1874).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1874")]
public sealed class EnrichmentAttemptRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private EnrichmentAttemptRepository _repository = null!;
    private Guid _testUserId;
    private Guid _gameAlphaId;
    private Guid _gameBetaId;
    private string _databaseName = null!;

    public EnrichmentAttemptRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_enrichatt_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(connectionString);
        await _dbContext.Database.MigrateAsync();

        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"enrich-att-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });

        _gameAlphaId = await SeedSharedGameAsync("Twilight Imperium 4E");
        _gameBetaId = await SeedSharedGameAsync("Gloomhaven JOTL");
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _repository = new EnrichmentAttemptRepository(_dbContext, CreateEventCollector());
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task GetFailedAggregatesAsync_NoAttempts_ReturnsEmptyResult()
    {
        var (items, total) = await _repository.GetFailedAggregatesAsync(days: 30, limit: 50);

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetFailedAggregatesAsync_AggregatesByGame_KeepsMostRecentFailureRow()
    {
        // Game alpha: 4 attempts (3 failed with retry 0/1/2, then 1 final fail with retry 3)
        await PersistFailureAsync(_gameAlphaId, "BGG_API_RATE_LIMIT_429", "first try", retryCount: 0, ageSeconds: 1000);
        await PersistFailureAsync(_gameAlphaId, "BGG_API_RATE_LIMIT_429", "second try", retryCount: 1, ageSeconds: 800);
        await PersistFailureAsync(_gameAlphaId, "BGG_API_RATE_LIMIT_429", "third try", retryCount: 2, ageSeconds: 600);
        await PersistFailureAsync(_gameAlphaId, "BGG_API_RATE_LIMIT_429", "final", retryCount: 3, ageSeconds: 100);

        // Game beta: 1 failure SCHEMA_MISMATCH
        await PersistFailureAsync(_gameBetaId, "SCHEMA_MISMATCH", "field missing", retryCount: 1, ageSeconds: 200);

        var (items, total) = await _repository.GetFailedAggregatesAsync(days: 30, limit: 50);

        total.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().BeInDescendingOrder(i => i.LastAttemptAt);

        var alpha = items.Single(i => i.SharedGameId == _gameAlphaId);
        alpha.SharedGameTitle.Should().Be("Twilight Imperium 4E");
        alpha.ErrorCode.Should().Be("BGG_API_RATE_LIMIT_429");
        alpha.RetryCount.Should().Be(3, "the most recent failure's retry count is surfaced");

        var beta = items.Single(i => i.SharedGameId == _gameBetaId);
        beta.ErrorCode.Should().Be("SCHEMA_MISMATCH");
        beta.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task GetFailedAggregatesAsync_FiltersByDaysWindow()
    {
        // Beta: failed 7 days ago — inside 7d window
        await PersistFailureAsync(_gameBetaId, "ERR_RECENT", "recent", retryCount: 0, ageSeconds: 3 * 86400);
        // Alpha: failed 14 days ago — OUTSIDE 7d window
        await PersistFailureAsync(_gameAlphaId, "ERR_OLD", "old", retryCount: 0, ageSeconds: 14 * 86400);

        var (items, total) = await _repository.GetFailedAggregatesAsync(days: 7, limit: 50);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].SharedGameId.Should().Be(_gameBetaId);
    }

    [Fact]
    public async Task GetFailedAggregatesAsync_IgnoresSuccessfulAttempts()
    {
        await PersistSuccessAsync(_gameAlphaId, retryCount: 2);

        var (items, total) = await _repository.GetFailedAggregatesAsync(days: 30, limit: 50);

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task AddAsync_SuccessAttempt_PersistsAllFields()
    {
        var attempt = EnrichmentAttempt.RecordSuccess(_gameAlphaId, catalogSyncRunId: null, retryCount: 0);

        await _repository.AddAsync(attempt);
        await _dbContext.SaveChangesAsync();

        var persisted = await _dbContext.EnrichmentAttempts.FindAsync(attempt.Id);
        persisted.Should().NotBeNull();
        persisted!.Success.Should().BeTrue();
        persisted.ErrorCode.Should().BeNull();
        persisted.CatalogSyncRunId.Should().BeNull();
    }

    // ===== helpers =====

    private async Task<Guid> SeedSharedGameAsync(string title)
    {
        var id = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            YearPublished = 2020,
            Description = "test",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/img.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        return id;
    }

    private async Task PersistFailureAsync(Guid sharedGameId, string code, string detail, int retryCount, int ageSeconds)
    {
        var attempt = EnrichmentAttempt.RecordFailure(sharedGameId, catalogSyncRunId: null, code, detail, retryCount);
        await _repository.AddAsync(attempt);
        await _dbContext.SaveChangesAsync();

        // Backdate the persisted timestamp
        var entity = await _dbContext.EnrichmentAttempts.FindAsync(attempt.Id);
        entity!.AttemptedAt = DateTimeOffset.UtcNow.AddSeconds(-ageSeconds);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task PersistSuccessAsync(Guid sharedGameId, int retryCount)
    {
        var attempt = EnrichmentAttempt.RecordSuccess(sharedGameId, catalogSyncRunId: null, retryCount);
        await _repository.AddAsync(attempt);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private static IDomainEventCollector CreateEventCollector()
    {
        var mock = new Mock<IDomainEventCollector>();
        mock.Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return mock.Object;
    }
}
