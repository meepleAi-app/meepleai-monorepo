using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Tests for AddGlossaryEntryCommandHandler covering find-or-create pattern, duplicate detection, and cross-language rules.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class AddGlossaryEntryCommandHandlerTests
{
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFeatureFlagService> _featureFlagsMock = new();
    private readonly AddGlossaryEntryCommandHandler _handler;

    public AddGlossaryEntryCommandHandlerTests()
    {
        var loggerMock = new Mock<ILogger<AddGlossaryEntryCommandHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new AddGlossaryEntryCommandHandler(
            _gameMemoryRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NoExistingMemory_CreatesNewGameMemoryWithGlossaryEntry()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var command = new AddGlossaryEntryCommand(gameId, ownerId, "Meeple", "A playing piece shaped like a person", "en");

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
        capturedMemory.Should().NotBeNull();
        capturedMemory!.GameId.Should().Be(gameId);
        capturedMemory.OwnerId.Should().Be(ownerId);
        capturedMemory.GlossaryEntries.Should().ContainSingle();
        capturedMemory.GlossaryEntries[0].Term.Should().Be("Meeple");
        capturedMemory.GlossaryEntries[0].Definition.Should().Be("A playing piece shaped like a person");
        capturedMemory.GlossaryEntries[0].Language.Should().Be("en");
        capturedMemory.GlossaryEntries[0].Source.Should().Be(GlossaryEntrySource.UserDefined);

        _gameMemoryRepoMock.Verify(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Once);
        _gameMemoryRepoMock.Verify(r => r.UpdateAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingMemory_AddsGlossaryEntryToExisting()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemory = GameMemory.Create(gameId, ownerId);
        existingMemory.AddGlossaryEntry("Worker", "A game piece used for resource gathering", "en", GlossaryEntrySource.Manual);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemory);

        var command = new AddGlossaryEntryCommand(gameId, ownerId, "Resource", "A commodity collected during play", "en");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingMemory.GlossaryEntries.Count.Should().Be(2);
        existingMemory.GlossaryEntries[1].Term.Should().Be("Resource");

        _gameMemoryRepoMock.Verify(r => r.UpdateAsync(existingMemory, It.IsAny<CancellationToken>()), Times.Once);
        _gameMemoryRepoMock.Verify(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateTermSameLanguage_ThrowsConflictException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemory = GameMemory.Create(gameId, ownerId);
        existingMemory.AddGlossaryEntry("Meeple", "Original definition", "en", GlossaryEntrySource.Manual);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemory);

        // Duplicate — same term, same language
        var command = new AddGlossaryEntryCommand(gameId, ownerId, "Meeple", "Another definition", "en");

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SameTermDifferentLanguage_Succeeds()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemory = GameMemory.Create(gameId, ownerId);
        existingMemory.AddGlossaryEntry("Meeple", "A playing piece shaped like a person", "en", GlossaryEntrySource.Manual);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemory);

        _gameMemoryRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Same term but different language — should succeed
        var command = new AddGlossaryEntryCommand(gameId, ownerId, "Meeple", "Peça de jogo em formato de pessoa", "it");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingMemory.GlossaryEntries.Count.Should().Be(2);
        existingMemory.GlossaryEntries[1].Language.Should().Be("it");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FeatureDisabled_ThrowsConflictException()
    {
        // Arrange
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(false);

        var command = new AddGlossaryEntryCommand(Guid.NewGuid(), Guid.NewGuid(), "Term", "Definition", "en");

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
