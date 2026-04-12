using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class GetCurrentSessionQueryTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GetCurrentSessionQueryHandler _handler;

    public GetCurrentSessionQueryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new GetCurrentSessionQueryHandler(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_NoActiveOrPausedSession_ReturnsNull()
    {
        // Arrange — user has only a Finalized session.
        var userId = Guid.NewGuid();
        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "ABC123",
            SessionType = "Generic",
            Status = "Finalized",
            SessionDate = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CreatedBy = userId
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetCurrentSessionQuery(userId),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_HasActiveSession_ReturnsIt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = gameId,
            SessionCode = "XYZ789",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetCurrentSessionQuery(userId),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(sessionId);
        result.GameId.Should().Be(gameId);
        result.Status.Should().Be("Active");
        result.SessionCode.Should().Be("XYZ789");
    }

    [Fact]
    public async Task Handle_MultipleSessionsPicksMostRecent()
    {
        // Arrange — two active sessions; the one with newer UpdatedAt should win.
        var userId = Guid.NewGuid();
        var olderSessionId = Guid.NewGuid();
        var newerSessionId = Guid.NewGuid();

        _db.SessionTrackingSessions.AddRange(
            new SessionEntity
            {
                Id = olderSessionId,
                UserId = userId,
                GameId = Guid.NewGuid(),
                SessionCode = "OLD001",
                SessionType = "Generic",
                Status = "Paused",
                SessionDate = DateTime.UtcNow.AddHours(-3),
                UpdatedAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddHours(-3),
                CreatedBy = userId
            },
            new SessionEntity
            {
                Id = newerSessionId,
                UserId = userId,
                GameId = Guid.NewGuid(),
                SessionCode = "NEW001",
                SessionType = "Generic",
                Status = "Active",
                SessionDate = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                CreatedBy = userId
            });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetCurrentSessionQuery(userId),
            CancellationToken.None);

        // Assert — the session with the most recent UpdatedAt is returned.
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(newerSessionId);
        result.SessionCode.Should().Be("NEW001");
    }

    [Fact]
    public async Task Handle_DeletedSessionIsExcluded()
    {
        // Arrange — user has one Active session but it is soft-deleted.
        var userId = Guid.NewGuid();
        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = Guid.NewGuid(),
            SessionCode = "DEL001",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetCurrentSessionQuery(userId),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OtherUsersSessionsNotReturned()
    {
        // Arrange — another user has an active session.
        var requestingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            GameId = Guid.NewGuid(),
            SessionCode = "OTH001",
            SessionType = "Generic",
            Status = "Active",
            SessionDate = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = otherUserId
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetCurrentSessionQuery(requestingUserId),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
