using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class IsFeatureEnabledQueryHandlerTests
{
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock = new();
    private readonly IsFeatureEnabledQueryHandler _handler;

    public IsFeatureEnabledQueryHandlerTests()
    {
        _handler = new IsFeatureEnabledQueryHandler(_featureFlagServiceMock.Object);
    }

    [Fact]
    public async Task Handle_FeatureEnabled_ReturnsTrue()
    {
        // Arrange
        _featureFlagServiceMock.Setup(s => s.IsEnabledAsync("Feature.Test", null))
            .ReturnsAsync(true);

        var query = new IsFeatureEnabledQuery("Feature.Test");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task Handle_FeatureDisabled_ReturnsFalse()
    {
        // Arrange
        _featureFlagServiceMock.Setup(s => s.IsEnabledAsync("Feature.Disabled", null))
            .ReturnsAsync(false);

        var query = new IsFeatureEnabledQuery("Feature.Disabled");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
