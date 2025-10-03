using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class ApiEndpointIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactoryFixture _factory;

    public ApiEndpointIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("ok", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_CreatesNewUser_ReturnsSuccess()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "test@example.com",
            password = "TestPassword123",
            displayName = "Test User",
            role = "User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.user.email);
        Assert.Equal("test-tenant", result.user.tenantId);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - Create first user
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "duplicate@example.com",
            password = "TestPassword123",
            displayName = "First User",
            role = "User"
        };

        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        // Act - Try to register with same email
        var duplicateRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "duplicate@example.com",
            password = "AnotherPassword123",
            displayName = "Second User",
            role = "User"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", duplicateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange - Create user first
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "login@example.com",
            password = "TestPassword123",
            displayName = "Login User",
            role = "User"
        };

        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new
        {
            tenantId = "test-tenant",
            email = "login@example.com",
            password = "TestPassword123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.Equal("login@example.com", result.user.email);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange - Create user first
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "invalid@example.com",
            password = "CorrectPassword123",
            displayName = "Test User",
            role = "User"
        };

        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new
        {
            tenantId = "test-tenant",
            email = "invalid@example.com",
            password = "WrongPassword123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Seed_CreatesGameAndRuleSpec_ReturnsSuccess()
    {
        // Arrange - Create and login admin user first
        var registerRequest = new
        {
            tenantId = "demo-tenant",
            tenantName = "Demo Tenant",
            email = "admin@demo.com",
            password = "AdminPassword123",
            displayName = "Admin User",
            role = "Admin"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        var seedRequest = new SeedRequest("demo-tenant", "demo-game");

        // Act - Send seed request with authentication
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(seedRequest)
        };

        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify data was created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = await db.Games.FindAsync("demo-game");
        Assert.NotNull(game);

        var ruleSpec = db.RuleSpecs.FirstOrDefault(r => r.GameId == "demo-game");
        Assert.NotNull(ruleSpec);
    }

    [Fact]
    public async Task QA_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var qaRequest = new QaRequest("test-tenant", "test-game", "How many players?");

        // Act
        var response = await _client.PostAsJsonAsync("/agents/qa", qaRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSession_ReturnsOk()
    {
        // Arrange - Create and login user
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "logout@example.com",
            password = "TestPassword123",
            displayName = "Logout User",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);

        // Extract cookies from register response
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        foreach (var cookie in cookies)
        {
            logoutRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(logoutRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record AuthResponse(AuthUser user, string expiresAt);
    private record AuthUser(string id, string tenantId, string email, string? displayName, string role);
}
