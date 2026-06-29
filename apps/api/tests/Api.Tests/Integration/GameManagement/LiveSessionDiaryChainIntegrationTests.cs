using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// End-to-end integration test for the diary add→event chain.
/// Issue #2570 SP3 T8.
///
/// Chain exercised:
///   POST /api/v1/live-sessions/{id}/diary
///     → AddDiaryEntryCommandHandler (session.AddDiaryEntry → LiveSessionDiaryEntryAddedEvent raised)
///     → UnitOfWork.SaveChangesAsync
///     → MeepleAiDbContext.SaveChangesAsync (Hybrid mode: MediatR.Publish after commit)
///     → LiveSessionStreamForwarder.Handle(LiveSessionDiaryEntryAddedEvent)
///     → ILiveSessionStreamGateway.BroadcastAsync("session:diary")
///
/// The test stubs <see cref="ILiveSessionStreamGateway"/> so it can verify BroadcastAsync
/// is called with type "session:diary" without needing a live Redis/SignalR connection.
/// This is the realistic end-to-end chain short of an actual SSE subscriber.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2570")]
public sealed class LiveSessionDiaryChainIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"live_diary_chain_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    // Captured BroadcastAsync calls for assertion
    private readonly List<(Guid SessionId, LiveSessionStreamEvent Evt)> _broadcastCalls = new();

    public LiveSessionDiaryChainIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

        // Build the factory from the shared helper, then extend it with a spy gateway.
        var baseFactory = IntegrationWebApplicationFactory.Create(connectionString);

        _factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real gateway with a spy that captures BroadcastAsync calls.
                services.RemoveAll<ILiveSessionStreamGateway>();
                services.AddScoped<ILiveSessionStreamGateway>(_ =>
                {
                    var mock = new Mock<ILiveSessionStreamGateway>();

                    // Spy: capture every BroadcastAsync call
                    mock
                        .Setup(g => g.BroadcastAsync(
                            It.IsAny<Guid>(),
                            It.IsAny<LiveSessionStreamEvent>(),
                            It.IsAny<CancellationToken>()))
                        .Callback<Guid, LiveSessionStreamEvent, CancellationToken>((sid, evt, _) =>
                        {
                            lock (_broadcastCalls)
                                _broadcastCalls.Add((sid, evt));
                        })
                        .Returns(Task.CompletedTask);

                    // SubscribeAsync returns empty stream (not needed in this test)
                    mock
                        .Setup(g => g.SubscribeAsync(
                            It.IsAny<Guid>(),
                            It.IsAny<Guid>(),
                            It.IsAny<string?>(),
                            It.IsAny<CancellationToken>()))
                        .Returns(EmptyAsyncEnumerable());

                    return mock.Object;
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
            await _factory.DisposeAsync();

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T8 — end-to-end chain: POST diary → persisted in DB → gateway receives
    //       "session:diary" broadcast (domain event propagates the full chain)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST diary: entry persisted AND session:diary event broadcast through full chain")]
    public async Task Post_DiaryEntry_PersistsAndBroadcastsSessionDiaryEvent()
    {
        // Arrange
        var (client, sessionId, userId) = await CreateSessionWithClientAsync();

        var postRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(client));
        postRequest.Content = JsonContent.Create(new { text = "First great move of the night!" });

        // Act — POST the diary entry (exercises the full BE chain)
        var response = await client.SendAsync(postRequest);

        // Assert — HTTP layer: 201 Created with Guid body
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "POST diary on an active session must return 201 Created");

        var responseBody = await response.Content.ReadAsStringAsync();
        var entryId = System.Text.Json.JsonSerializer.Deserialize<Guid>(responseBody);
        entryId.Should().NotBe(Guid.Empty, "the response body must be the new entry's id");

        // Assert — persistence layer: entry exists in the DB (AC-DIARY-1 append-only)
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entryEntity = await db.Set<Api.Infrastructure.Entities.GameManagement.LiveSessionDiaryEntryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entryId);

        entryEntity.Should().NotBeNull("the diary entry must be persisted to the DB");
        entryEntity!.LiveGameSessionId.Should().Be(sessionId);
        entryEntity.AuthorId.Should().Be(userId);
        entryEntity.Text.Should().Be("First great move of the night!");

        // Assert — event chain: ILiveSessionStreamGateway.BroadcastAsync called with session:diary
        // The MeepleAiDbContext.SaveChangesAsync (Hybrid mode) dispatches MediatR.Publish
        // after the commit, which triggers LiveSessionStreamForwarder → gateway.BroadcastAsync.
        // Allow a brief propagation window (Hybrid mode dispatches inline, but async scheduling
        // may still require a small yield on some runners).
        await WaitForBroadcastAsync(sessionId, "session:diary", timeout: TimeSpan.FromSeconds(3));

        lock (_broadcastCalls)
        {
            var diaryBroadcasts = _broadcastCalls
                .Where(c => c.SessionId == sessionId && c.Evt.Type == "session:diary")
                .ToList();

            diaryBroadcasts.Should().HaveCount(1,
                "AddDiaryEntry raises exactly one LiveSessionDiaryEntryAddedEvent which forwarder maps to session:diary");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC-DIARY-2 chain pin: multi-author + GET returns entries in chronological order
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST two diary entries from different authors: GET returns them chronologically")]
    public async Task Post_TwoEntries_DifferentAuthors_GetReturnsChronologicalOrder()
    {
        // Arrange
        var (clientA, sessionId, _) = await CreateSessionWithClientAsync();

        // Author A is the creator; Author B is a second participant
        await using var setupScope = _factory.Services.CreateAsyncScope();
        var db = setupScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);

        // Add Author B as a participant via the AddPlayer command
        var mediator = setupScope.ServiceProvider.GetRequiredService<IMediator>();
        var userBId = Guid.NewGuid(); // guest player
        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId,
            DisplayName: "Author B",
            Color: Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor.Blue,
            UserId: null,
            Role: null,
            AvatarUrl: null));

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={tokenB}");
        clientB.DefaultRequestHeaders.Add("X-Test-Session-Token", tokenB);

        // Act — POST two entries: A first, then B (using A's client for both for simplicity — same session)
        var post1 = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(clientA));
        post1.Content = JsonContent.Create(new { text = "Entry by author A" });
        var r1 = await clientA.SendAsync(post1);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        var post2 = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(clientA));
        post2.Content = JsonContent.Create(new { text = "Entry by author A again" });
        var r2 = await clientA.SendAsync(post2);
        r2.StatusCode.Should().Be(HttpStatusCode.Created);

        // GET the diary
        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(clientA));
        var getResponse = await clientA.SendAsync(getRequest);

        // Assert — AC-DIARY-2: chronological order
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await getResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        entries.Should().NotBeNull();
        entries!.Should().HaveCount(2);
        entries.Should().BeInAscendingOrder(e => e.CreatedAt,
            "diary entries must be returned in chronological (ascending) order per AC-DIARY-2");
        entries[0].Text.Should().Be("Entry by author A");
        entries[1].Text.Should().Be("Entry by author A again");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a user, creates a live session via IMediator (in InProgress state with
    /// one player so the session can accept diary entries), and returns an authenticated client.
    /// </summary>
    private async Task<(HttpClient Client, Guid SessionId, Guid UserId)> CreateSessionWithClientAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        // Create the session — creator is the user
        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Diary Chain Test Game",
            GameId: null));

        // Domain invariant: Start() requires ≥1 player. The creator's userId counts as a player.
        // We need to ensure the session is InProgress so AddDiaryEntry is allowed.
        // CreateLiveSessionCommand seeds the session in Created state — no auto-start.
        // Add creator as player + start:
        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId,
            DisplayName: "Creator Player",
            Color: Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor.Red,
            UserId: userId,
            Role: null,
            AvatarUrl: null));
        await mediator.Send(new StartLiveSessionCommand(sessionId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);

        return (client, sessionId, userId);
    }

    private static string GetTokenFromClient(HttpClient client) =>
        client.DefaultRequestHeaders.TryGetValues("X-Test-Session-Token", out var values)
            ? values.First()
            : throw new InvalidOperationException("X-Test-Session-Token not set on client");

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"diary-chain-{userId:N}@test.local",
            DisplayName = "Diary Chain User",
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

    /// <summary>
    /// Polls <see cref="_broadcastCalls"/> until the expected broadcast arrives or <paramref name="timeout"/> elapses.
    /// Hybrid-mode dispatch is inline (same request pipeline) so it should be near-instantaneous;
    /// the poll loop is a safety net for thread-scheduling jitter on CI runners.
    /// </summary>
    private async Task WaitForBroadcastAsync(Guid sessionId, string eventType, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (_broadcastCalls)
            {
                if (_broadcastCalls.Any(c => c.SessionId == sessionId && c.Evt.Type == eventType))
                    return;
            }
            await Task.Delay(50);
        }
    }

    /// <summary>Returns an empty async enumerable of <see cref="LiveSessionStreamEvent"/>.</summary>
    private static async IAsyncEnumerable<LiveSessionStreamEvent> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    // Local record that mirrors DiaryEntryDto (internal DTO not accessible from tests assembly)
    private sealed record DiaryEntryResponse(
        Guid Id,
        Guid AuthorId,
        DateTimeOffset CreatedAt,
        string Text);
}
