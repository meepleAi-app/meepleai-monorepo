using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetAllFeatureFlagsQueryHandlerTests
{
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock = new();
    private readonly GetAllFeatureFlagsQueryHandler _handler;

    public GetAllFeatureFlagsQueryHandlerTests()
    {
        _handler = new GetAllFeatureFlagsQueryHandler(_featureFlagServiceMock.Object);
    }

    [Fact]
    public async Task Handle_FlagsExist_ReturnsList()
    {
        // Arrange
        var flags = new List<FeatureFlagDto>
        {
            new("Feature.A", true, null, null, null),
            new("Feature.B", false, null, null, null)
        };

        _featureFlagServiceMock.Setup(s => s.GetAllFeatureFlagsAsync())
            .ReturnsAsync(flags);

        var query = new GetAllFeatureFlagsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsEnabled);
        Assert.False(result[1].IsEnabled);
    }
}
