using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
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

        // Create in-memory database for PdfDocuments cross-context query
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
            .Options;

        // Create mocks for DbContext dependencies
        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(e => e.GetAndClearEvents()).Returns(new List<IDomainEvent>());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

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

        // Add fully indexed PDF document (ProcessingState = "Ready") to DbContext
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            ProcessingStatus = "completed",
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

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
