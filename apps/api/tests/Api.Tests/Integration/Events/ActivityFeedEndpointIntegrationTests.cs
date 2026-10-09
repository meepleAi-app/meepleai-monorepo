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

namespace Api.Tests.Integration.Events;

/// <summary>
/// Integration tests for <c>GET /api/v1/activity</c> — cross-entity activity feed.
/// BE-3 #1590 Task 8: verifies the endpoint reads <c>domain_event_logs</c> scoped
/// to the calling user, applies 90-day retention and limit, maps EventType to clean
/// entityType names, and enforces authentication + validation.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class ActivityFeedEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _webFactory = null!;

    public ActivityFeedEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"activity_feed_endpoint_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _webFactory = IntegrationWebApplicationFactory.Create(connectionString);
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_webFactory is not null)
        {
            await _webFactory.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_testDbName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_testDbName);
        }
    }

    // ── Test 1 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetActivity_returns_cross_entity_items_for_caller()
    {
        // Arrange — create user + seed game + POST a chat session (emits chat.session.created).
        //
        // Issue #4138: this used POST /api/v1/agents/quick-create to emit `agent.created`.
        // That route and that event are retired, but the SUBJECT of this test is the activity
        // feed, not agents — so it needs a surviving producer, not a deletion.
        // `chat.session.created` is the closest: same shape through domain_event_logs, and the
        // feed maps it to EntityType "ChatSession" the same way it mapped "Agent".
        using var setupScope = _webFactory.Services.CreateScope();
        var db = setupScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, title: "TestGame-Activity-T1");

        var client = _webFactory.CreateClient();

        // Create a chat session → emits chat.session.created
        var createSessionRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/chat/sessions",
            token,
            new { gameId, title = "Activity probe" });

        var createResponse = await client.SendAsync(createSessionRequest);
        createResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        // Act — GET /api/v1/activity?limit=20
        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/activity?limit=20",
            token);
        var response = await client.SendAsync(getRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ActivityFeedResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Items.Should().NotBeNull();
        body.Count.Should().Be(body.Items.Count);

        // Happy-path: the event MUST surface with the full mapped shape.
        var chatItems = body.Items
            .Where(i => string.Equals(i.EventType, "chat.session.created", StringComparison.Ordinal))
            .ToList();

        chatItems.Should().ContainSingle("one POST /chat/sessions emits exactly one event");
        var item = chatItems[0];
        item.EventType.Should().Be("chat.session.created");
        item.EntityType.Should().Be("ChatSession"); // mapped from eventType, not raw AggregateType
        item.EntityId.Should().NotBe(Guid.Empty);
        item.Title.Should().NotBeNullOrEmpty(); // AgentName snapshot in payload
        item.PayloadVersion.Should().Be(1);
    }

    // ── Test 2 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetActivity_unauthenticated_returns_401()
    {
        // Act — no cookie header
        var client = _webFactory.CreateClient();
        var response = await client.GetAsync("/api/v1/activity");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Test 3 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetActivity_invalid_limit_returns_422()
    {
        // Arrange
        using var scope = _webFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);

        var client = _webFactory.CreateClient();

        // Act — limit=999 exceeds max (100)
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/activity?limit=999",
            token);
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); // 422
    }

    // ── Test 4 ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetActivity_only_caller_events_not_other_users()
    {
        // Arrange — two independent users
        using var scope = _webFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userAId, tokenA) = await TestSessionHelper.CreateUserSessionAsync(db);
        var (userBId, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, title: "TestGame-Activity-T4");

        var client = _webFactory.CreateClient();

        // User A creates a chat session (seeds an event attributed to userA).
        // Issue #4138: was quick-create; see the note on test 1.
        var createReqA = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/chat/sessions",
            tokenA,
            new { gameId, title = "Isolation probe" });
        await client.SendAsync(createReqA);

        // Act — User B queries the activity feed
        var getReqB = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/activity?limit=50",
            tokenB);
        var response = await client.SendAsync(getReqB);

        // Assert — B's feed must not contain A's events
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ActivityFeedResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        body.Should().NotBeNull();

        // None of the items in B's feed should have userId == userA
        body!.Items.Should().NotContain(
            i => i.UserId == userAId,
            "User B should not see events belonging to User A");
    }

    // ── Response shape contracts ─────────────────────────────────────────────────

    private sealed record ActivityFeedResponse(
        bool Success,
        List<ActivityItemResponse> Items,
        int Count);

    private sealed record ActivityItemResponse(
        Guid Id,
        Guid EventId,
        string EventType,
        Guid UserId,
        string EntityType,
        Guid EntityId,
        string? Title,
        DateTime Timestamp,
        DateTime LoggedAt,
        int PayloadVersion);
}
