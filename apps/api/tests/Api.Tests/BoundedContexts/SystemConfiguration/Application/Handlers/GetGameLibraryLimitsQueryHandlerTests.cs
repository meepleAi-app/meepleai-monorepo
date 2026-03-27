using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

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
        result.Should().NotBeNull();
        result.FreeTierLimit.Should().Be(10);
        result.NormalTierLimit.Should().Be(25);
        result.PremiumTierLimit.Should().Be(100);
        result.LastUpdatedByUserId.Should().Be("admin-user-1");
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
        result.Should().NotBeNull();
        result.FreeTierLimit.Should().Be(5);    // Default for Free
        result.NormalTierLimit.Should().Be(20);  // Default for Normal
        result.PremiumTierLimit.Should().Be(50); // Default for Premium
        result.LastUpdatedByUserId.Should().BeNull();
    }
}
