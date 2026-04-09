using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Unit tests for VectorDocumentIndexedForKbFlagHandler.
/// S2 of library-to-game epic — maintains the denormalized
/// <c>has_knowledge_base</c> column on <c>shared_games</c> in response to
/// VectorDocumentIndexedEvent notifications from the KnowledgeBase BC.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class VectorDocumentIndexedForKbFlagHandlerTests
{
    private readonly Mock<ILogger<VectorDocumentIndexedForKbFlagHandler>> _logger = new();

    private static SharedGameEntity CreateSharedGame(Guid id, bool hasKb = false) =>
        new()
        {
            Id = id,
            Title = "Test Game",
            YearPublished = 2020,
            Description = "Desc",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 30,
            MinAge = 8,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            HasKnowledgeBase = hasKb,
        };

    private static VectorDocumentEntity CreateVectorDocument(Guid id, Guid? sharedGameId, Guid? gameId = null) =>
        new()
        {
            Id = id,
            GameId = gameId,
            SharedGameId = sharedGameId,
            PdfDocumentId = Guid.NewGuid(),
            ChunkCount = 42,
            TotalCharacters = 10000,
            IndexingStatus = "completed",
            EmbeddingModel = "nomic-embed-text",
            EmbeddingDimensions = 768,
        };

    [Fact]
    public async Task Handle_VectorDocumentWithSharedGameId_FlipsHasKnowledgeBaseToTrue()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        db.VectorDocuments.Add(CreateVectorDocument(documentId, sharedGameId));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, _logger.Object);
        var evt = new VectorDocumentIndexedEvent(documentId, sharedGameId, 42);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        var updated = await db.SharedGames.FindAsync(sharedGameId);
        updated.Should().NotBeNull();
        updated!.HasKnowledgeBase.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_VectorDocumentWithNullSharedGameId_DoesNotUpdateAnyGame()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var unrelatedGameId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: false));
        db.VectorDocuments.Add(CreateVectorDocument(documentId, sharedGameId: null, gameId: unrelatedGameId));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, _logger.Object);
        var evt = new VectorDocumentIndexedEvent(documentId, unrelatedGameId, 42);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        var shouldNotChange = await db.SharedGames.FindAsync(sharedGameId);
        shouldNotChange!.HasKnowledgeBase.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_VectorDocumentNotFound_DoesNotThrow()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new VectorDocumentIndexedForKbFlagHandler(db, _logger.Object);
        var evt = new VectorDocumentIndexedEvent(Guid.NewGuid(), Guid.NewGuid(), 42);

        // Act
        var act = async () => await handler.Handle(evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_SharedGameAlreadyHasKnowledgeBase_IsIdempotent()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateSharedGame(sharedGameId, hasKb: true));
        db.VectorDocuments.Add(CreateVectorDocument(documentId, sharedGameId));
        await db.SaveChangesAsync();

        var handler = new VectorDocumentIndexedForKbFlagHandler(db, _logger.Object);
        var evt = new VectorDocumentIndexedEvent(documentId, sharedGameId, 42);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — still true, no exception
        var stillTrue = await db.SharedGames.FindAsync(sharedGameId);
        stillTrue!.HasKnowledgeBase.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new VectorDocumentIndexedForKbFlagHandler(db, _logger.Object);

        // Act
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
