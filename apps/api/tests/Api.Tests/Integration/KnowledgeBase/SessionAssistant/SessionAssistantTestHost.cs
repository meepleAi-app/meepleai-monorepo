using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Hubs;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

#pragma warning disable MA0048 // File name must match type name - the shared host of the session assistant tests
namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Host condiviso fra i test di una classe, database per test: il profilo dell'agente è un singleton, e
/// ogni test lo pubblica a modo suo. Doppi solo ai margini: il gateway LLM (copione) e l'hub SignalR
/// (registra). Sono con stato e vivono quanto la classe: ogni test li azzera nel suo InitializeAsync.
/// </summary>
public sealed class SessionAssistantHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "session_assistant")
{
    internal ScriptedChatGateway Llm { get; } = new();

    internal RecordingSessionHub Hub { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmService>();
            services.AddSingleton(Llm.Service);
            // SignalR registra IHubContext<> aperto: una registrazione chiusa per questo hub vince.
            services.AddSingleton(Hub.Context);
        }));
}

/// <summary>Il gateway con un copione: una risposta per chiamata, nell'ordine dato, e le richieste ricevute.</summary>
internal sealed class ScriptedChatGateway
{
    private readonly ConcurrentQueue<LlmCompletionResult> _script = new();
    private readonly ConcurrentQueue<LlmChatRequest> _requests = new();
    private readonly Mock<ILlmService> _mock = new();

    public ScriptedChatGateway()
    {
        _mock.Setup(g => g.ChatAsync(It.IsAny<LlmChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmChatRequest request, CancellationToken _) =>
            {
                _requests.Enqueue(request);
                return _script.TryDequeue(out var next)
                    ? next
                    : LlmCompletionResult.CreateFailure("The test script has no more responses.");
            });
    }

    public ILlmService Service => _mock.Object;

    public IReadOnlyList<LlmChatRequest> Requests => [.. _requests];

    public void Reset(params LlmCompletionResult[] script)
    {
        _requests.Clear();
        _script.Clear();
        foreach (var response in script)
        {
            _script.Enqueue(response);
        }
    }

    public static LlmCompletionResult Calls(string tool, string argumentsJson) =>
        LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = [new LlmToolCall($"call_{Guid.NewGuid():N}", tool, argumentsJson)],
        };

    public static LlmCompletionResult Says(string text) => LlmCompletionResult.CreateSuccess(text);
}

/// <summary>L'hub della sessione, finto: conta i messaggi trasmessi ai gruppi, per nome del metodo.</summary>
internal sealed class RecordingSessionHub
{
    private readonly ConcurrentQueue<string> _methods = new();
    private readonly Mock<IHubContext<GameStateHub>> _context = new();

    public RecordingSessionHub()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, _, _) => _methods.Enqueue(method))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients> { DefaultValue = DefaultValue.Mock };
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        clients.Setup(c => c.All).Returns(proxy.Object);
        _context.Setup(c => c.Clients).Returns(clients.Object);
        _context.Setup(c => c.Groups).Returns(Mock.Of<IGroupManager>());
    }

    public IHubContext<GameStateHub> Context => _context.Object;

    public int Count(string method) => _methods.Count(m => string.Equals(m, method, StringComparison.Ordinal));

    public void Reset() => _methods.Clear();
}

internal sealed record LiveTable(Guid SessionId, IReadOnlyDictionary<string, Guid> Players);

internal static class SessionAssistantTestData
{
    public const string ItalianPrompt = "Sei l'assistente al tavolo. Non scrivi mai la partita: proponi.";
    public const string EnglishPrompt = "You are the table assistant. You never write the game: you propose.";

    public static async Task<(Guid UserId, string Token)> SeedHostAsync(WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: ct);
    }

    /// <summary>Un comando per scope, come una richiesta HTTP: ogni comando della sessione live salva da sé.</summary>
    public static async Task<T> SendAsync<T>(WebApplicationFactory<Program> factory, IRequest<T> request, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, ct);
    }

    public static async Task SendAsync(WebApplicationFactory<Program> factory, IRequest request, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, ct);
    }

    /// <summary>Una sessione senza gioco (niente quota) con la dimensione «punti» e i giocatori dati, nell'ordine.</summary>
    public static async Task<LiveTable> CreateTableAsync(
        WebApplicationFactory<Program> factory, Guid ownerId, bool start, CancellationToken ct, params string[] players)
    {
        var sessionId = await SendAsync(factory, new CreateLiveSessionCommand(
            UserId: ownerId, GameName: "Azul", GameId: null, ScoringDimensions: ["punti"]), ct);
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var i = 0; i < players.Length; i++)
        {
            ids[players[i]] = await SendAsync(factory, new AddPlayerToLiveSessionCommand(sessionId, players[i], (PlayerColor)i), ct);
        }

        if (start)
        {
            await SendAsync(factory, new StartLiveSessionCommand(sessionId, ownerId, UserTier.Free, Role.User), ct);
        }

        return new LiveTable(sessionId, ids);
    }

    /// <summary>
    /// Pubblica, come farà l'admin, una versione del profilo con l'assistente abilitato. Il catalogo viene
    /// dal seed, che dichiara il tool calling di <c>deepseek-flash</c>, il modello principale del profilo.
    /// </summary>
    /// <returns>L'id della versione pubblicata.</returns>
    public static async Task<Guid> EnableAssistantAsync(
        WebApplicationFactory<Program> factory,
        CancellationToken ct,
        Func<SessionAssistantSettings, SessionAssistantSettings>? change = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IMediator>().Send(new SeedAiModelsCommand(), ct);

        var profile = await services.GetRequiredService<IAgentProfileRepository>().GetAsync(ct)
            ?? throw new InvalidOperationException("The migrations publish the first version of the agent profile.");
        var c = profile.Published.Content;
        var assistant = SessionAssistantSettings.Create(
            enabled: true,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = ItalianPrompt, ["en"] = EnglishPrompt });
        profile.CreateDraft(AgentProfileContent.Create(
            c.Name, c.Description, c.Persona, c.PrimaryModelId, c.FallbackModelId, c.Temperature, c.MaxResponseTokens,
            c.CitationStyle, c.SystemPrompts, c.FallbackLanguage, c.CandidatePoolSize, c.FinalTopK, c.MinScore,
            c.VectorWeight, c.KeywordWeight, c.RerankerEnabled, c.AllowedCategories.ToList(),
            change?.Invoke(assistant) ?? assistant));
        var toolCallingModels = await services.GetRequiredService<IToolCallingModelCatalog>().GetToolCallingModelIdsAsync(ct);
        var version = profile.PublishDraft(toolCallingModels);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        return version.Id;
    }

    /// <summary>Una domanda all'assistente: lo stato HTTP, gli eventi SSE (se la risposta è uno stream) e il corpo.</summary>
    public static async Task<(HttpStatusCode Status, List<JsonElement> Events, string Body)> AskAsync(
        HttpClient client, string token, Guid sessionId, string question, CancellationToken ct, string? language = "it")
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/live-sessions/{sessionId}/assistant", token, new { question, language });
        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        var events = new List<JsonElement>();
        if (string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.Ordinal))
        {
            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(trimmed["data:".Length..].Trim());
                events.Add(document.RootElement.Clone());
            }
        }

        return (response.StatusCode, events, body);
    }
}
