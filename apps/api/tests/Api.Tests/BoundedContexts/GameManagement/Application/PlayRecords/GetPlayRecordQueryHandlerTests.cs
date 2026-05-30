using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="GetPlayRecordQueryHandler"/>.
/// Verifies Phase 1 outcome fields: TotalScore on SessionPlayerDto,
/// WinnerPlayerIds and OutcomeType on PlayRecordDto.
/// Issue #1663: Phase 1 – reskin-required fields computed on read.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1663")]
public class GetPlayRecordQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly GetPlayRecordQueryHandler _handler;

    public GetPlayRecordQueryHandlerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetPlayRecordQueryHandler(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Handle_RecordWithWinner_ReturnsCorrectWinnerPlayerIds()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(winnerId, recordId, ("wins", 1)),
            MakePlayer(loserId, recordId, ("wins", 0))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerPlayerIds.Should().ContainSingle().Which.Should().Be(winnerId);
    }

    [Fact]
    public async Task Handle_TieGame_ReturnsBothPlayersAsWinners()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(player1Id, recordId, ("wins", 1)),
            MakePlayer(player2Id, recordId, ("wins", 1))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerPlayerIds.Should().HaveCount(2)
            .And.Contain(player1Id)
            .And.Contain(player2Id);
    }

    [Fact]
    public async Task Handle_NoWinsDimension_ReturnsEmptyWinnersAndNoneOutcome()
    {
        // Arrange — cooperative / in-progress: no "wins" dimension
        var recordId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(player1Id, recordId, ("points", 30)),
            MakePlayer(player2Id, recordId, ("points", 45))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.WinnerPlayerIds.Should().BeEmpty();
        result.OutcomeType.Should().Be("none");
    }

    [Fact]
    public async Task Handle_PlayerWithPoints_ReturnsCorrectTotalScore()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(playerId, recordId, ("points", 42))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var player = result.Players.Should().ContainSingle().Subject;
        player.TotalScore.Should().Be(42);
    }

    [Fact]
    public async Task Handle_PlayerWithNoPoints_ReturnsTotalScoreNull()
    {
        // Arrange — player has "wins" but not "points"
        var recordId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(playerId, recordId, ("wins", 1))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Players.Should().ContainSingle().Subject.TotalScore.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompetitiveGame_OutcomeTypeIsCompetitive()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity>
        {
            MakePlayer(playerId, recordId, ("wins", 1))
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.OutcomeType.Should().Be("competitive");
    }

    [Fact]
    public async Task Handle_RecordNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var query = new GetPlayRecordQuery(Guid.NewGuid());

        // Act & Assert
        var act = () => _handler.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #region Test Helpers

    private static PlayRecordEntity MakePlayRecord(Guid id) => new()
    {
        Id = id,
        GameId = Guid.NewGuid(),
        GameName = "Test Game",
        CreatedByUserId = Guid.NewGuid(),
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
