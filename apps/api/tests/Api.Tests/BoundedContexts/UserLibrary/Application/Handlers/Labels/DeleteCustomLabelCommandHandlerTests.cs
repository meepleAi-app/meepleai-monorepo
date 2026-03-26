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
public class DeleteCustomLabelCommandHandlerTests
{
    private readonly Mock<IGameLabelRepository> _labelRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeleteCustomLabelCommandHandler _handler;

    public DeleteCustomLabelCommandHandlerTests()
    {
        _handler = new DeleteCustomLabelCommandHandler(
            _labelRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCustomLabel_DeletesAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var label = GameLabel.CreateCustom(userId, "My Label", "#FF0000");

        _labelRepoMock.Setup(r => r.GetByIdAsync(label.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(label);

        var command = new DeleteCustomLabelCommand(userId, label.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        _labelRepoMock.Verify(r => r.DeleteAsync(label, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LabelNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _labelRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameLabel?)null);

        var command = new DeleteCustomLabelCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PredefinedLabel_ThrowsConflictException()
    {
        // Arrange
        var label = GameLabel.CreatePredefined("Strategy", "#0000FF");

        _labelRepoMock.Setup(r => r.GetByIdAsync(label.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(label);

        var command = new DeleteCustomLabelCommand(Guid.NewGuid(), label.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
