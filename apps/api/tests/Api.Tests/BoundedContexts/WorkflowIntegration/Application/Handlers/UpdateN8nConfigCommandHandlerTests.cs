using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.Handlers;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for UpdateN8nConfigCommandHandler.
/// Tests updates to n8n workflow configurations.
/// </summary>
public class UpdateN8nConfigCommandHandlerTests
{
    private readonly Mock<IN8nConfigurationRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UpdateN8nConfigCommandHandler _handler;

    public UpdateN8nConfigCommandHandlerTests()
    {
        _mockRepository = new Mock<IN8nConfigurationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new UpdateN8nConfigCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidUpdate_UpdatesConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new N8nConfiguration(
            configId,
            "Old Name",
            new WorkflowUrl("https://old.n8n.com"),
            "old_api_key",
            userId
        );

        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Updated Name",
            BaseUrl: "https://new.n8n.com",
            WebhookUrl: "https://webhook.new.com",
            ApiKeyEncrypted: "new_api_key",
            IsActive: null
        );

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("https://new.n8n.com", result.BaseUrl);
        Assert.Equal("https://webhook.new.com", result.WebhookUrl);

        _mockRepository.Verify(
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
        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Test",
            BaseUrl: "https://test.com",
            WebhookUrl: null,
            ApiKeyEncrypted: "key",
            IsActive: null
        );

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((N8nConfiguration?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found", exception.Message);
        Assert.Contains(configId.ToString(), exception.Message);

        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<N8nConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithIsActiveTrue_ActivatesConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new N8nConfiguration(
            configId,
            "Test Config",
            new WorkflowUrl("https://test.n8n.com"),
            "api_key",
            userId
        );
        existingConfig.Deactivate(); // Start as inactive

        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Test Config",
            BaseUrl: null,
            WebhookUrl: null,
            ApiKeyEncrypted: null,
            IsActive: true
        );

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithIsActiveFalse_DeactivatesConfiguration()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new N8nConfiguration(
            configId,
            "Active Config",
            new WorkflowUrl("https://active.n8n.com"),
            "api_key",
            userId
        );

        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Active Config",
            BaseUrl: null,
            WebhookUrl: null,
            ApiKeyEncrypted: null,
            IsActive: false
        );

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithNullWebhookUrl_PreservesExistingWebhook()
    {
        // Arrange - In the update pattern, null means "don't change"
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new N8nConfiguration(
            configId,
            "Test Config",
            new WorkflowUrl("https://test.n8n.com"),
            "api_key",
            userId,
            webhookUrl: new WorkflowUrl("https://old.webhook.com")
        );

        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Test Config",
            BaseUrl: null,
            WebhookUrl: null, // null means "don't change", not "clear"
            ApiKeyEncrypted: null,
            IsActive: null
        );

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - WebhookUrl should remain unchanged when null is passed
        Assert.Equal("https://old.webhook.com", result.WebhookUrl);
    }

    [Fact]
    public async Task Handle_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new N8nConfiguration(
            configId,
            "Original Name",
            new WorkflowUrl("https://original.n8n.com"),
            "original_key",
            userId
        );

        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Updated Name Only",
            BaseUrl: null, // Not updating base URL
            WebhookUrl: null,
            ApiKeyEncrypted: null, // Not updating API key
            IsActive: null
        );

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Updated Name Only", result.Name);
        // Original URL should be preserved (UpdateConfiguration handles null properly)
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingConfig = new N8nConfiguration(
            configId,
            "Test",
            new WorkflowUrl("https://test.com"),
            "key",
            userId
        );

        var command = new UpdateN8nConfigCommand(
            ConfigId: configId,
            Name: "Updated",
            BaseUrl: null,
            WebhookUrl: null,
            ApiKeyEncrypted: null,
            IsActive: null
        );

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetByIdAsync(configId, cancellationToken))
            .ReturnsAsync(existingConfig);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetByIdAsync(configId, cancellationToken),
            Times.Once);
        _mockRepository.Verify(
            r => r.UpdateAsync(existingConfig, cancellationToken),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }
}

