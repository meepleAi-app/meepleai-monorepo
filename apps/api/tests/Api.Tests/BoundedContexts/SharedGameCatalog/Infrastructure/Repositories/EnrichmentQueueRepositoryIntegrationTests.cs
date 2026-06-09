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
/// Integration tests for <see cref="EnrichmentQueueRepository"/> (#1874).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1874")]
public sealed class EnrichmentQueueRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private EnrichmentQueueRepository _repository = null!;
    private Guid _testUserId;
    private Guid _gameAlphaId;
    private Guid _gameBetaId;
    private Guid _gameGammaId;
    private string _databaseName = null!;

    public EnrichmentQueueRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_enrichqueue_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(connectionString);
        await _dbContext.Database.MigrateAsync();

        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"enrich-q-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });

        _gameAlphaId = await SeedSharedGameAsync("Alpha Game");
        _gameBetaId = await SeedSharedGameAsync("Beta Game");
        _gameGammaId = await SeedSharedGameAsync("Gamma Game");

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _repository = new EnrichmentQueueRepository(_dbContext, CreateEventCollector());
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task GetPendingAsync_EmptyQueue_ReturnsEmptyResult()
    {
        var (items, total) = await _repository.GetPendingAsync(priority: null, limit: 25);

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetPendingAsync_OrdersByPriorityDescThenQueuedAtAsc()
    {
        // Queue order: stale (oldest), normal, high — expect ordering high → normal → stale.
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Stale, "stale skeletons", null, addedSecondsAgo: 30);
        await EnqueueAsync(_gameBetaId, EnrichmentPriority.Normal, "manual enqueue", _testUserId, addedSecondsAgo: 20);
        await EnqueueAsync(_gameGammaId, EnrichmentPriority.High, "errata", _testUserId, addedSecondsAgo: 10);

        var (items, total) = await _repository.GetPendingAsync(priority: null, limit: 25);

        total.Should().Be(3);
        items.Should().HaveCount(3);
        items[0].Entry.Priority.Should().Be(EnrichmentPriority.High);
        items[1].Entry.Priority.Should().Be(EnrichmentPriority.Normal);
        items[2].Entry.Priority.Should().Be(EnrichmentPriority.Stale);

        items[0].SharedGameTitle.Should().Be("Gamma Game");
        items[1].SharedGameTitle.Should().Be("Beta Game");
    }

    [Fact]
    public async Task GetPendingAsync_FilterByPriority_ReturnsOnlyMatching()
    {
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.High, "errata", _testUserId);
        await EnqueueAsync(_gameBetaId, EnrichmentPriority.Normal, "manual", _testUserId);
        await EnqueueAsync(_gameGammaId, EnrichmentPriority.Stale, "stale", null);

        var (items, total) = await _repository.GetPendingAsync(priority: EnrichmentPriority.High, limit: 25);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].Entry.Priority.Should().Be(EnrichmentPriority.High);
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesProcessedEntries()
    {
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Normal, "manual", _testUserId);
        await EnqueueAsync(_gameBetaId, EnrichmentPriority.Normal, "manual", _testUserId);

        // Mark one as processed
        var (loaded, _) = await _repository.GetPendingAsync(priority: null, limit: 25);
        var first = loaded[0].Entry;
        first.MarkProcessed();
        await _repository.UpdateAsync(first);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var (items, total) = await _repository.GetPendingAsync(priority: null, limit: 25);

        total.Should().Be(1);
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesEntriesForSoftDeletedSharedGames()
    {
        // Mark gameAlpha as soft-deleted post-seed
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Normal, "for deleted", _testUserId);
        await EnqueueAsync(_gameBetaId, EnrichmentPriority.Normal, "alive", _testUserId);

        var deleted = await _dbContext.SharedGames.IgnoreQueryFilters()
            .FirstAsync(g => g.Id == _gameAlphaId);
        deleted.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var (items, total) = await _repository.GetPendingAsync(priority: null, limit: 25);

        total.Should().Be(1, "entries pointing to soft-deleted shared games must be filtered out");
        items.Should().OnlyContain(i => i.Entry.SharedGameId == _gameBetaId);
    }

    [Fact]
    public async Task GetPendingAsync_LimitCapsResults()
    {
        for (int i = 0; i < 5; i++)
        {
            var id = await SeedSharedGameAsync($"Game {i}");
            await _dbContext.SaveChangesAsync();
            await EnqueueAsync(id, EnrichmentPriority.Normal, $"#{i}", _testUserId);
        }

        var (items, total) = await _repository.GetPendingAsync(priority: null, limit: 2);

        total.Should().Be(5);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddRangeAsync_PersistsMultipleEntries()
    {
        var entries = new[]
        {
            EnrichmentQueueEntry.Enqueue(_gameAlphaId, EnrichmentPriority.Stale, "batch a", null),
            EnrichmentQueueEntry.Enqueue(_gameBetaId, EnrichmentPriority.Stale, "batch b", null),
        };

        await _repository.AddRangeAsync(entries);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var (items, total) = await _repository.GetPendingAsync(priority: EnrichmentPriority.Stale, limit: 25);

        total.Should().Be(2);
    }

    // ===== #1907 GetPendingForGameAsync — cascade MarkProcessed support =====

    [Fact]
    public async Task GetPendingForGameAsync_EmptyGuid_Throws()
    {
        var act = () => _repository.GetPendingForGameAsync(Guid.Empty);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPendingForGameAsync_NoEntries_ReturnsEmpty()
    {
        var entries = await _repository.GetPendingForGameAsync(_gameAlphaId);
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingForGameAsync_ReturnsAllPendingForTargetGame()
    {
        // Two pending entries for gameAlpha (Normal + Stale priority — both valid).
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Normal, "admin retry", _testUserId);
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Stale, "skeleton sweep", null);
        // Distractor entries for other games — must NOT appear in the result.
        await EnqueueAsync(_gameBetaId, EnrichmentPriority.Normal, "beta unrelated", _testUserId);
        await EnqueueAsync(_gameGammaId, EnrichmentPriority.High, "gamma errata", _testUserId);

        var entries = await _repository.GetPendingForGameAsync(_gameAlphaId);

        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.SharedGameId == _gameAlphaId);
        entries.Select(e => e.Priority).Should().BeEquivalentTo(
            new[] { EnrichmentPriority.Normal, EnrichmentPriority.Stale });
    }

    [Fact]
    public async Task GetPendingForGameAsync_ExcludesProcessedEntries()
    {
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Normal, "first", _testUserId);
        await EnqueueAsync(_gameAlphaId, EnrichmentPriority.Stale, "second", null);

        // Mark the first one as processed — simulates a previous terminal outcome that
        // cascaded MarkProcessed before a new admin-initiated retry was enqueued.
        var (loaded, _) = await _repository.GetPendingAsync(priority: null, limit: 25);
        var first = loaded.First(x => x.Entry.Reason == "first").Entry;
        first.MarkProcessed();
        await _repository.UpdateAsync(first);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var entries = await _repository.GetPendingForGameAsync(_gameAlphaId);

        entries.Should().HaveCount(1);
        entries[0].Reason.Should().Be("second");
        entries[0].IsProcessed.Should().BeFalse();
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

    private async Task EnqueueAsync(
        Guid sharedGameId,
        EnrichmentPriority priority,
        string reason,
        Guid? queuedBy,
        int addedSecondsAgo = 0)
    {
        var entry = EnrichmentQueueEntry.Enqueue(sharedGameId, priority, reason, queuedBy);
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();

        // Backdate when needed so ordering tests can rely on QueuedAt ASC.
        if (addedSecondsAgo > 0)
        {
            var entity = await _dbContext.EnrichmentQueueEntries.FindAsync(entry.Id);
            entity!.QueuedAt = DateTimeOffset.UtcNow.AddSeconds(-addedSecondsAgo);
            await _dbContext.SaveChangesAsync();
        }

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
