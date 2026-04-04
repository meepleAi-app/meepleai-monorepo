using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class GetConfigCategoriesQueryHandlerTests
{
    private readonly Mock<IConfigurationRepository> _configRepoMock = new();
    private readonly GetConfigCategoriesQueryHandler _handler;

    public GetConfigCategoriesQueryHandlerTests()
    {
        _handler = new GetConfigCategoriesQueryHandler(_configRepoMock.Object);
    }

    [Fact]
    public async Task Handle_MultipleCategories_ReturnsDistinctSorted()
    {
        // Arrange
        var configs = new List<SystemConfig>
        {
            new SystemConfig(Guid.NewGuid(), new ConfigKey("k1"), "v1", "string", Guid.NewGuid(), "d1", "Performance"),
            new SystemConfig(Guid.NewGuid(), new ConfigKey("k2"), "v2", "string", Guid.NewGuid(), "d2", "General"),
            new SystemConfig(Guid.NewGuid(), new ConfigKey("k3"), "v3", "string", Guid.NewGuid(), "d3", "Performance"),
            new SystemConfig(Guid.NewGuid(), new ConfigKey("k4"), "v4", "string", Guid.NewGuid(), "d4", "Security")
        };

        _configRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new GetConfigCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("General", result[0]);
        Assert.Equal("Performance", result[1]);
        Assert.Equal("Security", result[2]);
    }

    [Fact]
    public async Task Handle_NoConfigs_ReturnsEmptyList()
    {
        // Arrange
        _configRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemConfig>());

        var query = new GetConfigCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
