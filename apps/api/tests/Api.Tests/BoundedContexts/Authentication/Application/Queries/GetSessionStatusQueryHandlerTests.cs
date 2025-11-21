using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Tests for GetSessionStatusQueryHandler focusing on authorization.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
public class GetSessionStatusQueryHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var domainEventCollectorMock = new Mock<IDomainEventCollector>();
        return new MeepleAiDbContext(options, mediatorMock.Object, domainEventCollectorMock.Object);
    }

    /// <summary>
    /// Creates a GetSessionStatusQueryHandler instance with the given context
    /// </summary>
    private static GetSessionStatusQueryHandler CreateHandler(MeepleAiDbContext context)
    {
        var loggerMock = new Mock<ILogger<GetSessionStatusQueryHandler>>();
        return new GetSessionStatusQueryHandler(context, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_OwnerAccessesOwnSession_ReturnsSession()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = Role.User.Value
        };

        var sessionId = Guid.NewGuid();
        var session = new UserSessionEntity
        {
            Id = sessionId,
            UserId = userId,
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            User = user
        };

        context.Users.Add(user);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        var query = new GetSessionStatusQuery(sessionId, userId, false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId.ToString(), result.Id);
        Assert.Equal(userId.ToString(), result.UserId);
    }

    [Fact]
    public async Task Handle_AdminAccessesAnySession_ReturnsSession()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var sessionOwnerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var owner = new UserEntity
        {
            Id = sessionOwnerId,
            Email = "owner@example.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
            Role = Role.User.Value
        };

        var sessionId = Guid.NewGuid();
        var session = new UserSessionEntity
        {
            Id = sessionId,
            UserId = sessionOwnerId,
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            User = owner
        };

        context.Users.Add(owner);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        var query = new GetSessionStatusQuery(sessionId, adminUserId, true);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId.ToString(), result.Id);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ReturnsNull()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var sessionOwnerId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var owner = new UserEntity
        {
            Id = sessionOwnerId,
            Email = "owner@example.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
            Role = Role.User.Value
        };

        var sessionId = Guid.NewGuid();
        var session = new UserSessionEntity
        {
            Id = sessionId,
            UserId = sessionOwnerId,
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            User = owner
        };

        context.Users.Add(owner);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        var query = new GetSessionStatusQuery(sessionId, differentUserId, false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNull()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);

        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetSessionStatusQuery(sessionId, userId, false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}