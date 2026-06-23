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
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LaunchSessionAgentCommandHandler _handler;

    public LaunchSessionAgentCommandHandlerTests()
    {
        _sessionRepoMock = new Mock<IAgentSessionRepository>();
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
            _unitOfWorkMock.Object,
            NullLogger<LaunchSessionAgentCommandHandler>.Instance);
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
