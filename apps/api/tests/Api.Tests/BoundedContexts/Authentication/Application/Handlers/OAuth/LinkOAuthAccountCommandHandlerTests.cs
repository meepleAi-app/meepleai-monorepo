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
/// Tests for LinkOAuthAccountCommandHandler.
/// Validates OAuth account linking logic with domain validation.
/// </summary>
public class LinkOAuthAccountCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IOAuthAccountRepository> _oauthAccountRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<LinkOAuthAccountCommandHandler>> _loggerMock;
    private readonly LinkOAuthAccountCommandHandler _handler;

    public LinkOAuthAccountCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _oauthAccountRepositoryMock = new Mock<IOAuthAccountRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<LinkOAuthAccountCommandHandler>>();

        _handler = new LinkOAuthAccountCommandHandler(
            _userRepositoryMock.Object,
            _oauthAccountRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    #region Success Cases

    [Fact]
    public async Task Handle_ValidCommand_LinksOAuthAccountSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        var command = CreateTestCommand(userId, "google");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthAccount?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OAuthAccountId);
        Assert.Null(result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsOAuthAccountId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        var command = CreateTestCommand(userId, "discord");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "discord", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthAccount?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OAuthAccountId);
        Assert.NotEqual(Guid.Empty, result.OAuthAccountId.Value);
    }

    [Fact]
    public async Task Handle_ValidCommand_LogsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        var command = CreateTestCommand(userId, "github");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "github", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthAccount?)null);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully linked")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Handle_UserNotFound_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand(userId, "google");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.OAuthAccountId);
        Assert.Equal("User not found", result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ProviderAlreadyLinked_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        var command = CreateTestCommand(userId, "google");
        var existingAccount = CreateTestOAuthAccount(userId, "google");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccount);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.OAuthAccountId);
        Assert.Contains("already linked", result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DomainExceptionFromUser_ReturnsErrorResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        var command = CreateTestCommand(userId, "google");

        // Pre-link an OAuth account to trigger domain exception
        var existingOAuthAccount = CreateTestOAuthAccount(userId, "google");
        user.LinkOAuthAccount(existingOAuthAccount);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthAccount?)null); // Repository check passes, domain logic fails

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.OAuthAccountId);
        Assert.NotNull(result.ErrorMessage);
        _oauthAccountRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<OAuthAccount>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_LogsWarning()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = CreateTestCommand(userId, "google");

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
        var user = CreateTestUser(userId);
        var command = CreateTestCommand(userId, "google");

        // Pre-link to trigger domain exception
        var existingOAuthAccount = CreateTestOAuthAccount(userId, "google");
        user.LinkOAuthAccount(existingOAuthAccount);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _oauthAccountRepositoryMock
            .Setup(r => r.GetByUserIdAndProviderAsync(userId, "google", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthAccount?)null);

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

    #endregion

    #region Helper Methods

    private static User CreateTestUser(Guid userId)
    {
        return new User(
            id: userId,
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.User
        );
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

    private static LinkOAuthAccountCommand CreateTestCommand(Guid userId, string provider)
    {
        return new LinkOAuthAccountCommand
        {
            UserId = userId,
            Provider = provider,
            ProviderUserId = $"{provider}_user_123",
            AccessTokenEncrypted = "encrypted_access_token",
            RefreshTokenEncrypted = "encrypted_refresh_token",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1)
        };
    }

    #endregion
}

