using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Infrastructure.Health;

/// <summary>
/// Integration tests for comprehensive health check system.
/// Tests the /api/v1/health endpoint with all registered health checks.
/// </summary>
public class HealthCheckIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOkStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnExpectedJsonStructure()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        // Assert
        root.TryGetProperty("overallStatus", out _).Should().BeTrue();
        root.TryGetProperty("checks", out var checks).Should().BeTrue();
        root.TryGetProperty("timestamp", out _).Should().BeTrue();

        checks.GetArrayLength().Should().BeGreaterThan(0);

        var firstCheck = checks.EnumerateArray().First();
        firstCheck.TryGetProperty("serviceName", out _).Should().BeTrue();
        firstCheck.TryGetProperty("status", out _).Should().BeTrue();
        firstCheck.TryGetProperty("description", out _).Should().BeTrue();
        firstCheck.TryGetProperty("isCritical", out _).Should().BeTrue();
        firstCheck.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludeCriticalServices()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var checks = json.RootElement.GetProperty("checks");

        var criticalServices = checks.EnumerateArray()
            .Where(c => c.GetProperty("isCritical").GetBoolean())
            .Select(c => c.GetProperty("serviceName").GetString())
            .ToList();

        // Assert
        criticalServices.Should().Contain("postgres", "Critical core infrastructure services must be checked");
        criticalServices.Should().Contain("redis", "Redis is critical for caching and sessions");
        criticalServices.Should().Contain("qdrant", "Qdrant is critical for vector search");
        criticalServices.Should().Contain("embedding", "Embedding service is critical for RAG pipeline");
    }

    [Fact]
    public async Task HealthEndpoint_ShouldIncludeNonCriticalServices()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var checks = json.RootElement.GetProperty("checks");

        var nonCriticalServices = checks.EnumerateArray()
            .Where(c => !c.GetProperty("isCritical").GetBoolean())
            .Select(c => c.GetProperty("serviceName").GetString())
            .ToList();

        // Assert - At least some non-critical services should be checked
        nonCriticalServices.Should().NotBeEmpty();
        // Examples: openrouter, reranker, bggapi, oauth, smtp, grafana, prometheus, hyperdx
    }

    [Fact]
    public async Task HealthEndpoint_WithAllServicesRunning_ShouldReturnHealthyOrDegraded()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var overallStatus = json.RootElement.GetProperty("overallStatus").GetString();

        // Assert
        // In test environment, some external services might not be available
        // so overall status can be Healthy or Degraded, but not Unhealthy
        overallStatus.Should().BeOneOf("Healthy", "Degraded");
    }

    [Fact]
    public async Task HealthEndpoint_ShouldCompleteWithin10Seconds()
    {
        // Arrange - Each health check has 5s timeout, but they run in parallel
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var response = await _client.GetAsync("/api/v1/health", cancellationTokenSource.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("redis")]
    [InlineData("qdrant")]
    [InlineData("embedding")]
    public async Task HealthEndpoint_ShouldIncludeCriticalService(string serviceName)
    {
        // Act
        var response = await _client.GetAsync("/api/v1/health");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var checks = json.RootElement.GetProperty("checks");

        var service = checks.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("serviceName").GetString() == serviceName);

        // Assert
        service.ValueKind.Should().NotBe(JsonValueKind.Undefined, $"{serviceName} check should be present");
        service.GetProperty("isCritical").GetBoolean().Should().BeTrue($"{serviceName} should be marked as critical");
    }
}
