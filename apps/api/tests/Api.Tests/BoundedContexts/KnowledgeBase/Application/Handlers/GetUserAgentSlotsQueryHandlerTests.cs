using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for GetUserAgentSlotsQueryHandler.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetUserAgentSlotsQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _mockRepository;
    private readonly Mock<ILogger<GetUserAgentSlotsQueryHandler>> _mockLogger;
    private readonly GetUserAgentSlotsQueryHandler _handler;

    public GetUserAgentSlotsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentRepository>();
        _mockLogger = new Mock<ILogger<GetUserAgentSlotsQueryHandler>>();
        _handler = new GetUserAgentSlotsQueryHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_FreeTier_NoAgents_ReturnsAllSlotsAvailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var query = new GetUserAgentSlotsQuery(userId, "free", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(0, result.Used);
        Assert.Equal(3, result.Available);
        Assert.Equal(3, result.Slots.Count);
        Assert.All(result.Slots, slot => Assert.Equal("available", slot.Status));
    }

    [Fact]
    public async Task Handle_FreeTier_WithAgents_ReturnsCorrectSlotCounts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agents = new List<Agent>
        {
            CreateAgent("Agent1", isActive: true, gameId: gameId, userId: userId),
            CreateAgent("Agent2", isActive: true, gameId: gameId, userId: userId)
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetUserAgentSlotsQuery(userId, "free", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Used);
        Assert.Equal(1, result.Available);
        Assert.Equal(3, result.Slots.Count);

        // First 2 slots should be active with agent details
        Assert.Equal("active", result.Slots[0].Status);
        Assert.NotNull(result.Slots[0].AgentId);
        Assert.Equal("Agent1", result.Slots[0].AgentName);

        Assert.Equal("active", result.Slots[1].Status);
        Assert.NotNull(result.Slots[1].AgentId);

        // Third slot should be available
        Assert.Equal("available", result.Slots[2].Status);
        Assert.Null(result.Slots[2].AgentId);
    }

    [Fact]
    public async Task Handle_FreeTier_AllSlotsFull_ReturnsZeroAvailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agents = Enumerable.Range(0, 3)
            .Select(i => CreateAgent($"Agent{i}", isActive: true, userId: userId))
            .ToList();

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetUserAgentSlotsQuery(userId, "free", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Used);
        Assert.Equal(0, result.Available);
        Assert.All(result.Slots, slot => Assert.Equal("active", slot.Status));
    }

    [Theory]
    [InlineData("free", 3)]
    [InlineData("normal", 10)]
    [InlineData("premium", 50)]
    [InlineData("pro", 50)]
    [InlineData("enterprise", 200)]
    public async Task Handle_DifferentTiers_ReturnsCorrectTotal(string tier, int expectedTotal)
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var query = new GetUserAgentSlotsQuery(userId, tier, "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedTotal, result.Total);
        Assert.Equal(expectedTotal, result.Available);
    }

    [Fact]
    public async Task Handle_InactiveAgents_NotCountedAsUsed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agents = new List<Agent>
        {
            CreateAgent("Active1", isActive: true, userId: userId),
            CreateAgent("Inactive1", isActive: false, userId: userId),
            CreateAgent("Active2", isActive: true, userId: userId)
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetUserAgentSlotsQuery(userId, "free", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Used);
        Assert.Equal(1, result.Available);
        // Only active agents appear in slots
        var activeSlots = result.Slots.Where(s => s.Status == "active").ToList();
        Assert.Equal(2, activeSlots.Count);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    public async Task Handle_AdminRole_ReturnsUnlimitedAvailable(string role)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agents = new List<Agent>
        {
            CreateAgent("Agent1", isActive: true, userId: userId)
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetUserAgentSlotsQuery(userId, "free", role);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(int.MaxValue, result.Available);
        Assert.Equal(1, result.Used);
        // For admins, only active agent slots are returned (no empty filler slots)
        Assert.Single(result.Slots);
    }

    [Fact]
    public async Task Handle_NullTier_DefaultsToFree()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var query = new GetUserAgentSlotsQuery(userId, null!, "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentTierLimits.DefaultMaxAgents, result.Total);
    }

    [Fact]
    public async Task Handle_UnknownTier_DefaultsToFree()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());

        var query = new GetUserAgentSlotsQuery(userId, "nonexistent", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentTierLimits.DefaultMaxAgents, result.Total);
    }

    [Fact]
    public async Task Handle_SlotIndexesAreSequential()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agents = new List<Agent>
        {
            CreateAgent("Agent1", isActive: true, userId: userId)
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetUserAgentSlotsQuery(userId, "free", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        for (var i = 0; i < result.Slots.Count; i++)
        {
            Assert.Equal(i + 1, result.Slots[i].SlotIndex);
        }
    }

    [Fact]
    public async Task Handle_AgentGameIdIncludedInSlot()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agents = new List<Agent>
        {
            CreateAgent("Agent1", isActive: true, gameId: gameId, userId: userId)
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetUserAgentSlotsQuery(userId, "free", "user");

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(gameId, result.Slots[0].GameId);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private static Agent CreateAgent(
        string name,
        bool isActive = true,
        Guid? gameId = null,
        Guid? userId = null)
    {
        var agentType = AgentType.Parse("RAG");
        var strategy = AgentStrategy.SingleModel();
        var agent = new Agent(
            Guid.NewGuid(),
            name,
            agentType,
            strategy,
            isActive,
            gameId: gameId,
            createdByUserId: userId);
        return agent;
    }
}
