using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Asse A semantic alignment #1896 (T10, DEC-1): unit tests for the polymorphic
/// score update handler. Covers the 4 ScoreType variants round-trip through
/// <c>Session.SetScores</c> + IScoringStrategy.ComputeWinnerPlayerId.
///
/// <para>Full HTTP integration test (Testcontainers Postgres + auth seed) is deferred —
/// these unit tests with Moq cover the happy path + the 4 polymorphic schemas via the
/// real <see cref="ScoringStrategyFactory"/>. Schema validation is covered separately
/// by <c>UpdateSessionScoresCommandValidatorTests</c>.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdateSessionScoresCommandHandlerTests
{
    private static readonly Guid PlayerA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlayerB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ScoringStrategyFactory _factory = new();
    private readonly UpdateSessionScoresCommandHandler _handler;

    public UpdateSessionScoresCommandHandlerTests()
    {
        _handler = new UpdateSessionScoresCommandHandler(
            _sessionRepoMock.Object,
            _unitOfWorkMock.Object,
            _factory);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var cmd = new UpdateSessionScoresCommand(
            Guid.NewGuid(),
            ScoreType.Points,
            $$"""{"scores":[{"playerId":"{{PlayerA}}","points":10}]}""");

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidPointsScoring_PersistsAndReturnsWinner()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var json = $$"""{"scores":[{"playerId":"{{PlayerA}}","points":42},{"playerId":"{{PlayerB}}","points":15}]}""";
        var cmd = new UpdateSessionScoresCommand(session.Id, ScoreType.Points, json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.SessionId.Should().Be(session.Id);
        result.ScoringType.Should().Be(ScoreType.Points);
        result.ComputedWinnerId.Should().Be(PlayerA);
        session.ScoringType.Should().Be(ScoreType.Points);
        session.ScoreData.Should().Be(json);
        _sessionRepoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TiedPointsScoring_ReturnsNullWinner()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var json = $$"""{"scores":[{"playerId":"{{PlayerA}}","points":50},{"playerId":"{{PlayerB}}","points":50}]}""";
        var cmd = new UpdateSessionScoresCommand(session.Id, ScoreType.Points, json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ComputedWinnerId.Should().BeNull("ties yield null winner per PointsScoringStrategy");
    }

    [Fact]
    public async Task Handle_BinaryWinScoring_PersistsAndReturnsWinner()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var json = $$"""{"results":[{"playerId":"{{PlayerA}}","isWinner":true},{"playerId":"{{PlayerB}}","isWinner":false}]}""";
        var cmd = new UpdateSessionScoresCommand(session.Id, ScoreType.BinaryWin, json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ScoringType.Should().Be(ScoreType.BinaryWin);
        result.ComputedWinnerId.Should().Be(PlayerA);
        session.ScoringType.Should().Be(ScoreType.BinaryWin);
        session.ScoreData.Should().Be(json);
    }

    [Fact]
    public async Task Handle_CooperativeAllWin_ReturnsNullWinner()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var json = $$"""{"results":[{"playerId":"{{PlayerA}}","isWinner":true},{"playerId":"{{PlayerB}}","isWinner":true}]}""";
        var cmd = new UpdateSessionScoresCommand(session.Id, ScoreType.BinaryWin, json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ComputedWinnerId.Should().BeNull("cooperative all-win yields null per BinaryWinScoringStrategy");
    }

    [Fact]
    public async Task Handle_ObjectivesScoring_PersistsAndReturnsWinner()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var json = $$"""{"completedByPlayer":[{"playerId":"{{PlayerA}}","objectives":["A","B","C"]},{"playerId":"{{PlayerB}}","objectives":["A"]}]}""";
        var cmd = new UpdateSessionScoresCommand(session.Id, ScoreType.Objectives, json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ScoringType.Should().Be(ScoreType.Objectives);
        result.ComputedWinnerId.Should().Be(PlayerA);
        session.ScoringType.Should().Be(ScoreType.Objectives);
        session.ScoreData.Should().Be(json);
    }

    [Fact]
    public async Task Handle_RankingScoring_PersistsAndReturnsWinner()
    {
        var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var json = $$"""{"positions":[{"playerId":"{{PlayerA}}","position":1},{"playerId":"{{PlayerB}}","position":2}]}""";
        var cmd = new UpdateSessionScoresCommand(session.Id, ScoreType.Ranking, json);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.ScoringType.Should().Be(ScoreType.Ranking);
        result.ComputedWinnerId.Should().Be(PlayerA);
        session.ScoringType.Should().Be(ScoreType.Ranking);
        session.ScoreData.Should().Be(json);
    }

    [Fact]
    public async Task Handle_AllFourScoreTypes_RoundTripPersistsScoringTypeAndData()
    {
        // Single-test sweep: verify that all 4 ScoreType values round-trip through SetScores
        // (handler → Session.SetScores → UpdateAsync → SaveChangesAsync) without exception
        // AND that the ScoringType + ScoreData are written back to the aggregate.
        var cases = new[]
        {
            (Type: ScoreType.Points,
             Json: $$"""{"scores":[{"playerId":"{{PlayerA}}","points":1}]}"""),
            (Type: ScoreType.BinaryWin,
             Json: $$"""{"results":[{"playerId":"{{PlayerA}}","isWinner":true}]}"""),
            (Type: ScoreType.Objectives,
             Json: $$"""{"completedByPlayer":[{"playerId":"{{PlayerA}}","objectives":["X"]}]}"""),
            (Type: ScoreType.Ranking,
             Json: $$"""{"positions":[{"playerId":"{{PlayerA}}","position":1}]}"""),
        };

        foreach (var (type, json) in cases)
        {
            var session = Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.Generic);
            _sessionRepoMock
                .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);

            var cmd = new UpdateSessionScoresCommand(session.Id, type, json);
            var result = await _handler.Handle(cmd, CancellationToken.None);

            result.ScoringType.Should().Be(type, $"round-trip for {type} should echo ScoringType");
            session.ScoringType.Should().Be(type, $"Session.ScoringType should equal {type} after SetScores");
            session.ScoreData.Should().Be(json, $"Session.ScoreData should equal payload for {type}");
        }
    }
}
