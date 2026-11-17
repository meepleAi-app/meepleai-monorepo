using System.Net;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// La fixture di <see cref="ChatSessionEndpointsIdorIntegrationTests"/>: host per classe, database
/// per test.
/// </summary>
/// <remarks>
/// #5039. La classe costruiva l'host nel proprio <c>InitializeAsync</c>, quindi una volta per test, e
/// il <c>Timeout</c> di ogni test contava anche quel tempo. Su un runner lento il build dell'host si
/// allunga per tutte le fixture: qui finiva oltre i 90 s, e tre dei quattro test sono andati in
/// timeout anche su <c>main-dev</c> (run 43882007678, mediana <c>host=</c> 33,2 s). Con l'host nella
/// fixture il <c>Timeout</c> torna a misurare la richiesta.
/// </remarks>
public sealed class ChatSessionEndpointsIdorHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "chat_session_idor");

/// <summary>
/// HTTP-layer IDOR tests for the user chat-session list endpoints.
/// #3120: GET /users/{userId}/games/{gameId}/chat-sessions and
/// GET /users/{userId}/chat-sessions/recent must reject a caller whose id != {userId}
/// (mirroring the sibling GET /users/{userId}/chat-sessions/limit).
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3120")]
[Trait("Issue", "5039")]
public sealed class ChatSessionEndpointsIdorIntegrationTests
    : IClassFixture<ChatSessionEndpointsIdorHostFixture>, IAsyncLifetime
{
    private readonly ChatSessionEndpointsIdorHostFixture _hostFixture;
    private readonly Guid _gameId = Guid.NewGuid();
    private HttpClient _ownerClient = null!;
    private HttpClient _otherClient = null!;
    private Guid _ownerId;
    private string _ownerToken = null!;
    private string _otherToken = null!;

    public ChatSessionEndpointsIdorIntegrationTests(ChatSessionEndpointsIdorHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync prima di ogni uso dell'host: con l'ordine invertito i due utenti
        // finirebbero nel database di avvio, e le richieste leggerebbero quello del test.
        await _hostFixture.BeginTestAsync();
        var factory = _hostFixture.Factory;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        (_ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _otherToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        _ownerClient = factory.CreateClient();
        _otherClient = factory.CreateClient();
    }

    // I client sono di questa istanza; host e database sono della fixture.
    public ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _otherClient?.Dispose();
        return ValueTask.CompletedTask;
    }

    private string UserGameSessionsUrl => $"/api/v1/users/{_ownerId}/games/{_gameId}/chat-sessions";
    private string RecentSessionsUrl => $"/api/v1/users/{_ownerId}/chat-sessions/recent";

    [Fact(Timeout = 90_000)]
    public async Task GetUserGameSessions_CallerIsOwner_Returns200()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, UserGameSessionsUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetUserGameSessions_CallerIsNotOwner_Returns403()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, UserGameSessionsUrl, _otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetRecentSessions_CallerIsOwner_Returns200()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, RecentSessionsUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetRecentSessions_CallerIsNotOwner_Returns403()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, RecentSessionsUrl, _otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
