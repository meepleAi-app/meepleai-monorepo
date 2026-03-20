using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for GetAllConfigsQueryHandler.
/// Tests retrieval of configurations with filtering and pagination.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, ActiveOnly: false);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Items.Count.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Items.Count.Should().Be(2);
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

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, Category: "Database", ActiveOnly: false);

        _mockConfigRepository
            .Setup(r => r.GetByCategoryAsync("Database", It.IsAny<CancellationToken>()))
            .ReturnsAsync(databaseConfigs);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
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

        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, Environment: "Production", ActiveOnly: false);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Items.Count.Should().Be(2);
        result.Items.Should().Contain(i => i.Environment == "Production");
        result.Items.Should().Contain(i => i.Environment == "All");
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

        var query = new GetAllConfigsQuery(Page: 2, PageSize: 10, ActiveOnly: false);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(25);
        result.Items.Count.Should().Be(10);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyPage()
    {
        // Arrange
        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, ActiveOnly: false);

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemConfig>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
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
            Environment: "Production",
            ActiveOnly: false);

        _mockConfigRepository
            .Setup(r => r.GetByCategoryAsync("Cache", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheConfigs);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Items.Count.Should().Be(2);
        Assert.All(result.Items, item => Assert.Equal("Cache", item.Category));
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var query = new GetAllConfigsQuery(Page: 1, PageSize: 10, ActiveOnly: false);
        using var cancellationTokenSource = new CancellationTokenSource();
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


