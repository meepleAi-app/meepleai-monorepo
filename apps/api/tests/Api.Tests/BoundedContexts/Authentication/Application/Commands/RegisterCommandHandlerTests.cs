using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Comprehensive tests for RegisterCommandHandler.
/// Tests user registration, role assignment, session creation, and business rules.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ISessionRepository> _sessionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly TimeProvider _timeProvider;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _sessionRepositoryMock = new Mock<ISessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProvider = TimeProvider.System;

        _handler = new RegisterCommandHandler(
            _userRepositoryMock.Object,
            _sessionRepositoryMock.Object,
            _unitOfWorkMock.Object
        );
    }
    [Fact]
    public async Task Handle_WithValidData_FirstUser_RegistersAsAdmin()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "admin@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Admin User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Role.Admin.Value, result.User.Role);
        Assert.Equal("admin@example.com", result.User.Email);
        Assert.Equal("Admin User", result.User.DisplayName);
        Assert.NotNull(result.SessionToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidData_SecondUser_RegistersAsUser()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "user@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Regular User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Role.User.Value, result.User.Role);
        Assert.Equal("user@example.com", result.User.Email);
        Assert.Equal("Regular User", result.User.DisplayName);
        Assert.False(result.User.IsTwoFactorEnabled);
        Assert.Null(result.User.TwoFactorEnabledAt);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FirstUser_WithExplicitAdminRole_RegistersAsAdmin()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "admin@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Admin User",
            Role: Role.Admin.Value,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.Admin.Value, result.User.Role);
    }

    [Fact]
    public async Task Handle_FirstUser_WithUserRole_RegistersAsUser()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "user@example.com",
            Password: "SecurePassword123!",
            DisplayName: "User",
            Role: Role.User.Value,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.User.Value, result.User.Role);
    }

    [Fact]
    public async Task Handle_CreatesSessionWithCorrectMetadata()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "192.168.1.100",
            UserAgent: "Mozilla/5.0"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Session? capturedSession = null;
        _sessionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((session, _) => capturedSession = session);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedSession);
        Assert.NotNull(result.SessionToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.True(result.ExpiresAt <= DateTime.UtcNow.AddDays(31)); // Default 30-day session
    }

    [Fact]
    public async Task Handle_TrimsDisplayName()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "  Trimmed User  ",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Trimmed User", result.User.DisplayName);
    }

    [Fact]
    public async Task Handle_NormalizesEmailToLowercase()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "Test@EXAMPLE.COM",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("test@example.com", result.User.Email);
    }
    [Fact]
    public async Task Handle_WithDuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var existingUser = CreateTestUser("existing@example.com");

        var command = new RegisterCommand(
            Email: "existing@example.com",
            Password: "SecurePassword123!",
            DisplayName: "New User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Email is already registered", exception.Message);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_CaseInsensitive_ThrowsDomainException()
    {
        // Arrange
        var existingUser = CreateTestUser("user@example.com");

        var command = new RegisterCommand(
            Email: "USER@EXAMPLE.COM",
            Password: "SecurePassword123!",
            DisplayName: "New User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }
    [Fact]
    public async Task Handle_SubsequentUser_WithAdminRole_ThrowsDomainException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "malicious@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Malicious User",
            Role: Role.Admin.Value,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Only administrators can assign elevated roles", exception.Message);

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SubsequentUser_WithEditorRole_ThrowsDomainException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "malicious@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Malicious User",
            Role: Role.Editor.Value,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Only administrators can assign elevated roles", exception.Message);
    }

    [Fact]
    public async Task Handle_SubsequentUser_WithUserRole_Succeeds()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "user@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Regular User",
            Role: Role.User.Value,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Role.User.Value, result.User.Role);
    }
    [Fact]
    public async Task Handle_WithInvalidEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "not-an-email",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );

        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_WithWhitespaceEmail_ThrowsValidationException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "   ",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_WithEmailTooLong_ThrowsValidationException()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@test.com"; // >256 characters

        var command = new RegisterCommand(
            Email: longEmail,
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }
    [Fact]
    public async Task Handle_WithInvalidRole_ThrowsValidationException()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: "superadmin",
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken)
        );
    }
    [Fact]
    public async Task Handle_MapsDtoCorrectly()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.User);
        Assert.NotEqual(Guid.Empty, result.User.Id);
        Assert.Equal("test@example.com", result.User.Email);
        Assert.Equal("Test User", result.User.DisplayName);
        Assert.Equal(Role.User.Value, result.User.Role);
        Assert.False(result.User.IsTwoFactorEnabled);
        Assert.Null(result.User.TwoFactorEnabledAt);
        Assert.True(result.User.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.User.CreatedAt >= DateTime.UtcNow.AddSeconds(-5));
    }
    [Fact]
    public async Task Handle_CallsSaveChangesOnce()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AddsUserBeforeSession()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var callOrder = new List<string>();

        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add(Role.User.Value));

        _sessionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("session"));

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, callOrder.Count);
        Assert.Equal(Role.User.Value, callOrder[0]);
        Assert.Equal("session", callOrder[1]);
    }
    [Fact]
    public async Task Handle_WithNullIpAddress_Succeeds()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: null,
            UserAgent: "TestAgent"
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithNullUserAgent_Succeeds()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: null
        );

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecurePassword123!",
            DisplayName: "Test User",
            Role: null,
            IpAddress: "127.0.0.1",
            UserAgent: "TestAgent"
        );

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), token))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.HasAnyUsersAsync(token))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByEmailAsync(It.IsAny<Email>(), token), Times.Once);
        _userRepositoryMock.Verify(x => x.HasAnyUsersAsync(token), Times.Once);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>(), token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Session>(), token), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(token), Times.Once);
    }
    private User CreateTestUser(string email = "test@example.com", string password = "SecurePassword123!")
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create(password),
            role: Role.User
        );
    }
}
