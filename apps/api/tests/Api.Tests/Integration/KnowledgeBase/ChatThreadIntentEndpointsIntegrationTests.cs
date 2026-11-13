using System.Net;
using System.Text.Json;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="ChatThreadIntentEndpointsIntegrationTests"/>: host per classe, database per test.
/// </summary>
public sealed class ChatThreadIntentHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "chat_thread_intent");

/// <summary>
/// #4998 — l'intento di un thread sulle due rotte che lo scrivono, la creazione e lo switch.
/// </summary>
/// <remarks>
/// <para>
/// La creazione accettava qualunque stringa, mentre lo switch accetta solo i tre intenti (#4885): un id ritirato
/// arrivava ancora nella colonna. Ora la creazione segue la regola dello switch. Il rifiuto è un errore di
/// validazione, quindi un 422, come ogni validatore di questo repository.
/// </para>
/// <para>
/// Lo switch su un thread chiuso rispondeva 500: il dominio lancia un'<c>InvalidOperationException</c> che il
/// middleware non riconosce. Ora è un 409, come la chiusura di un thread chiuso (#3224).
/// </para>
/// <para>
/// Ogni rotta è provata nelle due direzioni: il rifiuto, e la richiesta valida che riesce.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4998")]
public sealed class ChatThreadIntentEndpointsIntegrationTests
    : IClassFixture<ChatThreadIntentHostFixture>, IAsyncLifetime
{
    private readonly ChatThreadIntentHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private string _token = null!;
    private Guid _gameId;

    public ChatThreadIntentEndpointsIntegrationTests(ChatThreadIntentHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory.
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_, _token) = await TestSessionHelper.CreateUserSessionAsync(db);
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Chat Thread Intent Game");

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string url, T body) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(method, url, _token, body));

    private async Task<Guid> CreateThreadAsync(string? agentType)
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/chat-threads", new { gameId = _gameId, agentType });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<int> ThreadCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ChatThreads.CountAsync();
    }

    private async Task<string?> StoredAgentTypeAsync(Guid threadId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ChatThreads.AsNoTracking().Where(t => t.Id == threadId).Select(t => t.AgentType).SingleAsync();
    }

    [Fact]
    public async Task Create_WithARetiredId_IsRefused_AndWithAnIntent_StoresItNormalized()
    {
        var refused = await SendAsync(HttpMethod.Post, "/api/v1/chat-threads", new { gameId = _gameId, agentType = "arbitro" });

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await refused.Content.ReadAsStringAsync());
        (await ThreadCountAsync()).Should().Be(0, "a refused creation stores nothing");

        var threadId = await CreateThreadAsync(" Tutor ");

        (await StoredAgentTypeAsync(threadId)).Should().Be("tutor", "creation stores the intent as the switch does");
    }

    [Fact]
    public async Task SwitchIntent_OnAClosedThread_Is409_AndOnAnOpenThread_ReturnsTheNewIntent()
    {
        var threadId = await CreateThreadAsync("tutor");
        var closed = await SendAsync(HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/close", new { });
        closed.StatusCode.Should().Be(HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());

        var refused = await SendAsync(HttpMethod.Patch, $"/api/v1/chat-threads/{threadId}/agent", new { agentType = "secca" });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync());
        (await StoredAgentTypeAsync(threadId)).Should().Be("tutor", "a refused switch leaves the intent as it was");

        var reopened = await SendAsync(HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/reopen", new { });
        reopened.StatusCode.Should().Be(HttpStatusCode.OK, await reopened.Content.ReadAsStringAsync());

        var switched = await SendAsync(HttpMethod.Patch, $"/api/v1/chat-threads/{threadId}/agent", new { agentType = "secca" });

        switched.StatusCode.Should().Be(HttpStatusCode.OK, await switched.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await switched.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("agentType").GetString().Should().Be("secca");
        (await StoredAgentTypeAsync(threadId)).Should().Be("secca");
    }
}
