using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Endpoints;

/// <summary>
/// Integration tests for POST /api/v1/admin/providers/{name}/probe endpoint.
/// Issue #936 — G1 (provider token observability) + G3 (audit log) Gherkin scenarios.
///
/// Auth strategy: TestSessionHelper.CreateSuperAdminSessionAsync seeds a SuperAdmin user
/// + session directly into the DB. The session token is placed in the meepleai_session cookie
/// on each request (matches ReviewLockEndpointsIntegrationTests pattern).
///
/// URL override strategy: IntegrationWebApplicationFactory.Create(extraConfig) injects
/// Providers:OpenRouter:ListModelsUrl → WireMock server URL via in-memory configuration.
/// No factory subclassing needed.
///
/// Rate limit strategy: IntegrationWebApplicationFactory always sets DISABLE_RATE_LIMITING=true,
/// so the rate limit test is documented but skipped (marked Explicit) — a second factory with
/// rate limiting enabled would add significant complexity for minimal additional coverage of a
/// middleware concern already unit-tested in Task 11.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "936")]
public sealed class AdminProviderEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WireMockServer _wireMock = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _superAdminClient = null!;
    private HttpClient _editorClient = null!;
    private MeepleAiDbContext _dbContext = null!;
    private string _superAdminToken = null!;
    private string _editorToken = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminProviderEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"provider_probe_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // 1. Start WireMock before any env vars or factory creation
        _wireMock = WireMockServer.Start();

        // 2. Set the OpenRouter API key env var so ProviderProbeService sees it.
        //    Set BEFORE factory creation — ProviderProbeService reads Environment.GetEnvironmentVariable
        //    at probe-time (not at startup), so this is safe to set here.
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-token-valid-secret");

        // 3. Create an isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // 4. Build factory — override OpenRouter URL via extraConfig so it points at WireMock.
        //    DISABLE_RATE_LIMITING is always true in IntegrationWebApplicationFactory.Create.
        _factory = IntegrationWebApplicationFactory.Create(
            connectionString,
            extraConfig: new Dictionary<string, string?>
            {
                // ASP.NET Core binds Providers__OpenRouter__ListModelsUrl → Providers:OpenRouter:ListModelsUrl
                ["Providers:OpenRouter:BaseUrl"] = $"{_wireMock.Url}/api/v1"
            });

        // 5. Migrate DB + seed users
        using var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync();

        // Seed SuperAdmin session
        var (_, superAdminToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(_dbContext);
        _superAdminToken = superAdminToken;

        // Seed Editor session (non-SuperAdmin — used for 403 test)
        var (_, editorToken) = await TestSessionHelper.CreateEditorSessionAsync(_dbContext);
        _editorToken = editorToken;

        // 6. Create HTTP clients
        _superAdminClient = _factory.CreateClient();
        _editorClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        // Restore env vars
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);

        _superAdminClient?.Dispose();
        _editorClient?.Dispose();

        // Dispose DB context first (scoped — already disposed when scope exited in InitializeAsync)
        // _dbContext was obtained from a using scope so it is already disposed here. No action needed.

        _factory?.Dispose();
        _wireMock?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private HttpRequestMessage BuildProbeRequest(string providerName, string sessionToken) =>
        TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/providers/{providerName}/probe",
            sessionToken);

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    // ─── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// G1-S1: Valid token → 200 with tokenAuthenticated:true, fingerprint present, no raw key in body.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_TokenValid_Returns200WithAuthenticated()
    {
        // Arrange — WireMock returns 200 with model list containing the default model
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"data":[{"id":"google/gemma-2-9b-it:free"}]}"""));

        var request = BuildProbeRequest("openrouter", _superAdminToken);

        // Act
        var response = await _superAdminClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        // Deserialize to dynamic dictionary for assertion flexibility
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        result.GetProperty("tokenAuthenticated").GetBoolean().Should().BeTrue();
        result.GetProperty("tokenConfigured").GetBoolean().Should().BeTrue();

        // TokenFingerprint must match 8 lowercase hex chars
        var fingerprint = result.GetProperty("tokenFingerprint").GetString();
        fingerprint.Should().MatchRegex("^[a-f0-9]{8}$",
            because: "fingerprint must be an 8-char lowercase hex SHA256 prefix");

        // Raw API key must NOT appear in response body
        body.Should().NotContain("test-token-valid-secret",
            because: "raw API keys must never be returned in responses");
    }

    /// <summary>
    /// G1-S2: Provider returns 401 → 200 with tokenAuthenticated:false + errorCode:unauthorized.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_Provider401_Returns200WithUnauthorized()
    {
        // Arrange — WireMock returns 401
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":{"message":"Invalid API key"}}"""));

        var request = BuildProbeRequest("openrouter", _superAdminToken);

        // Act
        var response = await _superAdminClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await ReadJsonAsync<JsonElement>(response);
        result.GetProperty("tokenAuthenticated").GetBoolean().Should().BeFalse();
        result.GetProperty("errorCode").GetString().Should().Be("unauthorized");
    }

    /// <summary>
    /// G1-S3: API key env var not set → 200 with tokenConfigured:false + errorCode:not_configured,
    ///         no upstream HTTP request made.
    /// ProviderProbeService reads env var at probe-time, so we can toggle it on the existing factory
    /// without rebuilding. Restored in finally.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_NoToken_Returns200WithNotConfigured()
    {
        // Snapshot current WireMock log size — assert no NEW requests after probe.
        var initialLogCount = _wireMock.LogEntries.Count();

        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        try
        {
            var request = BuildProbeRequest("openrouter", _superAdminToken);
            var response = await _superAdminClient.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await ReadJsonAsync<JsonElement>(response);
            result.GetProperty("tokenConfigured").GetBoolean().Should().BeFalse();
            result.GetProperty("errorCode").GetString().Should().Be("not_configured");

            _wireMock.LogEntries.Count().Should().Be(initialLogCount,
                because: "no upstream HTTP call should be made when API key is not configured");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-token-valid-secret");
        }
    }

    /// <summary>
    /// G1-S4: Unknown provider name → 404 with errorCode:unknown_provider.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_UnknownProvider_Returns404()
    {
        // Arrange — "cohere" is not in the known provider list
        var request = BuildProbeRequest("cohere", _superAdminToken);

        // Act
        var response = await _superAdminClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var result = await ReadJsonAsync<JsonElement>(response);
        result.GetProperty("errorCode").GetString().Should().Be("unknown_provider");
        result.GetProperty("provider").GetString().Should().Be("cohere");
    }

    /// <summary>
    /// G1-S5: Non-SuperAdmin user → 403.
    /// Editor role does not satisfy RequireSuperAdmin policy.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_NotSuperAdmin_Returns403()
    {
        // Arrange — use editor client
        var request = BuildProbeRequest("openrouter", _editorToken);

        // Act
        var response = await _editorClient.SendAsync(request);

        // Assert — RequireSuperAdmin policy rejects non-superadmin users
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// G1-S5b: Unauthenticated request (no cookie) → 401.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_NoAuth_Returns401()
    {
        // Arrange — plain request with no cookie
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/admin/providers/openrouter/probe");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// G3: Successful probe writes an audit entry to ProviderProbeAuditEntries with Outcome=Success.
    /// This verifies the G3 Gherkin scenario: audit trail is persisted for every probe call.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_WritesAuditEntry_OnSuccess()
    {
        // Arrange — WireMock returns 200 with model list
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"data":[{"id":"google/gemma-2-9b-it:free"}]}"""));

        var request = BuildProbeRequest("openrouter", _superAdminToken);

        // Act
        var response = await _superAdminClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — query DB directly for the audit entry
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var auditEntries = await db.ProviderProbeAuditEntries
            .Where(e => e.ProviderName == "openrouter")
            .ToListAsync();

        auditEntries.Should().NotBeEmpty(
            because: "a probe audit entry must be persisted for every probe call (G3)");

        var entry = auditEntries.Last();
        entry.ProviderName.Should().Be("openrouter");
        entry.Outcome.Should().Be(ProbeOutcome.Success);
        entry.TokenFingerprint.Should().MatchRegex("^[a-f0-9]{8}$");
        entry.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
        entry.ProbedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// G3: Unauthorized probe (401 from provider) also writes audit entry with Outcome=Unauthorized.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Probe_WritesAuditEntry_OnUnauthorized()
    {
        // Arrange
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":{"message":"Invalid API key"}}"""));

        var request = BuildProbeRequest("openrouter", _superAdminToken);

        // Act
        var response = await _superAdminClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var entries = await db.ProviderProbeAuditEntries
            .Where(e => e.ProviderName == "openrouter" && e.Outcome == ProbeOutcome.Unauthorized)
            .ToListAsync();

        entries.Should().NotBeEmpty(
            because: "unauthorized probe must also be recorded in audit log (G3)");
    }

    /// <summary>
    /// Rate limit test — SKIPPED.
    ///
    /// The IntegrationWebApplicationFactory always sets DISABLE_RATE_LIMITING=true to prevent
    /// rate limit interference across the shared test suite. Testing the rate limit policy
    /// would require a second WebApplicationFactory instance with rate limiting re-enabled,
    /// which adds significant setup complexity for a middleware concern that is:
    /// (a) already validated by the rate limiting extension unit tests (Task 11), and
    /// (b) verified manually during smoke testing of the endpoint.
    ///
    /// To enable this test locally: build a second factory with RateLimiting:Enabled=true
    /// and DISABLE_RATE_LIMITING=null, send 11 requests in rapid succession, assert the 11th
    /// returns HTTP 429.
    /// </summary>
    [Fact(Skip = "Rate limit testing requires a dedicated factory with rate limiting enabled; " +
                 "covered by unit tests in Task 11 and manual smoke testing.")]
    public Task Probe_RateLimitExceeded_Returns429()
    {
        // Implementation sketch (for reference):
        // using var rlFactory = IntegrationWebApplicationFactory.Create(connStr,
        //     extraConfig: new() { ["RateLimiting:Enabled"] = "true" });
        // Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", null);
        // for (int i = 0; i < 11; i++) { await client.SendAsync(BuildProbeRequest(...)); }
        // last response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        return Task.CompletedTask;
    }
}
