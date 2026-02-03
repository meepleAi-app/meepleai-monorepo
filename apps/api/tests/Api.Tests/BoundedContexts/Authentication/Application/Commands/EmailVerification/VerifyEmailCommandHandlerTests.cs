using Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.EmailVerification;

/// <summary>
/// Unit tests for VerifyEmailCommandHandler.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class VerifyEmailCommandHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IEmailVerificationService> _mockEmailVerificationService;
    private readonly Mock<ILogger<VerifyEmailCommandHandler>> _mockLogger;

    public VerifyEmailCommandHandlerTests()
    {
        _mockEmailVerificationService = new Mock<IEmailVerificationService>();
        _mockLogger = new Mock<ILogger<VerifyEmailCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        _mockEmailVerificationService
            .Setup(x => x.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new VerifyEmailCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new VerifyEmailCommand { Token = "valid-token-123" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Email verified successfully", result.Message);
        _mockEmailVerificationService.Verify(x => x.VerifyEmailAsync("valid-token-123", TestCancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ReturnsFailure()
    {
        // Arrange
        _mockEmailVerificationService
            .Setup(x => x.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new VerifyEmailCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new VerifyEmailCommand { Token = "invalid-token" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid or expired verification token", result.Message);
    }

    [Fact]
    public async Task Handle_WithEmptyToken_ReturnsFailure()
    {
        // Arrange
        var handler = new VerifyEmailCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new VerifyEmailCommand { Token = "" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid verification token", result.Message);
        _mockEmailVerificationService.Verify(x => x.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceToken_ReturnsFailure()
    {
        // Arrange
        var handler = new VerifyEmailCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new VerifyEmailCommand { Token = "   " };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid verification token", result.Message);
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsException_ReturnsFailure()
    {
        // Arrange
        _mockEmailVerificationService
            .Setup(x => x.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var handler = new VerifyEmailCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new VerifyEmailCommand { Token = "valid-token" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("error", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new VerifyEmailCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, TestCancellationToken));
    }
}
