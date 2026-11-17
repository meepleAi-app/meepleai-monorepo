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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Endpoints;

/// <summary>
/// La fixture di <see cref="AdminProviderQuotaEndpointIntegrationTests"/>: host per classe, database
/// per test, con il resolver delle credenziali sostituito.
/// </summary>
/// <remarks>
/// #5039: la classe costruiva un host per test, dentro il <c>Timeout</c> del test. Il doppio qui vive
/// quanto la classe, e può farlo perché non ha stato: risponde sempre «non configurato» e nessun test
/// conta le sue chiamate.
/// </remarks>
public sealed class AdminProviderQuotaHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "provider_quota_all")
{
    // Stub the credential resolver to "not configured" so the aggregate returns not_configured
    // entries deterministically, with no upstream call and WITHOUT touching global env vars.
    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var resolver = new Mock<IProviderCredentialResolver>();
            resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProviderCredentialNotConfiguredException("test"));
            services.AddScoped<IProviderCredentialResolver>(_ => resolver.Object);
        }));
}

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
[Trait("Issue", "5039")]
public sealed class AdminProviderQuotaEndpointIntegrationTests
    : IClassFixture<AdminProviderQuotaHostFixture>, IAsyncLifetime
{
    private readonly AdminProviderQuotaHostFixture _hostFixture;
    private HttpClient _adminClient = null!;
    private HttpClient _editorClient = null!;
    private string _adminToken = null!;
    private string _editorToken = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AdminProviderQuotaEndpointIntegrationTests(AdminProviderQuotaHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // BeginTestAsync prima di seminare: le sessioni vanno nel database del test.
        await _hostFixture.BeginTestAsync();
        var factory = _hostFixture.Factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (_, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db);
        _adminToken = adminToken;
        var (_, editorToken) = await TestSessionHelper.CreateEditorSessionAsync(db);
        _editorToken = editorToken;

        _adminClient = factory.CreateClient();
        _editorClient = factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _adminClient?.Dispose();
        _editorClient?.Dispose();
        return ValueTask.CompletedTask;
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
        using var client = _hostFixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/providers/quota");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
