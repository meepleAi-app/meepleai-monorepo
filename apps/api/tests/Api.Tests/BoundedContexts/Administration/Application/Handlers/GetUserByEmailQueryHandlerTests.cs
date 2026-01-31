using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetUserByEmailQueryHandler.
/// Tests user retrieval by email address with session tracking.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetUserByEmailQueryHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    private static GetUserByEmailQueryHandler CreateHandler(MeepleAiDbContext context)
    {
        return new GetUserByEmailQueryHandler(context);
    }

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsUserDto()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserByEmailQuery("test@example.com");

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("User", result.Role);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ReturnsNull()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var query = new GetUserByEmailQuery("nonexistent@example.com");

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.Handle(null!, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_WithUserHavingActiveSessions_ReturnsLastSeenAt()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

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

        var session = new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "session_token",
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastSeenAt = lastSeenDate,
            RevokedAt = null // Active session
        };

        context.Users.Add(user);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserByEmailQuery("active@example.com");

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LastSeenAt);
        Assert.Equal(lastSeenDate, result.LastSeenAt.Value, TestConstants.Timing.VeryShortTimeout);
    }

    [Fact]
    public async Task Handle_WithMultipleSessions_ReturnsLatestLastSeenAt()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

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

        var oldSession = new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "old_session",
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastSeenAt = oldLastSeen,
            RevokedAt = null
        };

        var recentSession = new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "recent_session",
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastSeenAt = recentLastSeen,
            RevokedAt = null
        };

        context.Users.Add(user);
        context.UserSessions.AddRange(oldSession, recentSession);
        await context.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserByEmailQuery("multisession@example.com");

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LastSeenAt);
        Assert.Equal(recentLastSeen, result.LastSeenAt.Value, TestConstants.Timing.VeryShortTimeout);
    }

    [Fact]
    public async Task Handle_IgnoresRevokedSessions_WhenCalculatingLastSeen()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var userId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = userId,
            Email = "revoked@example.com",
            DisplayName = "Revoked Session User",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var revokedSession = new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "revoked_session",
            User = user,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastSeenAt = DateTime.UtcNow.AddHours(-2),
            RevokedAt = DateTime.UtcNow.AddHours(-1) // Revoked session
        };

        context.Users.Add(user);
        context.UserSessions.Add(revokedSession);
        await context.SaveChangesAsync(TestCancellationToken);

        var query = new GetUserByEmailQuery("revoked@example.com");

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LastSeenAt); // Should ignore revoked sessions
    }

    [Fact]
    public async Task Handle_WithExactEmailMatch_ReturnsUser()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Case Test",
            Role = "User",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestCancellationToken);

        // Query with different case
        // Query with exact case match
        var query = new GetUserByEmailQuery("test@example.com");

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
    }
}
