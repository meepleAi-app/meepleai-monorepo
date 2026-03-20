using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using LibraryAgentConfiguration = Api.BoundedContexts.UserLibrary.Domain.ValueObjects.AgentConfiguration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for CreateGameAgentCommandHandler.
/// Issue #4944: Agent creation quota enforcement.
/// Focuses on quota validation (steps 1-3.5) to keep tests targeted and fast.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CreateGameAgentCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _gameRepoMock;
    private readonly Mock<IAgentTypologyRepository> _typologyRepoMock;
    private readonly Mock<IUserLibraryRepository> _libraryRepoMock;
    private readonly Mock<IVectorDocumentRepository> _vectorDocRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateGameAgentCommandHandler>> _loggerMock;
    private readonly CreateGameAgentCommandHandler _handler;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _typologyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateGameAgentCommandHandlerTests()
    {
        _gameRepoMock = new Mock<ISharedGameRepository>();
        _typologyRepoMock = new Mock<IAgentTypologyRepository>();
        _libraryRepoMock = new Mock<IUserLibraryRepository>();
        _vectorDocRepoMock = new Mock<IVectorDocumentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateGameAgentCommandHandler>>();

        _handler = new CreateGameAgentCommandHandler(
            _gameRepoMock.Object,
            _typologyRepoMock.Object,
            _libraryRepoMock.Object,
            _vectorDocRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);

        // Default: game and approved typology found, knowledge base completed
        SetupGameFound();
        SetupApprovedTypology();
        SetupKnowledgeBaseCompleted();
    }

    // ──────────────────────────────────────────────────
    // Quota enforcement
    // ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FreeTierAtAgentLimit_ThrowsConflictException()
    {
        // Arrange: free tier allows max 3 agents; user already has 3
        _libraryRepoMock
            .Setup(r => r.GetAgentConfigCountAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var command = BuildCommand(userTier: "Free", userRole: "User");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Agent limit reached (3)", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Upgrade your tier", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_FreeTierExceedingLimit_ThrowsConflictException()
    {
        // Arrange: user has MORE than the free-tier limit (edge case: shouldn't happen but defensive)
        _libraryRepoMock
            .Setup(r => r.GetAgentConfigCountAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var command = BuildCommand(userTier: "Free", userRole: "User");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_AdminRole_BypassesAgentQuota()
    {
        // Arrange: admin has int.MaxValue limit.
        // User has 999 agents (far above any tier limit), no existing agent for this specific game.
        // Handler must complete without throwing a quota exception.
        _libraryRepoMock
            .Setup(r => r.GetAgentConfigCountAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(999);

        // No existing entry for this game → new entry will be created
        _libraryRepoMock
            .Setup(r => r.GetByUserAndGameAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        _libraryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = BuildCommand(userTier: "Free", userRole: "Admin");

        // Act & Assert: admin bypasses quota — handler completes successfully
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("ready", result.Status);
    }

    [Fact]
    public async Task Handle_EditorRole_BypassesAgentQuota()
    {
        // Arrange: Editor role also has unlimited agents.
        // User has 999 agents, no existing agent for this game.
        _libraryRepoMock
            .Setup(r => r.GetAgentConfigCountAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(999);

        _libraryRepoMock
            .Setup(r => r.GetByUserAndGameAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        _libraryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<UserLibraryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = BuildCommand(userTier: "Free", userRole: "Editor");

        // Act & Assert: editor bypasses quota — handler completes successfully
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("ready", result.Status);
    }

    [Fact]
    public async Task Handle_FreeTierBelowLimit_ProceedsToLibraryStep()
    {
        // Arrange: user has 2 agents (below free limit of 3)
        _libraryRepoMock
            .Setup(r => r.GetAgentConfigCountAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Return existing entry with agent already configured → "already exists" conflict
        var existingEntry = CreateEntryWithAgent(_userId, _gameId);
        _libraryRepoMock
            .Setup(r => r.GetByUserAndGameAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        var command = BuildCommand(userTier: "Free", userRole: "User");

        // Act & Assert: quota check passes; conflicts on "already exists" (not quota)
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("Agent limit reached", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_PremiumTierAtFreeLimitButBelowPremiumLimit_ProceedsToLibraryStep()
    {
        // Arrange: premium user with 3 agents — above free limit (3) but below premium limit (50)
        _libraryRepoMock
            .Setup(r => r.GetAgentConfigCountAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var existingEntry = CreateEntryWithAgent(_userId, _gameId);
        _libraryRepoMock
            .Setup(r => r.GetByUserAndGameAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEntry);

        var command = BuildCommand(userTier: "Premium", userRole: "User");

        // Act & Assert: quota passes (premium allows 50); conflicts on "already exists"
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("Agent limit reached", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    private CreateGameAgentCommand BuildCommand(string userTier, string userRole)
        => new(
            GameId: _gameId,
            TypologyId: _typologyId,
            StrategyName: "Balanced",
            StrategyParameters: null,
            UserId: _userId,
            UserTier: userTier,
            UserRole: userRole);

    private void SetupGameFound()
    {
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A board game for testing",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 8.0m,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());

        _gameRepoMock
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
    }

    private void SetupKnowledgeBaseCompleted()
    {
        var indexingInfo = new VectorDocumentIndexingInfo(
            VectorDocumentIndexingStatus.Completed,
            ChunkCount: 10,
            IndexingError: null);

        _vectorDocRepoMock
            .Setup(r => r.GetIndexingInfoByGameIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexingInfo);
    }

    private void SetupApprovedTypology()
    {
        var strategy = AgentStrategy.Custom("Balanced", new Dictionary<string, object>(StringComparer.Ordinal));
        var typology = new AgentTypology(
            _typologyId,
            "Standard Typology",
            "Test description",
            "System prompt",
            strategy,
            Guid.NewGuid(),
            TypologyStatus.Approved);

        _typologyRepoMock
            .Setup(r => r.GetByIdAsync(_typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);
    }

    private static UserLibraryEntry CreateEntryWithAgent(Guid userId, Guid gameId)
    {
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        var config = LibraryAgentConfiguration.Create(
            llmModel: "gpt-4",
            temperature: 0.7,
            maxTokens: 2000,
            personality: "Test personality",
            detailLevel: "Balanced");
        entry.ConfigureAgent(config);
        return entry;
    }
}
