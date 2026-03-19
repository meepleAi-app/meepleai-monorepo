using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Tests for ImportConfigsCommandHandler.
/// Tests import of configuration entries from backup/export with overwrite options.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ImportConfigsCommandHandlerTests
{
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly ImportConfigsCommandHandler _handler;

    public ImportConfigsCommandHandlerTests()
    {
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new ImportConfigsCommandHandler(
            _mockConfigRepository.Object,
            _mockUnitOfWork.Object);
    }

    /// <summary>
    /// Helper: sets up GetByKeysAsync mock to return the given configs.
    /// The handler now uses batch GetByKeysAsync instead of individual GetByKeyAsync.
    /// </summary>
    private void SetupGetByKeysAsync(params SystemConfig[] existingConfigs)
    {
        _mockConfigRepository
            .Setup(r => r.GetByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> _, string? _, bool _, CancellationToken _) =>
                (IReadOnlyList<SystemConfig>)existingConfigs.ToList());
    }

    [Fact]
    public async Task Handle_WithNewConfigurations_ImportsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configurations = new List<ConfigurationImportItem>
        {
            new("app.setting1", "value1", "string", "Setting 1", "General", true, false, "Production"),
            new("app.setting2", "100", "int", "Setting 2", "Performance", true, true, "Production"),
            new("app.setting3", "true", "bool", null, "Features", true, false, "All")
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);

        SetupGetByKeysAsync(); // No existing configs

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(3);
        _mockConfigRepository.Verify(
            r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithOverwriteExisting_UpdatesExistingConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingConfigId = Guid.NewGuid();
        var existingConfig = CreateTestConfig(existingConfigId, "existing.key", "old_value", "string", "General", "Production", userId);

        var configurations = new List<ConfigurationImportItem>
        {
            new("existing.key", "new_value", "string", "Updated setting", "General", true, false, "Production")
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: true, UserId: userId);

        SetupGetByKeysAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(1);
        _mockConfigRepository.Verify(
            r => r.UpdateAsync(existingConfig, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockConfigRepository.Verify(
            r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        existingConfig.Value.Should().Be("new_value");
    }

    [Fact]
    public async Task Handle_WithoutOverwrite_SkipsExistingConfigs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingConfigId = Guid.NewGuid();
        var existingConfig = CreateTestConfig(existingConfigId, "existing.key", "old_value", "string", "General", "Production", userId);

        var configurations = new List<ConfigurationImportItem>
        {
            new("existing.key", "new_value", "string", "Updated setting", "General", true, false, "Production")
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);

        SetupGetByKeysAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(0);
        _mockConfigRepository.Verify(
            r => r.UpdateAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockConfigRepository.Verify(
            r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        existingConfig.Value.Should().Be("old_value");
    }

    [Fact]
    public async Task Handle_WithInactiveConfiguration_DeactivatesAfterCreation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configurations = new List<ConfigurationImportItem>
        {
            new("inactive.setting", "value", "string", "Inactive config", "General", IsActive: false, false, "Production")
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);

        SetupGetByKeysAsync(); // No existing configs

        SystemConfig? capturedConfig = null;
        _mockConfigRepository
            .Setup(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()))
            .Callback<SystemConfig, CancellationToken>((config, _) => capturedConfig = config)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(1);
        capturedConfig.Should().NotBeNull();
        capturedConfig!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithMixedExistingAndNew_ProcessesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingConfigId = Guid.NewGuid();
        var existingConfig = CreateTestConfig(existingConfigId, "existing.key", "old_value", "string", "General", "Production", userId);

        var configurations = new List<ConfigurationImportItem>
        {
            new("existing.key", "updated_value", "string", "Updated", "General", true, false, "Production"),
            new("new.key", "new_value", "string", "New config", "General", true, false, "Production")
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: true, UserId: userId);

        SetupGetByKeysAsync(existingConfig); // Only existing.key exists

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(2);
        _mockConfigRepository.Verify(
            r => r.UpdateAsync(existingConfig, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockConfigRepository.Verify(
            r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyList_ReturnsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configurations = new List<ConfigurationImportItem>();
        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);

        SetupGetByKeysAsync(); // No keys to look up

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(0);
        _mockConfigRepository.Verify(
            r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        ImportConfigsCommand? command = null;

        // Act
        var act = async () => await _handler.Handle(command!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configurations = new List<ConfigurationImportItem>
        {
            new("test.key", "value", "string", null, "General", true, false, "All")
        };
        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockConfigRepository
            .Setup(r => r.GetByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), false, cancellationToken))
            .ReturnsAsync((IReadOnlyList<SystemConfig>)new List<SystemConfig>());

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.GetByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), false, cancellationToken),
            Times.Once);
        _mockConfigRepository.Verify(
            r => r.AddAsync(It.IsAny<SystemConfig>(), cancellationToken),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithRequiresRestart_SetsRequiresRestartFlag()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configurations = new List<ConfigurationImportItem>
        {
            new("restart.setting", "value", "string", "Requires restart", "System", true, RequiresRestart: true, "Production")
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);

        SetupGetByKeysAsync(); // No existing configs

        SystemConfig? capturedConfig = null;
        _mockConfigRepository
            .Setup(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()))
            .Callback<SystemConfig, CancellationToken>((config, _) => capturedConfig = config)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedConfig.Should().NotBeNull();
        capturedConfig!.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PreservesAllConfigurationProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configurations = new List<ConfigurationImportItem>
        {
            new(
                Key: "full.config",
                Value: "test_value",
                ValueType: "string",
                Description: "Full configuration test",
                Category: "TestCategory",
                IsActive: true,
                RequiresRestart: true,
                Environment: "Staging"
            )
        };

        var command = new ImportConfigsCommand(configurations, OverwriteExisting: false, UserId: userId);

        SetupGetByKeysAsync(); // No existing configs

        SystemConfig? capturedConfig = null;
        _mockConfigRepository
            .Setup(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()))
            .Callback<SystemConfig, CancellationToken>((config, _) => capturedConfig = config)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedConfig.Should().NotBeNull();
        capturedConfig!.Key.Value.Should().Be("full.config");
        capturedConfig.Value.Should().Be("test_value");
        capturedConfig.ValueType.Should().Be("string");
        capturedConfig.Description.Should().Be("Full configuration test");
        capturedConfig.Category.Should().Be("TestCategory");
        capturedConfig.IsActive.Should().BeTrue();
        capturedConfig.RequiresRestart.Should().BeTrue();
        capturedConfig.Environment.Should().Be("Staging");
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ImportConfigsCommandHandler(null!, _mockUnitOfWork.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configurationRepository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ImportConfigsCommandHandler(_mockConfigRepository.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("unitOfWork");
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
