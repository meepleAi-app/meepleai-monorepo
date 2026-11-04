using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Api.Tests.Integration.KnowledgeBase.SessionAssistant.ScriptedChatGateway;
using DomainChatMessage = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.ChatMessage;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §6.2, dalla richiesta HTTP al database: un turno dell'assistente legge la sessione
/// vera, salva messaggi e proposte, e non scrive la partita. Il gateway LLM è un copione.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class SessionAssistantTurnEndpointTests(SessionAssistantHostFixture host)
    : IClassFixture<SessionAssistantHostFixture>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _owner;
    private string _token = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await host.BeginTestAsync();
        host.Llm.Reset();
        host.Hub.Reset();
        _client = host.Factory.CreateClient();
        (_owner, _token) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<LiveTable> StartedTableAsync() =>
        SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Marco", "Giulia", "Luca");

    private static string Type(JsonElement turnEvent) => turnEvent.GetProperty("type").GetString()!;

    [Fact]
    public async Task ATurn_ReadsTheRealSession_SavesTheProposal_AndDoesNotWriteTheGame()
    {
        var versionId = await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(
            Calls("stato_partita", "{}"),
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta: +5 a Marco. Confermala dalla card."));

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "Marco fa 5 punti", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        events.Select(Type).Should().Equal("status", "status", "status", "proposal", "answer", "done");
        events[1].GetProperty("tool").GetString().Should().Be("stato_partita");
        events[2].GetProperty("tool").GetString().Should().Be("proponi_punteggio");

        var card = events[3].GetProperty("proposal");
        card.GetProperty("kind").GetString().Should().Be("Score");
        card.GetProperty("status").GetString().Should().Be("Pending");
        card.GetProperty("playerId").GetGuid().Should().Be(table.Players["Marco"]);
        card.GetProperty("round").GetInt32().Should().Be(1, "appena avviata, la partita è al primo giro");
        card.GetProperty("points").GetInt32().Should().Be(5);
        card.GetProperty("cellValueBefore").GetInt32().Should().Be(0);
        card.GetProperty("resultingValue").GetInt32().Should().Be(5);
        events[5].GetProperty("profileVersionId").GetGuid().Should().Be(versionId);

        // Lo stato arrivato al modello è quello della sessione vera.
        var toolResult = host.Llm.Requests[1].Messages[^1];
        toolResult.Role.Should().Be("tool");
        toolResult.Content.Should().Contain("Marco").And.Contain("Giulia").And.Contain("Luca").And.Contain("punti");

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var proposalId = card.GetProperty("id").GetGuid();
        var proposal = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(proposalId, Ct);
        proposal!.Status.Should().Be(AssistantProposalStatus.Pending);
        proposal.MessageId.Should().Be(events[5].GetProperty("responseId").GetGuid());

        var thread = await scope.ServiceProvider.GetRequiredService<IChatThreadRepository>().FindByLiveSessionIdAsync(table.SessionId, Ct);
        thread!.Messages.Select(m => m.Role).Should().Equal("user", "assistant");
        thread.Messages[1].Id.Should().Be(proposal.MessageId);

        // Nessuna scrittura senza conferma: la partita è com'era.
        var session = await SessionAssistantTestData.SendAsync(host.Factory, new GetLiveSessionQuery(table.SessionId), Ct);
        session.RoundScores.Should().BeEmpty();
        session.CurrentTurnIndex.Should().Be(1);
    }

    [Fact]
    public async Task TheSecondTurn_ExpiresTheUnconfirmedProposal_AndSendsTheHistory()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""), Says("Proposta pronta."));
        var (_, first, _) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "Marco fa 5 punti", Ct);
        var firstProposal = first.Single(e => Type(e) == "proposal").GetProperty("proposal").GetProperty("id").GetGuid();

        host.Llm.Reset(Says("Tocca a Marco."));
        var (status, second, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        second.Select(Type).Should().Equal("status", "answer", "done");
        var sent = host.Llm.Requests.Single().Messages;
        sent.Select(m => m.Role).Should().Equal("system", "system", "user", "assistant", "user");
        sent[3].Content.Should().Contain("Proposta pronta.").And.Contain("scaduta senza conferma");

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var expired = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(firstProposal, Ct);
        expired!.Status.Should().Be(AssistantProposalStatus.Expired);
        var thread = await scope.ServiceProvider.GetRequiredService<IChatThreadRepository>().FindByLiveSessionIdAsync(table.SessionId, Ct);
        thread!.Messages.Should().HaveCount(4);
        thread.LiveSessionId.Should().Be(table.SessionId, "il secondo salvataggio non deve azzerare la colonna");
    }

    // ─── Campo del profilo → effetto (spec §3.2) ────────────────────────────────

    [Fact]
    public async Task WithoutTheSectionInThePublishedProfile_TheAssistantIsOff()
    {
        await SessionAssistantTestData.DisableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("assistant_disabled");
        events.Should().BeEmpty("un controllo fallito risponde in JSON, non con uno stream");
        host.Llm.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task TheFieldsOfTheSection_ReachTheGateway()
    {
        var versionId = await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct, assistant =>
            Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.SessionAssistantSettings.Create(
                true, assistant.SystemPrompts.ToDictionary(StringComparer.Ordinal), temperature: 0.4m, maxResponseTokens: 450,
                maxToolIterations: 1));
        var table = await StartedTableAsync();
        host.Llm.Reset(Calls("stato_partita", "{}"), Says("Tocca a Marco."));

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "who's next?", Ct, language: "en");

        status.Should().Be(HttpStatusCode.OK, body);
        var requests = host.Llm.Requests;
        requests.Should().HaveCount(2, "un giro con strumenti, poi la chiamata finale");
        requests[0].Messages[0].Content.Should().Be(SessionAssistantTestData.EnglishPrompt);
        requests[0].Temperature.Should().Be(0.4);
        requests[0].MaxTokens.Should().Be(450);
        requests[0].PrimaryModelId.Should().Be("deepseek-flash");
        requests[0].FallbackModelId.Should().BeNull();
        requests[0].ProfileVersionId.Should().Be(versionId);
        requests[0].Tools.Should().HaveCount(3);
        requests[1].Tools.Should().BeEmpty("esauriti i giri della sezione, il modello deve rispondere");
    }

    // ─── Chi può chiedere, e quando ─────────────────────────────────────────────

    [Fact]
    public async Task AParticipantWhoIsNotTheOwner_IsForbidden()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var (guest, guestToken) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: false, Ct, "Marco");
        await SessionAssistantTestData.SendAsync(host.Factory, new AddPlayerToLiveSessionCommand(
            table.SessionId, "Giulia", Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor.Blue, UserId: guest), Ct);
        await SessionAssistantTestData.SendAsync(host.Factory, new StartLiveSessionCommand(
            table.SessionId, _owner, Api.SharedKernel.Domain.ValueObjects.UserTier.Free, Api.SharedKernel.Domain.ValueObjects.Role.User), Ct);

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, guestToken, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.Forbidden, body);
        host.Llm.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ACompletedSession_IsAConflict_WithItsCode()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(table.SessionId), Ct);

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("session_state_invalid");
    }

    [Fact]
    public async Task AnEmptyQuestion_IsABadRequest_AndAnAnonymousCallerIsUnauthorized()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();

        var (empty, _, emptyBody) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "  ", Ct);
        var anonymous = await _client.PostAsync(
            $"/api/v1/live-sessions/{table.SessionId}/assistant",
            System.Net.Http.Json.JsonContent.Create(new { question = "a chi tocca?" }), Ct);

        empty.Should().Be(HttpStatusCode.BadRequest, emptyBody);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AGatewayFailure_IsAnErrorEvent_NotABrokenStream()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset();   // copione vuoto: il gateway fallisce senza codice

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        events.Select(Type).Should().Equal("status", "answer", "error");
        events[^1].GetProperty("code").GetString().Should().Be("assistant_unavailable");
    }

    // ─── Ciò che la review dell'intero branch ha chiesto di fissare ─────────────

    /// <summary>
    /// Review Focus 9, sul cruscotto: il conteggio e l'attività recente leggono ChatThreads senza passare
    /// dal repository. Con l'id in mano, il thread dell'assistente si potrebbe aprire, chiudere o
    /// cancellare dagli endpoint delle chat.
    /// </summary>
    [Fact]
    public async Task TheAssistantThread_IsNotAmongTheChatsOfTheDashboard()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(Says("Tocca a Marco."));
        var (asked, _, askedBody) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);
        asked.Should().Be(HttpStatusCode.OK, askedBody);

        Guid ordinaryId;
        Guid assistantId;
        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var threads = scope.ServiceProvider.GetRequiredService<IChatThreadRepository>();
            var ordinary = new Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread(
                Guid.NewGuid(), _owner, gameId: null, title: "Regole di Azul");
            await threads.AddAsync(ordinary, Ct);
            await scope.ServiceProvider.GetRequiredService<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>().SaveChangesAsync(Ct);
            ordinaryId = ordinary.Id;
            assistantId = (await threads.FindByLiveSessionIdAsync(table.SessionId, Ct))!.Id;
        }

        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/dashboard", _token), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var dashboard = JsonDocument.Parse(body);
        dashboard.RootElement.GetProperty("stats").GetProperty("chatCount").GetInt32().Should().Be(1);
        dashboard.RootElement.GetProperty("recentActivity").EnumerateArray()
            .Where(activity => activity.GetProperty("type").GetString() == "chat_saved")
            .Select(activity => activity.GetProperty("chatId").GetString())
            .Should().Equal(ordinaryId.ToString());
        body.Should().NotContain(assistantId.ToString());
    }

    /// <summary>
    /// Il contratto della scadenza (spec §5.2 e §6.2): le proposte ancora Pending scadono quando il turno
    /// successivo INIZIA, anche se poi quel turno fallisce. Il primo evento «status» lo dice al client.
    /// </summary>
    [Fact]
    public async Task AFailedTurn_StillExpiresTheEarlierProposals()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""), Says("Proposta pronta."));
        var (_, first, _) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "Marco fa 5 punti", Ct);
        var firstProposal = first.Single(e => Type(e) == "proposal").GetProperty("proposal").GetProperty("id").GetGuid();

        host.Llm.Reset();   // copione vuoto: il gateway fallisce
        var (status, second, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        second.Select(Type).Should().Equal("status", "answer", "error");
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var earlier = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(firstProposal, Ct);
        earlier!.Status.Should().Be(AssistantProposalStatus.Expired);
    }

    /// <summary>I risultati degli strumenti tornati al modello nella seconda chiamata, nell'ordine.</summary>
    private List<string> ToolResults() =>
        host.Llm.Requests[1].Messages.Where(message => message.Role == "tool").Select(message => message.Content).ToList();

    private static Api.Services.LlmCompletionResult CallsAll(string tool, params string[] argumentsJson) =>
        Api.Services.LlmCompletionResult.CreateSuccess(string.Empty) with
        {
            ToolCalls = argumentsJson.Select((arguments, i) => new Api.Services.LlmToolCall($"call_{i}", tool, arguments)).ToList(),
        };

    /// <summary>
    /// Spec §7.1, «errori tipizzati e confini»: ogni validazione degli strumenti, con il lettore reale.
    /// Il modello manda sette chiamate sbagliate in un giro: ognuna torna come errore tipizzato che può
    /// leggere, e nessuna diventa una proposta.
    /// </summary>
    [Fact]
    public async Task EveryToolValidation_ReachesTheModelAsATypedError_OnTheRealSession()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var sessionId = await SessionAssistantTestData.SendAsync(host.Factory, new CreateLiveSessionCommand(
            UserId: _owner, GameName: "Azul", GameId: null, ScoringDimensions: ["punti", "bonus"]), Ct);
        var color = 0;
        foreach (var name in new[] { "Marco Rossi", "Marco Bianchi", "Giulia" })
        {
            await SessionAssistantTestData.SendAsync(host.Factory, new AddPlayerToLiveSessionCommand(
                sessionId, name, (Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor)color++), Ct);
        }

        await SessionAssistantTestData.SendAsync(host.Factory, new StartLiveSessionCommand(
            sessionId, _owner, Api.SharedKernel.Domain.ValueObjects.UserTier.Free, Api.SharedKernel.Domain.ValueObjects.Role.User), Ct);
        (string Arguments, string Error)[] wrong =
        [
            ("""{"giocatore":"","punti":5,"dimensione":"punti"}""", "giocatore_mancante"),
            ("""{"giocatore":"Marco","punti":5,"dimensione":"punti"}""", "giocatore_ambiguo"),
            ("""{"giocatore":"Paolo","punti":5,"dimensione":"punti"}""", "giocatore_sconosciuto"),
            ("""{"giocatore":"Giulia","punti":5}""", "dimensione_richiesta"),
            ("""{"giocatore":"Giulia","punti":5,"dimensione":"oro"}""", "dimensione_sconosciuta"),
            ("""{"giocatore":"Giulia","punti":5,"dimensione":"punti","round":0}""", "round_non_valido"),
            ("""{"giocatore":"Giulia","punti":100001,"dimensione":"punti"}""", "punti_fuori_limite"),
        ];
        host.Llm.Reset(
            CallsAll("proponi_punteggio", wrong.Select(call => call.Arguments).ToArray()),
            Says("Mi servono più dati: chi ha fatto i punti?"));

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, sessionId, "segna i punti", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        events.Select(Type).Should().NotContain("proposal");
        Type(events[^1]).Should().Be("done");
        var results = ToolResults();
        results.Should().HaveCount(wrong.Length);
        for (var i = 0; i < wrong.Length; i++)
        {
            results[i].Should().Contain(wrong[i].Error, $"chiamata {i}: {wrong[i].Arguments}");
        }

        await using var scope = host.Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().ListOpenBySessionAsync(sessionId, Ct))
            .Should().BeEmpty();
    }

    /// <summary>
    /// Review Focus 6, dalla richiesta HTTP: prima dell'avvio l'assistente risponde, ma i due strumenti di
    /// proposta rifiutano con un errore tipizzato, e la partita resta com'era.
    /// </summary>
    [Fact]
    public async Task BeforeTheGameStarts_TheProposalToolsRefuse_WithATypedError()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: false, Ct, "Marco", "Giulia");
        host.Llm.Reset(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Calls("proponi_turno", "{}"),
            Says("La partita non è ancora iniziata."));

        var (status, events, body) = await SessionAssistantTestData.AskAsync(
            _client, _token, table.SessionId, "Marco fa 5 punti e passa il turno", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        events.Select(Type).Should().Equal("status", "status", "status", "answer", "done");
        host.Llm.Requests[1].Messages[^1].Content.Should().Contain("sessione_non_modificabile");
        host.Llm.Requests[2].Messages[^1].Content.Should().Contain("sessione_non_in_corso");
        var session = await SessionAssistantTestData.SendAsync(host.Factory, new GetLiveSessionQuery(table.SessionId), Ct);
        session.RoundScores.Should().BeEmpty();
        session.CurrentTurnIndex.Should().Be(0);
    }

    /// <summary>
    /// #4411: l'host non ha dichiarato di possedere il gioco e la KB non è pubblica. cerca_regole risponde con un
    /// errore tipizzato, prima del recupero: con il controllo saltato (o un ruolo che lo supera) il recupero parte,
    /// non trova niente nel database di prova e il modello leggerebbe «non ne parla», non questo codice.
    /// </summary>
    [Fact]
    public async Task AHostWithoutAccessToTheRulebook_GetsATypedErrorFromTheTool()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateGameTableAsync(host.Factory, _owner, Ct, "Marco", "Giulia");
        host.Llm.Reset(
            Calls("cerca_regole", """{"domanda":"quante tessere?"}"""),
            Says("Non posso consultare il regolamento."));

        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "quante tessere?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        host.Llm.Requests[0].Tools.Select(t => t.Name).Should().Contain("cerca_regole");
        var toolResult = host.Llm.Requests[1].Messages.Last(m => m.Role == "tool");
        toolResult.Content.Should().Contain("regolamento_non_accessibile");
        events.Should().Contain(e => Type(e) == "done");
    }

    // ─── #4456: il costo del turno sul messaggio ─────────────────────────────────────────────────

    private static LlmCompletionResult Costing(LlmCompletionResult result, decimal cost, int prompt, int completion) =>
        result with
        {
            Usage = new LlmUsage(prompt, completion, prompt + completion),
            Cost = new LlmCost { InputCost = cost, OutputCost = 0m, ModelId = "deepseek-flash", Provider = "OpenRouter" },
        };

    private async Task<DomainChatMessage> SavedAnswerAsync(Guid sessionId)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var thread = await scope.ServiceProvider.GetRequiredService<IChatThreadRepository>().FindByLiveSessionIdAsync(sessionId, Ct);
        return thread!.Messages.Last(m => m.Role == DomainChatMessage.AssistantRole);
    }

    /// <summary>
    /// #4456: il costo del turno va al database con la risposta. Un messaggio salvato prima non ha la chiave nel
    /// JSON: si legge senza errori, e il suo costo è assente, non zero.
    /// </summary>
    [Fact]
    public async Task TheCostOfATurn_IsSavedWithItsAnswer_AndAnOlderMessageHasNone()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(
            Costing(Calls("stato_partita", "{}"), 0.0012m, 100, 20),
            Costing(Says("Tocca a Marco."), 0.0008m, 150, 30));

        var (status, _, body) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        status.Should().Be(HttpStatusCode.OK, body);
        var saved = await SavedAnswerAsync(table.SessionId);
        saved.CostUsd.Should().Be(0.0020m);
        saved.TokenCount.Should().Be(300);

        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            const string costKey = ",\"CostUsd\":[^,}]*";
            await db.Database.ExecuteSqlAsync(
                $"UPDATE \"ChatThreads\" SET \"MessagesJson\" = regexp_replace(\"MessagesJson\", {costKey}, '', 'g') WHERE \"LiveSessionId\" = {table.SessionId}",
                Ct);
            var json = await db.ChatThreads.AsNoTracking()
                .Where(t => t.LiveSessionId == table.SessionId).Select(t => t.MessagesJson).SingleAsync(Ct);
            json.Should().NotContain("CostUsd", "il messaggio deve essere come uno salvato prima di #4456");
        }

        var older = await SavedAnswerAsync(table.SessionId);
        older.CostUsd.Should().BeNull();
        older.TokenCount.Should().Be(300);
    }
}
