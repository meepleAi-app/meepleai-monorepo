using Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Tests for GetTurnPhasesQueryHandler.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetTurnPhasesQueryHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly FakeTimeProvider _timeProvider;

    public GetTurnPhasesQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    }

    private GetTurnPhasesQueryHandler CreateHandler() =>
        new(_repositoryMock.Object);

    private LiveGameSession CreateStartedSession(Guid? sessionId = null)
    {
        var session = LiveGameSession.Create(
            sessionId ?? Guid.NewGuid(), Guid.NewGuid(), "Test Game", _timeProvider);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        return session;
    }

    [Fact]
    public async Task Handle_SessionWithPhases_ReturnsCorrectDto()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetTurnPhasesQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.CurrentPhaseIndex);
        Assert.Equal("Draw", result.CurrentPhaseName);
        Assert.Equal(3, result.PhaseNames.Length);
        Assert.Equal(3, result.TotalPhases);
        Assert.True(result.HasPhases);
    }

    [Fact]
    public async Task Handle_SessionWithoutPhases_ReturnsEmptyDto()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetTurnPhasesQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.CurrentPhaseIndex);
        Assert.Null(result.CurrentPhaseName);
        Assert.Empty(result.PhaseNames);
        Assert.Equal(0, result.TotalPhases);
        Assert.False(result.HasPhases);
    }

    [Fact]
    public async Task Handle_AfterPhaseAdvance_ReturnsUpdatedPhase()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);
        session.ConfigurePhases(new[] { "Draw", "Action", "Cleanup" }, _timeProvider);
        session.AdvancePhase(_timeProvider); // → Action (1)

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetTurnPhasesQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.CurrentPhaseIndex);
        Assert.Equal("Action", result.CurrentPhaseName);
    }

    [Fact]
    public async Task Handle_IncludesCurrentTurnIndex()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);
        session.ConfigurePhases(new[] { "Draw", "Action" }, _timeProvider);
        session.AdvanceTurn(_timeProvider); // Turn 1 → 2

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetTurnPhasesQuery(sessionId), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.CurrentTurnIndex);
        Assert.Equal(0, result.CurrentPhaseIndex); // Reset after turn advance
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetTurnPhasesQuery(sessionId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNull()
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateStartedSession(sessionId);
        var ct = TestContext.Current.CancellationToken;

        _repositoryMock.Setup(r => r.GetByIdAsync(sessionId, ct))
            .ReturnsAsync(session);

        var handler = CreateHandler();
        await handler.Handle(new GetTurnPhasesQuery(sessionId), ct);

        _repositoryMock.Verify(r => r.GetByIdAsync(sessionId, ct), Times.Once);
    }
}
