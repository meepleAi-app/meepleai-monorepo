using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class DeleteGameErrataCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeleteGameErrataCommandHandler>> _loggerMock;
    private readonly DeleteGameErrataCommandHandler _handler;

    public DeleteGameErrataCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeleteGameErrataCommandHandler>>();
        _handler = new DeleteGameErrataCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_DeletesErrataSuccessfully()
    {
        // Arrange
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var errata = GameErrata.Create(game.Id, "Description", "Page 5", DateTime.UtcNow);
        game.AddErrata(errata);

        var command = new DeleteGameErrataCommand(ErrataId: errata.Id);

        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errata.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _repositoryMock.Verify(r => r.Update(game), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentErrata_ThrowsInvalidOperationException()
    {
        // Arrange
        var errataId = Guid.NewGuid();
        var command = new DeleteGameErrataCommand(ErrataId: errataId);

        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errataId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
