using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Domain.Entities;

/// <summary>
/// Domain tests for N8nConfiguration aggregate.
/// Tests n8n configuration creation and lifecycle.
/// </summary>
public class N8nConfigurationTests
{
    [Fact]
    public void N8nConfiguration_Create_WithRequiredFields_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Production N8n";
        var baseUrl = "https://n8n.example.com";
        var apiKey = "test_api_key_123";

        // Act
        var config = new N8nConfiguration(id, name, baseUrl, apiKey);

        // Assert
        Assert.Equal(id, config.Id);
        Assert.Equal(name, config.Name);
        Assert.Equal(baseUrl, config.BaseUrl);
        Assert.Equal(apiKey, config.ApiKey);
        Assert.True(config.IsActive);
    }

    [Fact]
    public void N8nConfiguration_Activate_WhenInactive_SetsActive()
    {
        // Arrange
        var config = new N8nConfiguration(
            Guid.NewGuid(),
            "Test N8n",
            "https://test.n8n.com",
            "key123");

        config.Deactivate();

        // Act
        config.Activate();

        // Assert
        Assert.True(config.IsActive);
    }

    [Fact]
    public void N8nConfiguration_Deactivate_WhenActive_SetsInactive()
    {
        // Arrange
        var config = new N8nConfiguration(
            Guid.NewGuid(),
            "Test N8n",
            "https://test.n8n.com",
            "key123");

        // Act
        config.Deactivate();

        // Assert
        Assert.False(config.IsActive);
    }
}
