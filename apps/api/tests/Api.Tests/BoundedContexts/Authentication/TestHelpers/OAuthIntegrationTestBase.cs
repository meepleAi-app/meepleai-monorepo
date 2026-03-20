using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Helper class for OAuth integration tests.
/// Provides HTTP mocking infrastructure for OAuth provider endpoints.
/// </summary>
internal sealed class OAuthIntegrationTestBase : IDisposable
{
    public Mock<IOAuthService> OAuthServiceMock { get; }
    public Mock<IMediator> MediatorMock { get; }
    public Mock<ILogger<HandleOAuthCallbackCommandHandler>> CallbackLoggerMock { get; }
    public Mock<ILogger<InitiateOAuthLoginCommandHandler>> InitiateLoggerMock { get; }
    public Mock<IEncryptionService> EncryptionServiceMock { get; }
    public Mock<TimeProvider> TimeProviderMock { get; }
    public MeepleAiDbContext DbContext { get; }
    public Mock<HttpMessageHandler> HttpHandlerMock { get; }
    public HttpClient HttpClient { get; }

    private bool _disposed;

    public OAuthIntegrationTestBase()
    {
        OAuthServiceMock = new Mock<IOAuthService>();
        MediatorMock = new Mock<IMediator>();
        CallbackLoggerMock = new Mock<ILogger<HandleOAuthCallbackCommandHandler>>();
        InitiateLoggerMock = new Mock<ILogger<InitiateOAuthLoginCommandHandler>>();
        EncryptionServiceMock = new Mock<IEncryptionService>();
        TimeProviderMock = new Mock<TimeProvider>();

        DbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Setup HTTP mocking infrastructure
        HttpHandlerMock = new Mock<HttpMessageHandler>();
        HttpClient = HttpHandlerMock.CreateClient();

        // Setup default mocks
        TimeProviderMock.Setup(t => t.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        EncryptionServiceMock
            .Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("encrypted_token");
    }

    /// <summary>
    /// Creates a test OAuth token response.
    /// </summary>
    public OAuthTokenResponse CreateTokenResponse(
        string accessToken = "test_access_token",
        string? refreshToken = "test_refresh_token",
        int expiresIn = 3600)
    {
        return new OAuthTokenResponse(accessToken, refreshToken, expiresIn, "Bearer");
    }

    /// <summary>
    /// Creates a test Google user info response.
    /// </summary>
    public OAuthUserInfo CreateGoogleUserInfo(
        string sub = "google_user_123",
        string email = "test@gmail.com",
        string name = "Test User")
    {
        return new OAuthUserInfo(sub, email, name);
    }

    /// <summary>
    /// Creates a test Discord user info response.
    /// </summary>
    public OAuthUserInfo CreateDiscordUserInfo(
        string id = "discord_user_456",
        string email = "test@discord.com",
        string username = "testuser")
    {
        return new OAuthUserInfo(id, email, username);
    }

    /// <summary>
    /// Creates a test GitHub user info response.
    /// </summary>
    public OAuthUserInfo CreateGitHubUserInfo(
        string id = "github_user_789",
        string email = "test@github.com",
        string login = "testuser")
    {
        return new OAuthUserInfo(id, email, login);
    }

    /// <summary>
    /// Mocks successful OAuth token exchange for provider.
    /// </summary>
    public void MockSuccessfulTokenExchange(string provider, string code, OAuthTokenResponse response)
    {
        OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(provider, code))
            .ReturnsAsync(response);
    }

    /// <summary>
    /// Mocks successful user info retrieval from provider.
    /// </summary>
    public void MockSuccessfulUserInfo(string provider, string accessToken, OAuthUserInfo userInfo)
    {
        OAuthServiceMock
            .Setup(s => s.GetUserInfoAsync(provider, accessToken))
            .ReturnsAsync(userInfo);
    }

    /// <summary>
    /// Mocks successful state validation.
    /// </summary>
    public void MockValidState(string state)
    {
        OAuthServiceMock
            .Setup(s => s.ValidateStateAsync(state))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Mocks invalid state validation.
    /// </summary>
    public void MockInvalidState(string state)
    {
        OAuthServiceMock
            .Setup(s => s.ValidateStateAsync(state))
            .ReturnsAsync(false);
    }

    /// <summary>
    /// Mocks token exchange failure (invalid code).
    /// </summary>
    public void MockFailedTokenExchange(string provider, string code)
    {
        OAuthServiceMock
            .Setup(s => s.ExchangeCodeForTokenAsync(provider, code))
            .ThrowsAsync(new HttpRequestException("Invalid authorization code", null, HttpStatusCode.BadRequest));
    }

    /// <summary>
    /// Mocks user info retrieval failure (invalid token).
    /// </summary>
    public void MockFailedUserInfo(string provider, string accessToken)
    {
        OAuthServiceMock
            .Setup(s => s.GetUserInfoAsync(provider, accessToken))
            .ThrowsAsync(new HttpRequestException("Invalid access token", null, HttpStatusCode.Unauthorized));
    }

    /// <summary>
    /// Mocks HTTP response for Google token endpoint.
    /// </summary>
    public void MockGoogleTokenEndpoint(OAuthTokenResponse response)
    {
        HttpHandlerMock
            .SetupRequest("https://oauth2.googleapis.com/token")
            .ReturnsJsonResponse(response);
    }

    /// <summary>
    /// Mocks HTTP response for Google user info endpoint.
    /// </summary>
    public void MockGoogleUserInfoEndpoint(OAuthUserInfo userInfo)
    {
        HttpHandlerMock
            .SetupRequest("https://www.googleapis.com/oauth2/v2/userinfo")
            .ReturnsJsonResponse(userInfo);
    }

    /// <summary>
    /// Mocks HTTP response for Discord token endpoint.
    /// </summary>
    public void MockDiscordTokenEndpoint(OAuthTokenResponse response)
    {
        HttpHandlerMock
            .SetupRequest("https://discord.com/api/oauth2/token")
            .ReturnsJsonResponse(response);
    }

    /// <summary>
    /// Mocks HTTP response for Discord user info endpoint.
    /// </summary>
    public void MockDiscordUserInfoEndpoint(OAuthUserInfo userInfo)
    {
        HttpHandlerMock
            .SetupRequest("https://discord.com/api/users/@me")
            .ReturnsJsonResponse(userInfo);
    }

    /// <summary>
    /// Mocks HTTP response for GitHub token endpoint.
    /// </summary>
    public void MockGitHubTokenEndpoint(OAuthTokenResponse response)
    {
        HttpHandlerMock
            .SetupRequest("https://github.com/login/oauth/access_token")
            .ReturnsJsonResponse(response);
    }

    /// <summary>
    /// Mocks HTTP response for GitHub user info endpoint.
    /// </summary>
    public void MockGitHubUserInfoEndpoint(OAuthUserInfo userInfo)
    {
        HttpHandlerMock
            .SetupRequest("https://api.github.com/user")
            .ReturnsJsonResponse(userInfo);
    }

    /// <summary>
    /// Mocks HTTP error response for any endpoint.
    /// </summary>
    public void MockHttpError(string url, HttpStatusCode statusCode, string errorMessage)
    {
        HttpHandlerMock
            .SetupRequest(url)
            .ReturnsResponse(statusCode, errorMessage);
    }

    /// <summary>
    /// Mocks network timeout for any endpoint.
    /// </summary>
    public void MockTimeout(string url)
    {
        HttpHandlerMock
            .SetupRequest(url)
            .ThrowsAsync(new TaskCanceledException("Request timeout"));
    }

    /// <summary>
    /// Creates a test HandleOAuthCallbackCommand.
    /// </summary>
    public HandleOAuthCallbackCommand CreateTestCallbackCommand(
        string provider = "google",
        string code = "test_auth_code",
        string state = "test_csrf_state",
        string? ipAddress = "127.0.0.1")
    {
        return new HandleOAuthCallbackCommand
        {
            Provider = provider,
            Code = code,
            State = state,
            IpAddress = ipAddress
        };
    }

    /// <summary>
    /// Creates a test InitiateOAuthLoginCommand.
    /// </summary>
    public InitiateOAuthLoginCommand CreateTestInitiateCommand(
        string provider = "google",
        string? ipAddress = "127.0.0.1")
    {
        return new InitiateOAuthLoginCommand
        {
            Provider = provider,
            IpAddress = ipAddress
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            DbContext.Dispose();
            HttpClient.Dispose();
        }

        _disposed = true;
    }
}