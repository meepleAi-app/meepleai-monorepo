using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
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
/// Integration tests for <see cref="MechanicGoldenBggTagRepository"/> (ADR-051 Sprint 1 / Task 15)
/// against a real PostgreSQL database (Testcontainers).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicGoldenBggTagRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private MechanicGoldenBggTagRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public MechanicGoldenBggTagRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_bgg_tag_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new MechanicGoldenBggTagRepository(_dbContext, mockCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // Happy path: UpsertBatchAsync inserts new tags and is readable via GetByGame
    // ============================================================

    [Fact]
    public async Task UpsertBatchAsync_InsertsNewTags_AndReadsThemBack()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var batch = new[]
        {
            ("Deck Building", "Mechanism"),
            ("Area Control", "Mechanism"),
            ("Fantasy", "Category"),
        };

        // Act
        var insertedCount = await _repository.UpsertBatchAsync(sharedGameId, batch);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var tags = await _repository.GetByGameAsync(sharedGameId);

        // Assert
        insertedCount.Should().Be(3, "all 3 tags are new — none collide on (SharedGameId, Name)");
        tags.Should().HaveCount(3);
        tags.Select(t => t.Name).Should().BeEquivalentTo(new[]
        {
            "Area Control", "Deck Building", "Fantasy",
        });
        tags.Should().OnlyContain(t => t.SharedGameId == sharedGameId);
    }

    // ============================================================
    // Edge case: upsert on duplicate (game, name) leaves existing row untouched
    // ============================================================

    [Fact]
    public async Task UpsertBatchAsync_IsIdempotent_OnDuplicateTagName()
    {
        // Arrange: first pass inserts 2 tags.
        var sharedGameId = await SeedSharedGameAsync();
        var firstInserted = await _repository.UpsertBatchAsync(sharedGameId, new[]
        {
            ("Deck Building", "Mechanism"),
            ("Fantasy", "Category"),
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        firstInserted.Should().Be(2);

        var existingAll = await _dbContext.MechanicGoldenBggTags.AsNoTracking()
            .Where(t => t.SharedGameId == sharedGameId).ToListAsync();
        var originalIds = existingAll.Select(t => t.Id).OrderBy(x => x).ToList();
        originalIds.Should().HaveCount(2);

        // Act: second pass overlaps on "Deck Building" and adds a new tag.
        var secondInserted = await _repository.UpsertBatchAsync(sharedGameId, new[]
        {
            ("Deck Building", "Mechanism"),   // duplicate — must be skipped, not raise unique violation
            ("Area Control", "Mechanism"),    // new
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: only the new tag was added; the duplicate was skipped silently.
        secondInserted.Should().Be(1, "second pass had 1 new tag and 1 duplicate of an existing row");

        // Assert: tag count is 3 (2 original + 1 new), original rows untouched.
        var afterAll = await _dbContext.MechanicGoldenBggTags.AsNoTracking()
            .Where(t => t.SharedGameId == sharedGameId).ToListAsync();
        afterAll.Should().HaveCount(3);
        var afterIds = afterAll.Select(t => t.Id).ToHashSet();
        originalIds.Should().OnlyContain(id => afterIds.Contains(id), "existing rows must be retained verbatim");
    }

    // ============================================================
    // DeleteAsync: removes single tag
    // ============================================================

    [Fact]
    public async Task DeleteAsync_RemovesSingleTag()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        await _repository.UpsertBatchAsync(sharedGameId, new[]
        {
            ("Deck Building", "Mechanism"),
            ("Fantasy", "Category"),
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var target = await _dbContext.MechanicGoldenBggTags.AsNoTracking()
            .FirstAsync(t => t.SharedGameId == sharedGameId
                && string.Equals(t.Name, "Fantasy", StringComparison.Ordinal));

        // Act
        await _repository.DeleteAsync(target.Id);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var remaining = await _repository.GetByGameAsync(sharedGameId);
        remaining.Should().HaveCount(1);
        string.Equals(remaining[0].Name, "Deck Building", StringComparison.Ordinal).Should().BeTrue();
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<Guid> SeedSharedGameAsync()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Tag Test Game",
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 5,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return sharedGameId;
    }
}
