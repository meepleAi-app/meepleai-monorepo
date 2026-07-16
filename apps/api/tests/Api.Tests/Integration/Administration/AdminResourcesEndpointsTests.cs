using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for the self-contained system resources endpoint (Issue #3041):
///   GET /api/v1/resources/system — Admin+ (process/host CPU/RAM/uptime via System.Diagnostics)
/// Verifies auth (401/403), and that the endpoint returns real metrics WITHOUT depending
/// on Prometheus/exporters (the service reads System.Diagnostics directly).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class AdminResourcesEndpointsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AdminResourcesEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"resources_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ========================================
    // GET /api/v1/resources/system
    // ========================================

    [Fact]
    public async Task GetSystemResources_WithAdminAuth_Returns200WithRealMetrics()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/resources/system", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — self-contained: real metrics returned even with no Prometheus in test env
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("processorCount").GetInt32().Should().BeGreaterThan(0);
        root.GetProperty("hostMemoryTotalBytes").GetInt64().Should().BeGreaterThan(0);
        root.GetProperty("processWorkingSetBytes").GetInt64().Should().BeGreaterThan(0);
        root.GetProperty("processCpuPercent").GetDouble().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSystemResources_WithRegularUserAuth_Returns403()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/resources/system", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSystemResources_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/resources/system");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // Helpers
    // ========================================

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string sessionToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={sessionToken}");
        return request;
    }
}
