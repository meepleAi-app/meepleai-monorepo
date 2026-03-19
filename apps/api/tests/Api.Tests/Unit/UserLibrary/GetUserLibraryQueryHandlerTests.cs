using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// Unit tests for GetUserLibraryQueryHandler.
/// Issue #3208: Verify hasPdfDocuments field population.
/// Issue #4998: Updated to verify KB-aware fields (hasKb, kbCardCount, kbIndexedCount, kbProcessingCount).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetUserLibraryQueryHandlerTests : IDisposable
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo;
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetUserLibraryQueryHandler _handler;

    public GetUserLibraryQueryHandlerTests()
    {
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockSharedGameRepo = new Mock<ISharedGameRepository>();

        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        _handler = new GetUserLibraryQueryHandler(
            _mockLibraryRepo.Object,
            _mockSharedGameRepo.Object,
            _dbContext);
    }

    [Fact]
    public async Task Handle_WithReadyPdf_SetsHasKbTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var sharedGame = Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test Description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/game-thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 12345);

        var gameId = sharedGame.Id;
        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        // Seed a Games record that maps SharedGameId → game record ID
        // The handler resolves SharedGameId → games.Id before querying PDFs
        var gameRecordId = Guid.NewGuid();
        _dbContext.Games.Add(new GameEntity
        {
            Id = gameRecordId,
            Name = "Test Game",
            SharedGameId = gameId
        });

        // Add fully indexed PDF document (ProcessingState = "Ready") to DbContext
        // PDF GameId references the game record ID (not SharedGameId)
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameRecordId,
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            ProcessingState = "Ready" // Issue #4998: fully indexed in RAG
        });
        await _dbContext.SaveChangesAsync();

        _mockLibraryRepo
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { libraryEntry }, 1));

        _mockSharedGameRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>
            {
                [gameId] = sharedGame
            });

        var query = new GetUserLibraryQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items.First();
        Assert.True(item.HasKb, "Should have hasKb=true when a PDF is fully indexed (Ready)");
        Assert.Equal(1, item.KbCardCount);
        Assert.Equal(1, item.KbIndexedCount);
        Assert.Equal(0, item.KbProcessingCount);
    }

    [Fact]
    public async Task Handle_WithoutPdfDocuments_SetsHasKbFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var sharedGame = Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test Description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/game-thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 12345);

        var gameId = sharedGame.Id;
        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        // No PDF documents added to DbContext

        _mockLibraryRepo
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { libraryEntry }, 1));

        _mockSharedGameRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>
            {
                [gameId] = sharedGame
            });

        var query = new GetUserLibraryQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items.First();
        Assert.False(item.HasKb, "Should have hasKb=false when no PDF exists");
        Assert.Equal(0, item.KbCardCount);
        Assert.Equal(0, item.KbIndexedCount);
    }

    [Fact]
    public async Task Handle_WithPrivateGameAndReadyPdf_SetsHasKbTrue()
    {
        // Arrange
        // Fix #private-game-kb-stats: private game PDFs are stored with GameId = PrivateGameId
        // AND PrivateGameId = PrivateGameId. The handler must query via PrivateGameId column,
        // not via the domain entry's GameId (which equals Guid.Empty from the EF computed property).
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        // Add PrivateGame entity to in-memory DB
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = privateGameId,
            OwnerId = userId,
            Title = "My Private Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual
        });

        // Add UserLibraryEntry entity with PrivateGameId (no SharedGameId)
        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = entryId,
            UserId = userId,
            PrivateGameId = privateGameId
            // SharedGameId = null (private game — GameId computed property returns Guid.Empty)
        });

        // Add PDF for private game: GameId = PrivateGameId (placeholder), PrivateGameId = PrivateGameId
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = privateGameId,      // Private game PDFs use PrivateGameId as GameId placeholder
            PrivateGameId = privateGameId,
            FileName = "private-rules.pdf",
            FilePath = "/pdfs/private-rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = userId,
            ProcessingState = "Ready"    // Fully indexed in RAG
        });
        await _dbContext.SaveChangesAsync();

        // Mock library repo: return entry with privateGameId as GameId
        // (In production the EF entity's computed GameId = Guid.Empty for private games,
        // which would crash the domain constructor. Here we use the real GUID so the handler
        // falls into the private game branch via the EF-loaded privateGameEntries dictionary.)
        var libraryEntry = new UserLibraryEntry(entryId, userId, privateGameId);

        _mockLibraryRepo
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>(),
                It.IsAny<string[]?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { libraryEntry }, 1));

        // No shared game matches — forces handler into the private game branch
        _mockSharedGameRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>());

        var query = new GetUserLibraryQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items.First();
        Assert.Equal(privateGameId, item.GameId);
        Assert.True(item.HasKb, "Private game with a Ready PDF should have hasKb=true");
        Assert.Equal(1, item.KbCardCount);
        Assert.Equal(1, item.KbIndexedCount);
        Assert.Equal(0, item.KbProcessingCount);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
