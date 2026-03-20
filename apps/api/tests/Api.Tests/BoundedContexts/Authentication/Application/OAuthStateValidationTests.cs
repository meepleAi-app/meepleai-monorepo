using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

/// <summary>
/// Tests for OAuth state validation scenarios.
/// Issue #2648: These tests prevent regressions in OAuth callback state handling.
///
/// Root Cause Analysis:
/// - OAuth state was not being stored in Redis due to REDIS_URL password mismatch
/// - The API container had a hardcoded password in docker-compose.dev.yml
/// - But Redis container used the password from redis.secret
/// - This caused state validation to fail on callback (state not found)
///
/// These tests verify:
/// 1. Callback fails gracefully when state is invalid (not stored/expired)
/// 2. Callback fails gracefully when state is missing
/// 3. Callback handles URL-encoded states correctly
/// 4. Error responses contain appropriate error messages
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class OAuthStateValidationTests : IDisposable
{
    private readonly OAuthIntegrationTestBase _helper;
    private readonly HandleOAuthCallbackCommandHandler _handler;

    public OAuthStateValidationTests()
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

    #region Invalid State Tests

    [Fact]
    public async Task HandleCallback_InvalidState_ReturnsCsrfError()
    {
        // Arrange - Simulate state that was never stored (e.g., Redis connection failed)
        var command = _helper.CreateTestCallbackCommand(
            provider: "discord",
            code: "valid_auth_code",
            state: "nonexistent_state_token");

        _helper.MockInvalidState(command.State);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.UserId.Should().BeNull();
        result.SessionToken.Should().BeNull();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().ContainAny("state", "CSRF", "invalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task HandleCallback_EmptyOrNullState_ReturnsCsrfError(string? state)
    {
        // Arrange
        var command = new HandleOAuthCallbackCommand
        {
            Provider = "google",
            Code = "valid_auth_code",
            State = state!,
            IpAddress = "127.0.0.1"
        };

        // State validation should fail for empty/null states
        _helper.OAuthServiceMock
            .Setup(s => s.ValidateStateAsync(It.Is<string>(st => string.IsNullOrWhiteSpace(st))))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleCallback_ExpiredState_ReturnsStateExpiredError()
    {
        // Arrange - Simulate state that was stored but has expired (TTL exceeded)
        var expiredState = "expired_state_token_12345";
        var command = _helper.CreateTestCallbackCommand(
            provider: "github",
            code: "valid_auth_code",
            state: expiredState);

        // State validation returns false for expired states
        _helper.MockInvalidState(expiredState);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.UserId.Should().BeNull();
        result.ErrorMessage.Should().NotBeNull();
    }

    #endregion

    #region URL Encoding Tests

    [Theory]
    [InlineData("abc123+def456/ghi789==")]  // Base64 with + / =
    [InlineData("state/with/slashes")]       // Slashes that need encoding
    [InlineData("state+with+plus+signs")]    // Plus signs that get encoded as spaces
    [InlineData("hi1p/YQVMcM4cXy9ZD/YNUitf3cOHZwmb/+KJHuiK28=")] // Real Base64 state
    public async Task HandleCallback_Base64StateWithSpecialChars_ValidatesCorrectly(string state)
    {
        // Arrange - State with special characters that require URL encoding
        var command = _helper.CreateTestCallbackCommand(
            provider: "discord",
            code: "valid_auth_code",
            state: state);

        // Mock valid state (the exact state should be validated, not URL-encoded version)
        _helper.MockValidState(state);

        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateDiscordUserInfo();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(
                Guid.NewGuid(), "test@discord.com", "Test User", "user", "normal", DateTime.UtcNow, false, null, 1, 0),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockSuccessfulTokenExchange("discord", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("discord", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.SessionToken.Should().NotBeNull();

        // Verify state was validated with the exact value (not URL-encoded)
        _helper.OAuthServiceMock.Verify(
            s => s.ValidateStateAsync(state),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallback_UrlEncodedState_DecodesAndValidatesCorrectly()
    {
        // Arrange - Test that URL-encoded state from provider callback is properly decoded
        // ASP.NET Core automatically decodes URL parameters, so we test the decoded value
        var originalState = "abc+def/ghi==";
        var command = _helper.CreateTestCallbackCommand(
            provider: "google",
            code: "valid_auth_code",
            state: originalState);

        _helper.MockValidState(originalState);

        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(
                Guid.NewGuid(), "test@gmail.com", "Test User", "user", "normal", DateTime.UtcNow, false, null, 1, 0),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify the original state was validated (not double-encoded)
        _helper.OAuthServiceMock.Verify(
            s => s.ValidateStateAsync(originalState),
            Times.Once);
    }

    #endregion

    #region Provider Error Tests

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    public async Task HandleCallback_InvalidState_LogsWarning(string provider)
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand(
            provider: provider,
            code: "valid_code",
            state: "invalid_state");

        _helper.MockInvalidState(command.State);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify warning was logged
        _helper.CallbackLoggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("state") || v.ToString()!.Contains("CSRF")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleCallback_InvalidState_DoesNotAttemptTokenExchange()
    {
        // Arrange - Ensure invalid state short-circuits before token exchange
        var command = _helper.CreateTestCallbackCommand(
            provider: "discord",
            code: "some_code",
            state: "invalid_state");

        _helper.MockInvalidState(command.State);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();

        // Verify token exchange was never attempted
        _helper.OAuthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        // Verify user info was never fetched
        _helper.OAuthServiceMock.Verify(
            s => s.GetUserInfoAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Concurrent State Validation Tests

    [Fact]
    public async Task HandleCallback_SameStateTwice_SecondCallFails()
    {
        // Arrange - OAuth state should be single-use (consumed on first validation)
        var state = "single_use_state_token";

        // First call succeeds
        var command1 = _helper.CreateTestCallbackCommand(
            provider: "google",
            code: "code_1",
            state: state);

        // Setup: First validation succeeds, subsequent fails (state consumed)
        var callCount = 0;
        _helper.OAuthServiceMock
            .Setup(s => s.ValidateStateAsync(state))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1; // Only first call returns true
            });

        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();
        var sessionResponse = new CreateSessionResponse(
            User: new UserDto(
                Guid.NewGuid(), "test@gmail.com", "Test User", "user", "normal", DateTime.UtcNow, false, null, 1, 0),
            SessionToken: "session_token",
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        );

        _helper.MockSuccessfulTokenExchange("google", "code_1", tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.MediatorMock
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        // Act - First callback should succeed
        var result1 = await _handler.Handle(command1, CancellationToken.None);

        // Create second handler instance (simulating concurrent request)
        var handler2 = new HandleOAuthCallbackCommandHandler(
            _helper.OAuthServiceMock.Object,
            _helper.MediatorMock.Object,
            _helper.CallbackLoggerMock.Object,
            _helper.EncryptionServiceMock.Object,
            _helper.TimeProviderMock.Object,
            _helper.DbContext);

        var command2 = _helper.CreateTestCallbackCommand(
            provider: "google",
            code: "code_2",
            state: state);

        // Second callback should fail (state already consumed)
        var result2 = await handler2.Handle(command2, CancellationToken.None);

        // Assert
        result1.Success.Should().BeTrue("first callback should succeed");
        result2.Success.Should().BeFalse("second callback should fail - state already consumed");
        result2.ErrorMessage.Should().NotBeNull();
    }

    #endregion

    public void Dispose() => _helper.Dispose();
}