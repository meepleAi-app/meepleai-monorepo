using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Endpoints;

/// <summary>
/// Integration tests for the catalog-sync admin endpoints (#1861 Phase 4):
/// GET /status, GET /runs, GET /runs/{id}/logs, POST /trigger.
/// Asserts auth enforcement, happy-path 200/202, 404 / 409 mappings.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AdminCatalogSyncEndpointsTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/catalog-ingestion";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;

    private static readonly Guid TestAdminId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AdminCatalogSyncEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"catalog_sync_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

            var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(dbContext, TestAdminId);
            _adminSessionToken = token;
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ────────────────────────────────────────────────────────────────────
    // Auth enforcement
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "/status")]
    [InlineData("GET", "/runs")]
    [InlineData("GET", "/runs/00000000-0000-0000-0000-000000000001/logs")]
    [InlineData("POST", "/trigger")]
    public async Task Anonymous_Returns401(string method, string relative)
    {
        var url = $"{EndpointBase}{relative}";
        var request = method == "POST"
            ? new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { provider = CatalogSyncProvider.BggApi }) }
            : new HttpRequestMessage(new HttpMethod(method), url);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /status
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Status_EmptyDb_Returns200_NeverRun()
    {
        var response = await SendAsync(HttpMethod.Get, "/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<CatalogSyncStatusResult>(response);
        result.Should().NotBeNull();
        result!.Status.Should().Be("never_run");
        result.CurrentRun.Should().BeNull();
        result.LastRun.Should().BeNull();
    }

    [Fact]
    public async Task Status_RunningExists_Returns200_Running()
    {
        await SeedRunAsync(CatalogSyncStatus.Running);

        var response = await SendAsync(HttpMethod.Get, "/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<CatalogSyncStatusResult>(response);
        result!.Status.Should().Be("running");
        result.CurrentRun.Should().NotBeNull();
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /runs
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Runs_DefaultPaging_Returns200WithItems()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedRunAsync(CatalogSyncStatus.Success, $"run-{i}");
            await Task.Delay(5);
        }

        var response = await SendAsync(HttpMethod.Get, "/runs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<PagedCatalogSyncRunsResult>(response);
        result!.Items.Should().HaveCount(5);
        result.Total.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(12);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Runs_ExplicitPaging_RespectsQueryString()
    {
        for (var i = 0; i < 15; i++)
        {
            await SeedRunAsync(CatalogSyncStatus.Success, $"r-{i}");
            await Task.Delay(5);
        }

        var response = await SendAsync(HttpMethod.Get, "/runs?page=2&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<PagedCatalogSyncRunsResult>(response);
        result!.Items.Should().HaveCount(5);
        result.Total.Should().Be(15);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.HasMore.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /runs/{id}/logs
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logs_RunNotFound_Returns404()
    {
        var randomId = Guid.NewGuid();
        var response = await SendAsync(HttpMethod.Get, $"/runs/{randomId}/logs");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logs_RunWithoutLogFile_Returns200_LogsUnavailable()
    {
        var runId = await SeedRunAsync(CatalogSyncStatus.Success);

        var response = await SendAsync(HttpMethod.Get, $"/runs/{runId}/logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeAsync<CatalogSyncRunLogsResult>(response);
        result!.RunId.Should().Be(runId);
        result.LogsAvailable.Should().BeFalse();
        result.Logs.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /trigger
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Trigger_NoRunningRun_Returns202_WithRunId()
    {
        var response = await SendAsync(HttpMethod.Post, "/trigger", new { provider = CatalogSyncProvider.BggApi });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var result = await DeserializeAsync<JsonElement>(response);
        result.GetProperty("runId").GetGuid().Should().NotBe(Guid.Empty);
        result.GetProperty("status").GetString().Should().Be("queued");
    }

    [Fact]
    public async Task Trigger_RunAlreadyRunning_Returns409()
    {
        await SeedRunAsync(CatalogSyncStatus.Running);

        var response = await SendAsync(HttpMethod.Post, "/trigger", new { provider = CatalogSyncProvider.BggApi });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string relative, object? body = null)
    {
        var request = body is null
            ? TestSessionHelper.CreateAuthenticatedRequest(method, $"{EndpointBase}{relative}", _adminSessionToken)
            : TestSessionHelper.CreateAuthenticatedRequest(method, $"{EndpointBase}{relative}", _adminSessionToken, body);
        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    /// <summary>
    /// Persists a CatalogSyncRun in the requested terminal/running state. Title optional.
    /// </summary>
    private async Task<Guid> SeedRunAsync(CatalogSyncStatus targetStatus, string title = "seeded run")
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, title, null);

        switch (targetStatus)
        {
            case CatalogSyncStatus.Queued:
                break;
            case CatalogSyncStatus.Running:
                run.MarkRunning();
                break;
            case CatalogSyncStatus.Success:
                run.MarkRunning();
                run.Complete();
                break;
            case CatalogSyncStatus.Failed:
                run.MarkRunning();
                run.Fail("TEST_ERR", "seeded failure");
                break;
            case CatalogSyncStatus.TimedOut:
                run.MarkRunning();
                run.TimeOut("seeded timeout");
                break;
        }

        await repo.AddAsync(run);
        await dbContext.SaveChangesAsync();
        return run.Id;
    }
}
