using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// La fixture di <see cref="AdminKbNavCountsEndpointIntegrationTests"/>: host per classe, database
/// per test.
/// </summary>
/// <remarks>
/// #5039: stessa forma di <c>ChatSessionEndpointsIdorIntegrationTests</c>, un host per test dentro il
/// <c>Timeout</c> del test. Database per test e non per classe perché i conteggi sono globali: oggi
/// l'asserzione è solo <c>&gt;= 0</c>, ma un'asserzione su un valore esatto non deve dipendere da cosa
/// hanno seminato gli altri test.
/// </remarks>
public sealed class AdminKbNavCountsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "kbnav_endpoint");

/// <summary>
/// HTTP-layer integration tests for GET /api/v1/admin/kb/nav-counts.
/// Verifies 401 (anonymous), 403 (editor), and 200 (superadmin) scenarios.
/// Issue #1655 — F3-FU-6 KbSubNav count badges.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
[Trait("Issue", "5039")]
public sealed class AdminKbNavCountsEndpointIntegrationTests
    : IClassFixture<AdminKbNavCountsHostFixture>, IAsyncLifetime
{
    private readonly AdminKbNavCountsHostFixture _hostFixture;
    private HttpClient _anonClient = null!;
    private HttpClient _editorClient = null!;
    private HttpClient _adminClient = null!;
    private string _editorToken = null!;
    private string _adminToken = null!;

    public AdminKbNavCountsEndpointIntegrationTests(AdminKbNavCountsHostFixture hostFixture)
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

        (_, _adminToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(db);
        (_, _editorToken) = await TestSessionHelper.CreateEditorSessionAsync(db);

        _anonClient = factory.CreateClient();
        _editorClient = factory.CreateClient();
        _adminClient = factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _anonClient?.Dispose();
        _editorClient?.Dispose();
        _adminClient?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static HttpRequestMessage NewRequest(string token) =>
        TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/kb/nav-counts",
            token);

    [Fact(Timeout = 90_000)]
    public async Task Get_WithoutSession_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/v1/admin/kb/nav-counts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Timeout = 90_000)]
    public async Task Get_WithEditorSession_Returns403()
    {
        var response = await _editorClient.SendAsync(NewRequest(_editorToken));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Timeout = 90_000)]
    public async Task Get_WithAdminSession_Returns200WithDto()
    {
        var response = await _adminClient.SendAsync(NewRequest(_adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<KbNavCountsDto>();

        dto.Should().NotBeNull();
        dto!.ProcessingQueue.Should().BeGreaterThanOrEqualTo(0);
        dto.Feedback7d.Should().BeGreaterThanOrEqualTo(0);
        dto.AsOf.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
