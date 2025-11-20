using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Tests for DemoLoginCommandHandler.
/// Tests passwordless demo login flow, security restrictions, and error cases.
/// </summary>
public class DemoLoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DemoLoginCommandHandler _handler;

    public DemoLoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new DemoLoginCommandHandler(
            _userRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object
        );
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithValidDemoAccount_ReturnsFullSession()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.TempSessionToken);
        Assert.NotNull(result.User);
        Assert.NotNull(result.SessionToken);
        Assert.Equal("user@meepleai.dev", result.User.Email);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesSessionWithMetadata()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "192.168.1.100",
            UserAgent: "Mozilla/5.0"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Session? capturedSession = null;
        _sessionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((session, _) => capturedSession = session);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedSession);
        Assert.Equal(user.Id, capturedSession.UserId);
    }

    [Fact]
    public async Task Handle_CreatesDemoSessionWith1HourLifetime()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Session? capturedSession = null;
        _sessionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((session, _) => capturedSession = session);

        // Act
        var beforeExecution = DateTime.UtcNow;
        await _handler.Handle(command, CancellationToken.None);
        var afterExecution = DateTime.UtcNow;

        // Assert
        Assert.NotNull(capturedSession);

        // Demo sessions should have 1-hour lifetime (not 30-day default)
        var expectedExpiration = beforeExecution.AddHours(1);
        var maxExpectedExpiration = afterExecution.AddHours(1);

        Assert.True(capturedSession.ExpiresAt >= expectedExpiration,
            $"Session expires at {capturedSession.ExpiresAt:O} but should be >= {expectedExpiration:O}");
        Assert.True(capturedSession.ExpiresAt <= maxExpectedExpiration,
            $"Session expires at {capturedSession.ExpiresAt:O} but should be <= {maxExpectedExpiration:O}");

        // Verify it's NOT the default 30-day lifetime
        var defaultExpiration = afterExecution.AddDays(30);
        Assert.True(capturedSession.ExpiresAt < defaultExpiration.AddDays(-28),
            "Demo session should not have 30-day default lifetime");
    }

    [Fact]
    public async Task Handle_MapsDtoCorrectly()
    {
        // Arrange
        var user = CreateDemoUser("admin@meepleai.dev", Role.Admin);

        var command = new DemoLoginCommand(
            Email: "admin@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("admin@meepleai.dev", result.User.Email);
        Assert.Equal(user.DisplayName, result.User.DisplayName);
        Assert.Equal("admin", result.User.Role);
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task Handle_WithNonDemoAccount_ThrowsDomainException()
    {
        // Arrange
        var user = CreateRegularUser("regular@example.com");

        var command = new DemoLoginCommand(
            Email: "regular@example.com",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("This account is not a demo account. Please use regular login.", exception.Message);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new DemoLoginCommand(
            Email: "nonexistent@example.com",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Equal("Demo account not found", exception.Message);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Bypasses2FA_EvenIfEnabled()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret_base64"));

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Should bypass 2FA for demo accounts
        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
        Assert.NotNull(result.User);
        Assert.NotNull(result.SessionToken);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Email Validation Tests

    [Fact]
    public async Task Handle_WithInvalidEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new DemoLoginCommand(
            Email: "not-an-email",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new DemoLoginCommand(
            Email: "",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_CaseInsensitiveEmail_Succeeds()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "USER@MEEPLEAI.DEV",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.User);
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task Handle_CallsSaveChangesOnce()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithNullIpAddress_Succeeds()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: null,
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithNullUserAgent_Succeeds()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: null
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var user = CreateDemoUser("user@meepleai.dev");

        var command = new DemoLoginCommand(
            Email: "user@meepleai.dev",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), token))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByEmailAsync(It.IsAny<Email>(), token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), token), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(token), Times.Once);
    }

    #endregion

    #region Helper Methods

    private User CreateDemoUser(string email, Role? role = null)
    {
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Demo User",
            passwordHash: PasswordHash.Create("DemoPassword123!"),
            role: role ?? Role.User
        );

        user.MarkAsDemoAccount();

        return user;
    }

    private User CreateRegularUser(string email)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Regular User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );
    }

    #endregion
}
