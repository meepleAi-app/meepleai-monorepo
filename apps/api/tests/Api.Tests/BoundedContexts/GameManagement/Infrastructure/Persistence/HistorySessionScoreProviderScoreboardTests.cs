using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// #3022 — GetScoreboardAsync bridges GameSession → LiveGameSession to return the
/// polymorphic score plus the SessionPlayers aligned to scoreData.playerId.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class HistorySessionScoreProviderScoreboardTests
{
    [Fact]
    public async Task GetScoreboardAsync_ReturnsScoreAndAlignedPlayers()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameSessionId = Guid.NewGuid();
        var liveId = Guid.NewGuid();
        var trackingId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        db.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = liveId,
            SessionCode = "S-TEST",
            GameName = "Catan",
            ScoringConfigJson = "{}",
            CorrelatedGameSessionId = gameSessionId,
            TrackingSessionId = trackingId,
        });
        db.SessionPlayers.AddRange(
            new SessionPlayerEntity { Id = p1, LiveGameSessionId = liveId, DisplayName = "Alice", Color = "Red", Role = "Player" },
            new SessionPlayerEntity { Id = p2, LiveGameSessionId = liveId, DisplayName = "Bob", Color = "Blue", Role = "Player" });
        db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = trackingId,
            GameId = Guid.NewGuid(),
            ScoringType = "Points",
            ScoreData = $"{{\"scores\":[{{\"playerId\":\"{p1}\",\"points\":10}},{{\"playerId\":\"{p2}\",\"points\":8}}]}}",
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new HistorySessionScoreProvider(db);
        var result = await sut.GetScoreboardAsync(gameSessionId, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.ScoringType.Should().Be("Points");
        result.Value.Players.Should().HaveCount(2);
        result.Value.Players.Should().ContainSingle(p => p.Id == p1 && p.DisplayName == "Alice" && p.Color == "Red");
        result.Value.Players.Should().ContainSingle(p => p.Id == p2 && p.DisplayName == "Bob" && p.Color == "Blue");
    }

    [Fact]
    public async Task GetScoreboardAsync_NoCorrelatedLive_ReturnsNull()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new HistorySessionScoreProvider(db);

        var result = await sut.GetScoreboardAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetScoreboardAsync_MultipleCorrelated_ReturnsMostRecentTracking()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameSessionId = Guid.NewGuid();
        var liveOld = Guid.NewGuid();
        var liveNew = Guid.NewGuid();
        var trackOld = Guid.NewGuid();
        var trackNew = Guid.NewGuid();

        db.LiveGameSessions.AddRange(
            new LiveGameSessionEntity { Id = liveOld, SessionCode = "S1", GameName = "Catan", ScoringConfigJson = "{}", CorrelatedGameSessionId = gameSessionId, TrackingSessionId = trackOld },
            new LiveGameSessionEntity { Id = liveNew, SessionCode = "S2", GameName = "Catan", ScoringConfigJson = "{}", CorrelatedGameSessionId = gameSessionId, TrackingSessionId = trackNew });
        db.SessionTrackingSessions.AddRange(
            new SessionEntity { Id = trackOld, GameId = Guid.NewGuid(), ScoringType = "Points", ScoreData = "{\"scores\":[]}", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SessionEntity { Id = trackNew, GameId = Guid.NewGuid(), ScoringType = "BinaryWin", ScoreData = "{\"results\":[]}", UpdatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sut = new HistorySessionScoreProvider(db);
        var result = await sut.GetScoreboardAsync(gameSessionId, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Value.ScoringType.Should().Be("BinaryWin");
    }
}
