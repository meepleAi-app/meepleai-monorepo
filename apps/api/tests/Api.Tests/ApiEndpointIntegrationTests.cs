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

        var seedRequest = new SeedRequest("demo-game");

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
        var qaRequest = new QaRequest("test-game", "How many players?");

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

    [Fact]
    public async Task Me_WithAuthentication_ReturnsUserInfo()
    {
        // Arrange - Create and login user
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "me@example.com",
            password = "TestPassword123",
            displayName = "Me User",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.Equal("me@example.com", result.user.email);
    }

    [Fact]
    public async Task Login_WithNullPayload_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/auth/login", (LoginPayload?)null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QA_WithAuthentication_ReturnsAnswer()
    {
        // Arrange - Create user and seed data
        var registerRequest = new
        {
            tenantId = "qa-tenant",
            tenantName = "QA Tenant",
            email = "qa@example.com",
            password = "TestPassword123",
            displayName = "QA User",
            role = "Admin"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Seed demo data
        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("qa-game"))
        };
        foreach (var cookie in cookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }
        await _client.SendAsync(seedRequest);

        // Act
        var qaRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/qa")
        {
            Content = JsonContent.Create(new QaRequest("qa-game", "How many players?"))
        };
        foreach (var cookie in cookies)
        {
            qaRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(qaRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QA_WithDifferentTenantId_ReturnsSuccess()
    {
        // Arrange - Create user in one tenant
        var registerRequest = new
        {
            tenantId = "tenant-a",
            tenantName = "Tenant A",
            email = "usera@example.com",
            password = "TestPassword123",
            displayName = "User A",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act - Try to access different tenant's data
        var qaRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/qa")
        {
            Content = JsonContent.Create(new QaRequest("some-game", "test query"))
        };
        foreach (var cookie in cookies)
        {
            qaRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(qaRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QA_WithEmptyGameId_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "qa2@example.com",
            password = "TestPassword123",
            displayName = "QA User 2",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var qaRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/qa")
        {
            Content = JsonContent.Create(new QaRequest("", "test query"))
        };
        foreach (var cookie in cookies)
        {
            qaRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(qaRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Explain_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var explainRequest = new
        {
            tenantId = "test-tenant",
            gameId = "test-game",
            topic = "game setup"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/agents/explain", explainRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Explain_WithAuthentication_ReturnsOutline()
    {
        // Arrange - Create user and seed data
        var registerRequest = new
        {
            tenantId = "explain-tenant",
            tenantName = "Explain Tenant",
            email = "explain@example.com",
            password = "TestPassword123",
            displayName = "Explain User",
            role = "Admin"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Seed demo data
        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("explain-game"))
        };
        foreach (var cookie in cookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }
        await _client.SendAsync(seedRequest);

        // Act
        var explainRequest = new HttpRequestMessage(HttpMethod.Post, "/agents/explain")
        {
            Content = JsonContent.Create(new
            {
                tenantId = "explain-tenant",
                gameId = "explain-game",
                topic = "game setup"
            })
        };
        foreach (var cookie in cookies)
        {
            explainRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(explainRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRuleSpec_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/games/test-game/rulespec");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRuleSpec_WithAuthentication_ReturnsRuleSpec()
    {
        // Arrange - Create user and seed data
        var registerRequest = new
        {
            tenantId = "rulespec-tenant",
            tenantName = "RuleSpec Tenant",
            email = "rulespec@example.com",
            password = "TestPassword123",
            displayName = "RuleSpec User",
            role = "Admin"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Seed demo data
        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("rulespec-game"))
        };
        foreach (var cookie in cookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }
        await _client.SendAsync(seedRequest);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/games/rulespec-game/rulespec");
        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRuleSpec_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "rulespec2@example.com",
            password = "TestPassword123",
            displayName = "Test User",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/games/nonexistent-game/rulespec");
        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRuleSpec_AsEditor_ReturnsUpdatedSpec()
    {
        // Arrange - Create editor user and game
        var registerRequest = new
        {
            tenantId = "update-tenant",
            tenantName = "Update Tenant",
            email = "editor@example.com",
            password = "TestPassword123",
            displayName = "Editor User",
            role = "Editor"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Seed game
        var adminRegisterRequest = new
        {
            tenantId = "update-tenant",
            tenantName = "Update Tenant",
            email = "admin@update.com",
            password = "AdminPassword123",
            displayName = "Admin User",
            role = "Admin"
        };

        var adminRegisterResponse = await _client.PostAsJsonAsync("/auth/register", adminRegisterRequest);
        var adminCookies = adminRegisterResponse.Headers.GetValues("Set-Cookie").ToList();

        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("update-game"))
        };
        foreach (var cookie in adminCookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }
        await _client.SendAsync(seedRequest);

        // Act - Update as editor
        var ruleSpec = new
        {
            gameId = "update-game",
            version = "v2-updated",
            updatedAt = DateTime.UtcNow,
            rules = new[]
            {
                new { id = "r1", text = "Updated rule", section = "Setup", page = "1", line = "1" }
            }
        };

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, "/games/update-game/rulespec")
        {
            Content = JsonContent.Create(ruleSpec)
        };
        foreach (var cookie in cookies)
        {
            updateRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRuleSpec_AsUser_ReturnsForbidden()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "user@example.com",
            password = "TestPassword123",
            displayName = "Regular User",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var ruleSpec = new
        {
            gameId = "test-game",
            version = "v1",
            updatedAt = DateTime.UtcNow,
            rules = new[] { new { id = "r1", text = "Test", section = "Test", page = "1", line = "1" } }
        };

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, "/games/test-game/rulespec")
        {
            Content = JsonContent.Create(ruleSpec)
        };
        foreach (var cookie in cookies)
        {
            updateRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRuleSpec_WithMismatchedGameId_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "editor2@example.com",
            password = "TestPassword123",
            displayName = "Editor User",
            role = "Editor"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act - gameId in URL doesn't match gameId in body
        var ruleSpec = new
        {
            gameId = "game-a",
            version = "v1",
            updatedAt = DateTime.UtcNow,
            rules = new[] { new { id = "r1", text = "Test", section = "Test", page = "1", line = "1" } }
        };

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, "/games/game-b/rulespec")
        {
            Content = JsonContent.Create(ruleSpec)
        };
        foreach (var cookie in cookies)
        {
            updateRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPdfText_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/pdfs/test-pdf-id/text");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPdfText_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "pdf@example.com",
            password = "TestPassword123",
            displayName = "PDF User",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/pdfs/nonexistent-pdf/text");
        foreach (var cookie in cookies)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Seed_AsNonAdmin_ReturnsForbidden()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "user2@example.com",
            password = "TestPassword123",
            displayName = "Regular User",
            role = "User"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("test-game"))
        };
        foreach (var cookie in cookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(seedRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Seed_WithDifferentTenantId_ReturnsSuccess()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "tenant-a",
            tenantName = "Tenant A",
            email = "admin2@example.com",
            password = "TestPassword123",
            displayName = "Admin User",
            role = "Admin"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act - Try to seed different tenant
        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("some-game"))
        };
        foreach (var cookie in cookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(seedRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Seed_WithEmptyGameId_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new
        {
            tenantId = "test-tenant",
            tenantName = "Test Tenant",
            email = "admin3@example.com",
            password = "TestPassword123",
            displayName = "Admin User",
            role = "Admin"
        };

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var cookies = registerResponse.Headers.GetValues("Set-Cookie").ToList();

        // Act
        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest(""))
        };
        foreach (var cookie in cookies)
        {
            seedRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await _client.SendAsync(seedRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record AuthResponse(AuthUser user, string expiresAt);
    private record AuthUser(string id, string tenantId, string email, string? displayName, string role);
}
