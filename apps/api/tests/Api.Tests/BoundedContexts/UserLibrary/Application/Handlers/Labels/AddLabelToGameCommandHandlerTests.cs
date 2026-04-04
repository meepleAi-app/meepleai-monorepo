using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.Labels;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddLabelToGameCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepoMock = new();
    private readonly Mock<IGameLabelRepository> _labelRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AddLabelToGameCommandHandler _handler;

    public AddLabelToGameCommandHandlerTests()
    {
        _handler = new AddLabelToGameCommandHandler(
            _libraryRepoMock.Object, _labelRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsNotFoundException()
    {
        // Arrange
        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var command = new AddLabelToGameCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_LabelNotAccessible_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _labelRepoMock.Setup(r => r.GetAccessibleLabelAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameLabel?)null);

        var command = new AddLabelToGameCommand(userId, gameId, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsLabelAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var label = GameLabel.CreateCustom(userId, "Party", "#FFD700");

        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _labelRepoMock.Setup(r => r.GetAccessibleLabelAsync(userId, label.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(label);

        var command = new AddLabelToGameCommand(userId, gameId, label.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Party", result.Name);
        Assert.Equal("#FFD700", result.Color);
        _libraryRepoMock.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
