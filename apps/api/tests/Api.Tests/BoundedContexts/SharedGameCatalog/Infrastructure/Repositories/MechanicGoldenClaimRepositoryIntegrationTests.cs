using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
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
/// Integration tests for <see cref="MechanicGoldenClaimRepository"/> (ADR-051 Sprint 1 / Task 15)
/// against a real PostgreSQL database (Testcontainers).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicGoldenClaimRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private MechanicGoldenClaimRepository _repository = null!;
    private MechanicGoldenBggTagRepository _tagRepository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private Guid _curatorUserId;

    public MechanicGoldenClaimRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_golden_claim_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        // Seed a user row so the FK on curator_user_id is satisfiable
        // (FK was added in Sprint 1 migration 20260424094641 with ON DELETE RESTRICT).
        _curatorUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _curatorUserId,
            Email = $"curator-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new MechanicGoldenClaimRepository(_dbContext, mockCollector.Object);
        _tagRepository = new MechanicGoldenBggTagRepository(_dbContext, mockCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // Happy path: AddAsync + GetByIdAsync + GetByGameAsync round-trip
    // ============================================================

    [Fact]
    public async Task AddAsync_AndReload_PersistsAllFields()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var claim = await BuildClaimAsync(sharedGameId, section: MechanicSection.Mechanics,
            statement: "Players draft cards clockwise.", page: 3);

        // Act
        await _repository.AddAsync(claim);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var byId = await _repository.GetByIdAsync(claim.Id);
        var byGame = await _repository.GetByGameAsync(sharedGameId);

        // Assert
        byId.Should().NotBeNull();
        byId!.SharedGameId.Should().Be(sharedGameId);
        byId.Statement.Should().Be("Players draft cards clockwise.");
        byId.ExpectedPage.Should().Be(3);
        byId.Section.Should().Be(MechanicSection.Mechanics);
        byId.DeletedAt.Should().BeNull();

        byGame.Should().HaveCount(1);
        byGame[0].Id.Should().Be(claim.Id);
    }

    // ============================================================
    // Soft delete: global query filter hides deactivated claims
    // ============================================================

    [Fact]
    public async Task SoftDeletedClaim_IsHiddenFromDefaultQueries()
    {
        // Arrange: two claims, one deactivated.
        var sharedGameId = await SeedSharedGameAsync();
        var alive = await BuildClaimAsync(sharedGameId, MechanicSection.Victory,
            "First to 20 points wins.", 5);
        var dead = await BuildClaimAsync(sharedGameId, MechanicSection.Faq,
            "Can you trade cards? Yes.", 12);
        await _repository.AddAsync(alive);
        await _repository.AddAsync(dead);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(dead.Id);
        loaded!.Deactivate();
        await _repository.UpdateAsync(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var byGame = await _repository.GetByGameAsync(sharedGameId);
        var byIdHidden = await _repository.GetByIdAsync(dead.Id);

        // Assert: filter hides the soft-deleted row.
        byGame.Should().HaveCount(1);
        byGame[0].Id.Should().Be(alive.Id);
        byIdHidden.Should().BeNull();
    }

    // ============================================================
    // GetVersionHashAsync: loads claim triples + tag names and calls VersionHash.Compute
    // ============================================================

    [Fact]
    public async Task GetVersionHashAsync_ReturnsNull_WhenNoClaims()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();

        // Act
        var hash = await _repository.GetVersionHashAsync(sharedGameId);

        // Assert
        hash.Should().BeNull();
    }

    [Fact]
    public async Task GetVersionHashAsync_IsDeterministic_AcrossCallsWithSameData()
    {
        // Arrange: 2 claims + 2 tags.
        var sharedGameId = await SeedSharedGameAsync();
        var claim1 = await BuildClaimAsync(sharedGameId, MechanicSection.Mechanics, "Draw one card.", 2);
        var claim2 = await BuildClaimAsync(sharedGameId, MechanicSection.Phases, "Each round has 4 phases.", 4);
        await _repository.AddAsync(claim1);
        await _repository.AddAsync(claim2);
        await _tagRepository.UpsertBatchAsync(sharedGameId, new[]
        {
            ("Deck Building", "Mechanism"),
            ("Area Control", "Mechanism"),
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var hash1 = await _repository.GetVersionHashAsync(sharedGameId);
        _dbContext.ChangeTracker.Clear();
        var hash2 = await _repository.GetVersionHashAsync(sharedGameId);

        // Assert
        hash1.Should().NotBeNull();
        hash1!.Value.Should().HaveLength(64);
        hash2.Should().NotBeNull();
        hash2!.Value.Should().Be(hash1.Value);
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
            Title = "Golden Test Game",
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return sharedGameId;
    }

    private Task<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicGoldenClaim> BuildClaimAsync(
        Guid sharedGameId, MechanicSection section, string statement, int page)
    {
        var embedding = new StubEmbeddingService();
        var keywords = new StubKeywordExtractor();
        return Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicGoldenClaim.CreateAsync(
            sharedGameId: sharedGameId,
            section: section,
            statement: statement,
            expectedPage: page,
            sourceQuote: "Rulebook p." + page + ": " + statement,
            curatorUserId: _curatorUserId,
            embedding: embedding,
            keywords: keywords,
            ct: CancellationToken.None);
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct)
            => Task.FromResult(new float[768]);
    }

    private sealed class StubKeywordExtractor : IKeywordExtractor
    {
        public string[] Extract(string text)
            => text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
