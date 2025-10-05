using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public class ApiEndpointIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebApplicationFactoryFixture _factory;

    public ApiEndpointIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ReturnsAuthResponseWithoutTenantInformation()
    {
        using var client = _factory.CreateClient();

        var payload = new RegisterPayload(
            "register-user@example.com",
            "Password123!",
            "Register User",
            "Admin");

        var response = await client.PostAsJsonAsync("/auth/register", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);

        Assert.NotNull(authResponse);
        Assert.Equal(payload.email, authResponse!.user.email);
        Assert.Equal(payload.displayName, authResponse.user.displayName);
        Assert.Equal(payload.role, authResponse.user.role);

        var cookies = ExtractCookies(response);
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{AuthService.SessionCookieName}=", StringComparison.Ordinal));

        using var document = JsonDocument.Parse(json);
        AssertAuthResponsePayload(document.RootElement);
    }

    [Fact]
    public async Task Login_ReturnsAuthResponseWithoutTenantInformation()
    {
        var email = "login-user@example.com";
        await RegisterUserAsync(email);

        using var client = _factory.CreateClient();
        var payload = new LoginPayload
        {
            email = email,
            password = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/auth/login", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);

        Assert.NotNull(authResponse);
        Assert.Equal(email, authResponse!.user.email);

        var cookies = ExtractCookies(response);
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{AuthService.SessionCookieName}=", StringComparison.Ordinal));

        using var document = JsonDocument.Parse(json);
        AssertAuthResponsePayload(document.RootElement);
    }

    [Fact]
    public async Task SeedEndpoint_AllowsAdminWithoutTenantPayload()
    {
        using var client = _factory.CreateClient();
        var cookies = await RegisterAndAuthenticateAsync(client, "seed-admin@example.com", role: "Admin");

        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("terraforming-mars"))
        };

        foreach (var cookie in cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean());
        Assert.True(document.RootElement.TryGetProperty("spec", out var specElement));
        Assert.Equal("terraforming-mars", specElement.GetProperty("gameId").GetString());
    }

    [Fact]
    public async Task GenerateRuleSpecFromPdf_ReturnsStructuredSpec()
    {
        using var client = _factory.CreateClient();
        var email = "pdf-parser@example.com";
        var cookies = await RegisterAndAuthenticateAsync(client, email, role: "Admin");

        var pdfId = "pdf-integration";
        var gameId = "game-integration";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);

            if (!await db.Games.AnyAsync(g => g.Id == gameId))
            {
                db.Games.Add(new GameEntity
                {
                    Id = gameId,
                    Name = "Integration Game",
                    CreatedAt = DateTime.UtcNow
                });
            }

            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                GameId = gameId,
                FileName = "rules.pdf",
                FilePath = "/tmp/rules.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = user.Id,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "completed",
                ProcessedAt = DateTime.UtcNow,
                AtomicRules = JsonSerializer.Serialize(new[]
                {
                    "[Table on page 2] Setup: Place pieces; Count: 16",
                    "Victory condition: Highest score wins"
                })
            });

            await db.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/ingest/pdf/{pdfId}/rulespec");
        foreach (var cookie in cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var ruleSpec = JsonSerializer.Deserialize<RuleSpec>(json, JsonOptions);

        Assert.NotNull(ruleSpec);
        Assert.Equal(gameId, ruleSpec!.gameId);
        Assert.True(ruleSpec.rules.Count >= 2);
        Assert.Contains(ruleSpec.rules, atom => atom.page == "2");
    }

    private async Task RegisterUserAsync(string email, string role = "Admin")
    {
        using var client = _factory.CreateClient();
        await RegisterAndAuthenticateAsync(client, email, role);
    }

    private async Task<List<string>> RegisterAndAuthenticateAsync(HttpClient client, string email, string role = "Admin")
    {
        var payload = new RegisterPayload(
            email,
            "Password123!",
            "Test User",
            role);

        var response = await client.PostAsJsonAsync("/auth/register", payload);
        response.EnsureSuccessStatusCode();

        return ExtractCookies(response);
    }

    private static List<string> ExtractCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return new List<string>();
        }

        return values
            .Select(value => value.Split(';')[0])
            .ToList();
    }

    private static void AssertAuthResponsePayload(JsonElement root)
    {
        var topLevelProperties = root.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(new[] { "expiresAt", "user" }, topLevelProperties);

        var expiresAt = root.GetProperty("expiresAt");
        Assert.Equal(JsonValueKind.String, expiresAt.ValueKind);
        Assert.True(DateTime.TryParse(expiresAt.GetString(), out _));

        var userElement = root.GetProperty("user");

        var userProperties = userElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(new[] { "displayName", "email", "id", "role" }, userProperties);

        Assert.False(string.IsNullOrWhiteSpace(userElement.GetProperty("id").GetString()));
        Assert.Equal(JsonValueKind.String, userElement.GetProperty("email").ValueKind);
        Assert.Equal(JsonValueKind.String, userElement.GetProperty("role").ValueKind);
    }
}
