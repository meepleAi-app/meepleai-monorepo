using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Domain.Events;

/// <summary>
/// Tests for WorkflowIntegration domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 25
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkflowIntegrationDomainEventsTests
{
    #region N8NConfigurationCreatedEvent Tests

    [Fact]
    public void N8NConfigurationCreatedEvent_WithAllParameters_SetsProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var baseUrl = new WorkflowUrl("https://n8n.example.com");
        var webhookUrl = new WorkflowUrl("https://n8n.example.com/webhook/abc123");

        // Act
        var evt = new N8NConfigurationCreatedEvent(
            configId,
            "Production N8N",
            baseUrl,
            webhookUrl,
            isActive: true,
            userId);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Name.Should().Be("Production N8N");
        evt.BaseUrl.Should().Be(baseUrl);
        evt.WebhookUrl.Should().Be(webhookUrl);
        evt.IsActive.Should().BeTrue();
        evt.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void N8NConfigurationCreatedEvent_WithNullWebhookUrl_SetsNullWebhook()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var baseUrl = new WorkflowUrl("https://n8n.example.com");

        // Act
        var evt = new N8NConfigurationCreatedEvent(
            configId,
            "Test Config",
            baseUrl,
            webhookUrl: null,
            isActive: false,
            userId);

        // Assert
        evt.WebhookUrl.Should().BeNull();
        evt.IsActive.Should().BeFalse();
    }

    #endregion

    #region N8NConfigurationTestedEvent Tests

    [Fact]
    public void N8NConfigurationTestedEvent_WithSuccess_SetsProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var testedAt = DateTime.UtcNow;

        // Act
        var evt = new N8NConfigurationTestedEvent(
            configId,
            testSuccess: true,
            "Connection successful",
            testedAt);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.TestSuccess.Should().BeTrue();
        evt.TestResult.Should().Be("Connection successful");
        evt.TestedAt.Should().Be(testedAt);
    }

    [Fact]
    public void N8NConfigurationTestedEvent_WithFailure_SetsProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var testedAt = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var evt = new N8NConfigurationTestedEvent(
            configId,
            testSuccess: false,
            "Connection timeout after 30s",
            testedAt);

        // Assert
        evt.TestSuccess.Should().BeFalse();
        evt.TestResult.Should().Be("Connection timeout after 30s");
    }

    #endregion

    #region N8NConfigurationUpdatedEvent Tests

    [Fact]
    public void N8NConfigurationUpdatedEvent_WithAllParameters_SetsProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var newBaseUrl = new WorkflowUrl("https://new-n8n.example.com");
        var newWebhookUrl = new WorkflowUrl("https://new-n8n.example.com/webhook/xyz");

        // Act
        var evt = new N8NConfigurationUpdatedEvent(
            configId,
            name: "Updated Config",
            baseUrl: newBaseUrl,
            webhookUrl: newWebhookUrl,
            isActive: true);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Name.Should().Be("Updated Config");
        evt.BaseUrl.Should().Be(newBaseUrl);
        evt.WebhookUrl.Should().Be(newWebhookUrl);
        evt.IsActive.Should().BeTrue();
    }

    [Fact]
    public void N8NConfigurationUpdatedEvent_WithOnlyName_SetsNameOnly()
    {
        // Arrange
        var configId = Guid.NewGuid();

        // Act
        var evt = new N8NConfigurationUpdatedEvent(configId, name: "New Name");

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Name.Should().Be("New Name");
        evt.BaseUrl.Should().BeNull();
        evt.WebhookUrl.Should().BeNull();
        evt.IsActive.Should().BeNull();
    }

    [Fact]
    public void N8NConfigurationUpdatedEvent_WithOnlyIsActive_SetsActiveOnly()
    {
        // Arrange
        var configId = Guid.NewGuid();

        // Act
        var evt = new N8NConfigurationUpdatedEvent(configId, isActive: false);

        // Assert
        evt.Name.Should().BeNull();
        evt.BaseUrl.Should().BeNull();
        evt.WebhookUrl.Should().BeNull();
        evt.IsActive.Should().BeFalse();
    }

    #endregion

    #region WorkflowErrorLoggedEvent Tests

    [Fact]
    public void WorkflowErrorLoggedEvent_WithAllParameters_SetsProperties()
    {
        // Arrange
        var errorLogId = Guid.NewGuid();

        // Act
        var evt = new WorkflowErrorLoggedEvent(
            errorLogId,
            "workflow-123",
            "exec-456",
            "Failed to process data",
            nodeName: "HTTP Request",
            stackTrace: "at ProcessData() line 42");

        // Assert
        evt.ErrorLogId.Should().Be(errorLogId);
        evt.WorkflowId.Should().Be("workflow-123");
        evt.ExecutionId.Should().Be("exec-456");
        evt.ErrorMessage.Should().Be("Failed to process data");
        evt.NodeName.Should().Be("HTTP Request");
        evt.StackTrace.Should().Be("at ProcessData() line 42");
    }

    [Fact]
    public void WorkflowErrorLoggedEvent_WithMinimalParameters_SetsNullOptionals()
    {
        // Arrange
        var errorLogId = Guid.NewGuid();

        // Act
        var evt = new WorkflowErrorLoggedEvent(
            errorLogId,
            "workflow-789",
            "exec-101",
            "Unknown error occurred");

        // Assert
        evt.ErrorLogId.Should().Be(errorLogId);
        evt.WorkflowId.Should().Be("workflow-789");
        evt.ExecutionId.Should().Be("exec-101");
        evt.ErrorMessage.Should().Be("Unknown error occurred");
        evt.NodeName.Should().BeNull();
        evt.StackTrace.Should().BeNull();
    }

    #endregion

    #region WorkflowRetriedEvent Tests

    [Fact]
    public void WorkflowRetriedEvent_WithAllParameters_SetsProperties()
    {
        // Arrange
        var errorLogId = Guid.NewGuid();

        // Act
        var evt = new WorkflowRetriedEvent(
            errorLogId,
            "workflow-retry-1",
            "exec-retry-1",
            retryCount: 2,
            maxRetries: 5);

        // Assert
        evt.ErrorLogId.Should().Be(errorLogId);
        evt.WorkflowId.Should().Be("workflow-retry-1");
        evt.ExecutionId.Should().Be("exec-retry-1");
        evt.RetryCount.Should().Be(2);
        evt.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void WorkflowRetriedEvent_WithDefaultMaxRetries_SetsThree()
    {
        // Arrange
        var errorLogId = Guid.NewGuid();

        // Act
        var evt = new WorkflowRetriedEvent(
            errorLogId,
            "workflow-def",
            "exec-def",
            retryCount: 1);

        // Assert
        evt.RetryCount.Should().Be(1);
        evt.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void WorkflowRetriedEvent_FirstRetry_SetsRetryCountToOne()
    {
        // Arrange
        var errorLogId = Guid.NewGuid();

        // Act
        var evt = new WorkflowRetriedEvent(
            errorLogId,
            "workflow-first",
            "exec-first",
            retryCount: 1,
            maxRetries: 3);

        // Assert
        evt.RetryCount.Should().Be(1);
    }

    [Fact]
    public void WorkflowRetriedEvent_FinalRetry_SetsRetryCountEqualToMax()
    {
        // Arrange
        var errorLogId = Guid.NewGuid();

        // Act
        var evt = new WorkflowRetriedEvent(
            errorLogId,
            "workflow-final",
            "exec-final",
            retryCount: 3,
            maxRetries: 3);

        // Assert
        evt.RetryCount.Should().Be(evt.MaxRetries);
    }

    #endregion
}
