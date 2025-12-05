using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers.OAuth;

/// <summary>
/// Tests for UnlinkOAuthAccountCommandHandler.
/// Validates OAuth account unlinking logic with lockout prevention.
/// </summary>
public class UnlinkOAuthAccountCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOAuthAccountRepository> _oauthAccountRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UnlinkOAuthAccountCommandHandler>> _loggerMock;
    private readonly UnlinkOAuthAccountCommandHandler _handler;

    public UnlinkOAuthAccountCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _oauthAccountRepositoryMock = new Mock<IOAuthAccountRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UnlinkOAuthAccountCommandHandler>>();

        _handler = new UnlinkOAuthAccountCommandHandler(
            _userRepositoryMock.Object,
            _oauthAccountRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }
    [Fact]
    public async Task Handle_ValidCommand_UnlinksOAuthAccountSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUserWithPassword(userId);
        var oauthAccount = CreateTestOAuthAccount(userId, "google");
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "google" };

        // Link OAuth account first
        user.LinkOAuthAccount(oauthAccount);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oauthAccount);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_LogsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUserWithPassword(userId);
        var oauthAccount = CreateTestOAuthAccount(userId, "discord");
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "discord" };

        user.LinkOAuthAccount(oauthAccount);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "discord", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oauthAccount);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully unlinked")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_UserNotFound_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "google" };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("User not found", result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ProviderNotLinked_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUserWithPassword(userId);
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "google" };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthAccount?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not linked", result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WouldCauseLockout_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUserWithoutPassword(userId); // No password
        var oauthAccount = CreateTestOAuthAccount(userId, "google");
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "google" };

        // User has only one OAuth account and no password
        user.LinkOAuthAccount(oauthAccount);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oauthAccount);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("authentication method", result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_LogsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "google" };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DomainException_LogsError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUserWithoutPassword(userId);
        var oauthAccount = CreateTestOAuthAccount(userId, "google");
        var command = new UnlinkOAuthAccountCommand { UserId = userId, Provider = "google" };

        user.LinkOAuthAccount(oauthAccount);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oauthAccount);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Domain validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    private static User CreateTestUserWithPassword(Guid userId)
    {
        return new User(
            id: userId,
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.User
        );
    }

    private static User CreateTestUserWithoutPassword(Guid userId)
    {
        // Create user with a password, then use reflection to set PasswordHash to null
        // This simulates an OAuth-only user for testing edge cases
        var user = new User(
            id: userId,
            email: new Email("oauth@example.com"),
            displayName: "OAuth User",
            passwordHash: PasswordHash.Create("TempPassword123!"),
            role: Role.User
        );

        // Use reflection to set PasswordHash to null for testing lockout prevention
        var passwordHashProperty = typeof(User).GetProperty("PasswordHash");
        passwordHashProperty?.SetValue(user, null);

        return user;
    }

    private static OAuthAccount CreateTestOAuthAccount(Guid userId, string provider)
    {
        return new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: provider,
            providerUserId: $"{provider}_user_123",
            accessTokenEncrypted: "encrypted_access_token",
            refreshTokenEncrypted: "encrypted_refresh_token",
            tokenExpiresAt: DateTime.UtcNow.AddHours(1)
        );
    }
}

