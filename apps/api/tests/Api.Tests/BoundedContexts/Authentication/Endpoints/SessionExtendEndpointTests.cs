using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// HTTP integration tests for POST /api/v1/auth/session/extend.
/// Spec: docs/superpowers/specs/2026-05-06-auth-flow-security-fixes-design.md (C1).
///
/// C1 — Hash Mismatch:
///   ExtendSession previously hashed UTF-8 bytes of the Base-64 cookie value, while
///   storage hashed the *decoded* token bytes (via SessionToken.ComputeHash). The
///   mismatched algorithms guaranteed a 401 even for valid sessions.
///
/// These tests exercise the full HTTP stack so a regression to the broken
/// inline hash would fail the suite.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public sealed class SessionExtendEndpointTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auth/session/extend";
    private const string SessionCookieName = "meepleai_session";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public SessionExtendEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"session_extend_{Guid.NewGuid():N}";
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
    public async Task ExtendSession_WithValidCookie_ShouldExtendExpiration()
    {
        // Arrange — register a fresh user; the response sets meepleai_session cookie.
        var (sessionToken, originalExpiresAt) = await RegisterAndCaptureSessionAsync();

        // Act — POST to /auth/session/extend with the cookie.
        var extendRequest = BuildAuthenticatedRequest(HttpMethod.Post, Endpoint, sessionToken);
        var extendResponse = await _client.SendAsync(extendRequest);

        // Assert — pre-fix this returns 401 (hash mismatch). Post-fix it returns 200 OK
        // with a NewExpiresAt that is at least equal to the original expiration.
        extendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await extendResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("expiresAt", out var expiresAtElement)
            .Should().BeTrue("response must expose the new expiration timestamp");

        var newExpiresAt = expiresAtElement.GetDateTime();
        newExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1),
            "extension should yield a future expiration");
        newExpiresAt.Should().BeOnOrAfter(originalExpiresAt,
            "extending must never shrink the existing window");
    }

    [Fact]
    public async Task ExtendSession_HashComputationMatchesStorage()
    {
        // Regression test for C1: hash computed at the endpoint MUST equal the
        // hash stored in user_sessions.token_hash (i.e. SessionToken.ComputeHash).
        var (sessionToken, _) = await RegisterAndCaptureSessionAsync();

        // Compute the hash both ways.
        var hashViaHasher = SessionTokenHasher.HashFromCookie(sessionToken);
        var hashViaSessionToken = SessionToken.FromStored(sessionToken).ComputeHash();

        hashViaHasher.Should().Be(hashViaSessionToken,
            "centralized hasher and SessionToken aggregate must agree (single source of truth).");

        // Confirm the stored hash in the DB matches as well.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var storedSession = await dbContext.Set<UserSessionEntity>()
            .FirstOrDefaultAsync(s => s.TokenHash == hashViaHasher);

        storedSession.Should().NotBeNull(
            "the hash computed via the canonical hasher must locate the session row inserted on registration.");

        // Sanity check — the broken inline hash (SHA-256 over the UTF-8 string of the cookie)
        // must NOT match the stored hash. This is the bug we are fixing.
        var brokenInlineHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)));

        brokenInlineHash.Should().NotBe(hashViaHasher,
            "C1 regression guard: inline hash over UTF-8 of cookie value must not collide with the canonical hash.");
    }

    [Fact]
    public async Task ExtendSession_WithMalformedBase64Cookie_ReturnsUnauthorized()
    {
        var request = BuildAuthenticatedRequest(HttpMethod.Post, Endpoint, "NOT_VALID_BASE64!@#");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "non-Base64 cookies must be rejected at the hashing layer with 401, never 500.");
    }

    [Fact]
    public async Task ExtendSession_WithEmptyToken_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("Cookie", $"{SessionCookieName}=");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "empty session cookie must short-circuit before hashing.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a fresh user and returns the session token plus its initial expiration.
    /// </summary>
    private async Task<(string SessionToken, DateTime ExpiresAt)> RegisterAndCaptureSessionAsync()
    {
        var registerPayload = new
        {
            Email = $"extend-{Guid.NewGuid():N}@test.local",
            Password = "ValidPassword123!",
            DisplayName = "Session Extend Tester"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerPayload);
        registerResponse.EnsureSuccessStatusCode();

        var sessionToken = ExtractSessionCookieValue(registerResponse)
            ?? throw new InvalidOperationException("Register endpoint did not set meepleai_session cookie.");

        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var expiresAt = body.GetProperty("expiresAt").GetDateTime();

        return (sessionToken, expiresAt);
    }

    private static HttpRequestMessage BuildAuthenticatedRequest(HttpMethod method, string url, string sessionToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionToken}");
        return request;
    }

    /// <summary>
    /// Extracts the meepleai_session cookie value from a Set-Cookie header.
    /// </summary>
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
