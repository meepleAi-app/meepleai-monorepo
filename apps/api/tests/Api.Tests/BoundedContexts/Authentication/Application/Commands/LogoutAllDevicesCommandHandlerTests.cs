using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Unit tests for LogoutAllDevicesCommandHandler (Issue #2643).
/// Tests session revocation, password verification, and cache invalidation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2643")]
public class LogoutAllDevicesCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ISessionCacheService> _sessionCacheMock;
    private readonly Mock<ILogger<LogoutAllDevicesCommandHandler>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly LogoutAllDevicesCommandHandler _handler;

    public LogoutAllDevicesCommandHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _sessionCacheMock = new Mock<ISessionCacheService>();
        _loggerMock = new Mock<ILogger<LogoutAllDevicesCommandHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 19, 12, 0, 0, TimeSpan.Zero));

        _handler = new LogoutAllDevicesCommandHandler(
            _sessionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object,
            _sessionCacheMock.Object,
            _timeProvider
        );
    }

    [Fact]
    public async Task Handle_RevokesAllSessionsForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = CreateTestSessions(userId, count: 3).AsReadOnly();

        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var command = new LogoutAllDevicesCommand(userId, null, false, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.RevokedSessionCount);
        Assert.False(result.CurrentSessionRevoked);
        _sessionRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExcludesCurrentSession_WhenSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentSession = CreateTestSession(userId);
        var currentSessionHash = currentSession.TokenHash;
        var sessions = new List<Session>
        {
            CreateTestSession(userId),
            currentSession,
            CreateTestSession(userId)
        }.AsReadOnly();

        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var command = new LogoutAllDevicesCommand(userId, currentSessionHash, false, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.RevokedSessionCount);
        Assert.False(result.CurrentSessionRevoked);
    }

    [Fact]
    public async Task Handle_IncludesCurrentSession_WhenRequested()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentSession = CreateTestSession(userId);
        var currentSessionHash = currentSession.TokenHash;
        var sessions = new List<Session>
        {
            CreateTestSession(userId),
            currentSession
        }.AsReadOnly();

        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var command = new LogoutAllDevicesCommand(userId, currentSessionHash, true, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.RevokedSessionCount);
        Assert.True(result.CurrentSessionRevoked);
    }

    [Fact]
    public async Task Handle_WithNoActiveSessions_ReturnsZeroCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>().AsReadOnly());

        var command = new LogoutAllDevicesCommand(userId, null, false, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.RevokedSessionCount);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPassword_VerifiesCorrectPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var password = "CorrectPassword123!";
        var user = CreateTestUser(userId, password);
        var sessions = CreateTestSessions(userId, count: 1).AsReadOnly();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var command = new LogoutAllDevicesCommand(userId, null, false, password);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.RevokedSessionCount);
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "CorrectPassword123!");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new LogoutAllDevicesCommand(userId, null, false, "WrongPassword");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.RevokedSessionCount);
        Assert.Contains("Invalid password", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_InvalidatesCache_ForRevokedSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = CreateTestSessions(userId, count: 2).AsReadOnly();

        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var command = new LogoutAllDevicesCommand(userId, null, false, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _sessionCacheMock.Verify(
            c => c.InvalidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ContinuesOnCacheFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessions = CreateTestSessions(userId, count: 2).AsReadOnly();

        _sessionRepositoryMock
            .Setup(r => r.GetActiveSessionsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        _sessionCacheMock
            .Setup(c => c.InvalidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache error"));

        var command = new LogoutAllDevicesCommand(userId, null, false, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.RevokedSessionCount);
    }

    private static User CreateTestUser(Guid id, string password = "TestPassword123!")
    {
        return new User(
            id: id,
            email: new Email($"test-{id}@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(password),
            role: Role.User
        );
    }

    private Session CreateTestSession(Guid userId, string? knownHash = null)
    {
        // Generate a new token each time for unique hashes
        var token = SessionToken.Generate();
        return new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: token,
            lifetime: TimeSpan.FromHours(1),
            ipAddress: "127.0.0.1",
            userAgent: "Test Browser",
            timeProvider: _timeProvider
        );
    }

    private List<Session> CreateTestSessions(Guid userId, int count)
    {
        var sessions = new List<Session>();
        for (int i = 0; i < count; i++)
        {
            sessions.Add(CreateTestSession(userId));
        }
        return sessions;
    }
}
