using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Domain.Entities;

/// <summary>
/// Domain tests for N8NConfiguration aggregate.
/// Tests n8n configuration creation and lifecycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class N8NConfigurationTests
{
    [Fact]
    public void N8NConfiguration_Create_WithRequiredFields_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Production N8N";
        var baseUrl = new WorkflowUrl("https://n8n.example.com");
        var apiKey = "test_api_key_123";
        var createdBy = Guid.NewGuid();

        // Act
        var config = new N8NConfiguration(id, name, baseUrl, apiKey, createdBy);

        // Assert
        config.Id.Should().Be(id);
        config.Name.Should().Be(name);
        config.BaseUrl.Value.Should().Be(baseUrl.Value);
        config.ApiKeyEncrypted.Should().Be(apiKey);
        config.IsActive.Should().BeTrue();
    }

    [Fact]
    public void N8NConfiguration_Activate_WhenInactive_SetsActive()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Test N8N",
            new WorkflowUrl("https://test.n8n.com"),
            "key123",
            Guid.NewGuid());

        config.Deactivate();

        // Act
        config.Activate();

        // Assert
        config.IsActive.Should().BeTrue();
    }

    [Fact]
    public void N8NConfiguration_Deactivate_WhenActive_SetsInactive()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Test N8N",
            new WorkflowUrl("https://test.n8n.com"),
            "key123",
            Guid.NewGuid());

        // Act
        config.Deactivate();

        // Assert
        config.IsActive.Should().BeFalse();
    }
}

