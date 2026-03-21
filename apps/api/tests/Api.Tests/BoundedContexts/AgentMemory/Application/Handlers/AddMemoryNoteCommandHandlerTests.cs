using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class AddMemoryNoteCommandHandlerTests
{
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFeatureFlagService> _featureFlagsMock = new();
    private readonly AddMemoryNoteCommandHandler _handler;

    public AddMemoryNoteCommandHandlerTests()
    {
        var loggerMock = new Mock<ILogger<AddMemoryNoteCommandHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new AddMemoryNoteCommandHandler(
            _gameMemoryRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NoExistingMemory_CreatesNewGameMemoryWithNote()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var command = new AddMemoryNoteCommand(gameId, ownerId, "Remember to shuffle extra");

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        GameMemory? capturedMemory = null;
        _gameMemoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()))
            .Callback<GameMemory, CancellationToken>((m, _) => capturedMemory = m)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedMemory);
        Assert.Equal(gameId, capturedMemory!.GameId);
        Assert.Equal(ownerId, capturedMemory.OwnerId);
        Assert.Single(capturedMemory.Notes);
        Assert.Equal("Remember to shuffle extra", capturedMemory.Notes[0].Content);

        _gameMemoryRepoMock.Verify(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingMemory_AddsNoteToExisting()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemory = GameMemory.Create(gameId, ownerId);
        existingMemory.AddNote("Existing note", ownerId);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemory);

        var command = new AddMemoryNoteCommand(gameId, ownerId, "New note");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, existingMemory.Notes.Count);
        Assert.Equal("New note", existingMemory.Notes[1].Content);

        _gameMemoryRepoMock.Verify(r => r.UpdateAsync(existingMemory, It.IsAny<CancellationToken>()), Times.Once);
        _gameMemoryRepoMock.Verify(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(false);

        var command = new AddMemoryNoteCommand(Guid.NewGuid(), Guid.NewGuid(), "Some note");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
