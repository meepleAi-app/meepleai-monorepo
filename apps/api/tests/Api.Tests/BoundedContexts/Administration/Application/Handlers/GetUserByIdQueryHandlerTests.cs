using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetUserByIdQueryHandler.
/// Tests user retrieval by ID with session tracking.
/// </summary>
public class GetUserByIdQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _handler = new GetUserByIdQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var query = new GetUserByIdQuery(userId.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId.ToString(), result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("User", result.Role);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var query = new GetUserByIdQuery(nonExistentId.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithUserHavingActiveSessions_ReturnsLastSeenAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lastSeenDate = DateTime.UtcNow.AddHours(-2);

        var user = new UserEntity
        {
            Id = userId,
            Email = "active@example.com",
            DisplayName = "Active User",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "session_token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastSeenAt = lastSeenDate,
            RevokedAt = null // Active session
        };

        _dbContext.Users.Add(user);
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var query = new GetUserByIdQuery(userId.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LastSeenAt);
        Assert.Equal(lastSeenDate, result.LastSeenAt.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_WithMultipleSessions_ReturnsLatestLastSeenAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldLastSeen = DateTime.UtcNow.AddDays(-5);
        var recentLastSeen = DateTime.UtcNow.AddHours(-1);

        var user = new UserEntity
        {
            Id = userId,
            Email = "multisession@example.com",
            DisplayName = "Multi Session User",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var oldSession = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "old_session",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastSeenAt = oldLastSeen,
            RevokedAt = null
        };

        var recentSession = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "recent_session",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastSeenAt = recentLastSeen,
            RevokedAt = null
        };

        _dbContext.Users.Add(user);
        _dbContext.Sessions.AddRange(oldSession, recentSession);
        await _dbContext.SaveChangesAsync();

        var query = new GetUserByIdQuery(userId.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LastSeenAt);
        Assert.Equal(recentLastSeen, result.LastSeenAt.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_IgnoresRevokedSessions_WhenCalculatingLastSeen()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var revokedLastSeen = DateTime.UtcNow.AddHours(-1);

        var user = new UserEntity
        {
            Id = userId,
            Email = "revoked@example.com",
            DisplayName = "Revoked Session User",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var revokedSession = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "revoked_session",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastSeenAt = revokedLastSeen,
            RevokedAt = DateTime.UtcNow.AddHours(-2) // Revoked session
        };

        _dbContext.Users.Add(user);
        _dbContext.Sessions.Add(revokedSession);
        await _dbContext.SaveChangesAsync();

        var query = new GetUserByIdQuery(userId.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LastSeenAt); // Should ignore revoked sessions
    }

    [Fact]
    public async Task Handle_WithEmptyDisplayName_ReturnsEmptyString()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "nodisplay@example.com",
            DisplayName = null, // Null display name
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var query = new GetUserByIdQuery(userId.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.DisplayName);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
