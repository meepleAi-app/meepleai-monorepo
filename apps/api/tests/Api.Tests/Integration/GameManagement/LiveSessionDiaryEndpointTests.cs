using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.Enums;
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
/// Integration tests for the diary endpoints:
///   POST /api/v1/live-sessions/{sessionId}/diary
///   GET  /api/v1/live-sessions/{sessionId}/diary
///
/// Issue #2570 SP3 T5.
///
/// Test strategy mirrors LiveSessionStreamEndpointTests:
/// - IntegrationWebApplicationFactory + SharedTestcontainersFixture (isolated DB per test class)
/// - TestSessionHelper for auth (session cookie)
/// - IMediator used directly to create sessions / mutate state without going through HTTP for setup
/// - 4 scenarios per the task brief:
///     1. POST adds an entry → 201 + valid Guid; GET lists it
///     2. POST on Completed session → 409
///     3. Unauthenticated caller → 401
///     4. GET on session with no diary entries → 200 + empty array
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2570")]
public sealed class LiveSessionDiaryEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"live_diary_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public LiveSessionDiaryEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

        _factory = IntegrationWebApplicationFactory.Create(connectionString);

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
    // Scenario 1: POST adds an entry → 201 + Guid; GET then lists it
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST diary adds entry → 201 with entry id; subsequent GET returns it")]
    public async Task Post_AddsDiaryEntry_ThenGet_ReturnsList()
    {
        // Arrange
        var (client, sessionId) = await CreateSessionWithClientAsync();

        var postRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/live-sessions/{sessionId}/diary",
            // token already set on client.DefaultRequestHeaders — reuse the cookie on the client
            GetTokenFromClient(client));
        postRequest.Content = JsonContent.Create(new { text = "We started the game at 20:00" });

        // Act — POST
        var postResponse = await client.SendAsync(postRequest);

        // Assert POST → 201 + Guid body
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "adding a diary entry to an active session must return 201 Created");

        var responseBody = await postResponse.Content.ReadAsStringAsync();
        var entryId = JsonSerializer.Deserialize<Guid>(responseBody, JsonOptions);
        entryId.Should().NotBe(Guid.Empty, "the response body should be the new entry's id");

        // Act — GET
        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(client));
        var getResponse = await client.SendAsync(getRequest);

        // Assert GET → 200 + array with one entry
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await getResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>(JsonOptions);
        entries.Should().NotBeNull();
        entries!.Should().HaveCount(1, "one entry was added");
        entries[0].Id.Should().Be(entryId);
        entries[0].Text.Should().Be("We started the game at 20:00");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 2: POST on a Completed session → 409
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST diary on Completed session returns 409")]
    public async Task Post_OnCompletedSession_Returns409()
    {
        // Arrange — create session, then complete it
        var (client, sessionId) = await CreateSessionWithClientAsync();
        await CompleteSessionAsync(sessionId);

        var postRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(client));
        postRequest.Content = JsonContent.Create(new { text = "Too late" });

        // Act
        var response = await client.SendAsync(postRequest);

        // Assert — ConflictException from domain → 409 via global middleware
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the domain raises ConflictException when the session is Completed; middleware maps it to 409");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 3a: Authenticated non-participant → 403 on POST
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST diary returns 403 when caller is authenticated but not a participant")]
    public async Task Post_NonParticipant_Returns403()
    {
        // Arrange — user A creates the session; user B is authenticated but NOT a participant
        var (_, sessionId) = await CreateSessionWithClientAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={tokenB}");

        var postRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/live-sessions/{sessionId}/diary",
            tokenB);
        postRequest.Content = JsonContent.Create(new { text = "Sneaking in" });

        // Act
        var response = await clientB.SendAsync(postRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an authenticated user who is not a participant must receive 403");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 3b: Authenticated non-participant → 403 on GET
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET diary returns 403 when caller is authenticated but not a participant")]
    public async Task Get_NonParticipant_Returns403()
    {
        // Arrange — user A creates the session; user B is authenticated but NOT a participant
        var (_, sessionId) = await CreateSessionWithClientAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={tokenB}");

        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/diary",
            tokenB);

        // Act
        var response = await clientB.SendAsync(getRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "an authenticated user who is not a participant must receive 403");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 4: Unauthenticated caller → 401
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST and GET diary return 401 when caller is not authenticated")]
    public async Task PostAndGet_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var sessionId = Guid.NewGuid(); // irrelevant — auth check fires first

        // POST without cookie
        var postResponse = await anonClient.PostAsJsonAsync(
            $"/api/v1/live-sessions/{sessionId}/diary",
            new { text = "no auth" });
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "RequireAuthenticatedUser() must reject unauthenticated callers on POST");

        // GET without cookie
        var getResponse = await anonClient.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/diary");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "RequireAuthenticatedUser() must reject unauthenticated callers on GET");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 5: GET on session with no diary entries → 200 + empty array
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET diary on session with no entries returns 200 empty array")]
    public async Task Get_NoDiaryEntries_Returns200EmptyArray()
    {
        // Arrange — create a session but don't add any diary entries
        var (client, sessionId) = await CreateSessionWithClientAsync();

        var getRequest = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/live-sessions/{sessionId}/diary",
            GetTokenFromClient(client));

        // Act
        var getResponse = await client.SendAsync(getRequest);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await getResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>(JsonOptions);
        entries.Should().NotBeNull();
        entries!.Should().BeEmpty("no diary entries have been added to this session");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a user + auth session, creates a LiveGameSession via IMediator,
    /// and returns an authenticated HttpClient + the session id.
    /// </summary>
    private async Task<(HttpClient Client, Guid SessionId)> CreateSessionWithClientAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Diary Test Game",
            GameId: null));

        var client = _factory.CreateClient();
        // Store the token in DefaultRequestHeaders so the helper can read it back via GetTokenFromClient.
        // Also set it so request factory helpers can read the cookie value.
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");

        // Tag the client with the token via a custom header so GetTokenFromClient can extract it.
        // This avoids a second scope-level lookup while keeping the helper self-contained.
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);

        return (client, sessionId);
    }

    /// <summary>
    /// Transitions the live session to Completed state.
    /// Domain invariants: needs ≥1 player before Start; must be InProgress before Complete.
    /// </summary>
    private async Task CompleteSessionAsync(Guid sessionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Add a player so the domain's HasPlayers check passes on Start.
        await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId,
            DisplayName: "Setup Player",
            Color: PlayerColor.Red,
            UserId: null,
            Role: null,
            AvatarUrl: null));

        // Domain invariant: session must be InProgress before Complete.
        await mediator.Send(new StartLiveSessionCommand(sessionId, Guid.NewGuid(), UserTier.Free, Role.User));
        await mediator.Send(new CompleteLiveSessionCommand(sessionId));
    }

    /// <summary>
    /// Extracts the raw session token from the custom X-Test-Session-Token header we
    /// inject during client setup — avoids a second DB lookup.
    /// </summary>
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
            Email = $"diary-test-{userId:N}@test.local",
            DisplayName = "Diary Test User",
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

    // Local record that mirrors DiaryEntryDto (internal DTO → not accessible from tests assembly)
    private sealed record DiaryEntryResponse(
        Guid Id,
        Guid AuthorId,
        DateTimeOffset CreatedAt,
        string Text);
}
