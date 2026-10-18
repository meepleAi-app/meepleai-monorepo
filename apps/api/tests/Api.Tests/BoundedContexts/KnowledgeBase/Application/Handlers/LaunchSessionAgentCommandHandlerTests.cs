using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for LaunchSessionAgentCommandHandler.
/// Issue #2500 (C1 fix): empty InitialGameStateJson defaults to GameState.Initial(UserId).
/// Issue #4282: the launch is for the owner of the game session, on its game, through the RAG gate.
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
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IRagAccessService> _ragAccessMock;
    private readonly LaunchSessionAgentCommandHandler _handler;

    public LaunchSessionAgentCommandHandlerTests()
    {
        _sessionRepoMock = new Mock<IAgentSessionRepository>();
        _definitionRepoMock = new Mock<IAgentDefinitionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();
        _ragAccessMock = new Mock<IRagAccessService>();

        // #4282: by default the caller owns the game session, the request names its game, and the
        // caller may query the game's KB. The guard tests below change one of the three.
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGameSessionOwnerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameSessionOwnerDto(_userId, _gameId));
        _ragAccessMock
            .Setup(r => r.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
            _mediatorMock.Object,
            _ragAccessMock.Object,
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
        UserRole: "User",
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
    public async Task Handle_WithoutAgentDefinitionId_PrefersTheActiveSystemAgent_OverAnOlderInactiveOne()
    {
        // ADR-094 prevede un solo agente di sistema, ma se ne esistessero due (uno vecchio spento e
        // uno attivo) il lancio non deve rispondere «non disponibile» avendo un agente attivo.
        var olderInactive = SystemAgent(active: false);
        var newerActive = SystemAgent(active: true);
        _definitionRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([olderInactive, newerActive]);

        AgentSession? persisted = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()))
            .Callback<AgentSession, CancellationToken>((s, _) => persisted = s)
            .Returns(Task.CompletedTask);

        await _handler.Handle(CommandWithoutAgent(), CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(newerActive.Id, persisted!.AgentDefinitionId);
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
            UserRole: "User",
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
            UserRole: "User",
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
            UserRole: "User",
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
            UserRole: "User",
            GameId: _gameId,
            InitialGameStateJson: string.Empty);

        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // #4282 — who may launch: the owner of the game session, on its game, through the RAG gate
    // ──────────────────────────────────────────────────────────────────────────────

    private void GameSessionIs(GameSessionOwnerDto? owner) =>
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGameSessionOwnerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

    private void NothingWasCreatedNorChecked()
    {
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Never);
        // The 409 comes after the guard: a refused caller learns nothing about an active agent.
        _sessionRepoMock.Verify(r => r.HasActiveSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AsksGameManagementForTheGameSessionOfTheRoute()
    {
        await _handler.Handle(CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId }, CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<GetGameSessionOwnerQuery>(q => q.GameSessionId == _gameSessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameSessionDoesNotExist_ThrowsNotFound()
    {
        GameSessionIs(null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId }, CancellationToken.None));
        NothingWasCreatedNorChecked();
    }

    [Fact]
    public async Task Handle_GameSessionOfAnotherUser_ThrowsForbidden_EvenWhenItHasAnActiveAgent()
    {
        GameSessionIs(new GameSessionOwnerDto(Guid.NewGuid(), _gameId));
        _sessionRepoMock
            .Setup(r => r.HasActiveSessionAsync(_gameSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId }, CancellationToken.None));
        NothingWasCreatedNorChecked();
    }

    [Fact]
    public async Task Handle_GameSessionWithoutOwner_ThrowsForbidden()
    {
        // A session created before #3070 has no owner: nobody launches its agent.
        GameSessionIs(new GameSessionOwnerDto(null, _gameId));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId }, CancellationToken.None));
        NothingWasCreatedNorChecked();
    }

    [Fact]
    public async Task Handle_GameOtherThanTheGameOfTheSession_ThrowsBadRequest_BeforeTheRagGate()
    {
        var command = CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId, GameId = Guid.NewGuid() };

        await Assert.ThrowsAsync<BadRequestException>(() => _handler.Handle(command, CancellationToken.None));
        NothingWasCreatedNorChecked();
        _ragAccessMock.Verify(r => r.CanAccessRagAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerCannotQueryTheGame_ThrowsRagOwnershipRequired()
    {
        _ragAccessMock
            .Setup(r => r.CanAccessRagAsync(_userId, _gameId, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<RagOwnershipRequiredException>(
            () => _handler.Handle(CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId }, CancellationToken.None));

        Assert.Equal("RAG_OWNERSHIP_REQUIRED", ex.ErrorCode);
        NothingWasCreatedNorChecked();
    }

    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("superadmin", UserRole.SuperAdmin)]
    [InlineData("User", UserRole.User)]
    [InlineData("not-a-role", UserRole.User)]
    public async Task Handle_PassesTheCallersRoleToTheRagGate(string claimedRole, UserRole expected)
    {
        var command = CommandWithoutAgent() with { AgentDefinitionId = _agentDefinitionId, UserRole = claimedRole };

        await _handler.Handle(command, CancellationToken.None);

        _ragAccessMock.Verify(r => r.CanAccessRagAsync(_userId, _gameId, expected, It.IsAny<CancellationToken>()), Times.Once);
    }
}
