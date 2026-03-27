using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for UpdateUserAgentCommandHandler.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateUserAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _repository = new();
    private readonly Mock<ILogger<UpdateUserAgentCommandHandler>> _logger = new();
    private readonly UpdateUserAgentCommandHandler _handler;

    public UpdateUserAgentCommandHandlerTests()
    {
        _handler = new UpdateUserAgentCommandHandler(_repository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_OwnerUpdatesName_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Old Name", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            Name: "New Name", StrategyName: null, StrategyParameters: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("New Name");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOwnerUpdates_ThrowsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: ownerId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: otherId, UserTier: "free", UserRole: "User",
            Name: "New Name", StrategyName: null, StrategyParameters: null);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AdminUpdatesOtherAgent_Succeeds()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: ownerId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: adminId, UserTier: "free", UserRole: "Admin",
            Name: "Admin Renamed", StrategyName: null, StrategyParameters: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Admin Renamed");
    }

    [Fact]
    public async Task Handle_FreeTierChangesStrategy_ThrowsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            Name: null, StrategyName: "HybridSearch", StrategyParameters: null);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NormalTierWithParameters_ThrowsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var parameters = new Dictionary<string, object> { ["TopK"] = 10 };
        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "normal", UserRole: "User",
            Name: null, StrategyName: "HybridSearch", StrategyParameters: parameters);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NormalTierWithStrategyOnly_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "normal", UserRole: "User",
            Name: null, StrategyName: "HybridSearch", StrategyParameters: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.StrategyName.Should().Be("HybridSearch");
    }

    [Fact]
    public async Task Handle_PremiumTierWithFullConfig_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Agent", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var parameters = new Dictionary<string, object> { ["TopK"] = 15, ["MinScore"] = 0.8 };
        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "premium", UserRole: "User",
            Name: null, StrategyName: "IterativeRAG", StrategyParameters: parameters);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.StrategyName.Should().Be("IterativeRAG");
        (result.StrategyParameters.Count > 0).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AgentNotFound_ThrowsNotFound()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: Guid.NewGuid(), UserTier: "free", UserRole: "User",
            Name: "X", StrategyName: null, StrategyParameters: null);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_RenameToDuplicateName_ThrowsConflict()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Old Name", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, "Taken Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            Name: "Taken Name", StrategyName: null, StrategyParameters: null);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ConflictException>()).Which;
        ex.Message.Should().Contain("Taken Name");
    }

    [Fact]
    public async Task Handle_RenameToSameName_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var agent = new Agent(agentId, "Same Name", AgentType.RagAgent, AgentStrategy.SingleModel(), createdByUserId: userId);
        _repository.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        var command = new UpdateUserAgentCommand(
            AgentId: agentId, UserId: userId, UserTier: "free", UserRole: "User",
            Name: "Same Name", StrategyName: null, StrategyParameters: null);

        // Act - should succeed without calling ExistsByNameForUserAsync
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Same Name");
        _repository.Verify(r => r.ExistsByNameForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
