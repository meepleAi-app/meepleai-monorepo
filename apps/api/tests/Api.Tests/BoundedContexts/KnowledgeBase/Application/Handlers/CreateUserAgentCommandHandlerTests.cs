using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for CreateUserAgentCommandHandler.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateUserAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _repository = new();
    private readonly Mock<ITierEnforcementService> _tierService = new();
    private readonly Mock<IRagAccessService> _ragAccessService = new();
    private readonly Mock<ILogger<CreateUserAgentCommandHandler>> _logger = new();
    private readonly MeepleAiDbContext _db;
    private readonly CreateUserAgentCommandHandler _handler;

    public CreateUserAgentCommandHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();

        // Default: ResolveGameIdAsync returns the same ID passed in (game exists in catalog)
        _repository.Setup(r => r.ResolveGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => id);

        // E2-3: Default tier enforcement — allows all operations
        _tierService.Setup(t => t.CanPerformAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default: RAG access allowed
        _ragAccessService.Setup(r => r.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new CreateUserAgentCommandHandler(_repository.Object, _db, _tierService.Object, _ragAccessService.Object, _logger.Object);
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
        result.Should().NotBeNull();
        result.Name.Should().Be("My Agent");
        result.Type.Should().Be("RAG");
        result.StrategyName.Should().Be("HybridSearch");
        result.GameId.Should().Be(gameId);
        result.CreatedByUserId.Should().Be(userId);

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
        result.StrategyName.Should().Be("SingleModel");
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
        result.StrategyName.Should().Be("IterativeRAG");
        (result.StrategyParameters.Count > 0).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_QuotaExceeded_ThrowsTierLimitExceededException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // E2-3: Tier enforcement denies agent creation
        _tierService.Setup(t => t.CanPerformAsync(userId, TierAction.CreateAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tierService.Setup(t => t.GetUsageAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageSnapshot(
                PrivateGames: 0, PrivateGamesMax: 5,
                PdfThisMonth: 0, PdfThisMonthMax: 3,
                AgentQueriesToday: 0, AgentQueriesTodayMax: 10,
                SessionQueries: 0, SessionQueriesMax: 10,
                Agents: 1, AgentsMax: 1,
                PhotosThisSession: 0, PhotosThisSessionMax: 5,
                SessionSaveEnabled: false,
                CatalogProposalsThisWeek: 0, CatalogProposalsThisWeekMax: 3));

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
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<TierLimitExceededException>()).Which;
        ex.LimitType.Should().Be("CreateAgent");
        ex.Current.Should().Be(1);
        ex.Max.Should().Be(1);
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
        result.Name.Should().StartWith("RAG-");
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
        result.Name.Should().Be("My Agent-1");
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
        result.Should().NotBeNull();
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
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ConflictException>()).Which;
        ex.Message.Should().Contain("Could not generate unique name");
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
