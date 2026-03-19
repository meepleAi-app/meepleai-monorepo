using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Tests for GetGameLibraryLimitsQueryHandler.
/// Issue #2444: Admin UI - Configure Game Library Tier Limits
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameLibraryLimitsQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly GetGameLibraryLimitsQueryHandler _handler;

    public GetGameLibraryLimitsQueryHandlerTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _handler = new GetGameLibraryLimitsQueryHandler(_mockConfigService.Object);
    }

    [Fact]
    public async Task Handle_WithExistingConfigurations_ReturnsConfiguredLimits()
    {
        // Arrange
        var query = new GetGameLibraryLimitsQuery();

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync("GameLibrary:FreeTierLimit", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "1", "GameLibrary:FreeTierLimit", "10", "int", null, "GameLibrary",
                true, false, "All", 1, null, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-5),
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync("GameLibrary:NormalTierLimit", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "2", "GameLibrary:NormalTierLimit", "25", "int", null, "GameLibrary",
                true, false, "All", 1, null, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-3),
                "admin-user-1", "admin-user-1", null
            ));

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync("GameLibrary:PremiumTierLimit", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                "3", "GameLibrary:PremiumTierLimit", "100", "int", null, "GameLibrary",
                true, false, "All", 1, null, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-1),
                "admin-user-1", "admin-user-1", null
            ));

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.FreeTierLimit);
        Assert.Equal(25, result.NormalTierLimit);
        Assert.Equal(100, result.PremiumTierLimit);
        Assert.Equal("admin-user-1", result.LastUpdatedByUserId);
    }

    [Fact]
    public async Task Handle_WithMissingConfigurations_ReturnsDefaults()
    {
        // Arrange
        var query = new GetGameLibraryLimitsQuery();

        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.FreeTierLimit);    // Default for Free
        Assert.Equal(20, result.NormalTierLimit);  // Default for Normal
        Assert.Equal(50, result.PremiumTierLimit); // Default for Premium
        Assert.Null(result.LastUpdatedByUserId);
    }
}
