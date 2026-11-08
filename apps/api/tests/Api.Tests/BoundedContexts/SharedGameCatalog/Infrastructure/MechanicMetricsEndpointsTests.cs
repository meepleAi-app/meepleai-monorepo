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

/// <summary>
/// La fixture di <see cref="MechanicMetricsEndpointsTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class MechanicMetricsEndpointsTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "me532_endpoints");

/// <summary>#532: the metrics endpoints are admin-gated and route to the query handlers.</summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicMetricsEndpointsTests
    : IClassFixture<MechanicMetricsEndpointsTestsHostFixture>, IAsyncLifetime
{
    private readonly MechanicMetricsEndpointsTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminToken = null!;

    public MechanicMetricsEndpointsTests(MechanicMetricsEndpointsTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, Guid.NewGuid());
        }
        _client = _factory.CreateClient();
    }

    // Host e database appartengono alla fixture; il client e' di questa istanza.
    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
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
