using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for SharedGameDocumentRepository.
/// Tests: SearchByTagsAsync using optimized JSONB ?| operator with GIN index.
/// Issue #2409 - Optimize tag search with JSONB operators.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameDocumentRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private ISharedGameDocumentRepository _repository = null!;
    private ISharedGameRepository _gameRepository = null!;
    private Guid _testUserId;
    private int _versionCounter = 0; // For unique document versions

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
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .Options;

        // Mock MediatR and DomainEventCollector (required by DbContext)
        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns((IReadOnlyList<IDomainEvent>)new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        // Create test user to satisfy FK constraints
        _testUserId = Guid.NewGuid();
        var testUser = new UserEntity
        {
            Id = _testUserId,
            Email = "test@meepleai.com",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(testUser);
        await _dbContext.SaveChangesAsync();

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
        var document = await CreateTestHomeruleDocumentAsync(game.Id, ["speed-mode", "2-players"]);
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
        var doc1 = await CreateTestHomeruleDocumentAsync(game.Id, ["speed-mode", "2-players"]);
        var doc2 = await CreateTestHomeruleDocumentAsync(game.Id, ["solo-variant", "easy-mode"]);
        var doc3 = await CreateTestHomeruleDocumentAsync(game.Id, ["speed-mode", "solo-variant"]);

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
        var document = await CreateTestHomeruleDocumentAsync(game.Id, ["speed-mode"]);
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
        var document = await CreateTestHomeruleDocumentAsync(game.Id, ["speed-mode"]);
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
        var document = await CreateTestHomeruleDocumentAsync(game.Id, ["speed-mode"]);
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
        var homerule = await CreateTestHomeruleDocumentAsync(game.Id, ["variant"]);
        await _repository.AddAsync(homerule);

        // Create a Rulebook (no tags, wrong document type)
        var pdfDoc = await CreateTestPdfDocumentAsync(game.Id);
        var rulebook = SharedGameDocument.Create(
            game.Id,
            pdfDoc.Id,
            SharedGameDocumentType.Rulebook,
            "1.0",
            _testUserId);
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
        var doc1 = await CreateTestHomeruleDocumentAsync(game.Id, ["solo-variant"]);
        var doc2 = await CreateTestHomeruleDocumentAsync(game.Id, ["2-players"]);
        var doc3 = await CreateTestHomeruleDocumentAsync(game.Id, ["competitive-mode"]);

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
        var gameId = Guid.NewGuid();
        var gameName = $"Test Game {Guid.NewGuid():N}";

        // Create GameEntity first to satisfy PdfDocument FK constraint
        var gameEntity = new GameEntity
        {
            Id = gameId,
            Name = gameName,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(gameEntity);

        // Create SharedGameEntity directly (avoids reflection issues)
        var sharedGameEntity = new SharedGameEntity
        {
            Id = gameId, // Same ID as GameEntity
            Title = gameName,
            YearPublished = 2020,
            Description = "Test game for document repository tests",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = 2.0m,
            AverageRating = 7.0m,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = 0, // Draft
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _dbContext.Set<SharedGameEntity>().Add(sharedGameEntity);
        await _dbContext.SaveChangesAsync();

        // Return domain model (repository will map from entity)
        var game = await _gameRepository.GetByIdAsync(gameId);
        return game!;
    }

    /// <summary>
    /// Creates a test PdfDocument entity and persists to database.
    /// Fix for Issue #2538 - Use SharedGame's ID for PdfDocument to satisfy FK.
    /// </summary>
    private async Task<PdfDocumentEntity> CreateTestPdfDocumentAsync(Guid sharedGameId)
    {
        // Use SharedGame's ID directly (SharedGame ID matches GameEntity ID)
        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId, // FK to games table (same as shared_games ID)
            FileName = $"test_{Guid.NewGuid():N}.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _testUserId,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "completed"
        };

        _dbContext.PdfDocuments.Add(pdfDoc);
        await _dbContext.SaveChangesAsync();

        return pdfDoc;
    }

    /// <summary>
    /// Creates a test Homerule document with valid PdfDocument FK and version format.
    /// Fix for Issue #2538 - Use valid MAJOR.MINOR version format and ensure FK exists.
    /// Uses incrementing version counter to avoid unique constraint violations.
    /// </summary>
    private async Task<SharedGameDocument> CreateTestHomeruleDocumentAsync(Guid gameId, List<string> tags)
    {
        // Create PdfDocument first to satisfy FK constraint (uses same gameId)
        var pdfDoc = await CreateTestPdfDocumentAsync(gameId);

        // Use valid MAJOR.MINOR format with incrementing counter for uniqueness
        var version = $"1.{_versionCounter++}";

        return SharedGameDocument.Create(
            gameId,
            pdfDoc.Id,
            SharedGameDocumentType.Homerule,
            version,
            _testUserId,
            tags);
    }

    #endregion
}
