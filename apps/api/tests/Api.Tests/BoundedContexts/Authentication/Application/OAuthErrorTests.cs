using System.Net;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

/// <summary>
/// Integration tests for OAuth error scenarios.
/// Tests error handling for invalid codes, expired states, provider errors, and network issues.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class OAuthErrorTests : IDisposable
{
    private readonly OAuthIntegrationTestBase _helper;
    private readonly HandleOAuthCallbackCommandHandler _handler;

    public OAuthErrorTests()
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

    [Fact]
    public async Task HandleCallback_InvalidState_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");

        _helper.MockInvalidState(command.State);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.Null(result.SessionToken);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("state", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify token exchange was NOT attempted
        _helper.OAuthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallback_ExpiredState_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");

        // Mock state validation failure (expired state returns false)
        _helper.MockInvalidState(command.State);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    public async Task HandleCallback_InvalidAuthorizationCode_ReturnsError(string provider)
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand(provider, code: "invalid_code_12345");

        _helper.MockValidState(command.State);
        _helper.MockFailedTokenExchange(provider, command.Code);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.Null(result.SessionToken);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("token", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify user info retrieval was NOT attempted
        _helper.OAuthServiceMock.Verify(
            s => s.GetUserInfoAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallback_Provider400Error_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync("google", command.Code))
            .ThrowsAsync(new HttpRequestException(
                "Bad Request: invalid_grant",
                null,
                HttpStatusCode.BadRequest));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_Provider500Error_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("discord");

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync("discord", command.Code))
            .ThrowsAsync(new HttpRequestException(
                "Internal Server Error",
                null,
                HttpStatusCode.InternalServerError));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCallback_NetworkTimeout_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("github");

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync("github", command.Code))
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_InvalidAccessToken_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse(accessToken: "invalid_token");

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockFailedUserInfo("google", tokenResponse.AccessToken!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.UserId);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("user", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCallback_UserDeniesAuthorization_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google", code: "error_code");

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync("google", command.Code))
            .ThrowsAsync(new HttpRequestException(
                "access_denied: The user denied your request",
                null,
                HttpStatusCode.BadRequest));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_MalformedTokenResponse_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("discord");

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync("discord", command.Code))
            .ThrowsAsync(new InvalidOperationException("Failed to parse token response"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_MalformedUserInfoResponse_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("github");
        var tokenResponse = _helper.CreateTokenResponse();

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("github", command.Code, tokenResponse);

        _helper.OAuthServiceMock
            .Setup(s => s.GetUserInfoAsync("github", tokenResponse.AccessToken!))
            .ThrowsAsync(new InvalidOperationException("Failed to parse user info response"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_MissingEmailInUserInfo_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfoWithoutEmail = _helper.CreateGoogleUserInfo(email: string.Empty);

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfoWithoutEmail);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("email", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCallback_EmptyProvider_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand(string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("provider", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify no OAuth service calls were made
        _helper.OAuthServiceMock.Verify(
            s => s.ValidateStateAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallback_UnsupportedProvider_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("facebook");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("unsupported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("facebook", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify no OAuth service calls were made
        _helper.OAuthServiceMock.Verify(
            s => s.ValidateStateAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallback_EmptyCode_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google", code: string.Empty);

        _helper.MockValidState(command.State);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("code", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify token exchange was NOT attempted
        _helper.OAuthServiceMock.Verify(
            s => s.ExchangeCodeForTokenAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleCallback_EncryptionFailure_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateGoogleUserInfo();

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("google", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("google", tokenResponse.AccessToken!, userInfo);

        _helper.EncryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Encryption service unavailable"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_DatabaseConnectionFailure_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("discord");
        var tokenResponse = _helper.CreateTokenResponse();
        var userInfo = _helper.CreateDiscordUserInfo();

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("discord", command.Code, tokenResponse);
        _helper.MockSuccessfulUserInfo("discord", tokenResponse.AccessToken!, userInfo);

        // Dispose DbContext to simulate database connection failure
        _helper.DbContext.Dispose();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("google", HttpStatusCode.Unauthorized)]
    [InlineData("discord", HttpStatusCode.Forbidden)]
    [InlineData("github", HttpStatusCode.TooManyRequests)]
    public async Task HandleCallback_ProviderHttpError_ReturnsSpecificError(string provider, HttpStatusCode statusCode)
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand(provider);

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(provider, command.Code))
            .ThrowsAsync(new HttpRequestException(
                $"Provider returned {statusCode}",
                null,
                statusCode));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_NullTokenResponse_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("google");

        _helper.MockValidState(command.State);

        _helper.OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync("google", command.Code))
            .ReturnsAsync((Models.OAuthTokenResponse)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallback_NullUserInfo_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestCallbackCommand("discord");
        var tokenResponse = _helper.CreateTokenResponse();

        _helper.MockValidState(command.State);
        _helper.MockSuccessfulTokenExchange("discord", command.Code, tokenResponse);

        _helper.OAuthServiceMock
            .Setup(s => s.GetUserInfoAsync("discord", tokenResponse.AccessToken!))
            .ReturnsAsync((Models.OAuthUserInfo)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("user", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _helper.Dispose();
}
