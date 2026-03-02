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
using Api.Tests.Constants;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Tests for GetSessionStatusQueryHandler focusing on authorization.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetSessionStatusQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<GetSessionStatusQueryHandler>> _loggerMock;
    private readonly GetSessionStatusQueryHandler _handler;

    public GetSessionStatusQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<GetSessionStatusQueryHandler>>();

        _handler = new GetSessionStatusQueryHandler(_dbContext, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_OwnerAccessesOwnSession_ReturnsSession()
    {
        // Arrange
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

        _dbContext.Users.Add(user);
        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSessionStatusQuery(sessionId, userId, false);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId.ToString(), result.Id);
        Assert.Equal(userId.ToString(), result.UserId);
    }

    [Fact]
    public async Task Handle_AdminAccessesAnySession_ReturnsSession()
    {
        // Arrange
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

        _dbContext.Users.Add(owner);
        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSessionStatusQuery(sessionId, adminUserId, true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sessionId.ToString(), result.Id);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ReturnsNull()
    {
        // Arrange
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

        _dbContext.Users.Add(owner);
        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetSessionStatusQuery(sessionId, differentUserId, false);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var query = new GetSessionStatusQuery(sessionId, userId, false);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }
}
