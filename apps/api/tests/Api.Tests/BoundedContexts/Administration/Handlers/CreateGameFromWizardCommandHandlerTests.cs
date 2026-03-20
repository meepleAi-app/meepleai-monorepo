using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Handlers;

/// <summary>
/// Unit tests for CreateGameFromWizardCommandHandler.
/// Issue #4673: Verifies BGG data → Game entity creation, SharedGame linkage, fallback title.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class CreateGameFromWizardCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ISharedGameRepository> _mockGameRepo;
    private readonly CreateGameFromWizardCommandHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CreatedGameId = Guid.NewGuid();

    public CreateGameFromWizardCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockGameRepo = new Mock<ISharedGameRepository>();

        _handler = new CreateGameFromWizardCommandHandler(
            _mockMediator.Object,
            _mockGameRepo.Object,
            NullLogger<CreateGameFromWizardCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidBggId_DelegatesToImportCommand()
    {
        // Arrange
        var command = new CreateGameFromWizardCommand(BggId: 174430, CreatedByUserId: UserId);

        _mockMediator
            .Setup(m => m.Send(It.Is<ImportGameFromBggCommand>(c => c.BggId == 174430), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedGameId);

        _mockGameRepo
            .Setup(r => r.GetByIdAsync(CreatedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeSharedGame(CreatedGameId, "Gloomhaven", 174430));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockMediator.Verify(
            m => m.Send(
                It.Is<ImportGameFromBggCommand>(c => c.BggId == 174430 && c.UserId == UserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidBggId_ReturnsCorrectResult()
    {
        // Arrange
        var command = new CreateGameFromWizardCommand(BggId: 174430, CreatedByUserId: UserId);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedGameId);

        _mockGameRepo
            .Setup(r => r.GetByIdAsync(CreatedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeSharedGame(CreatedGameId, "Gloomhaven", 174430));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SharedGameId.Should().Be(CreatedGameId);
        result.Title.Should().Be("Gloomhaven");
        result.BggId.Should().Be(174430);
        result.Status.Should().Be("created");
    }

    [Fact]
    public async Task Handle_WhenGameNotFoundAfterCreation_UsesFallbackTitle()
    {
        // Arrange
        var bggId = 999;
        var command = new CreateGameFromWizardCommand(BggId: bggId, CreatedByUserId: UserId);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatedGameId);

        // Repository returns null — game was created but not yet readable
        _mockGameRepo
            .Setup(r => r.GetByIdAsync(CreatedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be($"BGG #{bggId}");
        result.SharedGameId.Should().Be(CreatedGameId);
        result.Status.Should().Be("created");
    }

    [Fact]
    public async Task Handle_WhenImportCommandFails_ThrowsException()
    {
        // Arrange
        var command = new CreateGameFromWizardCommand(BggId: 1, CreatedByUserId: UserId);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("BGG API unavailable"));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("BGG API unavailable");
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ResultStatusIsAlwaysCreated()
    {
        // Arrange
        var command = new CreateGameFromWizardCommand(BggId: 68448, CreatedByUserId: UserId);
        var gameId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ImportGameFromBggCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameId);

        _mockGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFakeSharedGame(gameId, "7 Wonders", 68448));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("created");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static SharedGame CreateFakeSharedGame(Guid id, string title, int bggId)
    {
        return new SharedGame(
            id: id,
            title: title,
            yearPublished: 2017,
            description: "Test description",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 14,
            complexityRating: null,
            averageRating: null,
            imageUrl: "",
            thumbnailUrl: "",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow,
            modifiedAt: null,
            isDeleted: false,
            bggId: bggId);
    }
}