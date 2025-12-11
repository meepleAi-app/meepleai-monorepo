using Api.BoundedContexts.WorkflowIntegration.Application.Handlers;
using Api.BoundedContexts.WorkflowIntegration.Application.Queries;
using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for GetActiveN8NConfigQueryHandler.
/// Tests retrieval of the currently active n8n configuration.
/// </summary>
public class GetActiveN8NConfigQueryHandlerTests
{
    private readonly Mock<IN8NConfigurationRepository> _mockRepository;
    private readonly GetActiveN8NConfigQueryHandler _handler;

    public GetActiveN8NConfigQueryHandlerTests()
    {
        _mockRepository = new Mock<IN8NConfigurationRepository>();
        _handler = new GetActiveN8NConfigQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithActiveConfig_ReturnsConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeConfig = new N8NConfiguration(
            Guid.NewGuid(),
            "Production N8N",
            new WorkflowUrl("https://n8n.production.com"),
            "prod_api_key",
            userId,
            webhookUrl: new WorkflowUrl("https://webhook.production.com")
        );

        var query = new GetActiveN8NConfigQuery();

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeConfig);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Production N8N", result.Name);
        Assert.Equal("https://n8n.production.com", result.BaseUrl);
        Assert.Equal("https://webhook.production.com", result.WebhookUrl);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithNoActiveConfig_ReturnsNull()
    {
        // Arrange
        var query = new GetActiveN8NConfigQuery();

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((N8NConfiguration?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(
            r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithTestedConfig_ReturnsTestResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var testedConfig = new N8NConfiguration(
            Guid.NewGuid(),
            "Tested N8N",
            new WorkflowUrl("https://tested.n8n.com"),
            "tested_key",
            userId
        );

        // Simulate a test
        testedConfig.RecordTestResult(true, "Connection successful");

        var query = new GetActiveN8NConfigQuery();

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testedConfig);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LastTestedAt);
        Assert.NotNull(result.LastTestResult);
        Assert.Contains("successful", result.LastTestResult);
    }

    [Fact]
    public async Task Handle_WithUntestedConfig_ReturnsNullTestData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var untestedConfig = new N8NConfiguration(
            Guid.NewGuid(),
            "Untested N8N",
            new WorkflowUrl("https://untested.n8n.com"),
            "untested_key",
            userId
        );

        var query = new GetActiveN8NConfigQuery();

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(untestedConfig);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LastTestedAt);
        Assert.Null(result.LastTestResult);
    }

    [Fact]
    public async Task Handle_ReturnsConfigurationWithoutWebhook()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var configWithoutWebhook = new N8NConfiguration(
            Guid.NewGuid(),
            "No Webhook Config",
            new WorkflowUrl("https://nowebhook.n8n.com"),
            "no_webhook_key",
            userId,
            webhookUrl: null
        );

        var query = new GetActiveN8NConfigQuery();

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configWithoutWebhook);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.WebhookUrl);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var query = new GetActiveN8NConfigQuery();
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(cancellationToken))
            .ReturnsAsync((N8NConfiguration?)null);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockRepository.Verify(
            r => r.GetActiveConfigurationAsync(cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsTimestamps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Timestamp Config",
            new WorkflowUrl("https://timestamp.n8n.com"),
            "timestamp_key",
            userId
        );

        var query = new GetActiveN8NConfigQuery();

        _mockRepository
            .Setup(r => r.GetActiveConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.CreatedAt);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }
}

