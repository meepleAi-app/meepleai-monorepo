using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for CreateGameCommandHandler.
/// Tests game creation with value object validation and DTO mapping.
/// Issue #3372: Added IPdfDocumentRepository mock for PDF linking support.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateGameCommandHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IPdfDocumentRepository> _pdfDocumentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateGameCommandHandler _handler;

    public CreateGameCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _pdfDocumentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateGameCommandHandler(
            _gameRepositoryMock.Object,
            _pdfDocumentRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }
    [Fact]
    public async Task Handle_WithAllProperties_CreatesGameAndReturnsDto()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Ticket to Ride",
            Publisher: "Days of Wonder",
            YearPublished: 2004,
            MinPlayers: 2,
            MaxPlayers: 5,
            MinPlayTimeMinutes: 45,
            MaxPlayTimeMinutes: 60);

        Game? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Ticket to Ride", result.Title);
        Assert.Equal("Days of Wonder", result.Publisher);
        Assert.Equal(2004, result.YearPublished);
        Assert.Equal(2, result.MinPlayers);
        Assert.Equal(5, result.MaxPlayers);
        Assert.Equal(45, result.MinPlayTimeMinutes);
        Assert.Equal(60, result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId);
        Assert.NotEqual(default(DateTime), result.CreatedAt);

        // Verify repository interactions
        _gameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify captured game
        Assert.NotNull(capturedGame);
        Assert.Equal("Ticket to Ride", capturedGame.Title.Value);
        Assert.Equal("Days of Wonder", capturedGame.Publisher?.Name);
    }

    [Fact]
    public async Task Handle_WithMinimalProperties_CreatesGameWithTitleOnly()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Chess");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Chess", result.Title);
        Assert.Null(result.Publisher);
        Assert.Null(result.YearPublished);
        Assert.Null(result.MinPlayers);
        Assert.Null(result.MaxPlayers);
        Assert.Null(result.MinPlayTimeMinutes);
        Assert.Null(result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId);

        _gameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPartialPlayerCount_CreatesGameWithNullPlayerCount()
    {
        // Arrange - Only MinPlayers provided
        var command = new CreateGameCommand(
            Title: "Solitaire",
            MinPlayers: 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Solitaire", result.Title);
        Assert.Null(result.MinPlayers); // Not created because MaxPlayers is missing
        Assert.Null(result.MaxPlayers);
    }

    [Fact]
    public async Task Handle_WithPartialPlayTime_CreatesGameWithNullPlayTime()
    {
        // Arrange - Only MinPlayTimeMinutes provided
        var command = new CreateGameCommand(
            Title: "Quick Game",
            MinPlayTimeMinutes: 15);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Quick Game", result.Title);
        Assert.Null(result.MinPlayTimeMinutes); // Not created because MaxPlayTimeMinutes is missing
        Assert.Null(result.MaxPlayTimeMinutes);
    }

    [Fact]
    public async Task Handle_WithPlayerCountOnly_CreatesGameWithPlayerCount()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Pandemic",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pandemic", result.Title);
        Assert.Equal(2, result.MinPlayers);
        Assert.Equal(4, result.MaxPlayers);
        Assert.Null(result.Publisher);
        Assert.Null(result.YearPublished);
    }

    [Fact]
    public async Task Handle_WithPlayTimeOnly_CreatesGameWithPlayTime()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "7 Wonders",
            MinPlayTimeMinutes: 30,
            MaxPlayTimeMinutes: 45);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("7 Wonders", result.Title);
        Assert.Equal(30, result.MinPlayTimeMinutes);
        Assert.Equal(45, result.MaxPlayTimeMinutes);
        Assert.Null(result.Publisher);
        Assert.Null(result.MinPlayers);
    }

    [Fact]
    public async Task Handle_WithPublisherAndYearOnly_CreatesGameCorrectly()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Azul",
            Publisher: "Plan B Games",
            YearPublished: 2017);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Azul", result.Title);
        Assert.Equal("Plan B Games", result.Publisher);
        Assert.Equal(2017, result.YearPublished);
        Assert.Null(result.MinPlayers);
        Assert.Null(result.MinPlayTimeMinutes);
    }
    [Fact]
    public async Task Handle_WithVeryLongTitle_CreatesGame()
    {
        // Arrange
        var longTitle = new string('A', 200);
        var command = new CreateGameCommand(Title: longTitle);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longTitle, result.Title);
    }

    [Fact]
    public async Task Handle_WithSoloGame_CreatesWithOnePlayerCount()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Onirim",
            MinPlayers: 1,
            MaxPlayers: 1);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(1, result.MaxPlayers);
    }

    [Fact]
    public async Task Handle_WithManyPlayers_CreatesCorrectly()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Werewolf",
            MinPlayers: 5,
            MaxPlayers: 20);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.MinPlayers);
        Assert.Equal(20, result.MaxPlayers);
    }

    [Fact]
    public async Task Handle_WithOldGame_CreatesWithOldYear()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Chess",
            YearPublished: 1475); // Historical game

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1475, result.YearPublished);
    }

    [Fact]
    public async Task Handle_WithRecentGame_CreatesWithCurrentYear()
    {
        // Arrange
        var currentYear = DateTime.UtcNow.Year;
        var command = new CreateGameCommand(
            Title: "New Release 2025",
            YearPublished: currentYear);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(currentYear, result.YearPublished);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var command = new CreateGameCommand(Title: "Catan");
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _gameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Game>(), cancellationToken),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
    [Fact]
    public async Task Handle_MultipleGames_GeneratesDifferentIds()
    {
        // Arrange
        var command1 = new CreateGameCommand(Title: "Game 1");
        var command2 = new CreateGameCommand(Title: "Game 2");

        // Act
        var result1 = await _handler.Handle(command1, TestContext.Current.CancellationToken);
        var result2 = await _handler.Handle(command2, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(result1.Id, result2.Id);
    }
    [Fact]
    public async Task Handle_MapsAllPropertiesToDto()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Wingspan",
            Publisher: "Stonemaier Games",
            YearPublished: 2019,
            MinPlayers: 1,
            MaxPlayers: 5,
            MinPlayTimeMinutes: 40,
            MaxPlayTimeMinutes: 70);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - DTO should have all properties mapped
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Wingspan", result.Title);
        Assert.Equal("Stonemaier Games", result.Publisher);
        Assert.Equal(2019, result.YearPublished);
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(5, result.MaxPlayers);
        Assert.Equal(40, result.MinPlayTimeMinutes);
        Assert.Equal(70, result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId); // Not set during creation
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.CreatedAt >= DateTime.UtcNow.AddSeconds(-5)); // Created within last 5 seconds
    }

    // ===== VALIDATION TESTS =====

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyOrWhitespaceName_ThrowsValidationException(string invalidTitle)
    {
        // Arrange
        var command = new CreateGameCommand(Title: invalidTitle);

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TitleExceedsMaxLength_ThrowsValidationException()
    {
        // Arrange
        var longTitle = new string('A', 201); // Exceeds 200 character limit
        var command = new CreateGameCommand(Title: longTitle);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("cannot exceed 200 characters", exception.Message);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 4)]   // Min < 1
    [InlineData(-1, 4)]  // Negative min
    public async Task Handle_InvalidMinPlayers_ThrowsValidationException(int minPlayers, int maxPlayers)
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Test Game",
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Minimum player count cannot be less than 1", exception.Message);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2, 101)]  // Max > 100
    [InlineData(1, 150)]  // Max far exceeds limit
    public async Task Handle_MaxPlayersExceedsLimit_ThrowsValidationException(int minPlayers, int maxPlayers)
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Test Game",
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Maximum player count cannot exceed 100", exception.Message);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MinPlayersExceedsMaxPlayers_ThrowsValidationException()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Test Game",
            MinPlayers: 5,
            MaxPlayers: 2); // Min > Max

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("Minimum player count cannot exceed maximum", exception.Message);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MinPlayTimeExceedsMaxPlayTime_ThrowsValidationException()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Test Game",
            MinPlayTimeMinutes: 120,
            MaxPlayTimeMinutes: 60); // Min > Max

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Contains("cannot exceed", exception.Message, StringComparison.OrdinalIgnoreCase);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithBggId_LinksToBgg()
    {
        // Arrange
        var command = new CreateGameCommand(
            Title: "Scythe",
            BggId: 169786);

        Game? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Scythe", result.Title);
        Assert.Equal(169786, result.BggId);

        Assert.NotNull(capturedGame);
        Assert.Equal(169786, capturedGame.BggId);
    }

    // ===== PDF LINKING TESTS (Issue #3372) =====

    [Fact]
    public async Task Handle_WithPdfId_FetchesPdfAndSavesTwice()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var command = new CreateGameCommand(
            Title: "Settlers of Catan",
            PdfId: pdfId);

        // Create a real PdfDocument using Reconstitute (since class is sealed)
        var pdfDocument = Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument.Reconstitute(
            id: pdfId,
            gameId: Guid.Empty, // Initially not linked
            fileName: new Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.FileName("rules.pdf"),
            filePath: "/storage/rules.pdf",
            fileSize: new Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.FileSize(1024),
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow,
            processedAt: DateTime.UtcNow,
            pageCount: 10,
            processingError: null,
            language: Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.LanguageCode.English);

        _pdfDocumentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDocument);

        Game? capturedGame = null;
        _gameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedGame);

        // Verify PDF was fetched
        _pdfDocumentRepositoryMock.Verify(
            r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify SaveChanges was called twice (once for game, once for PDF link)
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // Verify PDF GameId was updated
        Assert.Equal(capturedGame.Id, pdfDocument.GameId);
    }

    [Fact]
    public async Task Handle_WithNonExistentPdfId_ThrowsNotFoundException()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var command = new CreateGameCommand(
            Title: "Terra Mystica",
            PdfId: pdfId);

        // PDF not found - returns null
        _pdfDocumentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        // Verify exception contains correct info
        Assert.Equal("PdfDocument", exception.ResourceType);
        Assert.Equal(pdfId.ToString(), exception.ResourceId);

        // Verify PDF was searched
        _pdfDocumentRepositoryMock.Verify(
            r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()),
            Times.Once);

        // Note: Game was created and saved before PDF check, so 1 SaveChanges call
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutPdfId_DoesNotQueryPdfRepository()
    {
        // Arrange
        var command = new CreateGameCommand(Title: "Dominion");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - PDF repository should not be queried
        _pdfDocumentRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

