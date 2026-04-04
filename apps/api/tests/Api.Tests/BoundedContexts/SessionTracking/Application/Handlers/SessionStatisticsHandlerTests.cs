using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GetSessionStatisticsHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly GetSessionStatisticsHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetSessionStatisticsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new GetSessionStatisticsHandler(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_NoSessions_ReturnsZeroStats()
    {
        var query = new GetSessionStatisticsQuery(_userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalSessions.Should().Be(0);
        result.TotalGamesPlayed.Should().Be(0);
        result.AverageSessionDuration.Should().Be("00:00:00");
        result.MostPlayedGames.Should().BeEmpty();
        result.MonthlyActivity.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithSessions_ReturnsTotalCount()
    {
        var gameId = Guid.NewGuid();
        await SeedSession(gameId, DateTime.UtcNow.AddDays(-1));
        await SeedSession(gameId, DateTime.UtcNow.AddDays(-2));
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddDays(-3));

        var query = new GetSessionStatisticsQuery(_userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalSessions.Should().Be(3);
        result.TotalGamesPlayed.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ExcludesDeletedSessions()
    {
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddDays(-2), isDeleted: true);

        var query = new GetSessionStatisticsQuery(_userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalSessions.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExcludesOtherUserSessions()
    {
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddDays(-2), userId: Guid.NewGuid());

        var query = new GetSessionStatisticsQuery(_userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalSessions.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CalculatesAverageDuration()
    {
        var now = DateTime.UtcNow;
        await SeedSession(Guid.NewGuid(), now.AddHours(-2), finalizedAt: now);
        await SeedSession(Guid.NewGuid(), now.AddHours(-4), finalizedAt: now);

        var query = new GetSessionStatisticsQuery(_userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.AverageSessionDuration.Should().NotBe("00:00:00");
    }

    [Fact]
    public async Task Handle_OrdersMostPlayedByCount()
    {
        var game1 = Guid.NewGuid();
        var game2 = Guid.NewGuid();
        await SeedSession(game1, DateTime.UtcNow.AddDays(-1));
        await SeedSession(game1, DateTime.UtcNow.AddDays(-2));
        await SeedSession(game1, DateTime.UtcNow.AddDays(-3));
        await SeedSession(game2, DateTime.UtcNow.AddDays(-4));

        var query = new GetSessionStatisticsQuery(_userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.MostPlayedGames.Count.Should().Be(2);
        result.MostPlayedGames[0].PlayCount.Should().Be(3);
        result.MostPlayedGames[0].GameId.Should().Be(game1);
    }

    [Fact]
    public async Task Handle_GroupsMonthlyActivity()
    {
        await SeedSession(Guid.NewGuid(), new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        await SeedSession(Guid.NewGuid(), new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));
        await SeedSession(Guid.NewGuid(), new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc));

        var query = new GetSessionStatisticsQuery(_userId, 12);
        var result = await _handler.Handle(query, CancellationToken.None);

        (result.MonthlyActivity.Count >= 2).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RespectsMonthsBackCutoff()
    {
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddDays(-10));
        await SeedSession(Guid.NewGuid(), DateTime.UtcNow.AddMonths(-13));

        var query = new GetSessionStatisticsQuery(_userId, 6);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalSessions.Should().Be(1);
    }

    private async Task SeedSession(
        Guid gameId,
        DateTime sessionDate,
        bool isDeleted = false,
        DateTime? finalizedAt = null,
        Guid? userId = null)
    {
        var entity = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? _userId,
            GameId = gameId,
            SessionCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
            SessionType = "GameSpecific",
            Status = finalizedAt.HasValue ? "Finalized" : "Active",
            SessionDate = sessionDate,
            FinalizedAt = finalizedAt,
            IsDeleted = isDeleted,
            CreatedAt = sessionDate,
            CreatedBy = userId ?? _userId,
        };

        _context.SessionTrackingSessions.Add(entity);
        await _context.SaveChangesAsync();
    }
}

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GetGameStatisticsHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly GetGameStatisticsHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public GetGameStatisticsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new GetGameStatisticsHandler(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_NoSessions_ReturnsZeros()
    {
        var query = new GetGameStatisticsQuery(_userId, _gameId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalPlays.Should().Be(0);
        result.Wins.Should().Be(0);
        result.WinRate.Should().Be(0.0);
        result.AverageSessionDuration.Should().Be("00:00:00");
    }

    [Fact]
    public async Task Handle_ReturnsTotalPlays()
    {
        await SeedSessionWithScore(DateTime.UtcNow.AddDays(-1), 50m);
        await SeedSessionWithScore(DateTime.UtcNow.AddDays(-2), 75m);

        var query = new GetGameStatisticsQuery(_userId, _gameId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalPlays.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CalculatesScoreAggregates()
    {
        await SeedSessionWithScore(DateTime.UtcNow.AddDays(-1), 50m);
        await SeedSessionWithScore(DateTime.UtcNow.AddDays(-2), 100m);

        var query = new GetGameStatisticsQuery(_userId, _gameId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.HighScore.Should().Be(100m);
        result.AverageScore.Should().Be(75.0);
    }

    [Fact]
    public async Task Handle_ExcludesDeletedSessions()
    {
        await SeedSessionWithScore(DateTime.UtcNow.AddDays(-1), 50m);
        await SeedSessionWithScore(DateTime.UtcNow.AddDays(-2), 75m, isDeleted: true);

        var query = new GetGameStatisticsQuery(_userId, _gameId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalPlays.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectGameId()
    {
        var query = new GetGameStatisticsQuery(_userId, _gameId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.GameId.Should().Be(_gameId);
    }

    [Fact]
    public async Task Handle_CalculatesWinRate()
    {
        var session1 = await SeedSession(DateTime.UtcNow.AddDays(-1));
        await SeedScoreEntry(session1, 50m, "winner");
        var session2 = await SeedSession(DateTime.UtcNow.AddDays(-2));
        await SeedScoreEntry(session2, 30m, "loser");

        var query = new GetGameStatisticsQuery(_userId, _gameId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.WinRate.Should().Be(0.5);
    }

    private async Task SeedSessionWithScore(DateTime sessionDate, decimal score, bool isDeleted = false)
    {
        var sessionId = await SeedSession(sessionDate, isDeleted);
        await SeedScoreEntry(sessionId, score);
    }

    private async Task<Guid> SeedSession(DateTime sessionDate, bool isDeleted = false)
    {
        var sessionId = Guid.NewGuid();
        var entity = new SessionEntity
        {
            Id = sessionId,
            UserId = _userId,
            GameId = _gameId,
            SessionCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
            SessionType = "GameSpecific",
            Status = "Active",
            SessionDate = sessionDate,
            IsDeleted = isDeleted,
            CreatedAt = sessionDate,
            CreatedBy = _userId,
        };

        _context.SessionTrackingSessions.Add(entity);
        await _context.SaveChangesAsync();
        return sessionId;
    }

    private async Task SeedScoreEntry(Guid sessionId, decimal score, string category = "score")
    {
        var entry = new ScoreEntryEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            RoundNumber = 1,
            Category = category,
            ScoreValue = score,
            Timestamp = DateTime.UtcNow,
            CreatedBy = _userId,
        };

        _context.SessionTrackingScoreEntries.Add(entry);
        await _context.SaveChangesAsync();
    }
}
