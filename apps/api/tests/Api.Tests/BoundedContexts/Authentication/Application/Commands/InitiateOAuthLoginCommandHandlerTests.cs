using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Unit tests for InitiateOAuthLoginCommandHandler (Issue #2643).
/// Tests OAuth login initiation with provider validation and state management.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2643")]
public class InitiateOAuthLoginCommandHandlerTests
{
    private readonly Mock<IOAuthService> _oauthServiceMock;
    private readonly Mock<ILogger<InitiateOAuthLoginCommandHandler>> _loggerMock;
    private readonly InitiateOAuthLoginCommandHandler _handler;

    public InitiateOAuthLoginCommandHandlerTests()
    {
        _oauthServiceMock = new Mock<IOAuthService>();
        _loggerMock = new Mock<ILogger<InitiateOAuthLoginCommandHandler>>();

        _handler = new InitiateOAuthLoginCommandHandler(
            _oauthServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    public async Task Handle_WithValidProvider_ReturnsAuthUrl(string provider)
    {
        // Arrange
        var expectedUrl = $"https://{provider}.com/oauth/authorize?state=test";

        _oauthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _oauthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync(provider, It.IsAny<string>()))
            .ReturnsAsync(expectedUrl);

        var command = new InitiateOAuthLoginCommand { Provider = provider, IpAddress = "127.0.0.1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedUrl, result.AuthorizationUrl);
        Assert.Null(result.ErrorMessage);

        _oauthServiceMock.Verify(s => s.StoreStateAsync(It.IsAny<string>()), Times.Once);
        _oauthServiceMock.Verify(s => s.GetAuthorizationUrlAsync(provider, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUnsupportedProvider_ReturnsError()
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = "facebook", IpAddress = "127.0.0.1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unsupported OAuth provider", result.ErrorMessage);
        Assert.Null(result.AuthorizationUrl);
    }

    [Fact]
    public async Task Handle_WithEmptyProvider_ReturnsError()
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = "", IpAddress = "127.0.0.1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("must be specified", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_OnServiceException_ReturnsError()
    {
        // Arrange
        _oauthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        var command = new InitiateOAuthLoginCommand { Provider = "google", IpAddress = "127.0.0.1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not available", result.ErrorMessage);
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("DISCORD")]
    [InlineData("GitHub")]
    public async Task Handle_IsCaseInsensitive(string provider)
    {
        // Arrange
        _oauthServiceMock
            .Setup(s => s.StoreStateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _oauthServiceMock
            .Setup(s => s.GetAuthorizationUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://oauth.example.com");

        var command = new InitiateOAuthLoginCommand { Provider = provider, IpAddress = "127.0.0.1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
    }
}
