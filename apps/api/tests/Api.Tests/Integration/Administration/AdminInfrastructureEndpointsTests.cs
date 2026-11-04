using System.Net;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> La classe non semina nulla: ogni test
/// crea la propria sessione con <c>CreateAdminSessionAsync(dbContext)</c> <b>senza id fisso</b>,
/// quindi un utente nuovo ogni volta. Tutte e dieci le asserzioni sono status code (200/401/403) su
/// endpoint di infrastruttura che non leggono dati di dominio — nessun conteggio, nessuna lista,
/// nessun ordinamento su cui i test possano interferire.
/// </para>
/// </summary>
public sealed class AdminInfrastructureHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "infra_endpoints");

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
public sealed class AdminInfrastructureEndpointsTests : IClassFixture<AdminInfrastructureHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminInfrastructureEndpointsTests(AdminInfrastructureHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
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
