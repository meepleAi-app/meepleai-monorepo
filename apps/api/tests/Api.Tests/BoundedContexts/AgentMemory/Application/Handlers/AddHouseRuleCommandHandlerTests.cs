using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Tests for AddHouseRuleCommandHandler covering find-or-create pattern and rule addition.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class AddHouseRuleCommandHandlerTests
{
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IFeatureFlagService> _featureFlagsMock;
    private readonly AddHouseRuleCommandHandler _handler;

    public AddHouseRuleCommandHandlerTests()
    {
        _gameMemoryRepoMock = new Mock<IGameMemoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _featureFlagsMock = new Mock<IFeatureFlagService>();
        var loggerMock = new Mock<ILogger<AddHouseRuleCommandHandler>>();

        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null))
            .ReturnsAsync(true);

        _handler = new AddHouseRuleCommandHandler(
            _gameMemoryRepoMock.Object,
            _unitOfWorkMock.Object,
            _featureFlagsMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NoExistingMemory_CreatesNewGameMemoryWithRule()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var command = new AddHouseRuleCommand(gameId, ownerId, "No table talk during combat");

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
        capturedMemory.HouseRules.Should().ContainSingle();
        capturedMemory.HouseRules[0].Description.Should().Be("No table talk during combat");
        capturedMemory.HouseRules[0].Source.Should().Be(HouseRuleSource.UserAdded);

        _gameMemoryRepoMock.Verify(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingMemory_AddsRuleToExisting()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemory = GameMemory.Create(gameId, ownerId);
        existingMemory.AddHouseRule("Existing rule", HouseRuleSource.UserAdded);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemory);

        var command = new AddHouseRuleCommand(gameId, ownerId, "New rule: shuffle twice");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingMemory.HouseRules.Count.Should().Be(2);
        existingMemory.HouseRules[1].Description.Should().Be("New rule: shuffle twice");

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

        var command = new AddHouseRuleCommand(Guid.NewGuid(), Guid.NewGuid(), "Some rule");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
