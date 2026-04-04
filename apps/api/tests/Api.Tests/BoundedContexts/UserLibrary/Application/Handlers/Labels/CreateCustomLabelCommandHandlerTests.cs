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
public class CreateCustomLabelCommandHandlerTests
{
    private readonly Mock<IGameLabelRepository> _labelRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CreateCustomLabelCommandHandler _handler;

    public CreateCustomLabelCommandHandlerTests()
    {
        _handler = new CreateCustomLabelCommandHandler(
            _labelRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesLabelAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _labelRepoMock.Setup(r => r.LabelNameExistsAsync(userId, "Favorites", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateCustomLabelCommand(userId, "Favorites", "#FF5733");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Favorites", result.Name);
        Assert.Equal("#FF5733", result.Color);
        Assert.False(result.IsPredefined);
        _labelRepoMock.Verify(r => r.AddAsync(It.IsAny<GameLabel>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _labelRepoMock.Setup(r => r.LabelNameExistsAsync(userId, "Existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateCustomLabelCommand(userId, "Existing", "#000000");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
