using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for LaunchSessionAgentCommandHandler.
/// Issue #2500 (C1 fix): empty InitialGameStateJson defaults to GameState.Initial(UserId).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LaunchSessionAgentCommandHandlerTests
{
    private static readonly Guid _gameSessionId = Guid.NewGuid();
    private static readonly Guid _agentDefinitionId = Guid.NewGuid();
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid _gameId = Guid.NewGuid();

    private const string ValidGameStateJson =
        """{"CurrentTurn":1,"ActivePlayer":"11111111-1111-1111-1111-111111111111","PlayerScores":{},"GamePhase":"setup","LastAction":"start"}""";

    private readonly Mock<IAgentSessionRepository> _sessionRepoMock;
    private readonly Mock<IAgentDefinitionRepository> _definitionRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LaunchSessionAgentCommandHandler _handler;

    public LaunchSessionAgentCommandHandlerTests()
    {
        _sessionRepoMock = new Mock<IAgentSessionRepository>();
        _definitionRepoMock = new Mock<IAgentDefinitionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _sessionRepoMock
            .Setup(r => r.HasActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new LaunchSessionAgentCommandHandler(
            _sessionRepoMock.Object,
            _definitionRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<LaunchSessionAgentCommandHandler>.Instance);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // #4154 / ADR-095 fetta 0b — senza id, il lancio usa l'agente di sistema
    // ──────────────────────────────────────────────────────────────────────────────

    private static LaunchSessionAgentCommand CommandWithoutAgent() => new(
        GameSessionId: _gameSessionId,
        AgentDefinitionId: null,
        UserId: _userId,
        GameId: _gameId,
        InitialGameStateJson: string.Empty);

    private static AgentDefinitionEntity SystemAgent(bool active)
    {
        var agent = AgentDefinitionEntity.CreateSystem(
            "Rules Expert", "Agente di sistema", AgentDefinitionConfig.Default(), "rules-expert");
        if (active)
            agent.Activate();
        return agent;
    }

    [Fact]
    public async Task Handle_WithoutAgentDefinitionId_LaunchesWithTheActiveSystemAgent()
    {
        var systemAgent = SystemAgent(active: true);
        var userAgent = AgentDefinitionEntity.Create("Agente utente", "Non di sistema", AgentDefinitionConfig.Default());
        userAgent.Activate();
        _definitionRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([userAgent, systemAgent]);

        AgentSession? persisted = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()))
            .Callback<AgentSession, CancellationToken>((s, _) => persisted = s)
            .Returns(Task.CompletedTask);

        await _handler.Handle(CommandWithoutAgent(), CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(systemAgent.Id, persisted!.AgentDefinitionId);
    }

    [Fact]
    public async Task Handle_WithoutAgentDefinitionId_InactiveSystemAgent_ThrowsConflict_AndPersistsNothing()
    {
        _definitionRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SystemAgent(active: false)]);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(CommandWithoutAgent(), CancellationToken.None));

        Assert.Equal("system_agent_unavailable", ex.ErrorCode);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutAgentDefinitionId_NoSystemAgent_ThrowsConflict()
    {
        _definitionRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(CommandWithoutAgent(), CancellationToken.None));

        Assert.Equal("system_agent_unavailable", ex.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithExplicitAgentDefinitionId_DoesNotResolveTheSystemAgent()
    {
        var command = CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId };

        await _handler.Handle(command, CancellationToken.None);

        _definitionRepoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // C1 fix — empty InitialGameStateJson uses GameState.Initial(UserId)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmptyInitialGameStateJson_CreatesSessionWithDefaultState()
    {
        // Arrange: empty JSON — handler must NOT call GameState.FromJson, must use GameState.Initial(UserId)
        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: string.Empty);

        // Act: must NOT throw
        var agentSessionId = await _handler.Handle(command, CancellationToken.None);

        // Assert: a session was persisted (AddAsync + SaveChanges called once)
        Assert.NotEqual(Guid.Empty, agentSessionId);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhitespaceInitialGameStateJson_CreatesSessionWithDefaultState()
    {
        // Arrange: whitespace — same as empty
        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: "   ");

        var agentSessionId = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, agentSessionId);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidGameStateJson_CreatesSessionUsingProvidedState()
    {
        // Arrange: valid JSON — handler must use GameState.FromJson (existing behaviour preserved)
        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: ValidGameStateJson);

        var agentSessionId = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, agentSessionId);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Guard — duplicate active session
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenActiveSessionExists_ThrowsConflictException()
    {
        _sessionRepoMock
            .Setup(r => r.HasActiveSessionAsync(_gameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new LaunchSessionAgentCommand(
            GameSessionId: _gameSessionId,
            AgentDefinitionId: _agentDefinitionId,
            UserId: _userId,
            GameId: _gameId,
            InitialGameStateJson: string.Empty);

        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
