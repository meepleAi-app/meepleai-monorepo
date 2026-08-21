using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// La guardia <c>WaitForPostgresReadyAsync</c> che questa classe chiamava prima di costruire l'host
/// non e' andata persa: e' nella base, subito dopo la creazione del database isolato.
/// </para>
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test semina il proprio utente
/// (<c>SeedUserAsync</c>: id nuovo, email parametrizzata su quell'id) e la propria sessione, e
/// <c>SeedDisputesAsync</c> scrive con un <c>ExecuteUpdateAsync</c> filtrato per <c>sessionId</c>,
/// asserendo <c>affected == 1</c>. Le asserzioni su conteggio e ordinamento
/// (<c>HaveCount(2)</c>, <c>Disputes[0]</c>, <c>BeEmpty()</c>) leggono il campo <c>Disputes</c>
/// <b>di quella singola sessione</b>: non e' un aggregato, quindi non c'e' lista globale in cui le
/// righe di un altro test possano interporsi.
/// </para>
/// </summary>
public sealed class GetSessionDisputesHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "session_disputes");

/// <summary>
/// Integration tests for the per-session dispute-history endpoint:
///   GET /api/v1/live-sessions/{sessionId}/disputes
///
/// Issue #3391 (finding C8): wire the existing (but unwired) GetSessionDisputesQuery so the
/// Arbitro tab can hydrate its dispute history on reload (REST), not only via SignalR.
///
/// Scope decision: per-session (not the pre-existing per-game /games/{gameId}/dispute-history),
/// because the Arbitro tab is scoped to a single live session and cross-session history is a
/// separate future concern (DisputeHistory.gameId prop is "reserved for future").
///
/// Test strategy mirrors LiveSessionDiaryEndpointTests:
/// - IntegrationWebApplicationFactory + SharedTestcontainersFixture (isolated DB per class)
/// - TestSessionHelper for auth (session cookie)
/// - IMediator to create sessions; DisputesJson seeded directly (same camelCase shape the
///   LiveGameSessionMapper reads back) to avoid depending on the multi-step Arbitro LLM flow.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "3391")]
public sealed class GetSessionDisputesEndpointTests : IClassFixture<GetSessionDisputesHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Mirrors the mapper's write options so the seeded DisputesJson round-trips through ToDomain.
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GetSessionDisputesEndpointTests(GetSessionDisputesHostFixture host)
    {
        _factory = host.Factory;
    }


    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 1: participant GET on a session with disputes → 200 + ordered list
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET disputes returns the session's persisted disputes ordered by timestamp asc")]
    public async Task Get_SessionWithDisputes_ReturnsOrderedList()
    {
        // Arrange — create session, seed two disputes out of chronological order
        var (client, sessionId) = await CreateSessionWithClientAsync();

        var older = new RuleDisputeEntry(
            Guid.NewGuid(), "Can I play two cards?", "No — one card per turn.",
            new List<string> { "p.12" }, "Alice", DateTime.UtcNow.AddMinutes(-10));
        var newer = new RuleDisputeEntry(
            Guid.NewGuid(), "Does a tie break by score?", "Yes — highest score wins ties.",
            new List<string> { "p.4", "p.5" }, "Bob", DateTime.UtcNow.AddMinutes(-2));

        // Seed newest-first to prove the handler orders ascending.
        await SeedDisputesAsync(sessionId, newer, older);

        // Act
        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/disputes",
            GetTokenFromClient(client));
        var response = await client.SendAsync(getRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetSessionDisputesResult>(JsonOptions);
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(sessionId);
        result.Disputes.Should().HaveCount(2);
        result.Disputes[0].Id.Should().Be(older.Id, "disputes must be ordered by timestamp ascending");
        result.Disputes[0].RaisedByPlayerName.Should().Be("Alice");
        result.Disputes[0].Verdict.Should().Be("No — one card per turn.");
        result.Disputes[0].RuleReferences.Should().ContainSingle().Which.Should().Be("p.12");
        result.Disputes[1].Id.Should().Be(newer.Id);
        result.Disputes[1].RuleReferences.Should().BeEquivalentTo(new[] { "p.4", "p.5" });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 2: authenticated non-participant → 403 (IDOR guard)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET disputes returns 403 when caller is authenticated but not a participant")]
    public async Task Get_NonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateSessionWithClientAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={tokenB}");

        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/disputes",
            tokenB);
        var response = await clientB.SendAsync(getRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an authenticated user who is not a participant must receive 403");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 3: unauthenticated caller → 401
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET disputes returns 401 when caller is not authenticated")]
    public async Task Get_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var sessionId = Guid.NewGuid(); // irrelevant — auth check fires first

        var response = await anonClient.GetAsync($"/api/v1/live-sessions/{sessionId}/disputes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "RequireAuthenticatedUser() must reject unauthenticated callers");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 4: session with no disputes → 200 + empty list
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET disputes on a session with no disputes returns 200 empty list")]
    public async Task Get_SessionWithNoDisputes_Returns200EmptyList()
    {
        var (client, sessionId) = await CreateSessionWithClientAsync();

        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/disputes",
            GetTokenFromClient(client));
        var response = await client.SendAsync(getRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetSessionDisputesResult>(JsonOptions);
        result.Should().NotBeNull();
        result!.Disputes.Should().BeEmpty("no disputes were raised in this session");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid SessionId)> CreateSessionWithClientAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Dispute Test Game",
            GameId: null));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);

        return (client, sessionId);
    }

    /// <summary>
    /// Seeds disputes directly onto the session's DisputesJson column using the same camelCase
    /// shape the LiveGameSessionMapper writes, so ToDomain reads them back verbatim.
    /// Uses ExecuteUpdateAsync (a direct SQL UPDATE) to bypass the change tracker and the xmin
    /// optimistic-concurrency token, and asserts exactly one row was written so a persistence
    /// failure surfaces here instead of as an empty read downstream.
    /// </summary>
    private async Task SeedDisputesAsync(Guid sessionId, params RuleDisputeEntry[] disputes)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var json = JsonSerializer.Serialize(disputes.ToList(), CamelCaseOptions);
        var affected = await db.LiveGameSessions
            .Where(e => e.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DisputesJson, json));

        affected.Should().Be(1, "the seed must update exactly the target session's DisputesJson");
    }

    private static string GetTokenFromClient(HttpClient client)
    {
        return client.DefaultRequestHeaders.TryGetValues("X-Test-Session-Token", out var values)
            ? values.First()
            : throw new InvalidOperationException("X-Test-Session-Token not set on client");
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"dispute-test-{userId:N}@test.local",
            DisplayName = "Dispute Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return userId;
    }
}
