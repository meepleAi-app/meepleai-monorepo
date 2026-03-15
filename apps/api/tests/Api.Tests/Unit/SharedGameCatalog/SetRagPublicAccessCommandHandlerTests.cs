using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.SharedGameCatalog;

/// <summary>
/// Unit tests for SetRagPublicAccessCommandHandler.
/// Ownership/RAG access feature.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SetRagPublicAccessCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ILogger<SetRagPublicAccessCommandHandler>> _mockLogger;

    public SetRagPublicAccessCommandHandlerTests()
    {
        _mockRepo = new Mock<ISharedGameRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<SetRagPublicAccessCommandHandler>>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_ToggleOnOff_UpdatesAndSaves(bool isRagPublic)
    {
        // Arrange
        var game = CreateTestSharedGame();

        _mockRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new SetRagPublicAccessCommandHandler(_mockRepo.Object, _mockUow.Object, _mockLogger.Object);
        var command = new SetRagPublicAccessCommand(game.Id, isRagPublic);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(isRagPublic, game.IsRagPublic);
        _mockRepo.Verify(r => r.Update(game), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var handler = new SetRagPublicAccessCommandHandler(_mockRepo.Object, _mockUow.Object, _mockLogger.Object);
        var command = new SetRagPublicAccessCommand(gameId, true);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ToggleFromTrueToFalse_SetsCorrectly()
    {
        // Arrange — game starts with IsRagPublic = true
        var game = CreateTestSharedGame();
        game.SetRagPublicAccess(true);
        Assert.True(game.IsRagPublic);

        _mockRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new SetRagPublicAccessCommandHandler(_mockRepo.Object, _mockUow.Object, _mockLogger.Object);
        var command = new SetRagPublicAccessCommand(game.Id, false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(game.IsRagPublic);
    }

    #region Helpers

    private static SharedGame CreateTestSharedGame()
    {
        return SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A test game for unit tests",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 12,
            complexityRating: 2.5m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());
    }

    #endregion
}
