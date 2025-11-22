using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for CreateConfigurationCommandHandler.
/// Tests creation of system configuration entries.
/// </summary>
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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("max.connections", result.Key);
        Assert.Equal("100", result.Value);
        Assert.Equal("int", result.ValueType);
        Assert.Equal("Database", result.Category);
        Assert.True(result.RequiresRestart);
        Assert.True(result.IsActive);
        Assert.Equal(1, result.Version);

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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("feature.enabled", result.Key);
        Assert.Equal("true", result.Value);
        Assert.Equal("bool", result.ValueType);
        Assert.Null(result.Description);
        Assert.Equal("Features", result.Category); // Category was passed explicitly in the command
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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("string", result.ValueType);
        Assert.Equal("https://api.example.com", result.Value);
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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Development", result.Environment);
        Assert.False(result.RequiresRestart);
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
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsActive); // Default is active
        Assert.Equal(1, result.Version); // Initial version is 1
        Assert.NotEqual(Guid.Empty, result.Id); // ID should be generated
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
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
        var cancellationTokenSource = new CancellationTokenSource();
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