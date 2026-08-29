using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// The one probe scenario that needs rate limiting ON. See the note on
/// <see cref="AdminProviderProbeNotConfiguredIntegrationTests"/> for why it is its own class.
///
/// Issue #3887: enableRateLimiting:true is a per-host configuration flag. This test used to blank
/// DISABLE_RATE_LIMITING in the process environment as well, which turned rate limiting on for
/// every host built meanwhile by a parallel xUnit collection — an unrelated test then failed
/// with 429.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "936")]
[Trait("Issue", "3887")]
public sealed class AdminProviderProbeRateLimitIntegrationTests : IAsyncLifetime
{
    private const string OpenRouterApiKeyName = "OPENROUTER_API_KEY";
    private const string OpenRouterApiKeyValue = "test-token-valid-secret";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WireMockServer _wireMock = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _superAdminToken = null!;

    public AdminProviderProbeRateLimitIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"probe_ratelimit_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // #3887: xUnit does not guarantee DisposeAsync when InitializeAsync throws, and the
        // Timeout covers this method — so a migration failure or a fired deadline would leak the
        // isolated database and the WireMock port on the shared Testcontainers instance, one per
        // failed run. The extracted test used to create its database inside a try/finally; this
        // restores that guarantee.
        try
        {
            await InitializeCoreAsync();
        }
        catch
        {
            await SafeCleanupAsync();
            throw;
        }
    }

    private async Task InitializeCoreAsync()
    {
        _wireMock = WireMockServer.Start();
        _wireMock
            .Given(Request.Create().WithPath("/api/v1/models").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"data":[]}"""));

        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(
            connectionString,
            extraConfig: new Dictionary<string, string?>
            {
                ["Providers:OpenRouter:BaseUrl"] = $"{_wireMock.Url}/api/v1",
                [OpenRouterApiKeyName] = OpenRouterApiKeyValue
            },
            enableRateLimiting: true);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_, _superAdminToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(db);

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync() => await SafeCleanupAsync();

    private async Task SafeCleanupAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _wireMock?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    /// <summary>
    /// G3: Rate limit policy AdminProviderProbe (10 req/min per user) returns 429 on the 11th call.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task Probe_RateLimitExceeded_Returns429()
    {
        int allowedCount = 0;
        HttpStatusCode? lastStatus = null;
        for (int i = 0; i < 11; i++)
        {
            var request = TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post,
                "/api/v1/admin/providers/openrouter/probe",
                _superAdminToken);
            var response = await _client.SendAsync(request);
            lastStatus = response.StatusCode;
            if (response.StatusCode == HttpStatusCode.OK) allowedCount++;
            else break;
        }

        allowedCount.Should().BeLessThanOrEqualTo(10);
        lastStatus.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
