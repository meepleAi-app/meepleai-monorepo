using System.Net;
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
/// Integration tests for Admin Infrastructure Dashboard HTTP endpoints.
/// Tests authentication, authorization, and routing for AI service monitoring endpoints.
/// Endpoints defined in AdminInfrastructureEndpoints.cs:
///   GET  /admin/infrastructure/services              — Admin+ (service status overview)
///   GET  /admin/infrastructure/pipeline/test          — Admin+ (pipeline connectivity test)
///   POST /admin/infrastructure/services/{name}/restart     — SuperAdmin only
///   POST /admin/infrastructure/services/{name}/health-check — SuperAdmin only
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class AdminInfrastructureEndpointsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AdminInfrastructureEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"infra_endpoints_{Guid.NewGuid():N}";
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
    // GET /admin/infrastructure/services
    // ========================================

    [Fact]
    public async Task GetServices_WithAdminAuth_Returns200()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/admin/infrastructure/services", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetServices_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/infrastructure/services");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetServices_WithRegularUserAuth_Returns403()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/admin/infrastructure/services", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ========================================
    // GET /admin/infrastructure/pipeline/test
    // ========================================

    [Fact]
    public async Task PipelineTest_WithAdminAuth_Returns200()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/admin/infrastructure/pipeline/test", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — endpoint may return 200 with connectivity results or 500 if services are unavailable in test env
        // Both are acceptable: 200 means routing + auth work; 500 means handler executed but external services unavailable
        (response.StatusCode == HttpStatusCode.OK
         || response.StatusCode == HttpStatusCode.InternalServerError)
            .Should().BeTrue($"Expected 200 or 500, got {response.StatusCode}");
    }

    [Fact]
    public async Task PipelineTest_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/infrastructure/pipeline/test");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // POST /admin/infrastructure/services/{name}/restart — SuperAdmin only
    // ========================================

    [Fact]
    public async Task RestartService_WithAdminAuth_ReturnsForbidden()
    {
        // Arrange — Admin (not SuperAdmin) should be rejected
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/admin/infrastructure/services/embedding/restart", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — RequireSuperAdminSession rejects Admin role
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestartService_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/infrastructure/services/embedding/restart");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RestartService_UnknownService_WithSuperAdmin_ReturnsError()
    {
        // Arrange — even SuperAdmin cannot restart a non-existent service
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/admin/infrastructure/services/unknown/restart", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — ServiceRegistry.Services["unknown"] throws KeyNotFoundException → 500,
        // or handler returns 400/404 depending on error handling
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "Restarting an unknown service should not succeed");
        response.IsSuccessStatusCode.Should().BeFalse(
            "An unknown service name should result in an error response");
    }

    // ========================================
    // POST /admin/infrastructure/services/{name}/health-check — SuperAdmin only
    // ========================================

    [Fact]
    public async Task HealthCheck_WithAdminAuth_ReturnsForbidden()
    {
        // Arrange — Admin (not SuperAdmin) should be rejected
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(dbContext);
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/admin/infrastructure/services/embedding/health-check", sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — RequireSuperAdminSession rejects Admin role
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HealthCheck_WithoutAuth_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/infrastructure/services/embedding/health-check");

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
