using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="GetUserPlayHistoryQueryHandler"/>.
/// Verifies Phase 1 outcome fields in <see cref="Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords.PlayRecordSummaryDto"/>:
/// GameId, WinnerPlayerIds, OutcomeType.
/// Issue #1663: Phase 1 – reskin-required fields computed on read.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1663")]
public class GetUserPlayHistoryQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly GetUserPlayHistoryQueryHandler _handler;

    public GetUserPlayHistoryQueryHandlerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetUserPlayHistoryQueryHandler(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Handle_SummaryWithWinner_ReturnsCorrectWinnerPlayerIds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId, userId, gameId: Guid.NewGuid());
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(winnerId, recordId, ("wins", 1)),
            MakePlayer(loserId, recordId, ("wins", 0))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetUserPlayHistoryQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var summary = result.Records.Should().ContainSingle().Subject;
        summary.WinnerPlayerIds.Should().ContainSingle().Which.Should().Be(winnerId);
    }

    [Fact]
    public async Task Handle_SummaryWithNoWinsDimension_ReturnsEmptyWinnersAndNoneOutcome()
    {
        // Arrange — no "wins" dimension → non-competitive
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId, userId, gameId: Guid.NewGuid());
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(Guid.NewGuid(), recordId, ("points", 20)),
            MakePlayer(Guid.NewGuid(), recordId, ("points", 35))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetUserPlayHistoryQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var summary = result.Records.Should().ContainSingle().Subject;
        summary.WinnerPlayerIds.Should().BeEmpty();
        summary.OutcomeType.Should().Be("none");
    }

    [Fact]
    public async Task Handle_SummaryWithWinsDimension_OutcomeTypeIsCompetitive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId, userId, gameId: Guid.NewGuid());
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(Guid.NewGuid(), recordId, ("wins", 1)),
            MakePlayer(Guid.NewGuid(), recordId, ("wins", 0))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetUserPlayHistoryQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Records.Should().ContainSingle().Subject.OutcomeType.Should().Be("competitive");
    }

    [Fact]
    public async Task Handle_FreeFormGame_GameIdIsNull()
    {
        // Arrange — free-form record: GameId = null
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId, userId, gameId: null);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(Guid.NewGuid(), recordId)
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetUserPlayHistoryQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Records.Should().ContainSingle().Subject.GameId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CatalogGame_GameIdIsExposed()
    {
        // Arrange — catalog record: GameId is set
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId, userId, gameId: gameId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(Guid.NewGuid(), recordId)
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetUserPlayHistoryQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Records.Should().ContainSingle().Subject.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task Handle_NoRecords_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetUserPlayHistoryQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Records.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MultipleRecords_AllHaveOutcomeFields()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Record 1: competitive with winner
        var record1Id = Guid.NewGuid();
        var winner1Id = Guid.NewGuid();
        var record1 = MakePlayRecord(record1Id, userId, gameId: Guid.NewGuid());
        record1.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(winner1Id, record1Id, ("wins", 1)),
            MakePlayer(Guid.NewGuid(), record1Id, ("wins", 0))
        };

        // Record 2: free-form (no wins dimension)
        var record2Id = Guid.NewGuid();
        var record2 = MakePlayRecord(record2Id, userId, gameId: null);
        record2.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(Guid.NewGuid(), record2Id, ("points", 99))
        };

        _context.PlayRecords.AddRange(record1, record2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetUserPlayHistoryQuery(userId, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Records.Should().HaveCount(2);
        var competitive = result.Records.First(r => r.OutcomeType == "competitive");
        competitive.WinnerPlayerIds.Should().ContainSingle().Which.Should().Be(winner1Id);
        var none = result.Records.First(r => r.OutcomeType == "none");
        none.WinnerPlayerIds.Should().BeEmpty();
        none.GameId.Should().BeNull();
    }

    #region Test Helpers

    private static PlayRecordEntity MakePlayRecord(Guid id, Guid userId, Guid? gameId) => new()
    {
        Id = id,
        GameId = gameId,
        GameName = "Test Game",
        CreatedByUserId = userId,
        Visibility = 0,
        SessionDate = DateTime.UtcNow.AddDays(-1),
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

    #endregion
}
