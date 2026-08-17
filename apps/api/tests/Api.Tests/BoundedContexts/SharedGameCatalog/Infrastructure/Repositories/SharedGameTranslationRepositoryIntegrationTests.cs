using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Integration tests for <see cref="SharedGameTranslationRepository"/> against a
/// real PostgreSQL database (Testcontainers). Issue #2339 — Wave 2 / Task 6.
/// </summary>
/// <remarks>
/// Covers:
/// <list type="number">
///   <item>Round-trip persistence via AddAsync + GetByGameIdAndLocaleAsync.</item>
///   <item>Batch fetch GetByGameIdsAsync excludes soft-deleted rows.</item>
///   <item>ExistsActiveAsync flips false after SoftDelete (HasQueryFilter honoured).</item>
///   <item>Partial unique index allows recreating (game, locale) after soft-delete.</item>
///   <item>Optimistic concurrency via xmin throws DbUpdateConcurrencyException.</item>
/// </list>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameTranslationRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private SharedGameTranslationRepository _repository = null!;
    private Guid _testUserId;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public SharedGameTranslationRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_sharedgametranslation_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"trans-test-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _repository = new SharedGameTranslationRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // 1. AddAsync + GetByGameIdAndLocaleAsync round-trip
    // ============================================================

    [Fact]
    public async Task AddAsync_PersistsAndReturnsViaGet()
    {
        // Arrange
        var gameId = await SeedSharedGameAsync("Catan");
        var translation = SharedGameTranslation.Create(
            gameId,
            Locale.Create("it"),
            "I Coloni di Catan",
            "Costruisci e scambia sull'isola di Catan",
            TranslationSource.Manual,
            _testUserId,
            DateTimeOffset.UtcNow);

        // Act
        await _repository.AddAsync(translation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var loaded = await _repository.GetByGameIdAndLocaleAsync(gameId, "it");
        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("I Coloni di Catan");
        loaded.Description.Should().Be("Costruisci e scambia sull'isola di Catan");
        loaded.Locale.Value.Should().Be("it");
        loaded.Source.Should().Be(TranslationSource.Manual);
        loaded.SharedGameId.Should().Be(gameId);
        loaded.IsDeleted.Should().BeFalse();
        loaded.Xmin.Should().NotBe(0u, "PostgreSQL assigns a non-zero xmin to every row");
    }

    // ============================================================
    // 2. GetByGameIdsAsync batch fetch excludes soft-deleted
    // ============================================================

    [Fact]
    public async Task GetByGameIdsAsync_BatchFetchesExcludingDeleted()
    {
        // Arrange
        var gameAId = await SeedSharedGameAsync("Game A");
        var gameBId = await SeedSharedGameAsync("Game B");
        var now = DateTimeOffset.UtcNow;

        var ta = SharedGameTranslation.Create(
            gameAId, Locale.Create("it"), "Gioco A", null,
            TranslationSource.Manual, _testUserId, now);
        var tbItalian = SharedGameTranslation.Create(
            gameBId, Locale.Create("it"), "Gioco B", null,
            TranslationSource.Manual, _testUserId, now);
        var tbFrenchDeleted = SharedGameTranslation.Create(
            gameBId, Locale.Create("fr"), "Jeu B", null,
            TranslationSource.Manual, _testUserId, now);
        tbFrenchDeleted.SoftDelete(_testUserId, now);

        await _repository.AddAsync(ta);
        await _repository.AddAsync(tbItalian);
        await _repository.AddAsync(tbFrenchDeleted);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByGameIdsAsync(new[] { gameAId, gameBId });

        // Assert
        result.Should().ContainKey(gameAId);
        result[gameAId].Should().HaveCount(1);
        result[gameAId][0].Locale.Value.Should().Be("it");

        result.Should().ContainKey(gameBId);
        // The French translation is soft-deleted, so only the Italian one survives the global query filter.
        result[gameBId].Should().HaveCount(1);
        result[gameBId][0].Locale.Value.Should().Be("it");
    }

    // ============================================================
    // 3. ExistsActiveAsync flips after SoftDelete (HasQueryFilter)
    // ============================================================

    [Fact]
    public async Task ExistsActiveAsync_True_AfterAdd_False_AfterSoftDelete()
    {
        // Arrange
        var gameId = await SeedSharedGameAsync("Game");
        var translation = SharedGameTranslation.Create(
            gameId, Locale.Create("it"), "Titolo", null,
            TranslationSource.Manual, _testUserId, DateTimeOffset.UtcNow);
        await _repository.AddAsync(translation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act + Assert (after add)
        (await _repository.ExistsActiveAsync(gameId, "it")).Should().BeTrue();

        // Reload, soft-delete, save
        var entity = await _dbContext.SharedGameTranslations
            .FirstAsync(t => t.SharedGameId == gameId && t.Locale == "it");
        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act + Assert (after soft-delete)
        // ExistsActiveAsync uses AnyAsync over the DbSet, which is filtered by HasQueryFilter(t => !t.IsDeleted),
        // so a soft-deleted row is invisible.
        (await _repository.ExistsActiveAsync(gameId, "it")).Should().BeFalse();
    }

    // ============================================================
    // 4. Partial unique index allows recreate after soft-delete
    // ============================================================

    [Fact]
    public async Task PartialUniqueIndex_AllowsRecreateAfterSoftDelete()
    {
        // Arrange
        var gameId = await SeedSharedGameAsync("Game");
        var now = DateTimeOffset.UtcNow;

        var first = SharedGameTranslation.Create(
            gameId, Locale.Create("it"), "Vecchio", null,
            TranslationSource.Manual, _testUserId, now);
        await _repository.AddAsync(first);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Soft-delete the existing row (direct EF update — repository will
        // add SoftDelete plumbing in Wave 3).
        var firstEntity = await _dbContext.SharedGameTranslations
            .FirstAsync(t => t.SharedGameId == gameId && t.Locale == "it");
        firstEntity.IsDeleted = true;
        firstEntity.DeletedAt = now;
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act — insert a fresh row for the same (game, locale). The partial
        // unique index has WHERE NOT is_deleted, so the constraint does not
        // fire even though a soft-deleted row with the same key exists.
        var second = SharedGameTranslation.Create(
            gameId, Locale.Create("it"), "Nuovo", null,
            TranslationSource.Manual, _testUserId, now.AddHours(1));
        await _repository.AddAsync(second);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        (await _repository.ExistsActiveAsync(gameId, "it")).Should().BeTrue();
        var active = await _repository.GetByGameIdAndLocaleAsync(gameId, "it");
        active.Should().NotBeNull();
        active!.Title.Should().Be("Nuovo");
        active.Id.Should().Be(second.Id);
    }

    // ============================================================
    // 5. UpdateAsync concurrent edit throws DbUpdateConcurrencyException
    // ============================================================

    [Fact]
    public async Task UpdateAsync_ConcurrentEdit_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange: seed an initial translation in scope A.
        var gameId = await SeedSharedGameAsync("Game");
        var initial = SharedGameTranslation.Create(
            gameId, Locale.Create("it"), "Titolo iniziale", null,
            TranslationSource.Manual, _testUserId, DateTimeOffset.UtcNow);
        await _repository.AddAsync(initial);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Scope A snapshots the aggregate (stale xmin will live here).
        var staleAggregate = await _repository.GetByGameIdAndLocaleAsync(gameId, "it");
        staleAggregate.Should().NotBeNull();

        // Scope B simulates another transaction that updates the row first,
        // which bumps xmin server-side. Use a separate DbContext so we don't
        // pollute scope A's change tracker.
        await using (var scopeBContext = _fixture.CreateDbContext(_connectionString))
        {
            var scopeBRepo = new SharedGameTranslationRepository(scopeBContext);
            var scopeBAggregate = await scopeBRepo.GetByGameIdAndLocaleAsync(gameId, "it");
            scopeBAggregate.Should().NotBeNull();
            scopeBAggregate!.UpdateTitle(
                "Aggiornato dall'altro", _testUserId, DateTimeOffset.UtcNow);
            await scopeBRepo.UpdateAsync(scopeBAggregate);
            await scopeBContext.SaveChangesAsync();
        }

        // Act: scope A tries to update with its stale xmin.
        staleAggregate!.UpdateTitle("Tentativo stale", _testUserId, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(staleAggregate);

        Func<Task> save = async () => await _dbContext.SaveChangesAsync();

        // Assert
        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<Guid> SeedSharedGameAsync(string title)
    {
        var id = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            YearPublished = 2020,
            Description = "Seeded game for translation repository tests",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return id;
    }
}
