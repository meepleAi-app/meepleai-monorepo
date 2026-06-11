using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// Issue #1823 Wave 3 M12 — HTTP integration smoke tests for the admin
/// <c>/api/v1/admin/wikidata/enrichment</c> surface. Covers the auth filter
/// chain end-to-end. The full happy-path + force-refresh + outcome propagation
/// matrix is deferred to the M13 admin dead-letter page PR (bundled with the
/// admin UI E2E sweep).
/// </summary>
/// <remarks>
/// Lisa Crispin guard against deferral creep (spec-panel sess.2026-06-11):
/// without this smoke test, the auth filter wiring + route constraint + body
/// deserialization (null → ForceRefresh=false) would have no automated guard
/// in CI until M13 ships. The single 401 anonymous probe verifies the filter
/// chain is wired and the route is reachable.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public sealed class AdminWikidataCoverEnrichmentEndpointsTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/wikidata/enrichment";
    private static readonly Guid TestGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public AdminWikidataCoverEnrichmentEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"wikidata_enrichment_admin_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Trigger_AnonymousRequest_Returns401()
    {
        // Hits the endpoint with no session cookie / auth header. The group-level
        // RequireAdminSessionFilter must short-circuit BEFORE the handler runs.
        var url = $"{EndpointBase}/{TestGameId}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { forceRefresh = false }),
        };

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the admin endpoint filter chain must reject anonymous probes BEFORE the MediatR pipeline executes");
    }

    [Fact]
    public async Task Trigger_AnonymousRequest_NoBody_Returns401()
    {
        // Confirms the auth filter fires regardless of body shape — null body
        // (treated as ForceRefresh=false at handler level) must still be 401
        // for an anonymous caller.
        var url = $"{EndpointBase}/{TestGameId}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListDeadLetters_AnonymousRequest_Returns401()
    {
        // Wave 3 M13 GET endpoint must also be gated by the group-level
        // RequireAdminSessionFilter, no different from POST.
        var url = $"{EndpointBase}/dead-letters?skip=0&take=10";

        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the dead-letter admin page contains sensitive operational data and MUST require an admin session");
    }
}
