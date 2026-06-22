using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// HTTP-layer integration tests for GET /api/v1/admin/kb/nav-counts.
/// Verifies 401 (anonymous), 403 (editor), and 200 (superadmin) scenarios.
/// Issue #1655 — F3-FU-6 KbSubNav count badges.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class AdminKbNavCountsEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _anonClient = null!;
    private HttpClient _editorClient = null!;
    private HttpClient _adminClient = null!;
    private string _editorToken = null!;
    private string _adminToken = null!;

    public AdminKbNavCountsEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"kbnav_endpoint_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        (_, _adminToken) = await TestSessionHelper.CreateSuperAdminSessionAsync(db);
        (_, _editorToken) = await TestSessionHelper.CreateEditorSessionAsync(db);

        _anonClient = _factory.CreateClient();
        _editorClient = _factory.CreateClient();
        _adminClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _anonClient?.Dispose();
        _editorClient?.Dispose();
        _adminClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
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
