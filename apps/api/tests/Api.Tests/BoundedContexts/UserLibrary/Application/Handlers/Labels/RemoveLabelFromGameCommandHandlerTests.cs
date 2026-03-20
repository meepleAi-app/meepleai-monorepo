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
public class RemoveLabelFromGameCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RemoveLabelFromGameCommandHandler _handler;

    public RemoveLabelFromGameCommandHandlerTests()
    {
        _handler = new RemoveLabelFromGameCommandHandler(
            _libraryRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsNotFoundException()
    {
        // Arrange
        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var command = new RemoveLabelFromGameCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRequest_RemovesLabelAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        entry.AddLabel(labelId);

        _libraryRepoMock.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var command = new RemoveLabelFromGameCommand(userId, gameId, labelId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        _libraryRepoMock.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
