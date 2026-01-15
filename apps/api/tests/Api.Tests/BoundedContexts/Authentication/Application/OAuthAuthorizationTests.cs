using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

/// <summary>
/// Integration tests for OAuth authorization URL generation (InitiateOAuthLoginCommand).
/// Tests authorization flow initiation for Google, Discord, and GitHub providers.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class OAuthAuthorizationTests : IDisposable
{
    private readonly OAuthIntegrationTestBase _helper;
    private readonly InitiateOAuthLoginCommandHandler _handler;

    public OAuthAuthorizationTests()
    {
        _helper = new OAuthIntegrationTestBase();
        _handler = new InitiateOAuthLoginCommandHandler(
            _helper.OAuthServiceMock.Object,
            _helper.InitiateLoggerMock.Object);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    public async Task InitiateOAuthLogin_WithValidProvider_ReturnsAuthorizationUrl(string provider)
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand(provider);
        var expectedState = "test_csrf_state_base64";
        var expectedAuthUrl = $"https://{provider}.com/authorize?client_id=test&state={expectedState}";

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync(provider, It.IsAny<string>()))
            .ReturnsAsync(expectedAuthUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.AuthorizationUrl);
        Assert.Contains(provider, result.AuthorizationUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ErrorMessage);

        // Verify state was stored
        _helper.OAuthServiceMock.Verify(
            s => s.StoreStateAsync(It.IsAny<string>()),
            Times.Once);

        // Verify authorization URL was generated
        _helper.OAuthServiceMock.Verify(
            s => s.GetAuthorizationUrlAsync(provider, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task InitiateOAuthLogin_Google_GeneratesValidUrl()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        var expectedAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth?client_id=test_client_id&redirect_uri=http://localhost:3000/callback&response_type=code&scope=openid%20email%20profile&state=test_state";

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("google", It.IsAny<string>()))
            .ReturnsAsync(expectedAuthUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("accounts.google.com", result.AuthorizationUrl);
        Assert.Contains("client_id=", result.AuthorizationUrl);
        Assert.Contains("state=", result.AuthorizationUrl);
        Assert.Contains("scope=", result.AuthorizationUrl);
    }

    [Fact]
    public async Task InitiateOAuthLogin_Discord_GeneratesValidUrl()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("discord");
        var expectedAuthUrl = "https://discord.com/api/oauth2/authorize?client_id=test_client_id&redirect_uri=http://localhost:3000/callback&response_type=code&scope=identify%20email&state=test_state";

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("discord", It.IsAny<string>()))
            .ReturnsAsync(expectedAuthUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("discord.com", result.AuthorizationUrl);
        Assert.Contains("client_id=", result.AuthorizationUrl);
        Assert.Contains("state=", result.AuthorizationUrl);
    }

    [Fact]
    public async Task InitiateOAuthLogin_GitHub_GeneratesValidUrl()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("github");
        var expectedAuthUrl = "https://github.com/login/oauth/authorize?client_id=test_client_id&redirect_uri=http://localhost:3000/callback&scope=user:email&state=test_state";

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("github", It.IsAny<string>()))
            .ReturnsAsync(expectedAuthUrl);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("github.com", result.AuthorizationUrl);
        Assert.Contains("client_id=", result.AuthorizationUrl);
        Assert.Contains("state=", result.AuthorizationUrl);
    }

    [Fact]
    public async Task InitiateOAuthLogin_WithEmptyProvider_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand(string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.AuthorizationUrl);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("provider must be specified", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify state was NOT stored
        _helper.OAuthServiceMock.Verify(
            s => s.StoreStateAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InitiateOAuthLogin_WithUnsupportedProvider_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("facebook");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.AuthorizationUrl);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("unsupported", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("facebook", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify state was NOT stored
        _helper.OAuthServiceMock.Verify(
            s => s.StoreStateAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task InitiateOAuthLogin_StateGeneration_IsSecure()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        string? capturedState = null;

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Callback<string>(state => capturedState = state)
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("google", It.IsAny<string>()))
            .ReturnsAsync("https://accounts.google.com/authorize?state=test");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedState);

        // Verify state is Base64-encoded and has sufficient entropy (min 32 bytes = 44 Base64 chars)
        Assert.True(capturedState.Length >= 44, "State token should have at least 32 bytes of entropy");

        // Verify it's valid Base64
        try
        {
            var decoded = Convert.FromBase64String(capturedState);
            Assert.True(decoded.Length >= 32, "Decoded state should be at least 32 bytes");
        }
        catch (FormatException)
        {
            Assert.Fail("State token should be valid Base64");
        }
    }

    [Fact]
    public async Task InitiateOAuthLogin_StateStorage_CallsStoreBeforeUrlGeneration()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        var callSequence = new List<string>();

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Callback(() => callSequence.Add("StoreState"))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callSequence.Add("GetAuthUrl"))
            .ReturnsAsync("https://test.com/auth");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify correct order
        Assert.Equal(2, callSequence.Count);
        Assert.Equal("StoreState", callSequence[0]);
        Assert.Equal("GetAuthUrl", callSequence[1]);
    }

    [Fact]
    public async Task InitiateOAuthLogin_RedirectUrl_IsIncludedInAuthorizationUrl()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");
        var authUrlWithRedirect = "https://accounts.google.com/authorize?redirect_uri=http://localhost:3000/oauth/callback&state=test";

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("google", It.IsAny<string>()))
            .ReturnsAsync(authUrlWithRedirect);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("redirect_uri=", result.AuthorizationUrl);
    }

    [Fact]
    public async Task InitiateOAuthLogin_WithStorageFailure_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.AuthorizationUrl);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not available", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitiateOAuthLogin_WithProviderConfigurationMissing_ReturnsError()
    {
        // Arrange
        var command = _helper.CreateTestInitiateCommand("google");

        _helper.OAuthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _helper.OAuthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync("google", It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("OAuth provider configuration not found"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.AuthorizationUrl);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not available", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _helper.Dispose();
}
