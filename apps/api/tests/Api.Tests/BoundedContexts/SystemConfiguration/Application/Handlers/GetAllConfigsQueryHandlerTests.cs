using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for GetAllConfigsQueryHandler.
/// Tests retrieval of configurations with filtering and pagination.
/// </summary>
public class GetAllConfigsQueryHandlerTests
{
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly GetAllConfigsQueryHandler _handler;

    public GetAllConfigsQueryHandlerTests()
    {
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _handler = new GetAllConfigsQueryHandler(_mockConfigRepository.Object);
    }

    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllConfigurations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            new(Guid.NewGuid(), new ConfigKey("key1"), "value1", "string", userId),
            new(Guid.NewGuid(), new ConfigKey("key2"), "value2", "int", userId),
            new(Guid.NewGuid(), new ConfigKey("key3"), "value3", "bool", userId)
        };

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFilter_ReturnsOnlyActiveConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeConfigs = new List<SystemConfig>
        {
            new(Guid.NewGuid(), new ConfigKey("active1"), "value1", "string", userId),
            new(Guid.NewGuid(), new ConfigKey("active2"), "value2", "int", userId)
        };

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, ActiveOnly: true);

        _mockConfigRepository
            .Setup(r => r.GetActiveConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConfigs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        _mockConfigRepository.Verify(
            r => r.GetActiveConfigurationsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_ReturnsConfigsFromCategory()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var databaseConfigs = new List<SystemConfig>
        {
            new(Guid.NewGuid(), new ConfigKey("db.host"), "localhost", "string", userId, category: "Database"),
            new(Guid.NewGuid(), new ConfigKey("db.port"), "5432", "int", userId, category: "Database")
        };

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, Category: "Database");

        _mockConfigRepository
            .Setup(r => r.GetByCategoryAsync("Database", It.IsAny<CancellationToken>()))
            .ReturnsAsync(databaseConfigs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Equal("Database", item.Category));
        _mockConfigRepository.Verify(
            r => r.GetByCategoryAsync("Database", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEnvironmentFilter_ReturnsMatchingConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            new(Guid.NewGuid(), new ConfigKey("prod.key"), "value", "string", userId, environment: "Production"),
            new(Guid.NewGuid(), new ConfigKey("all.key"), "value", "string", userId, environment: "All"),
            new(Guid.NewGuid(), new ConfigKey("dev.key"), "value", "string", userId, environment: "Development")
        };

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, Environment: "Production");

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count); // Production + All
        Assert.Contains(result.Items, i => i.Environment == "Production");
        Assert.Contains(result.Items, i => i.Environment == "All");
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>();
        for (int i = 1; i <= 25; i++)
        {
            configs.Add(new SystemConfig(
                Guid.NewGuid(),
                new ConfigKey($"key{i}"),
                $"value{i}",
                "string",
                userId));
        }

        var query = new GetAllConfigsQuery(Page: 2, PageSize: 10);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(25, result.Total);
        Assert.Equal(10, result.Items.Count); // Page 2: items 11-20
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyPage()
    {
        // Arrange
        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemConfig>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_WithMultipleFilters_AppliesAllFilters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cacheConfigs = new List<SystemConfig>
        {
            new(Guid.NewGuid(), new ConfigKey("cache.prod"), "value1", "string", userId, category: "Cache", environment: "Production"),
            new(Guid.NewGuid(), new ConfigKey("cache.all"), "value2", "string", userId, category: "Cache", environment: "All"),
            new(Guid.NewGuid(), new ConfigKey("cache.dev"), "value3", "string", userId, category: "Cache", environment: "Development")
        };

        var query = new GetAllConfigsQuery(
            Page: 1,
            PageSize: 10,
            Category: "Cache",
            Environment: "Production");

        _mockConfigRepository
            .Setup(r => r.GetByCategoryAsync("Cache", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheConfigs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count); // Production + All
        Assert.All(result.Items, item => Assert.Equal("Cache", item.Category));
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(cancellationToken))
            .ReturnsAsync(new List<SystemConfig>());

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.GetAllAsync(cancellationToken),
            Times.Once);
    }
}