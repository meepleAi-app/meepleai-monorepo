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
        result.Should().NotBeNull();
        result.Count.Should().Be(3);

        var freeLimits = result.First(l => l.Tier == "free");
        freeLimits.MaxPerDay.Should().Be(5);
        freeLimits.MaxPerWeek.Should().Be(20);
        freeLimits.MaxPerGame.Should().Be(1);
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
        freeLimits.MaxPerDay.Should().Be(10); // Configured
        freeLimits.MaxPerWeek.Should().Be(20); // Default
        freeLimits.MaxPerGame.Should().Be(1); // Default
    }
}
