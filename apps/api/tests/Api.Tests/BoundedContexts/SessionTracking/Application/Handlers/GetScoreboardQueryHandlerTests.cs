using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GetScoreboardQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly GetScoreboardQueryHandler _handler;

    public GetScoreboardQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new GetScoreboardQueryHandler(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange — no sessions seeded
        var query = new GetScoreboardQuery(Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeletedSession_ThrowsNotFoundException()
    {
        // Arrange
        var sessionId = await SeedSession(isDeleted: true);
        var query = new GetScoreboardQuery(sessionId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SessionWithNoScores_ReturnsZeroTotals()
    {
        // Arrange
        var sessionId = await SeedSessionWithParticipants();
        var query = new GetScoreboardQuery(sessionId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.NotEmpty(result.Participants);
        Assert.All(result.Participants, p => Assert.Equal(0, p.TotalScore));
    }

    [Fact]
    public async Task Handle_SessionWithScores_AssignsCorrectRanks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var participant1Id = Guid.NewGuid();
        var participant2Id = Guid.NewGuid();

        var session = new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "ABCD12",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Participants =
            [
                new ParticipantEntity
                {
                    Id = participant1Id,
                    SessionId = sessionId,
                    DisplayName = "Player1",
                    IsOwner = true,
                    Role = ParticipantRole.Host,
                    JoinOrder = 1,
                    CreatedAt = DateTime.UtcNow
                },
                new ParticipantEntity
                {
                    Id = participant2Id,
                    SessionId = sessionId,
                    DisplayName = "Player2",
                    IsOwner = false,
                    Role = ParticipantRole.Player,
                    JoinOrder = 2,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        _context.SessionTrackingSessions.Add(session);

        _context.SessionTrackingScoreEntries.AddRange(
            new ScoreEntryEntity { Id = Guid.NewGuid(), SessionId = sessionId, ParticipantId = participant1Id, ScoreValue = 50m, Timestamp = DateTime.UtcNow, CreatedBy = userId },
            new ScoreEntryEntity { Id = Guid.NewGuid(), SessionId = sessionId, ParticipantId = participant2Id, ScoreValue = 100m, Timestamp = DateTime.UtcNow, CreatedBy = userId }
        );

        await _context.SaveChangesAsync();

        var query = new GetScoreboardQuery(sessionId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Participants.Count);

        var leader = result.Participants.First();
        Assert.Equal(1, leader.CurrentRank);
        Assert.Equal(100m, leader.TotalScore);
        Assert.Equal(participant2Id, leader.ParticipantId);

        Assert.Equal(participant2Id, result.CurrentLeaderId);
    }

    [Fact]
    public async Task Handle_SessionWithRoundScores_GroupsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var session = new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "XYZ999",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Participants =
            [
                new ParticipantEntity
                {
                    Id = participantId,
                    SessionId = sessionId,
                    DisplayName = "Solo",
                    IsOwner = true,
                    Role = ParticipantRole.Host,
                    JoinOrder = 1,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        _context.SessionTrackingSessions.Add(session);

        _context.SessionTrackingScoreEntries.AddRange(
            new ScoreEntryEntity { Id = Guid.NewGuid(), SessionId = sessionId, ParticipantId = participantId, RoundNumber = 1, ScoreValue = 30m, Timestamp = DateTime.UtcNow, CreatedBy = userId },
            new ScoreEntryEntity { Id = Guid.NewGuid(), SessionId = sessionId, ParticipantId = participantId, RoundNumber = 2, ScoreValue = 20m, Timestamp = DateTime.UtcNow, CreatedBy = userId }
        );

        await _context.SaveChangesAsync();

        var query = new GetScoreboardQuery(sessionId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.ScoresByRound.Count);
        Assert.True(result.ScoresByRound.ContainsKey(1));
        Assert.True(result.ScoresByRound.ContainsKey(2));
        Assert.Equal(30m, result.ScoresByRound[1][participantId]);
        Assert.Equal(20m, result.ScoresByRound[2][participantId]);
    }

    // Helpers

    private async Task<Guid> SeedSession(bool isDeleted = false)
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _context.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "TST001",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });

        await _context.SaveChangesAsync();
        return sessionId;
    }

    private async Task<Guid> SeedSessionWithParticipants()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        _context.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "TST002",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Participants =
            [
                new ParticipantEntity
                {
                    Id = participantId,
                    SessionId = sessionId,
                    DisplayName = "Owner",
                    IsOwner = true,
                    Role = ParticipantRole.Host,
                    JoinOrder = 1,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        });

        await _context.SaveChangesAsync();
        return sessionId;
    }
}
