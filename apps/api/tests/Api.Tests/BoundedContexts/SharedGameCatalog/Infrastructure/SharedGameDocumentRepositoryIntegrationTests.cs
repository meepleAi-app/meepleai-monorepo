using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for SharedGameDocumentRepository.
/// Tests: SearchByTagsAsync using optimized JSONB ?| operator with GIN index.
/// Issue #2409 - Optimize tag search with JSONB operators.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameDocumentRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private ISharedGameDocumentRepository _repository = null!;
    private ISharedGameRepository _gameRepository = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public SharedGameDocumentRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sharedgamedoc_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique test database using fixture helper
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Build DbContext with test database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Mock MediatR and DomainEventCollector (required by DbContext)
        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns((IReadOnlyList<IDomainEvent>)new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        // Initialize repositories
        _repository = new SharedGameDocumentRepository(_dbContext);
        _gameRepository = new SharedGameRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region SearchByTagsAsync Tests

    [Fact]
    public async Task SearchByTagsAsync_WithMatchingTag_ReturnsDocuments()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var document = CreateTestHomeruleDocument(game.Id, ["speed-mode", "2-players"]);
        await _repository.AddAsync(document);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.SearchByTagsAsync(["speed-mode"]);

        // Assert
        Assert.Single(results);
        Assert.Equal(document.Id, results[0].Id);
        Assert.Contains("speed-mode", results[0].Tags);
    }

    [Fact]
    public async Task SearchByTagsAsync_WithMultipleTags_ReturnsMatchingDocuments()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var doc1 = CreateTestHomeruleDocument(game.Id, ["speed-mode", "2-players"]);
        var doc2 = CreateTestHomeruleDocument(game.Id, ["solo-variant", "easy-mode"]);
        var doc3 = CreateTestHomeruleDocument(game.Id, ["speed-mode", "solo-variant"]);

        await _repository.AddAsync(doc1);
        await _repository.AddAsync(doc2);
        await _repository.AddAsync(doc3);
        await _dbContext.SaveChangesAsync();

        // Act - Search for documents with ANY of these tags (OR semantics)
        var results = await _repository.SearchByTagsAsync(["speed-mode"]);

        // Assert - Should return doc1 and doc3 (both have speed-mode)
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("speed-mode", r.Tags));
    }

    [Fact]
    public async Task SearchByTagsAsync_WithNoMatchingTags_ReturnsEmptyList()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var document = CreateTestHomeruleDocument(game.Id, ["speed-mode"]);
        await _repository.AddAsync(document);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.SearchByTagsAsync(["non-existent-tag"]);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchByTagsAsync_WithEmptyTagList_ReturnsEmptyList()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var document = CreateTestHomeruleDocument(game.Id, ["speed-mode"]);
        await _repository.AddAsync(document);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.SearchByTagsAsync([]);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchByTagsAsync_WithCaseInsensitiveTag_ReturnsMatchingDocuments()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var document = CreateTestHomeruleDocument(game.Id, ["speed-mode"]);
        await _repository.AddAsync(document);
        await _dbContext.SaveChangesAsync();

        // Act - Search with uppercase variant
        var results = await _repository.SearchByTagsAsync(["SPEED-MODE"]);

        // Assert - Should find the document (tags are normalized to lowercase)
        Assert.Single(results);
    }

    [Fact]
    public async Task SearchByTagsAsync_OnlyReturnsHomeruleDocuments()
    {
        // Arrange
        var game = await CreateTestGameAsync();

        // Create a Homerule with tags
        var homerule = CreateTestHomeruleDocument(game.Id, ["variant"]);
        await _repository.AddAsync(homerule);

        // Create a Rulebook (no tags, wrong document type)
        var rulebook = SharedGameDocument.Create(
            game.Id,
            Guid.NewGuid(),
            SharedGameDocumentType.Rulebook,
            "1.0",
            TestUserId);
        await _repository.AddAsync(rulebook);
        await _dbContext.SaveChangesAsync();

        // Act - The search should only find Homerule documents
        var results = await _repository.SearchByTagsAsync(["variant"]);

        // Assert
        Assert.Single(results);
        Assert.Equal(SharedGameDocumentType.Homerule, results[0].DocumentType);
    }

    [Fact]
    public async Task SearchByTagsAsync_WithMultipleTagsInSearch_ReturnsDocumentsMatchingAnyTag()
    {
        // Arrange
        var game = await CreateTestGameAsync();
        var doc1 = CreateTestHomeruleDocument(game.Id, ["solo-variant"]);
        var doc2 = CreateTestHomeruleDocument(game.Id, ["2-players"]);
        var doc3 = CreateTestHomeruleDocument(game.Id, ["competitive-mode"]);

        await _repository.AddAsync(doc1);
        await _repository.AddAsync(doc2);
        await _repository.AddAsync(doc3);
        await _dbContext.SaveChangesAsync();

        // Act - Search for documents with ANY of these tags (OR semantics)
        var results = await _repository.SearchByTagsAsync(["solo-variant", "2-players"]);

        // Assert - Should return doc1 and doc2
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Tags.Contains("solo-variant"));
        Assert.Contains(results, r => r.Tags.Contains("2-players"));
    }

    #endregion

    #region Helper Methods

    private async Task<SharedGame> CreateTestGameAsync()
    {
        var game = SharedGame.Create(
            title: $"Test Game {Guid.NewGuid():N}",
            yearPublished: 2020,
            description: "Test game for document repository tests",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: TestUserId);

        await _gameRepository.AddAsync(game);
        await _dbContext.SaveChangesAsync();

        return game;
    }

    private static SharedGameDocument CreateTestHomeruleDocument(Guid gameId, List<string> tags)
    {
        // Issue #2541: Fix DocumentVersion format (must be MAJOR.MINOR like "1.0", "2.1")
        // Use Guid hash for deterministic but varied version numbers in tests
        var hash = Math.Abs(Guid.NewGuid().GetHashCode());
        var version = $"{hash % 10}.{hash % 100}";

        return SharedGameDocument.Create(
            gameId,
            Guid.NewGuid(),
            SharedGameDocumentType.Homerule,
            version,
            TestUserId,
            tags);
    }

    #endregion
}
