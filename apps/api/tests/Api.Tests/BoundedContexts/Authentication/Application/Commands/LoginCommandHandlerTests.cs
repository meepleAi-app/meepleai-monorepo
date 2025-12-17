using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Comprehensive tests for LoginCommandHandler.
/// Tests authentication, 2FA flow, session creation, and error cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<ITempSessionService> _tempSessionServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _tempSessionServiceMock = new Mock<ITempSessionService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _tempSessionServiceMock.Object,
            _unitOfWorkMock.Object
        );
    }
    [Fact]
    public async Task Handle_WithValidCredentials_No2FA_ReturnsFullSession()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
        Assert.Null(result.TempSessionToken);
        Assert.NotNull(result.User);
        Assert.NotNull(result.SessionToken);
        Assert.Equal("user@example.com", result.User.Email);
        Assert.Equal(Role.User.Value, result.User.Role);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tempSessionServiceMock.Verify(x => x.CreateTempSessionAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_2FAEnabled_ReturnsTempSession()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: true);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tempSessionServiceMock
            .Setup(x => x.CreateTempSessionAsync(user.Id, "127.0.0.1"))
            .ReturnsAsync("temp_session_token_abc123");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.RequiresTwoFactor);
        Assert.Equal("temp_session_token_abc123", result.TempSessionToken);
        Assert.Null(result.User);
        Assert.Null(result.SessionToken);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tempSessionServiceMock.Verify(x => x.CreateTempSessionAsync(user.Id, "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task Handle_CaseInsensitiveEmail_Succeeds()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "USER@EXAMPLE.COM",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
        Assert.NotNull(result.User);
    }

    [Fact]
    public async Task Handle_CreatesSessionWithMetadata()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
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
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedSession);
        Assert.Equal(user.Id, capturedSession.UserId);
    }
    [Fact]
    public async Task Handle_WithNonExistentEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "nonexistent@example.com",
            Password: "AnyPassword123!",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Invalid email or password", exception.Message);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser("user@example.com", "CorrectPassword123!", is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: "WrongPassword123!",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Invalid email or password", exception.Message);

        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidCredentials_UsesGenericErrorMessage()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "user@example.com",
            Password: "WrongPassword",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        // Generic error message prevents user enumeration attacks
        Assert.Equal("Invalid email or password", exception.Message);
    }
    [Fact]
    public async Task Handle_WithInvalidEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "not-an-email",
            Password: "SecurePassword123!",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _userRepositoryMock.Verify(x => x.GetByEmailAsync(It.IsAny<Email>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "",
            Password: "SecurePassword123!",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }
    [Fact]
    public async Task Handle_With2FAEnabled_PassesCorrectIpToTempSession()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: true);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "192.168.1.100",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tempSessionServiceMock
            .Setup(x => x.CreateTempSessionAsync(user.Id, "192.168.1.100"))
            .ReturnsAsync("temp_token");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _tempSessionServiceMock.Verify(x => x.CreateTempSessionAsync(user.Id, "192.168.1.100"), Times.Once);
    }

    [Fact]
    public async Task Handle_With2FAEnabled_NullIpAddress_PassesNullToTempSession()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: true);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: null,
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tempSessionServiceMock
            .Setup(x => x.CreateTempSessionAsync(user.Id, null))
            .ReturnsAsync("temp_token");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _tempSessionServiceMock.Verify(x => x.CreateTempSessionAsync(user.Id, null), Times.Once);
    }
    [Fact]
    public async Task Handle_MapsDtoCorrectly()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("user@example.com", result.User.Email);
        Assert.Equal(user.DisplayName, result.User.DisplayName);
        Assert.Equal(Role.User.Value, result.User.Role);
        Assert.False(result.User.IsTwoFactorEnabled);
        Assert.Null(result.User.TwoFactorEnabledAt);
    }

    [Fact]
    public async Task Handle_With2FAUser_MapsUserDtoFieldsCorrectly()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: true);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tempSessionServiceMock
            .Setup(x => x.CreateTempSessionAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync("temp_token");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - User is NOT returned in 2FA response
        Assert.Null(result.User);
        Assert.True(result.RequiresTwoFactor);
    }
    [Fact]
    public async Task Handle_No2FA_CallsSaveChangesOnce()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_With2FA_DoesNotCallSaveChanges()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: true);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tempSessionServiceMock
            .Setup(x => x.CreateTempSessionAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync("temp_token");

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Handle_WithNullIpAddress_Succeeds()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: null,
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.RequiresTwoFactor);
    }

    [Fact]
    public async Task Handle_WithNullUserAgent_Succeeds()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: null
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser("user@example.com", password, is2FAEnabled: false);

        var command = new LoginCommand(
            Email: "user@example.com",
            Password: password,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        using var cts = new CancellationTokenSource();
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
    private User CreateTestUser(string email, string password, bool is2FAEnabled)
    {
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(password),
            role: Role.User
        );

        if (is2FAEnabled)
        {
            user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret_base64"));
        }

        return user;
    }
}
