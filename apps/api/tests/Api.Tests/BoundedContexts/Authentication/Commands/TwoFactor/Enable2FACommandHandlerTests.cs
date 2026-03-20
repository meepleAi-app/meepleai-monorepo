using Api.BoundedContexts.Authentication.Application.Commands;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Commands.TwoFactor;

/// <summary>
/// Tests for Enable2FACommandHandler - Issue #2308 Week 4 Phase 3.
/// Tests 2FA enablement with branch coverage for success, invalid code, service exception.
/// Covers: Valid TOTP code, invalid code, service exception handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2308")]
public class Enable2FACommandHandlerTests
{
    private readonly Mock<ITotpService> _mockTotpService;
    private readonly Mock<ILogger<Enable2FACommandHandler>> _mockLogger;
    private readonly Enable2FACommandHandler _handler;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public Enable2FACommandHandlerTests()
    {
        _mockTotpService = new Mock<ITotpService>();
        _mockLogger = new Mock<ILogger<Enable2FACommandHandler>>();
        _handler = new Enable2FACommandHandler(_mockTotpService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidTotpCode_ShouldEnable2FA()
    {
        // Arrange
        var command = new Enable2FACommand(TestUserId, "123456");

        _mockTotpService
            .Setup(s => s.EnableTwoFactorAsync(TestUserId, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();

        _mockTotpService.Verify(
            s => s.EnableTwoFactorAsync(TestUserId, "123456", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithInvalidTotpCode_ShouldReturnFailure()
    {
        // Arrange
        var command = new Enable2FACommand(TestUserId, "999999");

        _mockTotpService
            .Setup(s => s.EnableTwoFactorAsync(TestUserId, "999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid verification code");
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsException_ShouldReturnFailureWithMessage()
    {
        // Arrange
        var command = new Enable2FACommand(TestUserId, "123456");

        _mockTotpService
            .Setup(s => s.EnableTwoFactorAsync(TestUserId, "123456", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("error occurred");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        Enable2FACommand? command = null;

        // Act & Assert
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockTotpService.Verify(
            s => s.EnableTwoFactorAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
