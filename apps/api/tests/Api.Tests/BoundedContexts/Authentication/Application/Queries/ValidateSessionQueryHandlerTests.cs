using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Comprehensive tests for ValidateSessionQueryHandler.
/// Tests session validation, expiration, revocation, and user retrieval.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("user@example.com", result.User.Email);
        Assert.NotNull(result.ExpiresAt);
        Assert.NotNull(result.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
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
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(session.LastSeenAt);
        Assert.NotEqual(initialLastSeen, session.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);
        Assert.Null(result.ExpiresAt);
        Assert.Null(result.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);
        // For expired sessions, timestamps ARE returned (for auditing) but IsValid=false
        Assert.Equal(session.ExpiresAt, result.ExpiresAt);
        Assert.Equal(session.LastSeenAt, result.LastSeenAt);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);

        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
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
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(
            x => x.GetByTokenHashAsync(expectedTokenHash, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result.User);
        Assert.False(result.IsValid);
    }
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(session.ExpiresAt, result.ExpiresAt); // Handler returns session timestamps
        Assert.Equal(session.LastSeenAt, result.LastSeenAt);
    }
    [Theory]
    [InlineData("invalid-token-format")]         // hyphens not in Base-64 alphabet
    [InlineData("not!base64@value")]             // special chars
    [InlineData("short")]                        // too short to be a real token
    [InlineData("  spaces  ")]                   // whitespace-only is blocked by FromStored
    public async Task Handle_WithInvalidTokenFormat_ReturnsInvalidStatus(string invalidToken)
    {
        // Arrange
        var query = new ValidateSessionQuery(invalidToken);

        // Act — must NOT throw; malformed cookies must return invalid session (not 500)
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Null(result.User);
        Assert.Null(result.ExpiresAt);
        Assert.Null(result.LastSeenAt);

        // Repository must never be called for a malformed token
        _sessionRepositoryMock.Verify(
            x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

        using var cts = new CancellationTokenSource();
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
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Query handlers don't call SaveChanges, but they do call UpdateLastSeenAsync
        _sessionRepositoryMock.Verify(x => x.UpdateLastSeenAsync(session.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
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
}
