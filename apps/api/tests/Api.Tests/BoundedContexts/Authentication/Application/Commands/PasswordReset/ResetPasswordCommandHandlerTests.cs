using Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Comprehensive tests for ResetPasswordCommandHandler.
/// Tests security-critical password reset completion flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IPasswordResetService> _passwordResetServiceMock;
    private readonly Mock<ILogger<ResetPasswordCommandHandler>> _loggerMock;
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _passwordResetServiceMock = new Mock<IPasswordResetService>();
        _loggerMock = new Mock<ILogger<ResetPasswordCommandHandler>>();
        _handler = new ResetPasswordCommandHandler(_passwordResetServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidTokenAndPassword_ReturnsSuccessWithUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ResetPasswordCommand { Token = "valid_token", NewPassword = "NewSecurePassword123!" };
        _passwordResetServiceMock.Setup(x => x.ResetPasswordAsync("valid_token", "NewSecurePassword123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, userId));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ReturnsFailure()
    {
        // Arrange
        var command = new ResetPasswordCommand { Token = "invalid_token", NewPassword = "NewSecurePassword123!" };
        _passwordResetServiceMock.Setup(x => x.ResetPasswordAsync("invalid_token", "NewSecurePassword123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (Guid?)null));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid or expired reset token", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsFailure()
    {
        // Arrange
        var command = new ResetPasswordCommand { Token = "expired_token", NewPassword = "NewSecurePassword123!" };
        _passwordResetServiceMock.Setup(x => x.ResetPasswordAsync("expired_token", "NewSecurePassword123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (Guid?)null));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid or expired reset token", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithEmptyToken_ReturnsFailure()
    {
        // Arrange
        var command = new ResetPasswordCommand { Token = "", NewPassword = "NewSecurePassword123!" };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Reset token is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithEmptyPassword_ReturnsFailure()
    {
        // Arrange
        var command = new ResetPasswordCommand { Token = "valid_token", NewPassword = "" };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("New password is required", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithWeakPassword_ReturnsFailureWithValidationError()
    {
        // Arrange
        var command = new ResetPasswordCommand { Token = "valid_token", NewPassword = "weak" };
        _passwordResetServiceMock.Setup(x => x.ResetPasswordAsync("valid_token", "weak", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Password must be at least 8 characters long"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Password must be at least 8 characters long", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenUnexpectedErrorOccurs_ReturnsFailureWithGenericMessage()
    {
        // Arrange
        var command = new ResetPasswordCommand { Token = "valid_token", NewPassword = "NewSecurePassword123!" };
        _passwordResetServiceMock.Setup(x => x.ResetPasswordAsync("valid_token", "NewSecurePassword123!", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("unexpected error", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
