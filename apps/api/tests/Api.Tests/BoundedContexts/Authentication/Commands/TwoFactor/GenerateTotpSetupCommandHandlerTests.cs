using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace Api.Tests.BoundedContexts.Authentication.Commands.TwoFactor;

/// <summary>
/// Tests for GenerateTotpSetupCommandHandler - Issue #2308 Week 4.
/// Tests TOTP setup generation with branch coverage for all validation paths.
/// Covers: Success path, validation failures, service exceptions, encoding edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2308")]
public class GenerateTotpSetupCommandHandlerTests
{
    private readonly Mock<ITotpService> _mockTotpService;
    private readonly Mock<ILogger<GenerateTotpSetupCommandHandler>> _mockLogger;
    private readonly GenerateTotpSetupCommandHandler _handler;

    public GenerateTotpSetupCommandHandlerTests()
    {
        _mockTotpService = new Mock<ITotpService>();
        _mockLogger = new Mock<ILogger<GenerateTotpSetupCommandHandler>>();
        _handler = new GenerateTotpSetupCommandHandler(_mockTotpService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldGenerateTotpSetup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GenerateTotpSetupCommand
        {
            UserId = userId,
            UserEmail = "user@test.com"
        };

        var mockSetup = new TotpSetupResponse
        {
            Secret = "BASE32ENCODEDSECRET",
            QrCodeUrl = "otpauth://totp/MeepleAI:user@test.com?secret=BASE32ENCODEDSECRET&issuer=MeepleAI",
            BackupCodes = new List<string> { "12345678", "87654321", "11111111", "22222222", "33333333", "44444444", "55555555", "66666666" }
        };

        _mockTotpService
            .Setup(s => s.GenerateSetupAsync(userId, "user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSetup);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Secret.Should().Be("BASE32ENCODEDSECRET");
        result.QrCodeUrl.Should().Contain("otpauth://totp/MeepleAI:user@test.com");
        result.BackupCodes.Should().HaveCount(8);
        result.BackupCodes.Should().Contain("12345678");

        _mockTotpService.Verify(
            s => s.GenerateSetupAsync(userId, "user@test.com", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithEmptyGuidUserId_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new GenerateTotpSetupCommand
        {
            UserId = Guid.Empty,
            UserEmail = "user@test.com"
        };

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID is required*");

        _mockTotpService.Verify(
            s => s.GenerateSetupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WithNullEmail_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GenerateTotpSetupCommand
        {
            UserId = userId,
            UserEmail = null!
        };

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User email is required*");

        _mockTotpService.Verify(
            s => s.GenerateSetupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WithEmptyEmail_ShouldThrowArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GenerateTotpSetupCommand
        {
            UserId = userId,
            UserEmail = "   "
        };

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User email is required*");

        _mockTotpService.Verify(
            s => s.GenerateSetupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WhenTotpServiceThrowsException_ShouldPropagateException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GenerateTotpSetupCommand
        {
            UserId = userId,
            UserEmail = "user@test.com"
        };

        _mockTotpService
            .Setup(s => s.GenerateSetupAsync(userId, "user@test.com", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TOTP service unavailable"));

        // Act & Assert
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TOTP service unavailable");
    }

    [Fact]
    public async Task Handle_WithSpecialCharactersInEmail_ShouldGenerateValidQrCode()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var emailWithSpecialChars = "user+test@example.com";
        var command = new GenerateTotpSetupCommand
        {
            UserId = userId,
            UserEmail = emailWithSpecialChars
        };

        var mockSetup = new TotpSetupResponse
        {
            Secret = "SECRETBASE32",
            QrCodeUrl = $"otpauth://totp/MeepleAI:{Uri.EscapeDataString(emailWithSpecialChars)}?secret=SECRETBASE32&issuer=MeepleAI",
            BackupCodes = new List<string> { "11111111" }
        };

        _mockTotpService
            .Setup(s => s.GenerateSetupAsync(userId, emailWithSpecialChars, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSetup);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.QrCodeUrl.Should().Contain(Uri.EscapeDataString(emailWithSpecialChars));
        result.Secret.Should().Be("SECRETBASE32");
        result.BackupCodes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        GenerateTotpSetupCommand? command = null;

        // Act & Assert
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockTotpService.Verify(
            s => s.GenerateSetupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
