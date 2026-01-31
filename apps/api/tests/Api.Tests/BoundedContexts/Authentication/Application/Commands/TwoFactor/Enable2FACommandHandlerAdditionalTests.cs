using Api.BoundedContexts.Authentication.Application.Commands;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Additional comprehensive tests for Enable2FACommandHandler.
/// Tests security-critical 2FA enablement flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class Enable2FACommandHandlerAdditionalTests
{
    private readonly Mock<ITotpService> _totpServiceMock;
    private readonly Mock<ILogger<Enable2FACommandHandler>> _loggerMock;
    private readonly Enable2FACommandHandler _handler;

    public Enable2FACommandHandlerAdditionalTests()
    {
        _totpServiceMock = new Mock<ITotpService>();
        _loggerMock = new Mock<ILogger<Enable2FACommandHandler>>();
        _handler = new Enable2FACommandHandler(_totpServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCode_EnablesTwoFactorSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new Enable2FACommand(userId, "123456");
        _totpServiceMock.Setup(x => x.EnableTwoFactorAsync(userId, "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithInvalidCode_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new Enable2FACommand(userId, "000000");
        _totpServiceMock.Setup(x => x.EnableTwoFactorAsync(userId, "000000", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid verification code", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsException_ReturnsFailureWithGenericMessage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new Enable2FACommand(userId, "123456");
        _totpServiceMock.Setup(x => x.EnableTwoFactorAsync(userId, "123456", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("error occurred", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new Enable2FACommand(userId, "123456");
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _totpServiceMock.Setup(x => x.EnableTwoFactorAsync(userId, "123456", token)).ReturnsAsync(true);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _totpServiceMock.Verify(x => x.EnableTwoFactorAsync(userId, "123456", token), Times.Once);
    }
}
