using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// Unit tests for GetUserLibraryQueryHandler.
/// Issue #3208: Verify hasPdfDocuments field population.
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

        _dbContext = new MeepleAiDbContext(options, null!, null!);

        _handler = new GetUserLibraryQueryHandler(
            _mockLibraryRepo.Object,
            _mockSharedGameRepo.Object,
            _dbContext);
    }

    [Fact]
    public async Task Handle_WithPdfDocuments_SetsHasPdfDocumentsTrue()
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
            imageUrl: "",
            thumbnailUrl: "",
            rules: null,
            createdBy: userId,
            bggId: 12345);

        var gameId = sharedGame.Id;
        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        // Add PDF document to DbContext
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            FileName = "rules.pdf",
            FilePath = "/pdfs/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            ProcessingStatus = "completed"
        });
        await _dbContext.SaveChangesAsync();

        _mockLibraryRepo
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, false, null, null, false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { libraryEntry }, 1));

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var query = new GetUserLibraryQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.True(result.Items.First().HasPdfDocuments, "Should have hasPdfDocuments=true when PDF exists");
    }

    [Fact]
    public async Task Handle_WithoutPdfDocuments_SetsHasPdfDocumentsFalse()
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
            imageUrl: "",
            thumbnailUrl: "",
            rules: null,
            createdBy: userId,
            bggId: 12345);

        var gameId = sharedGame.Id;
        var libraryEntry = new UserLibraryEntry(entryId, userId, gameId);

        // No PDF documents added to DbContext

        _mockLibraryRepo
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, false, null, null, false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { libraryEntry }, 1));

        _mockSharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var query = new GetUserLibraryQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.False(result.Items.First().HasPdfDocuments, "Should have hasPdfDocuments=false when no PDF exists");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
