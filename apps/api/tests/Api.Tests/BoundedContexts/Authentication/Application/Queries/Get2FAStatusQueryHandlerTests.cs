using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2644")]
public class Get2FAStatusQueryHandlerTests
{
    private readonly Mock<ITotpService> _totpServiceMock;
    private readonly Mock<ILogger<Get2FAStatusQueryHandler>> _loggerMock;
    private readonly Get2FAStatusQueryHandler _handler;

    public Get2FAStatusQueryHandlerTests()
    {
        _totpServiceMock = new Mock<ITotpService>();
        _loggerMock = new Mock<ILogger<Get2FAStatusQueryHandler>>();
        _handler = new Get2FAStatusQueryHandler(_totpServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidUserId_Returns2FAStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var status = new TwoFactorStatusResponse
        {
            IsEnabled = true,
            EnabledAt = DateTime.UtcNow.AddDays(-10),
            UnusedBackupCodesCount = 5
        };

        _totpServiceMock
            .Setup(s => s.GetTwoFactorStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var query = new Get2FAStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsEnabled);
        Assert.NotNull(result.EnabledAt);
        Assert.Equal(5, result.UnusedBackupCodesCount);
    }

    [Fact]
    public async Task Handle_With2FADisabled_ReturnsDisabledStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var status = new TwoFactorStatusResponse
        {
            IsEnabled = false,
            EnabledAt = null,
            UnusedBackupCodesCount = 0
        };

        _totpServiceMock
            .Setup(s => s.GetTwoFactorStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var query = new Get2FAStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsEnabled);
        Assert.Null(result.EnabledAt);
        Assert.Equal(0, result.UnusedBackupCodesCount);
    }

    [Fact]
    public async Task Handle_OnServiceException_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _totpServiceMock
            .Setup(s => s.GetTwoFactorStatusAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var query = new Get2FAStatusQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None)
        );
    }
}
