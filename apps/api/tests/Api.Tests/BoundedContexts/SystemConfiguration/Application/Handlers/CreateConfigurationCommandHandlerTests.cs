using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for CreateConfigurationCommandHandler.
/// Tests creation of system configuration entries.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateConfigurationCommandHandlerTests
{
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateConfigurationCommandHandler _handler;

    public CreateConfigurationCommandHandlerTests()
    {
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new CreateConfigurationCommandHandler(
            _mockConfigRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateConfigurationCommand(
            Key: "max.connections",
            Value: "100",
            ValueType: "int",
            CreatedByUserId: userId,
            Description: "Maximum database connections",
            Category: "Database",
            Environment: "Production",
            RequiresRestart: true
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("max.connections");
        result.Value.Should().Be("100");
        result.ValueType.Should().Be("int");
        result.Category.Should().Be("Database");
        result.RequiresRestart.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.Version.Should().Be(1);

        _mockConfigRepository.Verify(
            r => r.AddAsync(
                It.IsAny<SystemConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMinimalFields_CreatesConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateConfigurationCommand(
            Key: "feature.enabled",
            Value: "true",
            ValueType: "bool",
            Description: null,
            Category: "Features",
            Environment: "All",
            RequiresRestart: false,
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("feature.enabled");
        result.Value.Should().Be("true");
        result.ValueType.Should().Be("bool");
        result.Description.Should().BeNull();
        result.Category.Should().Be("Features");
    }

    [Fact]
    public async Task Handle_WithStringType_CreatesStringConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateConfigurationCommand(
            Key: "api.base.url",
            Value: "https://api.example.com",
            ValueType: "string",
            CreatedByUserId: userId,
            Description: "API base URL",
            Category: "API",
            Environment: "All",
            RequiresRestart: true
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ValueType.Should().Be("string");
        result.Value.Should().Be("https://api.example.com");
    }

    [Fact]
    public async Task Handle_WithEnvironmentSpecific_CreatesEnvironmentConfig()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateConfigurationCommand(
            Key: "cache.ttl",
            Value: "300",
            ValueType: "int",
            CreatedByUserId: userId,
            Description: "Cache TTL in seconds",
            Category: "Cache",
            Environment: "Development",
            RequiresRestart: false
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Environment.Should().Be("Development");
        result.RequiresRestart.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SetsDefaultsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateConfigurationCommand(
            Key: "test.config",
            Value: "value",
            ValueType: "string",
            Description: null,
            Category: "Test",
            Environment: "All",
            RequiresRestart: false,
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsActive.Should().BeTrue();
        result.Version.Should().Be(1);
        result.Id.Should().NotBe(Guid.Empty);
        (result.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateConfigurationCommand(
            Key: "test.key",
            Value: "test.value",
            ValueType: "string",
            Description: null,
            Category: "Test",
            Environment: "All",
            RequiresRestart: false,
            CreatedByUserId: userId
        );
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.AddAsync(
                It.IsAny<SystemConfig>(),
                cancellationToken),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}


