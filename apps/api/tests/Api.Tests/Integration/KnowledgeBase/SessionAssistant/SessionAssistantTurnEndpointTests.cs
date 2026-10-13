using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Api.Tests.Integration.KnowledgeBase.SessionAssistant.ScriptedChatGateway;

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
}
