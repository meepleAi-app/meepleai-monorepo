using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Tests for GetAllPdfLimitsQueryHandler.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetAllPdfLimitsQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly GetAllPdfLimitsQueryHandler _handler;

    public GetAllPdfLimitsQueryHandlerTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _handler = new GetAllPdfLimitsQueryHandler(_mockConfigService.Object);
    }

    [Fact]
    public async Task Handle_WhenNoConfigurationsExist_ReturnsDefaultLimits()
    {
        // Arrange
        var query = new GetAllPdfLimitsQuery();
        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        var freeLimits = result.First(l => l.Tier == "free");
        Assert.Equal(5, freeLimits.MaxPerDay);
        Assert.Equal(20, freeLimits.MaxPerWeek);
        Assert.Equal(1, freeLimits.MaxPerGame);
    }

    [Fact]
    public async Task Handle_WhenConfigurationsExist_ReturnsConfiguredLimits()
    {
        // Arrange
        var query = new GetAllPdfLimitsQuery();
        var updatedAt = DateTime.UtcNow.AddHours(-1);
        var userId = Guid.NewGuid();

        // Mock configured values for free tier
        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync("UploadLimits:free:DailyLimit", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemConfigurationDto(
                Guid.NewGuid().ToString(), "UploadLimits:free:DailyLimit", "10", "int", null, "UploadLimits",
                true, false, "All", 1, null, updatedAt.AddDays(-1), updatedAt,
                userId.ToString(), userId.ToString(), null
            ));

        // All other keys return null (will use defaults)
        _mockConfigService
            .Setup(s => s.GetConfigurationByKeyAsync(It.Is<string>(k => k != "UploadLimits:free:DailyLimit"), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var freeLimits = result.First(l => l.Tier == "free");
        Assert.Equal(10, freeLimits.MaxPerDay); // Configured
        Assert.Equal(20, freeLimits.MaxPerWeek); // Default
        Assert.Equal(1, freeLimits.MaxPerGame); // Default
    }
}
