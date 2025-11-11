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
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for API endpoints.
///
/// Feature: Global API endpoint functionality
/// As an API consumer
/// I want to interact with authentication, seeding, and ingestion endpoints
/// So that I can manage game data and authenticate users
/// </summary>
[Collection("Postgres Integration Tests")]
public class ApiEndpointIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiEndpointIntegrationTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: User registers via API
    ///   Given a new user with valid credentials
    ///   When user posts to /auth/register
    ///   Then registration succeeds
    ///   And user is tracked for automatic cleanup
    /// </summary>
    [Fact]
    public async Task Register_ReturnsAuthResponseWithoutTenantInformation()
    {
        // Given: A new user with valid credentials
        using var client = Factory.CreateHttpsClient();

        var payload = new RegisterPayload(
            Email: $"register-{TestRunId}@example.com",
            Password: "Password123!",
            DisplayName: "Register User",
            Role: null);

        // When: User posts to /auth/register
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Then: Registration succeeds
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);

        authResponse.Should().NotBeNull();
        authResponse!.User.Email.Should().Be(payload.Email);
        authResponse.User.DisplayName.Should().Be(payload.DisplayName);
        authResponse.User.Role.Should().Be("user".ToString());

        // And: User is tracked for automatic cleanup
        TrackUserId(authResponse.User.Id);

        var cookies = ExtractCookies(response);
        cookies.Should().Contain(cookie => cookie.StartsWith($"{AuthService.SessionCookieName}="));
        AssertSessionCookieSecure(response);

        using var document = JsonDocument.Parse(json);
        AssertAuthResponsePayload(document.RootElement);
    }

    /// <summary>
    /// Scenario: Registered user logs in via API
    ///   Given user is already registered
    ///   When user posts to /auth/login with valid credentials
    ///   Then login succeeds
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task Login_ReturnsAuthResponseWithoutTenantInformation()
    {
        // Given: User is already registered
        var email = $"login-{TestRunId}@example.com";
        await RegisterUserAsync(email);

        // When: User posts to /auth/login with valid credentials
        using var client = Factory.CreateHttpsClient();
        var payload = new LoginPayload
        {
            Email = email,
            Password = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", payload);

        // Then: Login succeeds
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);

        authResponse.Should().NotBeNull();
        authResponse!.User.Email.Should().Be(email);

        var cookies = ExtractCookies(response);
        cookies.Should().Contain(cookie => cookie.StartsWith($"{AuthService.SessionCookieName}="));
        AssertSessionCookieSecure(response);

        using var document = JsonDocument.Parse(json);
        AssertAuthResponsePayload(document.RootElement);
        // Cleanup happens automatically via DisposeAsync
    }

    /// <summary>
    /// Scenario: Admin seeds game data via API
    ///   Given admin user is authenticated
    ///   When admin posts to /admin/seed with game ID
    ///   Then seeding succeeds
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task SeedEndpoint_AllowsAdminWithoutTenantPayload()
    {
        // Given: Admin user is authenticated
        using var client = Factory.CreateHttpsClient();
        var cookies = await RegisterAndAuthenticateAsync(client, $"seed-admin-{TestRunId}@example.com", role: "Admin");

        // When: Admin posts to /admin/seed with game ID
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/seed")
        {
            Content = JsonContent.Create(new SeedRequest("terraforming-mars"))
        };

        foreach (var cookie in cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var response = await client.SendAsync(request);

        // Then: Seeding succeeds
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        (document.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean()).Should().BeTrue();
        document.RootElement.TryGetProperty("spec", out var specElement).Should().BeTrue();
        specElement.GetProperty("gameId").GetString().Should().Be("terraforming-mars");
        // Cleanup happens automatically via DisposeAsync
    }

    /// <summary>
    /// Scenario: Admin generates RuleSpec from PDF with atomic rules
    ///   Given admin user is authenticated
    ///   And a game exists
    ///   And a PDF with atomic rules exists
    ///   When admin requests RuleSpec generation from PDF
    ///   Then structured RuleSpec is returned
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task GenerateRuleSpecFromPdf_ReturnsStructuredSpec()
    {
        // Given: Admin user is authenticated
        var user = await CreateTestUserAsync("pdf-parser", "admin");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Integration Game");

        // And: A PDF with atomic rules exists
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfId = $"pdf-{TestRunId}";
        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = game.Id,
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
        };

        db.PdfDocuments.Add(pdfDoc);
        await db.SaveChangesAsync();
        TrackPdfDocumentId(pdfId);

        // When: Admin requests RuleSpec generation from PDF
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ingest/pdf/{pdfId}/rulespec");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: Structured RuleSpec is returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var ruleSpec = JsonSerializer.Deserialize<RuleSpec>(json, JsonOptions);

        ruleSpec.Should().NotBeNull();
        ruleSpec!.gameId.Should().Be(game.Id);
        (ruleSpec.rules.Count >= 2).Should().BeTrue();
        ruleSpec.rules.Should().Contain(atom => atom.page == "2");
        // Cleanup happens automatically via DisposeAsync
    }

    private async Task RegisterUserAsync(string email, string role = "Admin")
    {
        using var client = Factory.CreateHttpsClient();
        await RegisterAndAuthenticateAsync(client, email, role);
    }

    private static void AssertSessionCookieSecure(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var values).Should().BeTrue();
        var sessionCookie = values!.Where(value => value.StartsWith($"{AuthService.SessionCookieName}=", StringComparison.Ordinal)).Should().ContainSingle().Subject;
        sessionCookie.Should().Contain("secure");
        sessionCookie.Should().Contain("samesite=none");
    }

    private async Task<List<string>> RegisterAndAuthenticateAsync(HttpClient client, string email, string role = "Admin")
    {
        var payload = new RegisterPayload(
            Email: email,
            Password: "Password123!",
            DisplayName: "Test User",
            Role: null);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.EnsureSuccessStatusCode();

        // Track user for cleanup
        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);
        if (authResponse?.User.Id != null)
        {
            TrackUserId(authResponse.User.Id);
        }

        if (!string.Equals(role, "user".ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var parsedRole = Enum.Parse<UserRole>(role, true);
            await PromoteUserAsync(email, parsedRole);
        }

        return ExtractCookies(response);
    }

    /// <summary>
    /// Scenario: Non-bootstrap user attempts to register with elevated role
    ///   Given a regular user is already registered (not bootstrap)
    ///   When another user tries to register with elevated role
    ///   Then registration is rejected with Conflict
    ///   And cleanup is automatic
    /// </summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    public async Task Register_ReturnsConflictWhenNonBootstrapRequestsElevatedRole(string requestedRole)
    {
        // Given: A regular user is already registered (not bootstrap)
        using var client = Factory.CreateHttpsClient();

        var initialPayload = new RegisterPayload(
            Email: $"initial-{TestRunId}-{Guid.NewGuid():N}@example.com",
            Password: "Password123!",
            DisplayName: "Initial User",
            Role: null);

        var initialResponse = await client.PostAsJsonAsync("/api/v1/auth/register", initialPayload);
        initialResponse.EnsureSuccessStatusCode();

        // Track initial user for cleanup
        var initialJson = await initialResponse.Content.ReadAsStringAsync();
        var initialAuth = JsonSerializer.Deserialize<AuthResponse>(initialJson, JsonOptions);
        if (initialAuth?.User.Id != null)
        {
            TrackUserId(initialAuth.User.Id);
        }

        // When: Another user tries to register with elevated role
        var escalationPayload = new RegisterPayload(
            Email: $"escalate-{TestRunId}-{Guid.NewGuid():N}@example.com",
            Password: "Password123!",
            DisplayName: "Escalation User",
            Role: requestedRole);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", escalationPayload);

        // Then: Registration is rejected with Conflict
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.Should().NotBeNull();
        body!.TryGetValue("error", out var message).Should().BeTrue();
        message.Should().Be("Only administrators can assign elevated roles.");
        // Cleanup happens automatically via DisposeAsync
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

    private async Task PromoteUserAsync(string email, UserRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    private static void AssertAuthResponsePayload(JsonElement root)
    {
        var topLevelProperties = root.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToList();

        topLevelProperties.Should().BeEquivalentTo(new[] { "expiresAt", "user" });

        var expiresAt = root.GetProperty("expiresAt");
        expiresAt.ValueKind.Should().Be(JsonValueKind.String);
        DateTime.TryParse(expiresAt.GetString(), out _).Should().BeTrue();

        var userElement = root.GetProperty("user");

        var userProperties = userElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToList();

        userProperties.Should().BeEquivalentTo(new[] { "displayName", "email", "id", "role" });

        string.IsNullOrWhiteSpace(userElement.GetProperty("id").GetString()).Should().BeFalse();
        userElement.GetProperty("email").ValueKind.Should().Be(JsonValueKind.String);
        userElement.GetProperty("role").ValueKind.Should().Be(JsonValueKind.String);
    }
}