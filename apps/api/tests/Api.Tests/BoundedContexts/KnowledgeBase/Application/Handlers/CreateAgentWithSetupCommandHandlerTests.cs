using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CreateAgentWithSetupCommandHandler.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CreateAgentWithSetupCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly Mock<IChatThreadRepository> _mockChatRepo;
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<CreateAgentWithSetupCommandHandler>> _mockLogger;
    private readonly CreateAgentWithSetupCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public CreateAgentWithSetupCommandHandlerTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockChatRepo = new Mock<IChatThreadRepository>();
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<CreateAgentWithSetupCommandHandler>>();

        _handler = new CreateAgentWithSetupCommandHandler(
            _mockAgentRepo.Object,
            _mockChatRepo.Object,
            _mockLibraryRepo.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);

        // Default: no existing agents, name doesn't exist
        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Agent>());
        _mockAgentRepo
            .Setup(r => r.ExistsByNameForUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_BasicFlow_CreatesAgentAndThread()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result.AgentId);
        Assert.NotEqual(Guid.Empty, result.ThreadId);
        Assert.Equal(1, result.SlotUsed);
        Assert.False(result.GameAddedToCollection);

        _mockAgentRepo.Verify(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockChatRepo.Verify(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AddToCollection_True_GameNotInLibrary_AddsGame()
    {
        // Arrange
        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = CreateCommand(addToCollection: true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.GameAddedToCollection);
        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AddToCollection_True_GameAlreadyInLibrary_SkipsAdd()
    {
        // Arrange
        _mockLibraryRepo
            .Setup(r => r.IsGameInLibraryAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = CreateCommand(addToCollection: true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.GameAddedToCollection);
        _mockLibraryRepo.Verify(
            r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AddToCollection_False_NeverChecksLibrary()
    {
        // Arrange
        var command = CreateCommand(addToCollection: false);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLibraryRepo.Verify(
            r => r.IsGameInLibraryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SlotsFull_ThrowsConflictException()
    {
        // Arrange - 3 active agents for free tier (max 3)
        var existingAgents = Enumerable.Range(0, 3)
            .Select(i => CreateAgent($"Agent{i}"))
            .ToList();

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgents);

        var command = CreateCommand(tier: "free");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_SlotAvailable_WithExistingAgents_CorrectSlotNumber()
    {
        // Arrange - 2 active agents for free tier (max 3)
        var existingAgents = new List<Agent>
        {
            CreateAgent("Agent1"),
            CreateAgent("Agent2")
        };

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgents);

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.SlotUsed);
    }

    [Fact]
    public async Task Handle_InactiveAgents_NotCountedForQuota()
    {
        // Arrange - 3 agents but 1 inactive = 2 active, 1 slot available for free tier
        var agents = new List<Agent>
        {
            CreateAgent("Active1"),
            CreateAgent("Active2"),
            CreateAgent("Inactive1", isActive: false)
        };

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var command = CreateCommand();

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - should succeed (2 active + 1 new = 3 = max for free)
        Assert.Equal(3, result.SlotUsed);
    }

    [Fact]
    public async Task Handle_CustomAgentName_UsesProvidedName()
    {
        // Arrange
        var command = CreateCommand(agentName: "MyCustomAgent");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("MyCustomAgent", result.AgentName);
    }

    [Fact]
    public async Task Handle_NoAgentName_GeneratesDefault()
    {
        // Arrange
        var command = CreateCommand(agentName: null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("RAG-", result.AgentName);
    }

    [Fact]
    public async Task Handle_DuplicateName_AutoSuffixes()
    {
        // Arrange - first name exists, second doesn't
        _mockAgentRepo
            .SetupSequence(r => r.ExistsByNameForUserAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)   // "MyAgent" exists
            .ReturnsAsync(false); // "MyAgent-1" doesn't exist

        var command = CreateCommand(agentName: "MyAgent");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("MyAgent-1", result.AgentName);
    }

    [Fact]
    public async Task Handle_TransactionRolledBack_OnError()
    {
        // Arrange - agent add fails
        _mockAgentRepo
            .Setup(r => r.AddAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var command = CreateCommand();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_AdminRole_BypassesSlotLimit()
    {
        // Arrange - 200 agents but admin role
        var agents = Enumerable.Range(0, 200)
            .Select(i => CreateAgent($"Agent{i}"))
            .ToList();

        _mockAgentRepo
            .Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var command = CreateCommand(role: "Admin");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - should succeed (admin bypasses limits)
        Assert.Equal(201, result.SlotUsed);
    }

    private CreateAgentWithSetupCommand CreateCommand(
        string tier = "free",
        string role = "user",
        bool addToCollection = false,
        string agentName = null!,
        string? strategyName = null)
    {
        return new CreateAgentWithSetupCommand(
            UserId: _userId,
            UserTier: tier,
            UserRole: role,
            GameId: _gameId,
            AddToCollection: addToCollection,
            AgentType: "RAG",
            AgentName: agentName,
            StrategyName: strategyName,
            StrategyParameters: null);
    }

    private static Agent CreateAgent(string name, bool isActive = true)
    {
        return new Agent(
            Guid.NewGuid(),
            name,
            AgentType.Parse("RAG"),
            AgentStrategy.SingleModel(),
            isActive);
    }
}
