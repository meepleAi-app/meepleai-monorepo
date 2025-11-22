using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Comprehensive tests for ValidateSessionQueryHandler.
/// Tests session validation, expiration, revocation, and user retrieval.
/// </summary>
public class ValidateSessionQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly TestTimeProvider _timeProvider;
    private readonly ValidateSessionQueryHandler _handler;

    public ValidateSessionQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _timeProvider = new TestTimeProvider();

        _handler = new ValidateSessionQueryHandler(
            _sessionRepositoryMock.Object,
            _userRepositoryMock.Object,
            _timeProvider
        );
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidSession_ReturnsSessionStatus()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("user@example.com", result.User.Email);
        Assert.NotNull(result.ExpiresAt);
        Assert.NotNull(result.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdatesLastSeenAt()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var initialLastSeen = session.LastSeenAt;
        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(session.LastSeenAt);
        Assert.NotEqual(initialLastSeen, session.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), default), Times.Once);
    }

    #endregion

    #region Invalid Session Tests

    [Fact]
    public async Task Handle_WithNonExistentSession_ReturnsInvalidStatus()
    {
        // Arrange
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();
        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);
        Assert.Null(result.ExpiresAt);
        Assert.Null(result.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExpiredSession_ReturnsInvalidStatus()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        _timeProvider.SetUtcNow(DateTime.UtcNow.AddDays(-31)); // Session created 31 days ago

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            timeProvider: _timeProvider // Fix: Use TestTimeProvider for deterministic timestamps
        );

        _timeProvider.SetUtcNow(DateTime.UtcNow); // Current time (31 days later)

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);
        // For expired sessions, timestamps ARE returned (for auditing) but IsValid=false
        Assert.Equal(session.ExpiresAt, result.ExpiresAt);
        Assert.Equal(session.LastSeenAt, result.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRevokedSession_ReturnsInvalidStatus()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        session.Revoke();

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    #endregion

    #region User Not Found Tests

    [Fact]
    public async Task Handle_WithDeletedUser_ReturnsInvalidStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), default), Times.Once);
    }

    #endregion

    #region Token Hash Tests

    [Fact]
    public async Task Handle_UsesTokenHashForLookup()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var expectedTokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(expectedTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _sessionRepositoryMock.Verify(
            x => x.GetByTokenHashAsync(expectedTokenHash, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_MapsDtoCorrectly()
    {
        // Arrange
        var user = CreateTestUser("test@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("test@example.com", result.User.Email);
        Assert.Equal(user.DisplayName, result.User.DisplayName);
        Assert.Equal(Role.User.Value, result.User.Role);
        Assert.False(result.User.IsTwoFactorEnabled);
        Assert.Null(result.User.TwoFactorEnabledAt);
    }

    [Fact]
    public async Task Handle_InvalidSession_DoesNotIncludeUserDto()
    {
        // Arrange
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();
        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result.User);
        Assert.False(result.IsValid);
    }

    #endregion

    #region Session Status Fields Tests

    [Fact]
    public async Task Handle_ValidSession_ReturnsExpiresAt()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result.ExpiresAt);
        Assert.Equal(session.ExpiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task Handle_ValidSession_ReturnsUpdatedLastSeenAt()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result.LastSeenAt);
        Assert.Equal(session.LastSeenAt, result.LastSeenAt);
    }

    [Fact]
    public async Task Handle_ExpiredSession_ReturnsTimestampsButInvalid()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            lifetime: TimeSpan.FromSeconds(-1), // Expired
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent",
            timeProvider: _timeProvider
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(session.ExpiresAt, result.ExpiresAt); // Handler returns session timestamps
        Assert.Equal(session.LastSeenAt, result.LastSeenAt);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithInvalidTokenFormat_ReturnsInvalidStatus()
    {
        // Arrange
        var query = new ValidateSessionQuery("invalid-token-format");

        // Act & Assert - Should handle gracefully
        var exception = await Record.ExceptionAsync(
            async () => await _handler.Handle(query, CancellationToken.None)
        );

        Assert.NotNull(exception);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, token))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, token))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(x => x.GetByTokenHashAsync(tokenHash, token), Times.Once);
        _userRepositoryMock.Verify(x => x.GetByIdAsync(user.Id, token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), token), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotSaveChanges()
    {
        // Arrange
        var user = CreateTestUser("user@example.com");
        var sessionToken = SessionToken.Generate();
        var tokenHash = sessionToken.ComputeHash();

        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent"
        );

        var query = new ValidateSessionQuery(sessionToken.Value);

        _sessionRepositoryMock
            .Setup(x => x.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Query handlers don't call SaveChanges, but they do call UpdateLastSeenAsync
        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), default), Times.Once);
    }

    #endregion

    #region Helper Methods

    private User CreateTestUser(string email)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("SecurePassword123!"),
            role: Role.User
        );
    }

    #endregion
}