using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Tests for ExportConfigsQueryHandler.
/// Tests export of configuration entries for backup/migration.
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
    public async Task Handle_WithMatchingEnvironment_ReturnsFilteredConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "prod.setting1", "value1", "string", "General", "Production", userId),
            CreateTestConfig(Guid.NewGuid(), "prod.setting2", "value2", "string", "General", "Production", userId),
            CreateTestConfig(Guid.NewGuid(), "dev.setting", "dev_value", "string", "General", "Development", userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(2);
        result.Configurations.Should().OnlyContain(c => c.Environment == "Production");
        result.Environment.Should().Be("Production");
    }

    [Fact]
    public async Task Handle_WithAllEnvironment_IncludesAllEnvironmentConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "global.setting", "value", "string", "General", "All", userId),
            CreateTestConfig(Guid.NewGuid(), "prod.setting", "value", "string", "General", "Production", userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(2);
        result.Configurations.Should().Contain(c => c.Environment == "All");
        result.Configurations.Should().Contain(c => c.Environment == "Production");
    }

    [Fact]
    public async Task Handle_WithActiveOnlyTrue_ExcludesInactiveConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeConfig = CreateTestConfig(Guid.NewGuid(), "active.setting", "value", "string", "General", "Production", userId);
        var inactiveConfig = CreateTestConfig(Guid.NewGuid(), "inactive.setting", "value", "string", "General", "Production", userId);
        inactiveConfig.Deactivate();

        var configs = new List<SystemConfig> { activeConfig, inactiveConfig };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(1);
        result.Configurations.Should().OnlyContain(c => c.IsActive);
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFalse_IncludesAllConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeConfig = CreateTestConfig(Guid.NewGuid(), "active.setting", "value", "string", "General", "Production", userId);
        var inactiveConfig = CreateTestConfig(Guid.NewGuid(), "inactive.setting", "value", "string", "General", "Production", userId);
        inactiveConfig.Deactivate();

        var configs = new List<SystemConfig> { activeConfig, inactiveConfig };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: false);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().HaveCount(2);
        result.Configurations.Should().Contain(c => c.IsActive);
        result.Configurations.Should().Contain(c => !c.IsActive);
    }

    [Fact]
    public async Task Handle_WithNoMatchingConfigs_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "dev.setting", "value", "string", "General", "Development", userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Configurations.Should().BeEmpty();
        result.Environment.Should().Be("Production");
    }

    [Fact]
    public async Task Handle_ReturnsCorrectExportedAtTimestamp()
    {
        // Arrange
        var beforeExport = DateTime.UtcNow;

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemConfig>());

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        var afterExport = DateTime.UtcNow;

        // Assert
        result.ExportedAt.Should().BeOnOrAfter(beforeExport);
        result.ExportedAt.Should().BeOnOrBefore(afterExport);
    }

    [Fact]
    public async Task Handle_MapsAllPropertiesToDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var config = new SystemConfig(
            id: configId,
            key: new ConfigKey("full.mapping.test"),
            value: "test_value",
            valueType: "string",
            createdByUserId: userId,
            description: "Test description",
            category: "TestCategory",
            environment: "Production",
            requiresRestart: true
        );

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemConfig> { config });

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Configurations.Should().HaveCount(1);
        var dto = result.Configurations.First();
        dto.Id.Should().Be(configId);
        dto.Key.Should().Be("full.mapping.test");
        dto.Value.Should().Be("test_value");
        dto.ValueType.Should().Be("string");
        dto.Description.Should().Be("Test description");
        dto.Category.Should().Be("TestCategory");
        dto.Environment.Should().Be("Production");
        dto.RequiresRestart.Should().BeTrue();
        dto.IsActive.Should().BeTrue();
        dto.Version.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        ExportConfigsQuery? query = null;

        // Act
        var act = async () => await _handler.Handle(query!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(cancellationToken))
            .ReturnsAsync(new List<SystemConfig>());

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.GetAllAsync(cancellationToken),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ExportConfigsQueryHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configurationRepository");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("All")]
    public async Task Handle_WithDifferentEnvironments_FiltersCorrectly(string environment)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "setting1", "value", "string", "General", environment, userId),
            CreateTestConfig(Guid.NewGuid(), "setting2", "value", "string", "General", "Other", userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: environment, ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Environment.Should().Be(environment);
        result.Configurations.Should().OnlyContain(c =>
            c.Environment == environment || c.Environment == "All");
    }

    [Fact]
    public async Task Handle_WithMultipleCategories_ExportsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<SystemConfig>
        {
            CreateTestConfig(Guid.NewGuid(), "db.setting", "value", "string", "Database", "Production", userId),
            CreateTestConfig(Guid.NewGuid(), "cache.setting", "value", "string", "Cache", "Production", userId),
            CreateTestConfig(Guid.NewGuid(), "api.setting", "value", "string", "API", "Production", userId)
        };

        _mockConfigRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var query = new ExportConfigsQuery(Environment: "Production", ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Configurations.Should().HaveCount(3);
        result.Configurations.Select(c => c.Category).Should().BeEquivalentTo(new[] { "Database", "Cache", "API" });
    }

    private static SystemConfig CreateTestConfig(
        Guid id,
        string key,
        string value,
        string valueType,
        string category,
        string environment,
        Guid createdByUserId)
    {
        return new SystemConfig(
            id: id,
            key: new ConfigKey(key),
            value: value,
            valueType: valueType,
            createdByUserId: createdByUserId,
            description: null,
            category: category,
            environment: environment,
            requiresRestart: false
        );
    }
}
