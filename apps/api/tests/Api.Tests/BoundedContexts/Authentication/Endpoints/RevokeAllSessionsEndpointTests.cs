using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// HTTP integration tests for POST /api/v1/auth/sessions/revoke-all.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C1 — revoke-all branch).
///
/// Validates the C1 fix in the revoke-all path: the endpoint must hash the current
/// session cookie via <see cref="Api.BoundedContexts.Authentication.Domain.ValueObjects.SessionTokenHasher"/>
/// so the hash matches stored Session.TokenHash and the "exclude current session"
/// branch actually excludes the right row.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class RevokeAllSessionsEndpointTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auth/sessions/revoke-all";
    private const string SessionCookieName = "meepleai_session";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public RevokeAllSessionsEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"revoke_all_sessions_{Guid.NewGuid():N}";
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
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task RevokeAllSessions_WithCurrentSession_ShouldExcludeCurrent()
    {
        // Arrange — register one user (creates session A) then login again to mint session B.
        var email = $"revoke-{Guid.NewGuid():N}@test.local";
        const string password = "ValidUnusualPwd123!";
        var sessionA = await RegisterAsync(email, password);
        var sessionB = await LoginAsync(email, password);

        sessionA.Should().NotBeNullOrWhiteSpace();
        sessionB.Should().NotBeNullOrWhiteSpace();
        sessionA.Should().NotBe(sessionB,
            "second login must mint a fresh session token, distinct from the registration session.");

        // Act — call revoke-all from session A asking to keep the current session (A).
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionA}");
        request.Content = JsonContent.Create(new { IncludeCurrentSession = false });

        var response = await _client.SendAsync(request);

        // Assert response payload: at least 1 session revoked (B), current session NOT revoked.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("revokedCount").GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "session B must be revoked.");
        body.GetProperty("currentSessionRevoked").GetBoolean().Should().BeFalse(
            "session A is the current session and must be preserved.");

        // Assert behavior: session A still authenticates; session B does not.
        var meWithA = await CallAuthMeAsync(sessionA);
        meWithA.StatusCode.Should().Be(HttpStatusCode.OK,
            "session A (current session, excluded) must remain valid.");

        var meWithB = await CallAuthMeAsync(sessionB);
        meWithB.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "session B must be revoked by the revoke-all call.");
    }

    [Fact]
    public async Task RevokeAllSessions_IncludeCurrent_ShouldRevokeCurrentToo()
    {
        // Variant: includeCurrentSession=true — current session must end up revoked.
        var email = $"revoke-incl-{Guid.NewGuid():N}@test.local";
        const string password = "ValidUnusualPwd123!";
        var sessionA = await RegisterAsync(email, password);
        var sessionB = await LoginAsync(email, password);

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionA}");
        request.Content = JsonContent.Create(new { IncludeCurrentSession = true });

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("revokedCount").GetInt32().Should().BeGreaterThanOrEqualTo(2,
            "both sessions A and B must be revoked when includeCurrentSession=true.");
        body.GetProperty("currentSessionRevoked").GetBoolean().Should().BeTrue(
            "session A must be marked as revoked when includeCurrentSession=true and the hash lookup succeeds (C1 fix).");

        var meWithA = await CallAuthMeAsync(sessionA);
        meWithA.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "session A must be revoked alongside session B.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<string> RegisterAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = password,
            DisplayName = $"User {email.Split('@')[0]}"
        });
        response.EnsureSuccessStatusCode();
        return ExtractSessionCookieValue(response)
            ?? throw new InvalidOperationException("Register did not set meepleai_session cookie.");
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        return ExtractSessionCookieValue(response)
            ?? throw new InvalidOperationException("Login did not set meepleai_session cookie.");
    }

    private async Task<HttpResponseMessage> CallAuthMeAsync(string sessionToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionToken}");
        return await _client.SendAsync(request);
    }

    private static string? ExtractSessionCookieValue(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith($"{SessionCookieName}=", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = cookie.Split(';')[0];
            return value[(SessionCookieName.Length + 1)..];
        }

        return null;
    }
}
