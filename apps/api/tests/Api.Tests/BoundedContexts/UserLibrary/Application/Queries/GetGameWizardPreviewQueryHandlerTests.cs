using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Unit tests for GetGameWizardPreviewQueryHandler.
/// Issue #4823: Backend Game Preview API - Unified Wizard Data Endpoint
/// Epic #4817: User Collection Wizard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGameWizardPreviewQueryHandlerTests
{
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepository;
    private readonly Mock<IPdfDocumentRepository> _mockPdfDocumentRepository;
    private readonly Mock<IUserLibraryRepository> _mockUserLibraryRepository;
    private readonly GetGameWizardPreviewQueryHandler _handler;

    public GetGameWizardPreviewQueryHandlerTests()
    {
        _mockSharedGameRepository = new Mock<ISharedGameRepository>();
        _mockPdfDocumentRepository = new Mock<IPdfDocumentRepository>();
        _mockUserLibraryRepository = new Mock<IUserLibraryRepository>();
        _handler = new GetGameWizardPreviewQueryHandler(
            _mockSharedGameRepository.Object,
            _mockPdfDocumentRepository.Object,
            _mockUserLibraryRepository.Object
        );
    }

    [Fact]
    public async Task Handle_WhenGameExists_ReturnsCombinedPreviewData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, []);
        SetupUserLibraryRepository(userId, game.Id, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(game.Id);
        result.Title.Should().Be("Catan");
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(90);
        result.YearPublished.Should().Be(1995);
        result.Source.Should().Be("catalog");
        result.IsInUserLibrary.Should().BeFalse();
        result.LibraryEntryId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetGameWizardPreviewQuery(gameId, "catalog", userId);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{gameId}*");
    }

    [Fact]
    public async Task Handle_WhenGameInLibrary_ReturnsLibraryStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var libraryEntryId = Guid.NewGuid();
        var libraryEntry = new UserLibraryEntry(libraryEntryId, userId, game.Id);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, []);
        SetupUserLibraryRepository(userId, game.Id, libraryEntry);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsInUserLibrary.Should().BeTrue();
        result.LibraryEntryId.Should().Be(libraryEntryId);
    }

    [Fact]
    public async Task Handle_WhenGameHasDocuments_ReturnsDocumentSummaries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        var pdfDoc = new PdfDocument(
            id: Guid.NewGuid(),
            gameId: game.Id,
            fileName: new FileName("catan-rules.pdf"),
            filePath: "/uploads/catan-rules.pdf",
            fileSize: new FileSize(5_000_000),
            uploadedByUserId: userId
        );

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, [pdfDoc]);
        SetupUserLibraryRepository(userId, game.Id, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Documents.Should().HaveCount(1);
        result.DocumentCount.Should().Be(1);
        result.Documents[0].FileName.Should().Be("catan-rules.pdf");
        result.Documents[0].Status.Should().Be("Pending");
        result.Documents[0].DocumentType.Should().Be("base");
    }

    [Fact]
    public async Task Handle_WhenNoDocuments_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, []);
        SetupUserLibraryRepository(userId, game.Id, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Documents.Should().BeEmpty();
        result.DocumentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FetchesDocumentsAndLibraryStatusInParallel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, []);
        SetupUserLibraryRepository(userId, game.Id, null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - verify both repositories were called
        _mockPdfDocumentRepository.Verify(
            r => r.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUserLibraryRepository.Verify(
            r => r.GetByUserAndGameAsync(userId, game.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_MapsGameMetadataCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, []);
        SetupUserLibraryRepository(userId, game.Id, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ImageUrl.Should().Be("https://example.com/catan.jpg");
        result.ThumbnailUrl.Should().Be("https://example.com/catan-thumb.jpg");
        result.ComplexityRating.Should().Be(2.3m);
        result.AverageRating.Should().Be(7.2m);
        result.Description.Should().Be("Settlers of Catan board game");
    }

    [Fact]
    public async Task Handle_WhenCategoriesEmpty_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = CreateTestSharedGame(userId);
        var query = new GetGameWizardPreviewQuery(game.Id, "catalog", userId);

        SetupSharedGameRepository(game);
        SetupPdfDocumentRepository(game.Id, []);
        SetupUserLibraryRepository(userId, game.Id, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - SharedGame.Create doesn't populate categories/mechanics
        result.Categories.Should().BeEmpty();
        result.Mechanics.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullSharedGameRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetGameWizardPreviewQueryHandler(
            null!,
            _mockPdfDocumentRepository.Object,
            _mockUserLibraryRepository.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sharedGameRepository");
    }

    [Fact]
    public void Constructor_WithNullPdfDocumentRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetGameWizardPreviewQueryHandler(
            _mockSharedGameRepository.Object,
            null!,
            _mockUserLibraryRepository.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfDocumentRepository");
    }

    [Fact]
    public void Constructor_WithNullUserLibraryRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetGameWizardPreviewQueryHandler(
            _mockSharedGameRepository.Object,
            _mockPdfDocumentRepository.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userLibraryRepository");
    }

    #region Test Helpers

    private static SharedGame CreateTestSharedGame(Guid createdBy)
    {
        return SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Settlers of Catan board game",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.3m,
            averageRating: 7.2m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: null,
            createdBy: createdBy,
            bggId: 13);
    }

    private void SetupSharedGameRepository(SharedGame game)
    {
        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
    }

    private void SetupPdfDocumentRepository(Guid gameId, IReadOnlyList<PdfDocument> documents)
    {
        _mockPdfDocumentRepository
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);
    }

    private void SetupUserLibraryRepository(Guid userId, Guid gameId, UserLibraryEntry? entry)
    {
        _mockUserLibraryRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
    }

    #endregion
}
