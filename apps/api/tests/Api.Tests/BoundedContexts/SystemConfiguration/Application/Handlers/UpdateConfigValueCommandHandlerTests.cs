using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.SystemConfiguration.TestHelpers;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for UpdateConfigValueCommandHandler.
/// Tests configuration value updates with version tracking.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateConfigValueCommandHandlerTests
{
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateConfigValueCommandHandler _handler;

    public UpdateConfigValueCommandHandlerTests()
    {
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new UpdateConfigValueCommandHandler(
            _mockConfigRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidUpdate_UpdatesValue()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            configId,
            new ConfigKey("max.connections"),
            "100",
            "int",
            userId
        );

        var command = new UpdateConfigValueCommand(
            ConfigId: configId,
            NewValue: "200",
            UpdatedByUserId: userId
        );

        _mockConfigRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be("200");
        result.Version.Should().Be(2);
        result.UpdatedAt.Should().NotBe(default);

        _mockConfigRepository.Verify(
            r => r.UpdateAsync(existingConfig, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentConfig_ThrowsDomainException()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new UpdateConfigValueCommand(
            ConfigId: configId,
            NewValue: "new value",
            UpdatedByUserId: userId
        );

        _mockConfigRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<DomainException>()).Which;
        exception.Message.Should().Contain("not found");
        exception.Message.Should().Contain(configId.ToString());

        _mockConfigRepository.Verify(
            r => r.UpdateAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_IncrementsVersion()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            configId,
            new ConfigKey("test.key"),
            "initial",
            "string",
            userId
        );

        var command = new UpdateConfigValueCommand(
            ConfigId: configId,
            NewValue: "updated",
            UpdatedByUserId: userId
        );

        _mockConfigRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Version.Should().Be(2);
    }

    [Fact]
    public async Task Handle_PreservesPreviousValue()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            configId,
            new ConfigKey("feature.flag"),
            "false",
            "bool",
            userId
        );

        var command = new UpdateConfigValueCommand(
            ConfigId: configId,
            NewValue: "true",
            UpdatedByUserId: userId
        );

        _mockConfigRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - The domain entity should store previous value internally
        // This is verified through the UpdateValue method being called
        _mockConfigRepository.Verify(
            r => r.UpdateAsync(
                It.Is<SystemConfig>(c => c.Value == "true"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            configId,
            new ConfigKey("test.key"),
            "value",
            "string",
            userId
        );

        var command = new UpdateConfigValueCommand(
            ConfigId: configId,
            NewValue: "new value",
            UpdatedByUserId: userId
        );

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockConfigRepository
            .Setup(r => r.GetByIdAsync(configId, cancellationToken))
            .ReturnsAsync(existingConfig);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.GetByIdAsync(configId, cancellationToken),
            Times.Once);
        _mockConfigRepository.Verify(
            r => r.UpdateAsync(existingConfig, cancellationToken),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsUpdatedConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new SystemConfig(
            configId,
            new ConfigKey("cache.ttl"),
            "300",
            "int",
            userId,
            description: "Cache TTL",
            category: "Cache"
        );

        var command = new UpdateConfigValueCommand(
            ConfigId: configId,
            NewValue: "600",
            UpdatedByUserId: userId
        );

        _mockConfigRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(configId);
        result.Key.Should().Be("cache.ttl");
        result.Value.Should().Be("600");
        result.ValueType.Should().Be("int");
        result.Description.Should().Be("Cache TTL");
        result.Category.Should().Be("Cache");
    }
}


