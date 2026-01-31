using Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Comprehensive tests for RequestPasswordResetCommandHandler.
/// Tests security-critical password reset request flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RequestPasswordResetCommandHandlerTests
{
    private readonly Mock<IPasswordResetService> _passwordResetServiceMock;
    private readonly Mock<ILogger<RequestPasswordResetCommandHandler>> _loggerMock;
    private readonly RequestPasswordResetCommandHandler _handler;

    public RequestPasswordResetCommandHandlerTests()
    {
        _passwordResetServiceMock = new Mock<IPasswordResetService>();
        _loggerMock = new Mock<ILogger<RequestPasswordResetCommandHandler>>();
        _handler = new RequestPasswordResetCommandHandler(_passwordResetServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidEmail_ReturnsSuccessWithGenericMessage()
    {
        // Arrange
        var command = new RequestPasswordResetCommand { Email = "user@example.com" };
        _passwordResetServiceMock.Setup(x => x.RequestPasswordResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("If the email exists, a password reset link has been sent", result.Message);
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ReturnsSuccessToPreventEnumeration()
    {
        // Arrange
        var command = new RequestPasswordResetCommand { Email = "nonexistent@example.com" };
        _passwordResetServiceMock.Setup(x => x.RequestPasswordResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Security: Always returns success to prevent email enumeration
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ReturnsSuccessWithGenericMessage()
    {
        // Arrange
        var command = new RequestPasswordResetCommand { Email = "" };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        _passwordResetServiceMock.Verify(x => x.RequestPasswordResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRateLimitExceeded_ReturnsFailure()
    {
        // Arrange
        var command = new RequestPasswordResetCommand { Email = "user@example.com" };
        _passwordResetServiceMock.Setup(x => x.RequestPasswordResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Too many password reset requests"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Too many", result.Message);
    }

    [Fact]
    public async Task Handle_WhenUnexpectedErrorOccurs_ReturnsSuccessToPreventLeakage()
    {
        // Arrange
        var command = new RequestPasswordResetCommand { Email = "user@example.com" };
        _passwordResetServiceMock.Setup(x => x.RequestPasswordResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - Security: Returns generic success on unexpected errors
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
