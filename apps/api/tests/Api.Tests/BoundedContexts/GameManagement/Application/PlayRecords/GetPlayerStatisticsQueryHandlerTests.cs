using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
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
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static PlayRecordEntity MakePlayRecord(
        Guid id,
        Guid userId,
        Guid? gameId,
        string gameName = "Test Game",
        TimeSpan? duration = null) => new()
    {
        Id = id,
        GameId = gameId,
        GameName = gameName,
        CreatedByUserId = userId,
        Visibility = 0,
        SessionDate = DateTime.UtcNow.AddDays(-1),
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
