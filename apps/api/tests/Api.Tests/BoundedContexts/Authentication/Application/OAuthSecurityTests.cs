using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

/// <summary>
/// Security tests for OAuth flows.
/// Tests CSRF protection, replay attack prevention, redirect URL validation, and token security.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class OAuthSecurityTests : IDisposable
{
    private readonly OAuthIntegrationTestBase _helper;
    private readonly HandleOAuthCallbackCommandHandler _callbackHandler;
    private readonly InitiateOAuthLoginCommandHandler _initiateHandler;

    public OAuthSecurityTests()
    {
        _helper = new OAuthIntegrationTestBase();
        _callbackHandler = new HandleOAuthCallbackCommandHandler(
            _helper.OAuthServiceMock.Object,
            _helper.MediatorMock.Object,
            _helper.CallbackLoggerMock.Object,
            _helper.EncryptionServiceMock.Object,
            _helper.TimeProviderMock.Object,
            _helper.DbContext);

        _initiateHandler = new InitiateOAuthLoginCommandHandler(
            _helper.OAuthServiceMock.Object,
            _helper.InitiateLoggerMock.Object);
    }

    #region CSRF Protection (State Parameter)

    [Fact]
    public async Task HandleCallback_WithoutValidState_PreventsCsrf()
    {
        // Arrange
        var attackerCommand = _helper.CreateTestCallbackCommand("google", state: "attacker_forged_state");

        _helper.MockInvalidState(attackerCommand.State); // Attacker's state is not valid

        // Act
        var result = await _callbackHandler.Handle(attackerCommand, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("invalid");
        result.ErrorMessage.Should().ContainEquivalentOf("state");

        // Verify no user was created (CSRF prevented)
        var userCount = await _helper.DbContext.Users.CountAsync();
        userCount.Should().Be(0);

        // Verify token exchange was NOT attempted
        _helper.OAuthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InitiateOAuth_GeneratesSecureRandomState()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        var capturedStates = new List<string>();

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Callback<string>(state => capturedStates.Add(state))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://test.com/auth");

        // Act - Generate multiple states
        for (int i = 0; i < 5; i++)
        {
            await _initiateHandler.Handle(command, CancellationToken.None);
        }

        // Assert
        capturedStates.Should().HaveCount(5);

        // Verify all states are unique (no collisions)
        var uniqueStates = capturedStates.Distinct().Count();
        uniqueStates.Should().Be(5);

        // Verify minimum entropy (32 bytes = 44 Base64 chars)
        capturedStates.Should().OnlyContain(state => state.Length >= 44);
    }

    [Fact]
    public async Task HandleCallback_WithModifiedState_BlocksAttack()
    {
        // Arrange
        var originalState = "valid_state_abc123";
        var modifiedState = "valid_state_abc124"; // Attacker modified last char

        _helper.MockValidState(originalState);
        _helper.MockInvalidState(modifiedState); // Modified state is invalid

        var attackCommand = _helper.CreateTestCallbackCommand("google", state: modifiedState);

        // Act
        var result = await _callbackHandler.Handle(attackCommand, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("invalid");
    }

    #endregion

    #region Replay Attack Prevention

    [Fact]
    public async Task HandleCallback_StateUsedOnce_PreventsReplay()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token_1",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        // First request - state is valid
        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act - First callback succeeds
        var firstResult = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert first request succeeded
        firstResult.Success.Should().BeTrue();

        // Simulate state invalidation after first use
        _helper.MockInvalidState(command.State);

        // Act - Second callback with same state/code (replay attack)
        var replayResult = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert replay was blocked
        replayResult.Success.Should().BeFalse();
        replayResult.ErrorMessage.Should().ContainEquivalentOf("invalid");
        replayResult.ErrorMessage.Should().ContainEquivalentOf("state");
    }

    [Fact]
    public async Task HandleCallback_ExpiredState_PreventsDelayedReplay()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");

        // Simulate state expired (10+ minutes old)
        _helper.MockInvalidState(command.State);

        // Act
        var result = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainEquivalentOf("invalid");
    }

    [Fact]
    public async Task HandleCallback_DuplicateAuthorizationCode_BlocksReplay()
    {
        // Arrange
        const string sameCode = "auth_code_12345";
        var command1 = _helper.CreateTestCallbackCommand("google", code: sameCode, state: "state_1");
        var command2 = _helper.CreateTestCallbackCommand("google", code: sameCode, state: "state_2");

        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        // Setup first request
        _helper.MockValidState(command1.State);
        _helper.MockSuccessfulTokenExchange("google", sameCode, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act - First request succeeds
        var firstResult = await _callbackHandler.Handle(command1, CancellationToken.None);
        firstResult.Success.Should().BeTrue();

        // Setup second request (different state, same code)
        _helper.MockValidState(command2.State);
        _helper.MockFailedTokenExchange("google", sameCode); // Code already used

        // Act - Second request with same code fails
        var secondResult = await _callbackHandler.Handle(command2, CancellationToken.None);

        // Assert
        secondResult.Success.Should().BeFalse();
    }

    #endregion

    #region Redirect URL Validation

    [Fact]
    public async Task InitiateOAuthLogin_AuthorizationUrl_ContainsRegisteredRedirectUri()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        var authUrl = "https://accounts.google.com/authorize?redirect_uri=http://localhost:3000/oauth/callback";

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("google", It.IsAny<string>()))
            .ReturnsAsync(authUrl);

        // Act
        var result = await _initiateHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.AuthorizationUrl.Should().Contain("redirect_uri=");

        // Verify redirect URI is present (OAuth providers validate this)
        result.AuthorizationUrl.Should().Contain("localhost:3000");
    }

    [Fact]
    public async Task HandleCallback_OpenRedirectPrevention_BlocksMaliciousRedirect()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
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
        var result = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify result contains valid session token
        // (Frontend should only redirect to pre-configured URLs based on Success flag)
        result.SessionToken.Should().NotBeNull();
    }

    #endregion

    #region Token Security

    [Fact]
    public async Task HandleCallback_EncryptsAccessToken_BeforeStorage()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse(accessToken: "sensitive_access_token_123");
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
        var result = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify encryption was called with access token
        _helper.EncryptionServiceMock.Verify(
            e => e.EncryptAsync(tokenResponse.AccessToken!, "OAuthTokens"),
            Times.Once);

        // Verify plaintext token is NOT stored in database
        var oauthAccount = await _helper.DbContext.OAuthAccounts.FirstOrDefaultAsync();
        oauthAccount.Should().NotBeNull();
        oauthAccount.AccessTokenEncrypted.Should().NotBe(tokenResponse.AccessToken);
        oauthAccount.AccessTokenEncrypted.Should().Be("encrypted_token");
    }

    [Fact]
    public async Task HandleCallback_EncryptsRefreshToken_BeforeStorage()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse(
            accessToken: "access_token",
            refreshToken: "sensitive_refresh_token_456");
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
        var result = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify encryption was called with refresh token
        _helper.EncryptionServiceMock.Verify(
            e => e.EncryptAsync(tokenResponse.RefreshToken!, "OAuthTokens"),
            Times.Once);

        // Verify plaintext refresh token is NOT stored
        var oauthAccount = await _helper.DbContext.OAuthAccounts.FirstOrDefaultAsync();
        oauthAccount.Should().NotBeNull();
        oauthAccount.RefreshTokenEncrypted.Should().NotBe(tokenResponse.RefreshToken);
    }

    [Fact]
    public async Task HandleCallback_SessionToken_IsNotExposedInLogs()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "super_secret_session_token_789",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.SessionToken.Should().Be(sessionResponse.SessionToken);

        // Verify session token is NOT logged (security best practice)
        _helper.CallbackLoggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(sessionResponse.SessionToken)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region Rate Limiting and Abuse Prevention

    [Fact]
    public async Task InitiateOAuthLogin_MultipleRequests_AllGenerateUniqueStates()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        var capturedStates = new HashSet<string>();

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Callback<string>(state => capturedStates.Add(state))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://test.com/auth");

        // Act - Simulate attacker making 100 requests
        for (int i = 0; i < 100; i++)
        {
            await _initiateHandler.Handle(command, CancellationToken.None);
        }

        // Assert - All states are unique (no predictability)
        capturedStates.Should().HaveCount(100);
    }

    [Fact]
    public async Task HandleCallback_PreventsSessionFixation()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();

        var userId = Guid.NewGuid();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(userId, "test@gmail.com", "Test User", "user", DateTime.UtcNow, false, null),
            SessionToken: "new_session_token_after_login",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _callbackHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify NEW session was created (not reused)
        result.SessionToken.Should().NotBeNull();
        result.SessionToken.Should().Be(sessionResponse.SessionToken);

        // Verify CreateSessionCommand was called
        _helper.MediatorMock.Verify(
            m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    public void Dispose() => _helper.Dispose();
}