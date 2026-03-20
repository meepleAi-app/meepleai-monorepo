using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetSessionLimitsQueryHandlerTests
{
    private readonly Mock<IConfigurationService> _configServiceMock = new();
    private readonly GetSessionLimitsQueryHandler _handler;

    public GetSessionLimitsQueryHandlerTests()
    {
        _handler = new GetSessionLimitsQueryHandler(_configServiceMock.Object);
    }

    [Fact]
    public async Task Handle_AllConfigsExist_ReturnsParsedLimits()
    {
        // Arrange
        var freeConfig = CreateMockConfig("5");
        var normalConfig = CreateMockConfig("15");
        var premiumConfig = CreateMockConfig("-1");

        _configServiceMock.Setup(s => s.GetConfigurationByKeyAsync("SessionLimits:free:MaxSessions", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freeConfig);
        _configServiceMock.Setup(s => s.GetConfigurationByKeyAsync("SessionLimits:normal:MaxSessions", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalConfig);
        _configServiceMock.Setup(s => s.GetConfigurationByKeyAsync("SessionLimits:premium:MaxSessions", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(premiumConfig);

        var query = new GetSessionLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.FreeTierLimit);
        Assert.Equal(15, result.NormalTierLimit);
        Assert.Equal(-1, result.PremiumTierLimit);
    }

    [Fact]
    public async Task Handle_NoConfigs_ReturnsDefaults()
    {
        // Arrange
        _configServiceMock.Setup(s => s.GetConfigurationByKeyAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        var query = new GetSessionLimitsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.FreeTierLimit);    // default
        Assert.Equal(10, result.NormalTierLimit);  // default
        Assert.Equal(-1, result.PremiumTierLimit); // default unlimited
    }

    private static SystemConfigurationDto CreateMockConfig(string value) => new(
        Id: Guid.NewGuid().ToString(),
        Key: "test",
        Value: value,
        ValueType: "integer",
        Description: "test",
        Category: "test",
        IsActive: true,
        RequiresRestart: false,
        Environment: "All",
        Version: 1,
        PreviousValue: null,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow,
        CreatedByUserId: Guid.NewGuid().ToString(),
        UpdatedByUserId: null,
        LastToggledAt: null
    );
}
