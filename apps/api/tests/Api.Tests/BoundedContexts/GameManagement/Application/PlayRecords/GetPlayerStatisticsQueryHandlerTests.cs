using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="GetPlayerStatisticsQueryHandler"/>.
/// Verifies Phase 2 statistics dashboard fields:
/// TotalDurationMinutes, WinByGame, MostPlayedGames.
/// Also provides a regression guard ensuring existing TotalWins behaviour
/// is preserved after refactoring to <see cref="Api.BoundedContexts.GameManagement.Application.Services.PlayRecordOutcomeCalculator.HasWinner"/>.
/// Issue #1663: Phase 2 – statistics dashboard fields.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1663")]
public class GetPlayerStatisticsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly GetPlayerStatisticsQueryHandler _handler;

    public GetPlayerStatisticsQueryHandlerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetPlayerStatisticsQueryHandler(_context);
    }

    public void Dispose() => _context.Dispose();

    // ──────────────────────────────────────────────────────────────────────────
    // TotalWins regression guard (existing behaviour unchanged after refactor)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TotalWins_ThreeRecordsTwoWithWinner_ReturnsTwoAfterRefactor()
    {
        // Arrange — regression check: refactoring totalWins to use HasWinner must not change the result
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var win1 = MakePlayRecord(Guid.NewGuid(), userId, gameId, "Game A");
        win1.Players = [MakePlayer(Guid.NewGuid(), win1.Id, ("wins", 1))];

        var win2 = MakePlayRecord(Guid.NewGuid(), userId, gameId, "Game A");
        win2.Players = [MakePlayer(Guid.NewGuid(), win2.Id, ("wins", 1))];

        var loss = MakePlayRecord(Guid.NewGuid(), userId, gameId, "Game A");
        loss.Players = [MakePlayer(Guid.NewGuid(), loss.Id, ("wins", 0))];

        _context.PlayRecords.AddRange(win1, win2, loss);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(userId), TestContext.Current.CancellationToken);

        // Assert
        result.TotalWins.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WinByGame
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WinByGame_ThreeGameARecordsAndTwoGameBRecords_CorrectCountsAndOrder()
    {
        // Arrange
        // Game A: 3 records — 2 with winner, 1 without
        // Game B: 2 records — 1 with winner
        var userId = Guid.NewGuid();
        var gameAId = Guid.NewGuid();
        var gameBId = Guid.NewGuid();

        var a1 = MakePlayRecord(Guid.NewGuid(), userId, gameAId, "Game A");
        a1.Players = [MakePlayer(Guid.NewGuid(), a1.Id, ("wins", 1))];

        var a2 = MakePlayRecord(Guid.NewGuid(), userId, gameAId, "Game A");
        a2.Players = [MakePlayer(Guid.NewGuid(), a2.Id, ("wins", 1))];

        var a3 = MakePlayRecord(Guid.NewGuid(), userId, gameAId, "Game A");
        a3.Players = [MakePlayer(Guid.NewGuid(), a3.Id, ("wins", 0))];

        var b1 = MakePlayRecord(Guid.NewGuid(), userId, gameBId, "Game B");
        b1.Players = [MakePlayer(Guid.NewGuid(), b1.Id, ("wins", 1))];

        var b2 = MakePlayRecord(Guid.NewGuid(), userId, gameBId, "Game B");
        b2.Players = [MakePlayer(Guid.NewGuid(), b2.Id, ("wins", 0))];

        _context.PlayRecords.AddRange(a1, a2, a3, b1, b2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(userId), TestContext.Current.CancellationToken);

        // Assert — ordered by Played desc
        result.WinByGame.Should().HaveCount(2);

        var first = result.WinByGame[0]; // Game A — most played
        first.GameId.Should().Be(gameAId);
        first.GameName.Should().Be("Game A");
        first.Played.Should().Be(3);
        first.Won.Should().Be(2);

        var second = result.WinByGame[1]; // Game B
        second.GameId.Should().Be(gameBId);
        second.GameName.Should().Be("Game B");
        second.Played.Should().Be(2);
        second.Won.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MostPlayedGames
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MostPlayedGames_ThreeGameAAndTwoGameB_OrderedByPlaysDesc()
    {
        // Arrange — same setup as WinByGame test (shared scenario)
        var userId = Guid.NewGuid();
        var gameAId = Guid.NewGuid();
        var gameBId = Guid.NewGuid();

        var a1 = MakePlayRecord(Guid.NewGuid(), userId, gameAId, "Game A");
        a1.Players = [MakePlayer(Guid.NewGuid(), a1.Id, ("wins", 1))];

        var a2 = MakePlayRecord(Guid.NewGuid(), userId, gameAId, "Game A");
        a2.Players = [MakePlayer(Guid.NewGuid(), a2.Id, ("wins", 1))];

        var a3 = MakePlayRecord(Guid.NewGuid(), userId, gameAId, "Game A");
        a3.Players = [MakePlayer(Guid.NewGuid(), a3.Id, ("wins", 0))];

        var b1 = MakePlayRecord(Guid.NewGuid(), userId, gameBId, "Game B");
        b1.Players = [MakePlayer(Guid.NewGuid(), b1.Id, ("wins", 1))];

        var b2 = MakePlayRecord(Guid.NewGuid(), userId, gameBId, "Game B");
        b2.Players = [MakePlayer(Guid.NewGuid(), b2.Id, ("wins", 0))];

        _context.PlayRecords.AddRange(a1, a2, a3, b1, b2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(userId), TestContext.Current.CancellationToken);

        // Assert — ordered by Plays desc
        result.MostPlayedGames.Should().HaveCount(2);

        result.MostPlayedGames[0].GameId.Should().Be(gameAId);
        result.MostPlayedGames[0].Plays.Should().Be(3);

        result.MostPlayedGames[1].GameId.Should().Be(gameBId);
        result.MostPlayedGames[1].Plays.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TotalDurationMinutes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TotalDurationMinutes_OneAndHalfHoursPlusTwoHoursPlusNullDuration_Returns210Minutes()
    {
        // Arrange: 01:30:00 + 02:00:00 + null → 90 + 120 + 0 = 210
        var userId = Guid.NewGuid();

        var r1 = MakePlayRecord(Guid.NewGuid(), userId, Guid.NewGuid(), "Game A", duration: TimeSpan.FromHours(1.5));
        r1.Players = [MakePlayer(Guid.NewGuid(), r1.Id)];

        var r2 = MakePlayRecord(Guid.NewGuid(), userId, Guid.NewGuid(), "Game B", duration: TimeSpan.FromHours(2));
        r2.Players = [MakePlayer(Guid.NewGuid(), r2.Id)];

        var r3 = MakePlayRecord(Guid.NewGuid(), userId, Guid.NewGuid(), "Game C", duration: null);
        r3.Players = [MakePlayer(Guid.NewGuid(), r3.Id)];

        _context.PlayRecords.AddRange(r1, r2, r3);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(userId), TestContext.Current.CancellationToken);

        // Assert
        result.TotalDurationMinutes.Should().Be(210);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Free-form game (GameId == null)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WinByGame_FreeFormRecordsWithSameName_GroupedByNullGameIdAndGameName()
    {
        // Arrange — two free-form records with GameId=null but same GameName should aggregate
        var userId = Guid.NewGuid();

        var r1 = MakePlayRecord(Guid.NewGuid(), userId, gameId: null, gameName: "Home Rules");
        r1.Players = [MakePlayer(Guid.NewGuid(), r1.Id, ("wins", 1))];

        var r2 = MakePlayRecord(Guid.NewGuid(), userId, gameId: null, gameName: "Home Rules");
        r2.Players = [MakePlayer(Guid.NewGuid(), r2.Id, ("wins", 0))];

        _context.PlayRecords.AddRange(r1, r2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(userId), TestContext.Current.CancellationToken);

        // Assert — grouped into a single entry with GameId == null
        var entry = result.WinByGame.Should().ContainSingle().Subject;
        entry.GameId.Should().BeNull();
        entry.GameName.Should().Be("Home Rules");
        entry.Played.Should().Be(2);
        entry.Won.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Empty record set
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptyRecordSet_AllNewFieldsReturnDefaultValues()
    {
        // Arrange — no records for this user
        var query = new GetPlayerStatisticsQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.TotalDurationMinutes.Should().Be(0);
        result.WinByGame.Should().BeEmpty();
        result.MostPlayedGames.Should().BeEmpty();

        // Existing fields also default correctly
        result.TotalSessions.Should().Be(0);
        result.TotalWins.Should().Be(0);

        // #1540 / #1541 / #1550 — new field defaults
        result.LeaderboardRank.Should().BeNull();
        result.FavoriteAgentName.Should().BeNull();
        result.WinRateTrend.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #1540: LeaderboardRank
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Issue", "1540")]
    public async Task LeaderboardRank_UserWithZeroSessions_ReturnsNull()
    {
        // Arrange — no records for this user. The user is unranked.
        var userId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(userId), TestContext.Current.CancellationToken);

        // Assert
        result.TotalSessions.Should().Be(0);
        result.LeaderboardRank.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "1540")]
    public async Task LeaderboardRank_TwoOtherUsersWithMoreWins_ReturnsThirdPlace()
    {
        // Arrange — caller has 1 win; two other users have 3 and 2 wins respectively.
        // Both have STRICTLY more wins than caller → rank = 2 + 1 = 3.
        var caller = Guid.NewGuid();
        var rival1 = Guid.NewGuid();
        var rival2 = Guid.NewGuid();

        // Caller: 1 win, 1 loss
        var c1 = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A");
        c1.Players = [MakePlayer(Guid.NewGuid(), c1.Id, ("wins", 1))];
        var c2 = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A");
        c2.Players = [MakePlayer(Guid.NewGuid(), c2.Id, ("wins", 0))];

        // Rival 1: 3 wins
        for (var i = 0; i < 3; i++)
        {
            var r = MakePlayRecord(Guid.NewGuid(), rival1, Guid.NewGuid(), "Game A");
            r.Players = [MakePlayer(Guid.NewGuid(), r.Id, ("wins", 1))];
            _context.PlayRecords.Add(r);
        }

        // Rival 2: 2 wins
        for (var i = 0; i < 2; i++)
        {
            var r = MakePlayRecord(Guid.NewGuid(), rival2, Guid.NewGuid(), "Game A");
            r.Players = [MakePlayer(Guid.NewGuid(), r.Id, ("wins", 1))];
            _context.PlayRecords.Add(r);
        }

        _context.PlayRecords.AddRange(c1, c2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.TotalWins.Should().Be(1);
        result.LeaderboardRank.Should().Be(3); // 2 users ahead → rank 3
    }

    [Fact]
    [Trait("Issue", "1540")]
    public async Task LeaderboardRank_UserWithMostWins_ReturnsFirstPlace()
    {
        // Arrange — caller has 5 wins; one rival has 2. Caller is the leader.
        var caller = Guid.NewGuid();
        var rival = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            var r = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A");
            r.Players = [MakePlayer(Guid.NewGuid(), r.Id, ("wins", 1))];
            _context.PlayRecords.Add(r);
        }

        for (var i = 0; i < 2; i++)
        {
            var r = MakePlayRecord(Guid.NewGuid(), rival, Guid.NewGuid(), "Game A");
            r.Players = [MakePlayer(Guid.NewGuid(), r.Id, ("wins", 1))];
            _context.PlayRecords.Add(r);
        }

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.TotalWins.Should().Be(5);
        result.LeaderboardRank.Should().Be(1); // 0 users ahead → rank 1
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #1541: FavoriteAgentName
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Issue", "1541")]
    public async Task FavoriteAgentName_UserWithNoChatThreads_ReturnsNull()
    {
        // Arrange — caller has play records but no chat threads with agents.
        var caller = Guid.NewGuid();
        var r = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A");
        r.Players = [MakePlayer(Guid.NewGuid(), r.Id)];
        _context.PlayRecords.Add(r);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.FavoriteAgentName.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "1541")]
    public async Task FavoriteAgentName_TwoThreadsForAgentAOneForAgentB_ReturnsAgentAName()
    {
        // Arrange — caller has 2 threads with Agent A and 1 with Agent B.
        // Most-used agent (by thread count) = A. Returned name = "Mago di Wingspan".
        var caller = Guid.NewGuid();

        var agentA = AgentDefinition.Create(
            "Mago di Wingspan",
            "Wingspan tutor",
            AgentType.RagAgent,
            AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f));

        var agentB = AgentDefinition.Create(
            "Mago di Catan",
            "Catan tutor",
            AgentType.RagAgent,
            AgentDefinitionConfig.Create("gpt-4", 2048, 0.7f));

        _context.AgentDefinitions.AddRange(agentA, agentB);

        _context.ChatThreads.AddRange(
            new ChatThreadEntity { UserId = caller, AgentId = agentA.Id, MessagesJson = "[]" },
            new ChatThreadEntity { UserId = caller, AgentId = agentA.Id, MessagesJson = "[]" },
            new ChatThreadEntity { UserId = caller, AgentId = agentB.Id, MessagesJson = "[]" });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.FavoriteAgentName.Should().Be("Mago di Wingspan");
    }

    [Fact]
    [Trait("Issue", "1541")]
    public async Task FavoriteAgentName_ThreadsWithoutAgentId_AreIgnored()
    {
        // Arrange — caller has chat threads but all have AgentId == null
        // (anonymous / no-agent threads). Result must be NULL, not picked from null.
        var caller = Guid.NewGuid();

        _context.ChatThreads.AddRange(
            new ChatThreadEntity { UserId = caller, AgentId = null, MessagesJson = "[]" },
            new ChatThreadEntity { UserId = caller, AgentId = null, MessagesJson = "[]" });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.FavoriteAgentName.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #1550: WinRateTrend
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Issue", "1550")]
    public async Task WinRateTrend_NoRecordsInLast6Months_ReturnsEmpty()
    {
        // Arrange — caller has records BUT all are older than 6 months → excluded.
        var caller = Guid.NewGuid();
        var oldDate = DateTime.UtcNow.AddMonths(-12); // 1 year ago

        var r = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A", sessionDate: oldDate);
        r.Players = [MakePlayer(Guid.NewGuid(), r.Id, ("wins", 1))];
        _context.PlayRecords.Add(r);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.WinRateTrend.Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "1550")]
    public async Task WinRateTrend_TwoMonthsWithPlays_ReturnsMonthlyAggregatesSortedAsc()
    {
        // Arrange — caller has 4 records: 2 in current month (1 win), 2 in previous
        // month (2 wins). Trend should include both buckets, ordered ascending by month.
        var caller = Guid.NewGuid();
        var thisMonth = DateTime.UtcNow;
        var prevMonth = DateTime.UtcNow.AddMonths(-1);

        var c1 = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A", sessionDate: thisMonth);
        c1.Players = [MakePlayer(Guid.NewGuid(), c1.Id, ("wins", 1))];
        var c2 = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A", sessionDate: thisMonth);
        c2.Players = [MakePlayer(Guid.NewGuid(), c2.Id, ("wins", 0))];

        var p1 = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A", sessionDate: prevMonth);
        p1.Players = [MakePlayer(Guid.NewGuid(), p1.Id, ("wins", 1))];
        var p2 = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A", sessionDate: prevMonth);
        p2.Players = [MakePlayer(Guid.NewGuid(), p2.Id, ("wins", 1))];

        _context.PlayRecords.AddRange(c1, c2, p1, p2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert — 2 buckets, ascending order
        result.WinRateTrend.Should().HaveCount(2);

        var firstBucket = result.WinRateTrend[0]; // prev month
        firstBucket.Month.Should().Be($"{prevMonth.Year:D4}-{prevMonth.Month:D2}");
        firstBucket.WinRate.Should().Be(1.0); // 2 wins / 2 plays

        var secondBucket = result.WinRateTrend[1]; // this month
        secondBucket.Month.Should().Be($"{thisMonth.Year:D4}-{thisMonth.Month:D2}");
        secondBucket.WinRate.Should().Be(0.5); // 1 win / 2 plays
    }

    [Fact]
    [Trait("Issue", "1550")]
    public async Task WinRateTrend_RecordWithZeroWinsInMonth_ReturnsZeroWinRate()
    {
        // Arrange — single record in current month, no wins. WinRate must be 0
        // (valid datum: "played but never won"), NOT excluded from trend.
        var caller = Guid.NewGuid();

        var r = MakePlayRecord(Guid.NewGuid(), caller, Guid.NewGuid(), "Game A", sessionDate: DateTime.UtcNow);
        r.Players = [MakePlayer(Guid.NewGuid(), r.Id, ("wins", 0))];
        _context.PlayRecords.Add(r);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(new GetPlayerStatisticsQuery(caller), TestContext.Current.CancellationToken);

        // Assert
        result.WinRateTrend.Should().HaveCount(1);
        result.WinRateTrend[0].WinRate.Should().Be(0.0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static PlayRecordEntity MakePlayRecord(
        Guid id,
        Guid userId,
        Guid? gameId,
        string gameName = "Test Game",
        TimeSpan? duration = null,
        DateTime? sessionDate = null) => new()
    {
        Id = id,
        GameId = gameId,
        GameName = gameName,
        CreatedByUserId = userId,
        Visibility = 0,
        SessionDate = sessionDate ?? DateTime.UtcNow.AddDays(-1),
        Duration = duration,
        Status = 2, // Completed
        ScoringConfigJson = """{"Dimensions":["points","wins"],"Units":{"points":"pts","wins":"W"}}""",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static RecordPlayerEntity MakePlayer(
        Guid id,
        Guid playRecordId,
        params (string Dimension, int Value)[] scores) =>
        new()
        {
            Id = id,
            PlayRecordId = playRecordId,
            DisplayName = $"Player-{id:N}",
            Scores = scores.Select(s => new RecordScoreEntity
            {
                Id = Guid.NewGuid(),
                RecordPlayerId = id,
                Dimension = s.Dimension,
                Value = s.Value
            }).ToList()
        };
}
