using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Unit tests for ExportConfigsQueryHandler.
/// Tests configuration export operations with environment and active filters.
/// Issue: #2188
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ExportConfigsQueryHandlerTests
{
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly ExportConfigsQueryHandler _handler;

    public ExportConfigsQueryHandlerTests()
    {
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _handler = new ExportConfigsQueryHandler(_mockConfigRepository.Object);
    }

    [Fact]
    public async Task Handle_ExportsAllConfigsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "App:Name", "MeepleAI", "string", "All", true, userId),
            CreateTestConfig(Guid.NewGuid(), "App:Version", "1.0.0", "string", "All", true, userId),
            CreateTestConfig(Guid.NewGuid(), "Feature:Beta", "true", "bool", "All", false, userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "All", ActiveOnly: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(3);
        result.Environment.Should().Be("All");
        result.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Configurations.Should().Contain(c => c.Key == "App:Name");
        result.Configurations.Should().Contain(c => c.Key == "App:Version");
        result.Configurations.Should().Contain(c => c.Key == "Feature:Beta");
    }

    [Fact]
    public async Task Handle_WithEnvironmentFilter_ExportsMatchingConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "App:Name", "MeepleAI", "string", "Production", true, userId),
            CreateTestConfig(Guid.NewGuid(), "App:Debug", "false", "bool", "Production", true, userId),
            CreateTestConfig(Guid.NewGuid(), "App:DevMode", "true", "bool", "Development", true, userId),
            CreateTestConfig(Guid.NewGuid(), "Global:Setting", "value", "string", "All", true, userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(3); // 2 Production + 1 All
        result.Configurations.Should().Contain(c => c.Key == "App:Name");
        result.Configurations.Should().Contain(c => c.Key == "App:Debug");
        result.Configurations.Should().Contain(c => c.Key == "Global:Setting");
        result.Configurations.Should().NotContain(c => c.Key == "App:DevMode");
    }

    [Fact]
    public async Task Handle_WithActiveOnlyTrue_ExportsOnlyActiveConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeConfig1 = CreateTestConfig(Guid.NewGuid(), "Active:Config1", "value1", "string", "All", true, userId);
        var activeConfig2 = CreateTestConfig(Guid.NewGuid(), "Active:Config2", "value2", "string", "All", true, userId);
        var inactiveConfig = CreateTestConfig(Guid.NewGuid(), "Inactive:Config", "value3", "string", "All", true, userId);
        inactiveConfig.Deactivate();

        var configs = new List<SystemConfig> { activeConfig1, activeConfig2, inactiveConfig };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "All", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(2);
        result.Configurations.Should().Contain(c => c.Key == "Active:Config1");
        result.Configurations.Should().Contain(c => c.Key == "Active:Config2");
        result.Configurations.Should().NotContain(c => c.Key == "Inactive:Config");
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFalse_ExportsAllConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeConfig = CreateTestConfig(Guid.NewGuid(), "Active:Config", "value1", "string", "All", true, userId);
        var inactiveConfig = CreateTestConfig(Guid.NewGuid(), "Inactive:Config", "value2", "string", "All", true, userId);
        inactiveConfig.Deactivate();

        var configs = new List<SystemConfig> { activeConfig, inactiveConfig };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "All", ActiveOnly: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(2);
        result.Configurations.Should().Contain(c => c.Key == "Active:Config" && c.IsActive);
        result.Configurations.Should().Contain(c => c.Key == "Inactive:Config" && !c.IsActive);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None)
        );
    }

    // Helper method to create test configurations
    private static SystemConfig CreateTestConfig(
        Guid id,
        string key,
        string value,
        string valueType,
        string environment,
        bool isActive,
        Guid userId)
    {
        return new SystemConfig(
            id: id,
            key: new ConfigKey(key),
            value: value,
            valueType: valueType,
            createdByUserId: userId,
            description: $"Test config for {key}",
            category: key.Split(':')[0],
            environment: environment,
            requiresRestart: false
        );
    }
}
