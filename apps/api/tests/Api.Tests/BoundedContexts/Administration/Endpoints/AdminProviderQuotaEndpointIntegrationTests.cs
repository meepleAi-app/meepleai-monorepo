using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Endpoints;

/// <summary>
/// Integration tests for GET /api/v1/admin/providers/quota — Issue #3043 (resolver migration #3044).
///
/// Determinism + isolation: an <see cref="IProviderCredentialResolver"/> stub that throws
/// <see cref="ProviderCredentialNotConfiguredException"/> is injected via ConfigureTestServices, so
/// each entry returns not_configured (200, no upstream HTTP call). This deliberately avoids mutating
/// the process-global OPENROUTER_API_KEY / DEEPSEEK_API_KEY env vars — other integration collections
/// (e.g. the provider-probe tests) depend on those, and cross-collection env mutation is racy.
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
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Stub the credential resolver to "not configured" so the aggregate returns not_configured
        // entries deterministically, with no upstream call and WITHOUT touching global env vars.
        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                var resolver = new Mock<IProviderCredentialResolver>();
                resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new ProviderCredentialNotConfiguredException("test"));
                services.AddScoped<IProviderCredentialResolver>(_ => resolver.Object);
            }));

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

        // Resolver stubbed to not_configured → every entry quota-supported but not configured.
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
