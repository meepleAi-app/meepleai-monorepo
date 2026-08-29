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
/// Two probe scenarios that need a host configured differently from the one in
/// <see cref="AdminProviderEndpointsIntegrationTests"/>: one without the provider API key, one with
/// rate limiting on.
///
/// Issue #3887 — why these are separate classes rather than extra factories built inside a test.
/// Both settings are per-host configuration (the process environment is no longer touched), and
/// xUnit builds a fresh class instance — hence a fresh host, database and migration — for every
/// test method. A test that then builds a *second* host in its own body pays for two migrations,
/// and the Timeout attribute covers InitializeAsync too: measured, that took the rate-limit test
/// from 48s to over 90s under container contention, i.e. straight back into the "outcome depends
/// on when it runs" defect this issue is about. One host per class keeps every test at one
/// migration.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "936")]
[Trait("Issue", "3887")]
public sealed class AdminProviderProbeNotConfiguredIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WireMockServer _wireMock = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _superAdminToken = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminProviderProbeNotConfiguredIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"probe_notoken_{Guid.NewGuid():N}";
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
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Deliberately no OPENROUTER_API_KEY entry: "not configured" is a property of THIS host,
        // not a process-wide variable blanked for the duration of one test (#3887).
        _factory = IntegrationWebApplicationFactory.Create(
            connectionString,
            extraConfig: new Dictionary<string, string?>
            {
                ["Providers:OpenRouter:BaseUrl"] = $"{_wireMock.Url}/api/v1"
            });

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
    /// G1-S3: API key not configured returns 200 with tokenConfigured:false +
    /// errorCode:not_configured, and no upstream HTTP request made.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task Probe_NoToken_Returns200WithNotConfigured()
    {
        var initialLogCount = _wireMock.LogEntries.Count();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/admin/providers/openrouter/probe",
            _superAdminToken);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.GetProperty("tokenConfigured").GetBoolean().Should().BeFalse();
        result.GetProperty("errorCode").GetString().Should().Be("not_configured");

        _wireMock.LogEntries.Count().Should().Be(initialLogCount,
            because: "no upstream HTTP call should be made when API key is not configured");
    }
}
