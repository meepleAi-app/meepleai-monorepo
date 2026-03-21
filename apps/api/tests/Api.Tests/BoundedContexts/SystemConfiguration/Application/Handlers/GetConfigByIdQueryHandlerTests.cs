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
public class GetConfigByIdQueryHandlerTests
{
    private readonly Mock<IConfigurationRepository> _configRepoMock = new();
    private readonly GetConfigByIdQueryHandler _handler;

    public GetConfigByIdQueryHandlerTests()
    {
        _handler = new GetConfigByIdQueryHandler(_configRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ConfigExists_ReturnsMappedDto()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var config = new SystemConfig(Guid.NewGuid(),
            new ConfigKey("app.setting"), "42", "integer",
            Guid.NewGuid(), "A test setting", "General");

        _configRepoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var query = new GetConfigByIdQuery(configId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("app.setting", result!.Key);
        Assert.Equal("42", result.Value);
        Assert.Equal("integer", result.ValueType);
        Assert.Equal("General", result.Category);
    }

    [Fact]
    public async Task Handle_ConfigNotFound_ReturnsNull()
    {
        // Arrange
        _configRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var query = new GetConfigByIdQuery(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
