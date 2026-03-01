using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for CreateUserAgentCommandHandler.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateUserAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _repository = new();
    private readonly Mock<ILogger<CreateUserAgentCommandHandler>> _logger = new();
    private readonly CreateUserAgentCommandHandler _handler;

    public CreateUserAgentCommandHandlerTests()
    {
        _handler = new CreateUserAgentCommandHandler(_repository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesAgentWithCorrectFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "normal",
            UserRole: "User",
            GameId: gameId,
            AgentType: "RAG",
            Name: "My Agent",
            StrategyName: "HybridSearch",
            StrategyParameters: null
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("My Agent", result.Name);
        Assert.Equal("RAG", result.Type);
        Assert.Equal("HybridSearch", result.StrategyName);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal(userId, result.CreatedByUserId);

        _repository.Verify(r => r.AddAsync(
            It.Is<Agent>(a => a.GameId == gameId && a.CreatedByUserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FreeTier_DefaultsToSingleModel()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: "Free Agent",
            StrategyName: null,
            StrategyParameters: null
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("SingleModel", result.StrategyName);
    }

    [Fact]
    public async Task Handle_PremiumTier_UsesCustomParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var parameters = new Dictionary<string, object> { ["TopK"] = 15, ["MinScore"] = 0.8 };
        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "premium",
            UserRole: "User",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: "Premium Agent",
            StrategyName: "IterativeRAG",
            StrategyParameters: parameters
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("IterativeRAG", result.StrategyName);
        Assert.True(result.StrategyParameters.Count > 0);
    }

    [Fact]
    public async Task Handle_QuotaExceeded_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingAgents = Enumerable.Range(0, 3)
            .Select(_ => CreateTestAgent(userId))
            .ToList();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgents);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: "One Too Many",
            StrategyName: null,
            StrategyParameters: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Agent limit reached", ex.Message);
    }

    [Fact]
    public async Task Handle_NoName_GeneratesAutoName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: gameId,
            AgentType: "RAG",
            Name: null,
            StrategyName: null,
            StrategyParameters: null
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.StartsWith("RAG-", result.Name);
    }

    [Fact]
    public async Task Handle_DuplicateName_AutoSuffixes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, "My Agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, "My Agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: "My Agent",
            StrategyName: null,
            StrategyParameters: null
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("My Agent-1", result.Name);
    }

    [Fact]
    public async Task Handle_AdminRole_HasUnlimitedQuota()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingAgents = Enumerable.Range(0, 200)
            .Select(_ => CreateTestAgent(userId))
            .ToList();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgents);
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "Admin",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: "Admin Agent",
            StrategyName: null,
            StrategyParameters: null
        );

        // Act - should not throw even with 200 existing agents
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_DuplicateNameExhausted_ThrowsConflict()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repository.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        // All 5 suffix attempts return true (exist)
        _repository.Setup(r => r.ExistsByNameForUserAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateUserAgentCommand(
            UserId: userId,
            UserTier: "free",
            UserRole: "User",
            GameId: Guid.NewGuid(),
            AgentType: "RAG",
            Name: "Duplicate",
            StrategyName: null,
            StrategyParameters: null
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Could not generate unique name", ex.Message);
    }

    private static Agent CreateTestAgent(Guid userId)
    {
        return new Agent(
            id: Guid.NewGuid(),
            name: $"Agent-{Guid.NewGuid().ToString()[..8]}",
            type: AgentType.RagAgent,
            strategy: AgentStrategy.SingleModel(),
            createdByUserId: userId
        );
    }
}
