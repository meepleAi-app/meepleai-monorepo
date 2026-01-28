using Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.EmailVerification;

/// <summary>
/// Unit tests for ResendVerificationCommandHandler.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ResendVerificationCommandHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IEmailVerificationService> _mockEmailVerificationService;
    private readonly Mock<ILogger<ResendVerificationCommandHandler>> _mockLogger;

    public ResendVerificationCommandHandlerTests()
    {
        _mockEmailVerificationService = new Mock<IEmailVerificationService>();
        _mockLogger = new Mock<ILogger<ResendVerificationCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidEmail_ReturnsSuccess()
    {
        // Arrange
        _mockEmailVerificationService
            .Setup(x => x.ResendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new ResendVerificationCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new ResendVerificationCommand { Email = "test@example.com" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("verification link has been sent", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockEmailVerificationService.Verify(x => x.ResendVerificationEmailAsync("test@example.com", TestCancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ReturnsSuccessForSecurity()
    {
        // Arrange
        var handler = new ResendVerificationCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new ResendVerificationCommand { Email = "" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        // Returns success to prevent email enumeration
        Assert.True(result.Success);
        _mockEmailVerificationService.Verify(x => x.ResendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRateLimitExceeded_ReturnsFailure()
    {
        // Arrange
        _mockEmailVerificationService
            .Setup(x => x.ResendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Too many verification requests"));

        var handler = new ResendVerificationCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new ResendVerificationCommand { Email = "test@example.com" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Too many", result.Message);
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsUnexpectedException_ReturnsSuccessForSecurity()
    {
        // Arrange
        _mockEmailVerificationService
            .Setup(x => x.ResendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var handler = new ResendVerificationCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new ResendVerificationCommand { Email = "test@example.com" };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        // Returns success to prevent email enumeration for unexpected errors
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ResendVerificationCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_WithWhitespaceEmail_ReturnsSuccessForSecurity()
    {
        // Arrange
        var handler = new ResendVerificationCommandHandler(
            _mockEmailVerificationService.Object,
            _mockLogger.Object);

        var command = new ResendVerificationCommand { Email = "   " };

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        Assert.True(result.Success);
        _mockEmailVerificationService.Verify(x => x.ResendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
