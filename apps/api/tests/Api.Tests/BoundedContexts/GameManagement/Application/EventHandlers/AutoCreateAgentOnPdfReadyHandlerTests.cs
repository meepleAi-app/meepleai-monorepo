using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Unit tests for AutoCreateAgentOnPdfReadyHandler.
/// Verifies auto-creation of AgentDefinition when PDF processing completes
/// for a user's PrivateGame.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class AutoCreateAgentOnPdfReadyHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _privateGameRepository;
    private readonly Mock<IAgentDefinitionRepository> _agentDefinitionRepository;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<ITierEnforcementService> _tierEnforcementService;
    private readonly Mock<IPublisher> _publisher;
    private readonly Mock<ILogger<AutoCreateAgentOnPdfReadyHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();

    public AutoCreateAgentOnPdfReadyHandlerTests()
    {
        _privateGameRepository = new Mock<IPrivateGameRepository>();
        _agentDefinitionRepository = new Mock<IAgentDefinitionRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _tierEnforcementService = new Mock<ITierEnforcementService>();
        _publisher = new Mock<IPublisher>();
        _logger = new Mock<ILogger<AutoCreateAgentOnPdfReadyHandler>>();

        // Default: tier quota not exceeded
        _tierEnforcementService
            .Setup(s => s.CanPerformAsync(_userId, TierAction.CreateAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default: save succeeds
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Default: publisher is a no-op
        _publisher
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private AutoCreateAgentOnPdfReadyHandler CreateHandler() =>
        new(
            _privateGameRepository.Object,
            _agentDefinitionRepository.Object,
            _unitOfWork.Object,
            _tierEnforcementService.Object,
            _publisher.Object,
            _logger.Object);

    private VectorDocumentReadyIntegrationEvent CreateEvent(
        Guid? gameId = null,
        Guid? userId = null,
        Guid? documentId = null) =>
        new()
        {
            DocumentId = documentId ?? _documentId,
            GameId = gameId ?? _gameId,
            ChunkCount = 120,
            PdfDocumentId = _pdfDocumentId,
            UploadedByUserId = userId ?? _userId,
            FileName = "rulebook.pdf",
            CurrentProcessingState = "Indexing"
        };

    private PrivateGame CreatePrivateGame(Guid? ownerId = null, Guid? gameId = null)
    {
        // Use the internal constructor via reflection is not needed —
        // PrivateGame.CreateManual is the public factory.
        var owner = ownerId ?? _userId;
        var game = PrivateGame.CreateManual(
            ownerId: owner,
            title: "Catan",
            minPlayers: 2,
            maxPlayers: 4);

        // Override Id via property only in tests is not possible (private setter).
        // Use the game as-is; the repository mock controls what is returned.
        return game;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 1: Creates agent and links to private game
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PrivateGameWithNoAgent_CreatesAgentAndLinksGame()
    {
        // Arrange
        var game = CreatePrivateGame(ownerId: _userId);

        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        AgentDefinition? savedAgent = null;
        _agentDefinitionRepository
            .Setup(r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<AgentDefinition, CancellationToken>((a, _) => savedAgent = a)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — agent was created and persisted
        _agentDefinitionRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Once);

        savedAgent.Should().NotBeNull();
        savedAgent!.Name.Should().StartWith("Agent - Catan");
        savedAgent.KbCardIds.Should().Contain(_documentId);
        savedAgent.IsActive.Should().BeTrue();

        // Assert — game was updated to link agent
        _privateGameRepository.Verify(
            r => r.UpdateAsync(game, It.IsAny<CancellationToken>()),
            Times.Once);

        game.AgentDefinitionId.Should().Be(savedAgent.Id);

        // Assert — unit of work saved
        _unitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert — tier usage recorded
        _tierEnforcementService.Verify(
            s => s.RecordUsageAsync(_userId, TierAction.CreateAgent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 2: Game is not a PrivateGame (SharedGame) — skip silently
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GameNotFoundAsPrivateGame_SkipsWithoutCreatingAgent()
    {
        // Arrange — repository returns null (not a private game)
        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — no agent created
        _agentDefinitionRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PrivateGameOwnedByDifferentUser_SkipsWithoutCreatingAgent()
    {
        // Arrange — game exists but owned by a different user
        var differentOwner = Guid.NewGuid();
        var game = CreatePrivateGame(ownerId: differentOwner);

        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var handler = CreateHandler();
        // Event uploaded by _userId, but game owned by differentOwner
        var evt = CreateEvent(userId: _userId);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — no agent created
        _agentDefinitionRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 3: PrivateGame already has an agent — update KbCardIds
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PrivateGameAlreadyHasAgent_UpdatesKbCardIdsInsteadOfCreating()
    {
        // Arrange — game already has an agent linked
        var existingAgentId = Guid.NewGuid();
        var game = CreatePrivateGame(ownerId: _userId);
        game.LinkAgent(existingAgentId);

        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Existing agent with no kbCardIds
        var existingAgent = AgentDefinition.Create(
            name: "Agent - Catan",
            description: "Existing agent",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Default());

        _agentDefinitionRepository
            .Setup(r => r.GetByIdAsync(existingAgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgent);

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — no NEW agent was created
        _agentDefinitionRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — existing agent was updated
        _agentDefinitionRepository.Verify(
            r => r.UpdateAsync(existingAgent, It.IsAny<CancellationToken>()),
            Times.Once);

        existingAgent.KbCardIds.Should().Contain(_documentId);

        // Assert — unit of work saved
        _unitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PrivateGameAlreadyHasAgent_DocumentAlreadyLinked_SkipsUpdate()
    {
        // Arrange — game has agent, agent already has this documentId
        var existingAgentId = Guid.NewGuid();
        var game = CreatePrivateGame(ownerId: _userId);
        game.LinkAgent(existingAgentId);

        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var existingAgent = AgentDefinition.Create(
            name: "Agent - Catan",
            description: "Existing agent",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Default());
        // Pre-link the same documentId
        existingAgent.UpdateKbCardIds([_documentId]);

        _agentDefinitionRepository
            .Setup(r => r.GetByIdAsync(existingAgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAgent);

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — no update performed (document already present)
        _agentDefinitionRepository.Verify(
            r => r.UpdateAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 4: Tier quota exceeded — skip agent creation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TierQuotaExceeded_SkipsAgentCreationAndLogsWarning()
    {
        // Arrange
        var game = CreatePrivateGame(ownerId: _userId);

        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _tierEnforcementService
            .Setup(s => s.CanPerformAsync(_userId, TierAction.CreateAgent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert — no agent created
        _agentDefinitionRepository.Verify(
            r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — tier usage NOT recorded (creation didn't happen)
        _tierEnforcementService.Verify(
            s => s.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert — a warning was logged
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tier quota exceeded")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 5: Exception during handling — logs error, does not throw
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RepositoryThrows_LogsErrorAndDoesNotThrow()
    {
        // Arrange — repository throws an unexpected exception
        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act — should not propagate the exception
        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(evt, CancellationToken.None));

        // Assert — no exception thrown
        exception.Should().BeNull();

        // Assert — error was logged
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AutoCreateAgentOnPdfReadyHandler failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AgentRepositoryThrowsDuringCreate_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var game = CreatePrivateGame(ownerId: _userId);

        _privateGameRepository
            .Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _agentDefinitionRepository
            .Setup(r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Agent save failed"));

        var handler = CreateHandler();
        var evt = CreateEvent();

        // Act
        var exception = await Record.ExceptionAsync(() =>
            handler.Handle(evt, CancellationToken.None));

        // Assert — no exception thrown
        exception.Should().BeNull();

        // Assert — error logged
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor guard tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullPrivateGameRepository_ThrowsArgumentNullException()
    {
        var act = () => new AutoCreateAgentOnPdfReadyHandler(
            null!,
            _agentDefinitionRepository.Object,
            _unitOfWork.Object,
            _tierEnforcementService.Object,
            _publisher.Object,
            _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullAgentDefinitionRepository_ThrowsArgumentNullException()
    {
        var act = () => new AutoCreateAgentOnPdfReadyHandler(
            _privateGameRepository.Object,
            null!,
            _unitOfWork.Object,
            _tierEnforcementService.Object,
            _publisher.Object,
            _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        var act = () => new AutoCreateAgentOnPdfReadyHandler(
            _privateGameRepository.Object,
            _agentDefinitionRepository.Object,
            null!,
            _tierEnforcementService.Object,
            _publisher.Object,
            _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTierEnforcementService_ThrowsArgumentNullException()
    {
        var act = () => new AutoCreateAgentOnPdfReadyHandler(
            _privateGameRepository.Object,
            _agentDefinitionRepository.Object,
            _unitOfWork.Object,
            null!,
            _publisher.Object,
            _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPublisher_ThrowsArgumentNullException()
    {
        var act = () => new AutoCreateAgentOnPdfReadyHandler(
            _privateGameRepository.Object,
            _agentDefinitionRepository.Object,
            _unitOfWork.Object,
            _tierEnforcementService.Object,
            null!,
            _logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new AutoCreateAgentOnPdfReadyHandler(
            _privateGameRepository.Object,
            _agentDefinitionRepository.Object,
            _unitOfWork.Object,
            _tierEnforcementService.Object,
            _publisher.Object,
            null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
