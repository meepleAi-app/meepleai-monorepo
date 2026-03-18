using Api.BoundedContexts.AgentMemory.Application.EventHandlers;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.EventHandlers;

/// <summary>
/// Tests for OnDisputeOverriddenAddHouseRuleHandler: adds house rules on dispute override.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class OnDisputeOverriddenAddHouseRuleHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly OnDisputeOverriddenAddHouseRuleHandler _handler;

    public OnDisputeOverriddenAddHouseRuleHandlerTests()
    {
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _gameMemoryRepoMock = new Mock<IGameMemoryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<OnDisputeOverriddenAddHouseRuleHandler>>();

        _handler = new OnDisputeOverriddenAddHouseRuleHandler(
            _sessionRepoMock.Object,
            _gameMemoryRepoMock.Object,
            _unitOfWorkMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_VerdictOverriddenWithRule_AddsHouseRuleToNewGameMemory()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var overrideRule = "Player keeps card after discard phase";

        var verdict = new DisputeVerdict(RulingFor.Initiator, "Based on rules", null, VerdictConfidence.High);
        var notification = new StructuredDisputeResolvedEvent(
            Guid.NewGuid(), sessionId, gameId, verdict,
            DisputeOutcome.VerdictOverridden, overrideRule);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSession(sessionId, gameId, ownerId));

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        GameMemory? capturedMemory = null;
        _gameMemoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()))
            .Callback<GameMemory, CancellationToken>((m, _) => capturedMemory = m)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedMemory);
        Assert.Equal(gameId, capturedMemory!.GameId);
        Assert.Equal(ownerId, capturedMemory.OwnerId);
        Assert.Single(capturedMemory.HouseRules);
        Assert.Equal(overrideRule, capturedMemory.HouseRules[0].Description);
        Assert.Equal(HouseRuleSource.DisputeOverride, capturedMemory.HouseRules[0].Source);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_VerdictOverriddenWithRule_AddsHouseRuleToExistingGameMemory()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var overrideRule = "Custom tiebreaker rule";

        var existingMemory = GameMemory.Create(gameId, ownerId);
        existingMemory.AddHouseRule("Existing rule", HouseRuleSource.UserAdded);

        var verdict = new DisputeVerdict(RulingFor.Respondent, "Reasoning", null, VerdictConfidence.Medium);
        var notification = new StructuredDisputeResolvedEvent(
            Guid.NewGuid(), sessionId, gameId, verdict,
            DisputeOutcome.VerdictOverridden, overrideRule);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockSession(sessionId, gameId, ownerId));

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemory);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        Assert.Equal(2, existingMemory.HouseRules.Count);
        Assert.Equal(overrideRule, existingMemory.HouseRules[1].Description);
        Assert.Equal(HouseRuleSource.DisputeOverride, existingMemory.HouseRules[1].Source);

        _gameMemoryRepoMock.Verify(r => r.UpdateAsync(existingMemory, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_VerdictAccepted_DoesNothing()
    {
        // Arrange
        var verdict = new DisputeVerdict(RulingFor.Initiator, "Reasoning", null, VerdictConfidence.High);
        var notification = new StructuredDisputeResolvedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), verdict,
            DisputeOutcome.VerdictAccepted, "Some rule");

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _sessionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _gameMemoryRepoMock.Verify(r => r.AddAsync(It.IsAny<GameMemory>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_VerdictOverriddenButNullRule_DoesNothing()
    {
        // Arrange
        var verdict = new DisputeVerdict(RulingFor.Initiator, "Reasoning", null, VerdictConfidence.High);
        var notification = new StructuredDisputeResolvedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), verdict,
            DisputeOutcome.VerdictOverridden, null);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _sessionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_VerdictOverriddenButEmptyRule_DoesNothing()
    {
        // Arrange
        var verdict = new DisputeVerdict(RulingFor.Initiator, "Reasoning", null, VerdictConfidence.High);
        var notification = new StructuredDisputeResolvedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), verdict,
            DisputeOutcome.VerdictOverridden, "   ");

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _sessionRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static LiveGameSession CreateMockSession(Guid sessionId, Guid gameId, Guid createdByUserId)
    {
        return LiveGameSession.Create(
            id: sessionId,
            createdByUserId: createdByUserId,
            gameName: "Test Game",
            gameId: gameId);
    }
}
