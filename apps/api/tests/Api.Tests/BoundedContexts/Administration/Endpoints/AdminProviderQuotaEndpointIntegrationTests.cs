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
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Endpoints;

/// <summary>
/// Integration tests for GET /api/v1/admin/providers/quota — Issue #3043.
/// Aggregated quota across the quota-capable providers (SupportedProviderNames).
///
/// Determinism: the API keys are cleared in InitializeAsync so each entry returns
/// tokenConfigured:false with a 200 and NO upstream HTTP call — a deterministic array
/// of the registered quota providers (openrouter, deepseek), no WireMock needed.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3043")]
public sealed class AdminProviderQuotaEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _adminClient = null!;
    private HttpClient _editorClient = null!;
    private MeepleAiDbContext _dbContext = null!;
    private string _adminToken = null!;
    private string _editorToken = null!;
    private string? _prevOpenRouterKey;
    private string? _prevDeepSeekKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminProviderQuotaEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"provider_quota_all_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Clear provider API keys → each entry returns not_configured (200, no upstream call).
        _prevOpenRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        _prevDeepSeekKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", null);

        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync();

        var (_, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(_dbContext);
        _adminToken = adminToken;
        var (_, editorToken) = await TestSessionHelper.CreateEditorSessionAsync(_dbContext);
        _editorToken = editorToken;

        _adminClient = _factory.CreateClient();
        _editorClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", _prevOpenRouterKey);
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", _prevDeepSeekKey);
        _adminClient?.Dispose();
        _editorClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    // GET /api/v1/admin/providers/quota — admin → 200 with one entry per registered quota provider.
    // Count derives from SupportedProviderNames (registered IProviderQuotaProvider), NOT KNOWN_PROVIDERS.
    [Fact(Timeout = 90_000)]
    public async Task GetAllQuotas_Admin_Returns200WithSupportedProviders()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/admin/providers/quota", _adminToken);

        var response = await _adminClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var arr = await ReadJsonAsync<JsonElement>(response);
        arr.ValueKind.Should().Be(JsonValueKind.Array);

        var names = arr.EnumerateArray()
            .Select(e => e.GetProperty("providerName").GetString())
            .ToList();
        names.Should().BeEquivalentTo(new[] { "openrouter", "deepseek" });

        // Keys cleared → every entry is quota-supported but not configured (no upstream call).
        foreach (var e in arr.EnumerateArray())
        {
            e.GetProperty("quotaSupported").GetBoolean().Should().BeTrue();
            e.GetProperty("tokenConfigured").GetBoolean().Should().BeFalse();
        }
    }

    // Editor → 403 (RequireAdminOrAbove = SuperAdmin|Admin).
    [Fact(Timeout = 90_000)]
    public async Task GetAllQuotas_Editor_Returns403()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/admin/providers/quota", _editorToken);

        var response = await _editorClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Unauthenticated → 401.
    [Fact(Timeout = 90_000)]
    public async Task GetAllQuotas_NoAuth_Returns401()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/providers/quota");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
