using System.Net;

using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>#532: the metrics endpoints are admin-gated and route to the query handlers.</summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicMetricsEndpointsTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminToken = null!;

    public MechanicMetricsEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me532_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Database.MigrateAsync();
            (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, Guid.NewGuid());
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
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Summary_WithAdminSession_Returns200()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/admin/mechanic-analyses/metrics/summary", _adminToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Summary_WithoutSession_IsRejected()
    {
        var response = await _client.GetAsync("/api/v1/admin/mechanic-analyses/metrics/summary");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_WithAdminSession_ReturnsCsv()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/admin/mechanic-analyses/metrics/export", _adminToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }
}
