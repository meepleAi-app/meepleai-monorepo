using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class RemoveGameFromLibraryCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RemoveGameFromLibraryCommandHandler _handler;

    public RemoveGameFromLibraryCommandHandlerTests()
    {
        _handler = new RemoveGameFromLibraryCommandHandler(
            _libraryRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_GameInLibrary_RemovesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var command = new RemoveGameFromLibraryCommand(userId, gameId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _libraryRepoMock.Verify(r => r.DeleteAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsDomainException()
    {
        // Arrange
        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var command = new RemoveGameFromLibraryCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
