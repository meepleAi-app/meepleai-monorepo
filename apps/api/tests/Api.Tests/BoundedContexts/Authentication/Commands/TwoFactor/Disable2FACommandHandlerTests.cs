using Api.BoundedContexts.Authentication.Application.Commands;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.TwoFactor;

/// <summary>
/// Tests for Disable2FACommandHandler - Issue #2308 Week 4 Phase 3.
/// Tests 2FA disablement with branch coverage for success, unauthorized, service exception.
/// Covers: Valid password+code, invalid credentials, UnauthorizedAccessException, generic exceptions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2308")]
public class Disable2FACommandHandlerTests
{
    private readonly Mock<ITotpService> _mockTotpService;
    private readonly Mock<ILogger<Disable2FACommandHandler>> _mockLogger;
    private readonly Disable2FACommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public Disable2FACommandHandlerTests()
    {
        _mockTotpService = new Mock<ITotpService>();
        _mockLogger = new Mock<ILogger<Disable2FACommandHandler>>();
        _handler = new Disable2FACommandHandler(_mockTotpService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldDisable2FA()
    {
        // Arrange
        var command = new Disable2FACommand(TestUserId, "Password123!", "123456");

        _mockTotpService
            .Setup(s => s.DisableTwoFactorAsync(
                TestUserId,
                "Password123!",
                "123456",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();

        _mockTotpService.Verify(
            s => s.DisableTwoFactorAsync(TestUserId, "Password123!", "123456", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new Disable2FACommand(TestUserId, "WrongPassword", "123456");

        _mockTotpService
            .Setup(s => s.DisableTwoFactorAsync(
                TestUserId,
                "WrongPassword",
                "123456",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid password or TOTP code"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid password or TOTP code");
    }

    [Fact]
    public async Task Handle_WithInvalidBackupCode_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new Disable2FACommand(TestUserId, "Password123!", "invalid_backup_code");

        _mockTotpService
            .Setup(s => s.DisableTwoFactorAsync(
                TestUserId,
                "Password123!",
                "invalid_backup_code",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid backup code"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsGenericException_ShouldReturnFailure()
    {
        // Arrange
        var command = new Disable2FACommand(TestUserId, "Password123!", "123456");

        _mockTotpService
            .Setup(s => s.DisableTwoFactorAsync(
                TestUserId,
                "Password123!",
                "123456",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("error occurred");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        Disable2FACommand? command = null;

        // Act & Assert
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockTotpService.Verify(
            s => s.DisableTwoFactorAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
