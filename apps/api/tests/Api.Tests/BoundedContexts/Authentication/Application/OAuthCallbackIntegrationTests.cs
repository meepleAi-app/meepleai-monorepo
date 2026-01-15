using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

/// <summary>
/// Integration tests for OAuth callback handling (HandleOAuthCallbackCommand).
/// Tests complete OAuth flow with mocked provider HTTP responses.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class OAuthCallbackIntegrationTests : IDisposable
{
    private readonly OAuthIntegrationTestBase _helper;
    private readonly HandleOAuthCallbackCommandHandler _handler;
    private const int TIME_ASSERTION_TOLERANCE_SECONDS = 10;

    public OAuthCallbackIntegrationTests()
    {
        _helper = new OAuthIntegrationTestBase();
        _handler = new HandleOAuthCallbackCommandHandler(
            _helper.OAuthServiceMock.Object,
            _helper.MediatorMock.Object,
            _helper.CallbackLoggerMock.Object,
            _helper.EncryptionServiceMock.Object,
            _helper.TimeProviderMock.Object,
            _helper.DbContext);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    public async Task HandleCallback_NewUser_CreatesUserAndLinksOAuth(string provider)
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand(provider);
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = provider switch
        {
            "google" => _helper.CreateGoogleUserInfo(),
            "discord" => _helper.CreateDiscordUserInfo(),
            "github" => _helper.CreateGitHubUserInfo(),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token_123",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange(provider, command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo(provider, tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.UserId);
        Assert.True(result.IsNewUser);
        Assert.NotNull(result.SessionToken);
        Assert.Equal(sessionResponse.SessionToken, result.SessionToken);

        // Verify user was created in database
        var user = await _helper.DbContext.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email);
        Assert.NotNull(user);
        Assert.Equal(userInfo.Email, user.Email);
        Assert.Equal(userInfo.Name, user.DisplayName);

        // Verify OAuth account was linked
        var oauthAccount = await _helper.DbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.UserId == user.Id && o.Provider == provider);
        Assert.NotNull(oauthAccount);
        Assert.Equal(userInfo.Id, oauthAccount.ProviderUserId);

        // Verify session creation was called
        _helper.MediatorMock.Verify(
            m => m.Send(It.Is<CreateSessionCommand>(c => c.UserId == user.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallback_ExistingUser_LinksOAuthAccount()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "existing@gmail.com",
            DisplayName = "ExistingUser",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow
        };
        await _helper.DbContext.Users.AddAsync(existingUser);
        await _helper.DbContext.SaveChangesAsync();

        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo(email: existingUser.Email);

        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(existingUser.Id, existingUser.Email, existingUser.DisplayName ?? "Test", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token_456",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(existingUser.Id, result.UserId);
        Assert.False(result.IsNewUser); // User already existed
        Assert.NotNull(result.SessionToken);

        // Verify OAuth account was linked to existing user
        var oauthAccount = await _helper.DbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.UserId == existingUser.Id && o.Provider == "google");
        Assert.NotNull(oauthAccount);
        Assert.Equal(userInfo.Id, oauthAccount.ProviderUserId);

        // Verify no duplicate user was created
        var totalUsers = await _helper.DbContext.Users.CountAsync(u => u.Email == existingUser.Email);
        Assert.Equal(1, totalUsers);
    }

    [Fact]
    public async Task HandleCallback_ExistingOAuthAccount_UpdatesTokens()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "oauth_user@gmail.com",
            DisplayName = "OAuthUser",
            CreatedAt = DateTime.UtcNow
        };
        await _helper.DbContext.Users.AddAsync(existingUser);

        var existingOAuthAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            Provider = "google",
            ProviderUserId = "google_user_123",
            AccessTokenEncrypted = "old_encrypted_token",
            CreatedAt = DateTime.UtcNow
        };
        await _helper.DbContext.OAuthAccounts.AddAsync(existingOAuthAccount);
        await _helper.DbContext.SaveChangesAsync();

        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse(
            accessToken: "new_access_token",
            refreshToken: "new_refresh_token");
        var userInfo = _helper.CreateGoogleUserInfo(
            sub: "google_user_123",
            email: existingUser.Email);

        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(existingUser.Id, existingUser.Email, existingUser.DisplayName ?? "Test", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token_789",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(existingUser.Id, result.UserId);
        Assert.False(result.IsNewUser);

        // Verify tokens were updated
        var updatedAccount = await _helper.DbContext.OAuthAccounts.FindAsync(existingOAuthAccount.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal("encrypted_token", updatedAccount.AccessTokenEncrypted); // Mocked encryption result

        // Verify encryption was called with new access token
        _helper.EncryptionServiceMock.Verify(
            e => e.EncryptAsync(tokenResponse.AccessToken!, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallback_WithTokenExpiration_StoresExpiryTime()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse(expiresIn: 7200); // 2 hours
        var userInfo = _helper.CreateGoogleUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        var expectedExpiry = DateTimeOffset.UtcNow.AddSeconds(7200);

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var oauthAccount = await _helper.DbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.Provider == "google");
        Assert.NotNull(oauthAccount);
        Assert.NotNull(oauthAccount.TokenExpiresAt);

        // Verify expiry is approximately correct (within tolerance)
        var difference = Math.Abs((oauthAccount.TokenExpiresAt.Value - expectedExpiry).TotalSeconds);
        Assert.True(difference < TIME_ASSERTION_TOLERANCE_SECONDS,
            $"Token expiry time difference: {difference}s (tolerance: {TIME_ASSERTION_TOLERANCE_SECONDS}s)");
    }

    [Fact]
    public async Task HandleCallback_Google_WithRefreshToken_StoresRefreshToken()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse(
            accessToken: "google_access_token",
            refreshToken: "google_refresh_token");
        var userInfo = _helper.CreateGoogleUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        // Verify refresh token was encrypted and stored
        _helper.EncryptionServiceMock.Verify(
            e => e.EncryptAsync(tokenResponse.RefreshToken!, It.IsAny<string>()),
            Times.Once);

        var oauthAccount = await _helper.DbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.Provider == "google");
        Assert.NotNull(oauthAccount);
        Assert.NotNull(oauthAccount.RefreshTokenEncrypted);
    }

    [Fact]
    public async Task HandleCallback_GitHub_WithoutRefreshToken_DoesNotStoreRefreshToken()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("github");
        var tokenResponse = _helper.CreateTokenResponse(
            accessToken: "github_access_token",
            refreshToken: null); // GitHub doesn't provide refresh tokens
        var userInfo = _helper.CreateGitHubUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("github", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("github", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        var oauthAccount = await _helper.DbContext.OAuthAccounts
            .FirstOrDefaultAsync(o => o.Provider == "github");
        Assert.NotNull(oauthAccount);
        Assert.Null(oauthAccount.RefreshTokenEncrypted);

        // Verify encryption was NOT called for refresh token
        _helper.EncryptionServiceMock.Verify(
            e => e.EncryptAsync(It.Is<string>(s => s.Contains("refresh")), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallback_PreventsDuplicateOAuthAccounts()
    {
        // Arrange
        var existingUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "user@gmail.com",
            DisplayName = "User",
            CreatedAt = DateTime.UtcNow
        };
        await _helper.DbContext.Users.AddAsync(existingUser);

        var existingOAuthAccount = new OAuthAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
            Provider = "google",
            ProviderUserId = "google_user_123",
            AccessTokenEncrypted = "old_token",
            CreatedAt = DateTime.UtcNow
        };
        await _helper.DbContext.OAuthAccounts.AddAsync(existingOAuthAccount);
        await _helper.DbContext.SaveChangesAsync();

        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo(
            sub: "google_user_123",
            email: existingUser.Email);

        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(existingUser.Id, existingUser.Email, existingUser.DisplayName ?? "Test", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);

        // Verify no duplicate OAuth account was created
        var totalOAuthAccounts = await _helper.DbContext.OAuthAccounts.CountAsync(
            o => o.UserId == existingUser.Id && o.Provider == "google");
        Assert.Equal(1, totalOAuthAccounts);

        // Verify tokens were updated on existing account
        var updatedAccount = await _helper.DbContext.OAuthAccounts.FindAsync(existingOAuthAccount.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal("encrypted_token", updatedAccount.AccessTokenEncrypted);
    }

    [Fact]
    public async Task HandleCallback_SessionCreationFailure_RollsBackChanges()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Session service unavailable"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.Null(result.SessionToken);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("session", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify user and OAuth account were NOT persisted
        var user = await _helper.DbContext.Users.FirstOrDefaultAsync(u => u.Email == userInfo.Email);
        Assert.Null(user); // Rollback should have occurred
    }

    public void Dispose() => _helper.Dispose();
}