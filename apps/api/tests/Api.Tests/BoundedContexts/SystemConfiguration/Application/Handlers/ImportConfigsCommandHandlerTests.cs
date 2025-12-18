using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Unit tests for ImportConfigsCommandHandler.
/// Tests configuration import operations with overwrite and merge scenarios.
/// Issue: #2188
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
            _mockUnitOfWork.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidConfigs_CreatesNewConfigurations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<ConfigurationImportItem>
        {
            new("App:Name", "MeepleAI", "string", "Application name", "App", true, false, "All"),
            new("App:Version", "1.0.0", "string", "App version", "App", true, false, "All")
        };

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var command = new ImportConfigsCommand(configs, OverwriteExisting: false, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        _mockConfigRepository.Verify(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOverwriteTrue_UpdatesExistingConfig()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("App:MaxUsers"),
            value: "100",
            valueType: "int",
            createdByUserId: userId,
            description: "Max users",
            category: "App",
            environment: "All"
        );

        var importItem = new ConfigurationImportItem(
            "App:MaxUsers", "200", "int", "Max users updated", "App", true, false, "All"
        );

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("App:MaxUsers", "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        var command = new ImportConfigsCommand(
            new List<ConfigurationImportItem> { importItem },
            OverwriteExisting: true,
            userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        _mockConfigRepository.Verify(r => r.UpdateAsync(existingConfig, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        existingConfig.Value.Should().Be("200");
    }

    [Fact]
    public async Task Handle_WithOverwriteFalse_SkipsExistingConfig()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("App:MaxUsers"),
            value: "100",
            valueType: "int",
            createdByUserId: userId
        );

        var importItem = new ConfigurationImportItem(
            "App:MaxUsers", "200", "int", "Max users", "App", true, false, "All"
        );

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("App:MaxUsers", "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        var command = new ImportConfigsCommand(
            new List<ConfigurationImportItem> { importItem },
            OverwriteExisting: false,
            userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(0); // Skipped
        _mockConfigRepository.Verify(r => r.UpdateAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockConfigRepository.Verify(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInactiveConfig_DeactivatesConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var importItem = new ConfigurationImportItem(
            "Feature:NewFeature", "false", "bool", "New feature flag", "Features", IsActive: false, RequiresRestart: false, Environment: "All"
        );

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        SystemConfig? capturedConfig = null;
        _mockConfigRepository
            .Setup(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()))
            .Callback<SystemConfig, CancellationToken>((config, _) => capturedConfig = config)
            .Returns(Task.CompletedTask);

        var command = new ImportConfigsCommand(
            new List<ConfigurationImportItem> { importItem },
            OverwriteExisting: false,
            userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        capturedConfig.Should().NotBeNull();
        capturedConfig!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithMultipleConfigs_ReturnsCorrectCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            id: Guid.NewGuid(),
            key: new ConfigKey("Existing:Key"),
            value: "old",
            valueType: "string",
            createdByUserId: userId
        );

        var configs = new List<ConfigurationImportItem>
        {
            new("New:Key1", "value1", "string", "New config 1", "Test", true, false, "All"),
            new("Existing:Key", "new", "string", "Existing config", "Test", true, false, "All"),
            new("New:Key2", "value2", "string", "New config 2", "Test", true, false, "All")
        };

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("Existing:Key", "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("New:Key1", "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync("New:Key2", "All", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var command = new ImportConfigsCommand(configs, OverwriteExisting: true, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(3); // 2 new + 1 updated
        _mockConfigRepository.Verify(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockConfigRepository.Verify(r => r.UpdateAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_WithInvalidConfigKey_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configs = new List<ConfigurationImportItem>
        {
            new("Invalid Key With Spaces!", "value", "string", "Invalid", "Test", true, false, "All")
        };

        var command = new ImportConfigsCommand(configs, OverwriteExisting: false, userId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );
        exception.Message.Should().Contain("Configuration key");
    }
}
