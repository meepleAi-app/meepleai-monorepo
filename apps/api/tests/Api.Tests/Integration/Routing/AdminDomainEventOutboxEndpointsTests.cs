using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// Issue #1535 T6 — HTTP integration tests for the admin
/// <c>/api/v1/admin/event-outbox</c> surface. Covers auth enforcement, the
/// three GET endpoints and the POST retry endpoint (404 + 409 + 204 paths).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class AdminDomainEventOutboxEndpointsTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/event-outbox";
    private const string FakeEventAlias = "test.fake.event";

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

    public AdminDomainEventOutboxEndpointsTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"event_outbox_endpoints_{Guid.NewGuid():N}";
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

    // ─────────────────────────────────────────────────────────────────────────
    // Auth enforcement (all four endpoints)
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "/stats")]
    [InlineData("GET", "/failed")]
    [InlineData("GET", "/pending")]
    [InlineData("POST", "/00000000-0000-0000-0000-000000000001/retry")]
    public async Task Anonymous_Returns401(string method, string relative)
    {
        var url = $"{EndpointBase}{relative}";
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /stats — aggregate snapshot
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stats_EmptyOutbox_Returns200_Zeroes()
    {
        var response = await SendAsync(HttpMethod.Get, "/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await DeserializeAsync<JsonElement>(response);
        dto.GetProperty("pendingCount").GetInt64().Should().Be(0);
        dto.GetProperty("failedCount").GetInt64().Should().Be(0);
        dto.GetProperty("sentLast24h").GetInt64().Should().Be(0);
        dto.GetProperty("oldestPendingAgeSeconds").GetDouble().Should().Be(0);
    }

    [Fact]
    public async Task Stats_MixedRows_Returns200_AccurateCounts()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedRowAsync(DomainEventOutboxStatus.Pending, enqueuedAt: now.AddSeconds(-30));
        await SeedRowAsync(DomainEventOutboxStatus.Pending, enqueuedAt: now.AddSeconds(-10));
        await SeedRowAsync(DomainEventOutboxStatus.Failed);
        await SeedRowAsync(DomainEventOutboxStatus.Sent, dispatchedAt: now.AddHours(-1));
        await SeedRowAsync(DomainEventOutboxStatus.Sent, dispatchedAt: now.AddHours(-48));

        var response = await SendAsync(HttpMethod.Get, "/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await DeserializeAsync<JsonElement>(response);
        dto.GetProperty("pendingCount").GetInt64().Should().Be(2);
        dto.GetProperty("failedCount").GetInt64().Should().Be(1);
        dto.GetProperty("sentLast24h").GetInt64().Should().Be(1,
            because: "the 48h-old Sent row falls outside the rolling 24h window");
        dto.GetProperty("oldestPendingAgeSeconds").GetDouble().Should().BeGreaterThan(25,
            because: "the older Pending row was seeded ~30s before the request");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /failed
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Failed_DefaultLimit_Returns200_MostRecentFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await SeedRowAsync(DomainEventOutboxStatus.Failed, enqueuedAt: now.AddMinutes(-5));
        await SeedRowAsync(DomainEventOutboxStatus.Failed, enqueuedAt: now.AddMinutes(-1));
        await SeedRowAsync(DomainEventOutboxStatus.Pending);

        var response = await SendAsync(HttpMethod.Get, "/failed");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await DeserializeAsync<List<JsonElement>>(response);
        rows!.Should().HaveCount(2, because: "only Failed rows are returned");
        // First row is the more recently enqueued one.
        rows[0].GetProperty("enqueuedAt").GetDateTimeOffset()
            .Should().BeOnOrAfter(rows[1].GetProperty("enqueuedAt").GetDateTimeOffset());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /pending
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pending_DefaultLimit_Returns200_FIFOOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var oldest = await SeedRowAsync(DomainEventOutboxStatus.Pending, enqueuedAt: now.AddSeconds(-30));
        var newest = await SeedRowAsync(DomainEventOutboxStatus.Pending, enqueuedAt: now.AddSeconds(-5));
        await SeedRowAsync(DomainEventOutboxStatus.Failed);

        var response = await SendAsync(HttpMethod.Get, "/pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await DeserializeAsync<List<JsonElement>>(response);
        rows!.Should().HaveCount(2, because: "only Pending rows are returned");
        rows[0].GetProperty("id").GetGuid().Should().Be(oldest,
            because: "the queue head appears first (FIFO by EnqueuedAt ASC)");
        rows[1].GetProperty("id").GetGuid().Should().Be(newest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /{id}/retry — re-arm Failed row
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Retry_UnknownId_Returns404()
    {
        var randomId = Guid.NewGuid();
        var response = await SendAsync(HttpMethod.Post, $"/{randomId}/retry");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Retry_FailedRow_Returns204_AndTransitionsToPending()
    {
        var id = await SeedRowAsync(DomainEventOutboxStatus.Failed);

        var response = await SendAsync(HttpMethod.Post, $"/{id}/retry");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify state side-effect via a fresh scope (the EF cache from the seed scope
        // is not reused).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var row = await db.DomainEventOutbox.AsNoTracking().SingleAsync(r => r.Id == id);
        row.Status.Should().Be(DomainEventOutboxStatus.Pending);
        row.Attempts.Should().Be(0);
        row.LastError.Should().BeNull();
        row.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task Retry_PendingRow_Returns409()
    {
        // A Pending row cannot be re-armed — entity guard fires → endpoint returns 409.
        var id = await SeedRowAsync(DomainEventOutboxStatus.Pending);

        var response = await SendAsync(HttpMethod.Post, $"/{id}/retry");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

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
    /// Persists a <see cref="DomainEventOutboxEntity"/> row in the requested state.
    /// Bypasses MeepleAiDbContext.SaveChangesAsync's outbox routing (PeekEvents() is
    /// empty for the test scope, so step 2b is a no-op) and inserts the row directly.
    /// </summary>
    private async Task<Guid> SeedRowAsync(
        DomainEventOutboxStatus targetStatus,
        DateTimeOffset? enqueuedAt = null,
        DateTimeOffset? dispatchedAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var seedNow = enqueuedAt ?? DateTimeOffset.UtcNow;
        var fakeEvent = new SeedFakeEvent();
        var row = DomainEventOutboxEntity.Enqueue(
            fakeEvent,
            FakeEventAlias,
            payloadJson: """{"marker":"seed"}""",
            payloadVersion: 1,
            correlationId: null,
            now: seedNow);

        // Drive the row into the requested terminal state via the public transitions.
        switch (targetStatus)
        {
            case DomainEventOutboxStatus.Pending:
                // Already Pending after Enqueue.
                break;
            case DomainEventOutboxStatus.Sent:
                row.MarkSent(dispatchedAt ?? DateTimeOffset.UtcNow);
                break;
            case DomainEventOutboxStatus.Failed:
                row.MarkFailed("seeded terminal failure", DateTimeOffset.UtcNow);
                break;
        }

        db.DomainEventOutbox.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row.Id;
    }

    /// <summary>
    /// Minimal <see cref="IDomainEvent"/> stub used only as a factory argument to
    /// <see cref="DomainEventOutboxEntity.Enqueue"/> — the test never dispatches it.
    /// </summary>
    private sealed class SeedFakeEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
