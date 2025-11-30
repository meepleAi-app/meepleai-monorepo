using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for GetConfigByKeyQueryHandler.
/// Tests retrieval of configuration by key.
/// </summary>
public class GetConfigByKeyQueryHandlerTests
{
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly GetConfigByKeyQueryHandler _handler;

    public GetConfigByKeyQueryHandlerTests()
    {
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _handler = new GetConfigByKeyQueryHandler(_mockConfigRepository.Object);
    }

    [Fact]
    public async Task Handle_WithExistingKey_ReturnsConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("max.connections"),
            "100",
            "int",
            userId,
            description: "Maximum connections",
            category: "Database"
        );

        var query = new GetConfigByKeyQuery("max.connections");

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("max.connections", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("max.connections", result.Key);
        Assert.Equal("100", result.Value);
        Assert.Equal("int", result.ValueType);
        Assert.Equal("Maximum connections", result.Description);
        Assert.Equal("Database", result.Category);
    }

    [Fact]
    public async Task Handle_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var query = new GetConfigByKeyQuery("non.existent.key");

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("non.existent.key", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        _mockConfigRepository.Verify(
            r => r.GetByKeyAsync("non.existent.key", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithBooleanConfig_ReturnsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("feature.enabled"),
            "true",
            "bool",
            userId
        );

        var query = new GetConfigByKeyQuery("feature.enabled");

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("feature.enabled", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("bool", result.ValueType);
        Assert.Equal("true", result.Value);
    }

    [Fact]
    public async Task Handle_ReturnsVersionAndTimestamps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-7);
        var config = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("test.key"),
            "value",
            "string",
            userId
        );

        var query = new GetConfigByKeyQuery("test.key");

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("test.key", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Version); // Initial version
        Assert.NotNull(result.CreatedAt);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var query = new GetConfigByKeyQuery("test.key");
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("test.key", It.IsAny<string?>(), It.IsAny<bool>(), cancellationToken))
            .ReturnsAsync((SystemConfig?)null);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.GetByKeyAsync("test.key", It.IsAny<string?>(), It.IsAny<bool>(), cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithUpdatedConfig_ReturnsLatestVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("cache.ttl"),
            "300",
            "int",
            userId
        );

        // Simulate an update
        config.UpdateValue("600", userId);

        var query = new GetConfigByKeyQuery("cache.ttl");

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("cache.ttl", It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("600", result.Value);
        Assert.Equal(2, result.Version); // Version incremented after update
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task Handle_WithSpecificEnvironment_PassesCorrectParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("api.endpoint"),
            "https://prod.api.com",
            "string",
            userId,
            environment: "Production"
        );

        var query = new GetConfigByKeyQuery("api.endpoint", "Production", true);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("api.endpoint", "Production", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config)
            .Verifiable();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("api.endpoint", result.Key);
        Assert.Equal("https://prod.api.com", result.Value);
        _mockConfigRepository.Verify(
            r => r.GetByKeyAsync("api.endpoint", "Production", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFalse_PassesCorrectParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var inactiveConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("feature.disabled"),
            "false",
            "bool",
            userId
        );
        inactiveConfig.Deactivate();

        var query = new GetConfigByKeyQuery("feature.disabled", "Development", false);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("feature.disabled", "Development", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveConfig)
            .Verifiable();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsActive);
        _mockConfigRepository.Verify(
            r => r.GetByKeyAsync("feature.disabled", "Development", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullEnvironment_PassesNullToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("global.setting"),
            "value",
            "string",
            userId
        );

        var query = new GetConfigByKeyQuery("global.setting", null, true);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("global.setting", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config)
            .Verifiable();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _mockConfigRepository.Verify(
            r => r.GetByKeyAsync("global.setting", null, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDifferentEnvironments_PassesCorrectEnvironment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var stagingConfig = new SystemConfig(
            Guid.NewGuid(),
            new ConfigKey("db.connection"),
            "staging-db-url",
            "string",
            userId,
            environment: "Staging"
        );

        var query = new GetConfigByKeyQuery("db.connection", "Staging", true);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("db.connection", "Staging", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stagingConfig)
            .Verifiable();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("staging-db-url", result.Value);
        Assert.Equal("Staging", result.Environment);
        _mockConfigRepository.Verify(
            r => r.GetByKeyAsync("db.connection", "Staging", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

