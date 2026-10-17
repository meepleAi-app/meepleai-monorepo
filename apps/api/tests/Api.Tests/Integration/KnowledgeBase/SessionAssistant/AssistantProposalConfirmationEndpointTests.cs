using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using MediatR;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Api.Tests.Integration.KnowledgeBase.SessionAssistant.ScriptedChatGateway;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Spec 2026-10-11 §5.3 e §7.2, riga «Conferma», su Postgres vero: una conferma scrive la cella giusta;
/// una proposta superata non scrive; chi non è il proprietario non conferma; una sessione conclusa
/// rifiuta; più tap insieme si applicano una volta sola.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class AssistantProposalConfirmationEndpointTests(SessionAssistantHostFixture host)
    : IClassFixture<SessionAssistantHostFixture>, IAsyncLifetime
{
    private const string ScoreConfirmed = "ScoreConfirmed";

    private HttpClient _client = null!;
    private Guid _owner;
    private string _token = null!;
    private LiveTable _table = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Ogni test parte da una partita avviata con tre giocatori, Marco a 7 punti nel primo round, e l'assistente abilitato.</summary>
    public async ValueTask InitializeAsync()
    {
        await host.BeginTestAsync();
        host.Llm.Reset();
        host.WriteGate.Reset();
        _client = host.Factory.CreateClient();
        (_owner, _token) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        _table = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Marco", "Giulia", "Luca");
        await SessionAssistantTestData.SendAsync(
            host.Factory, new RecordLiveSessionScoreCommand(_table.SessionId, _table.Players["Marco"], 1, "punti", 7), Ct);
        host.Hub.Reset();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    /// <summary>Un turno dell'assistente che lascia una proposta: restituisce il suo id.</summary>
    private async Task<Guid> ProposeAsync(string tool, string argumentsJson, string question, Guid? sessionId = null)
    {
        host.Llm.Reset(Calls(tool, argumentsJson), Says("Proposta pronta: confermala dalla card."));
        var (status, events, body) = await SessionAssistantTestData.AskAsync(
            _client, _token, sessionId ?? _table.SessionId, question, Ct);
        status.Should().Be(HttpStatusCode.OK, body);
        return events.Single(e => e.GetProperty("type").GetString() == "proposal")
            .GetProperty("proposal").GetProperty("id").GetGuid();
    }

    private Task<Guid> ProposeFivePointsToMarcoAsync() =>
        ProposeAsync("proponi_punteggio", """{"giocatore":"Marco","punti":5}""", "Marco fa 5 punti");

    private Task<Guid> ProposeTheTurnAsync() => ProposeAsync("proponi_turno", "{}", "passa il turno");

    private async Task<(HttpStatusCode Status, string Body)> PostAsync(
        string action, Guid proposalId, object? body = null, string? token = null, Guid? sessionId = null)
    {
        var uri = $"/api/v1/live-sessions/{sessionId ?? _table.SessionId}/assistant/proposals/{proposalId}/{action}";
        var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, uri, token ?? _token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await _client.SendAsync(request, Ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// Porta la chiusura della partita all'handler dell'assistente, preso dal contenitore vero: così si prova che
    /// MediatR lo registra per <see cref="LiveSessionCompletedEvent"/>. In ambiente Testing l'host non registra il
    /// processore dell'outbox, che su staging consegna l'evento (la stessa consegna che genera il PlayRecord), quindi
    /// qui nessun handler di eventi parte da solo.
    /// </summary>
    private async Task DeliverTheCloseAsync(Guid sessionId)
    {
        var closed = await SessionAssistantTestData.SendAsync(host.Factory, new GetLiveSessionQuery(sessionId), Ct);
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<INotificationHandler<LiveSessionCompletedEvent>>()
            .OfType<ExpireAssistantProposalsOnSessionCompletedHandler>()
            .Should().ContainSingle("l'handler dell'assistente è registrato per la chiusura").Subject;
        await handler.Handle(
            new LiveSessionCompletedEvent(
                sessionId, closed.CompletedAt!.Value, closed.CurrentTurnIndex, closed.GameId, closed.GameName,
                _owner, PlayRecordVisibility.Private, groupId: null, closed.CreatedAt, closed.StartedAt, notes: null,
                players: [], scores: []),
            Ct);
    }

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement.Clone();

    private Task<LiveSessionDto> SessionAsync() =>
        SessionAssistantTestData.SendAsync(host.Factory, new GetLiveSessionQuery(_table.SessionId), Ct);

    private async Task<int> MarcoFirstRoundAsync() =>
        (await SessionAsync()).RoundScores
            .Single(s => s.PlayerId == _table.Players["Marco"] && s.Round == 1 && s.Dimension == "punti").Value;

    private async Task<AssistantProposal> StoredAsync(Guid proposalId)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetReadOnlyAsync(proposalId, Ct))!;
    }

    // ─── La conferma scrive ─────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_WritesTheCellPlusTheDelta_AndTellsTheParticipants()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.OK, body);
        var proposal = Json(body);
        proposal.GetProperty("status").GetString().Should().Be("Accepted");
        proposal.GetProperty("cellValueBefore").GetInt32().Should().Be(7);
        proposal.GetProperty("resultingValue").GetInt32().Should().Be(12);
        (await MarcoFirstRoundAsync()).Should().Be(12, "RecordScore sostituisce la cella: 7 più l'incremento");
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
        var stored = await StoredAsync(proposalId);
        stored.Status.Should().Be(AssistantProposalStatus.Accepted);
        stored.ResolvedBy.Should().Be(_owner);
    }

    [Fact]
    public async Task Confirm_WithACorrection_WritesTheCorrectedDelta_AndKeepsBoth()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (status, body) = await PostAsync("confirm", proposalId, new { points = 3 });

        status.Should().Be(HttpStatusCode.OK, body);
        Json(body).GetProperty("status").GetString().Should().Be("Edited");
        (await MarcoFirstRoundAsync()).Should().Be(10);
        var stored = await StoredAsync(proposalId);
        stored.Payload.Points.Should().Be(5, "la proposta originale resta leggibile");
        stored.ConfirmedPayload!.Points.Should().Be(3);
    }

    [Fact]
    public async Task Confirm_ATurnProposal_AdvancesTheTurn()
    {
        var proposalId = await ProposeTheTurnAsync();

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.OK, body);
        var session = await SessionAsync();
        session.CurrentTurnIndex.Should().Be(2);
        session.CurrentTurnPlayerId.Should().Be(_table.Players["Giulia"]);
    }

    // ─── La conferma non scrive ─────────────────────────────────────────────────

    /// <summary>Review Focus 4, sul database vero.</summary>
    [Fact]
    public async Task Confirm_WhenTheCellChanged_IsStale_AndDoesNotWrite()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        await SessionAssistantTestData.SendAsync(
            host.Factory, new RecordLiveSessionScoreCommand(_table.SessionId, _table.Players["Marco"], 1, "punti", 9), Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_stale");
        (await MarcoFirstRoundAsync()).Should().Be(9, "resta ciò che ha scritto l'host a mano");
        host.Hub.Count(ScoreConfirmed).Should().Be(0);
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Stale);
    }

    [Fact]
    public async Task Confirm_ATurnProposal_WhenTheTurnAlreadyMoved_IsStale()
    {
        var proposalId = await ProposeTheTurnAsync();
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(_table.SessionId), Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_stale");
        (await SessionAsync()).CurrentTurnIndex.Should().Be(2, "il turno è passato una volta sola, a mano");
    }

    [Fact]
    public async Task Confirm_ByAParticipantWhoIsNotTheOwner_IsForbidden()
    {
        var (guest, guestToken) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        await SessionAssistantTestData.SendAsync(
            host.Factory, new AddPlayerToLiveSessionCommand(_table.SessionId, "Ospite", PlayerColor.Yellow, UserId: guest), Ct);
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (confirm, confirmBody) = await PostAsync("confirm", proposalId, token: guestToken);
        var (reject, rejectBody) = await PostAsync("reject", proposalId, token: guestToken);

        confirm.Should().Be(HttpStatusCode.Forbidden, confirmBody);
        reject.Should().Be(HttpStatusCode.Forbidden, rejectBody);
        (await MarcoFirstRoundAsync()).Should().Be(7);
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Pending);
    }

    /// <summary>
    /// #4455: a partita chiusa nessuna card agisce. Prima la conferma prenotava la proposta, il comando della
    /// sessione la rifiutava e la proposta finiva Failed, contata come un'applicazione fallita. Gli eventi di
    /// dominio non vengono consegnati: è la finestra in cui l'outbox non ha ancora portato la chiusura
    /// all'handler, e la proposta la fa scadere la guardia della conferma.
    /// </summary>
    [Fact]
    public async Task Confirm_OnACompletedSession_IsNotPending_AndTheProposalHasExpired()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(_table.SessionId), Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_not_pending").And.NotContain("session_state_invalid");
        var stored = await StoredAsync(proposalId);
        stored.Status.Should().Be(AssistantProposalStatus.Expired);
        stored.FailureCode.Should().BeNull();
        (await MarcoFirstRoundAsync()).Should().Be(7);
    }

    /// <summary>#4455: la chiusura fa scadere le proposte in attesa della sua partita, non quelle di un'altra.</summary>
    [Fact]
    public async Task TheClose_ExpiresThePendingProposalsOfItsSession_NotThoseOfAnother()
    {
        var closing = await ProposeFivePointsToMarcoAsync();
        var other = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Anna", "Bruno");
        var stillOpen = await ProposeAsync(
            "proponi_punteggio", """{"giocatore":"Anna","punti":3}""", "Anna fa 3 punti", other.SessionId);

        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(_table.SessionId), Ct);
        await DeliverTheCloseAsync(_table.SessionId);

        var expired = await StoredAsync(closing);
        expired.Status.Should().Be(AssistantProposalStatus.Expired, "nessuno l'ha toccata: l'ha chiusa la fine della partita");
        expired.ResolvedAt.Should().NotBeNull();
        (await StoredAsync(stillOpen)).Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Fact]
    public async Task Confirm_WithTheIdOfAnotherSession_IsNotFound()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        var other = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Anna");

        var (status, body) = await PostAsync("confirm", proposalId, sessionId: other.SessionId);

        status.Should().Be(HttpStatusCode.NotFound, body);
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Pending);
    }

    // ─── I tap concorrenti (Review Focus 3) ─────────────────────────────────────

    /// <summary>
    /// Quante conferme partono insieme. Con due le richieste non si sovrappongono abbastanza: tolto il
    /// token di concorrenza da xmin, questi due test restavano verdi (tre esecuzioni su tre). Con otto
    /// diventano rossi alla prima, perché più richieste leggono la proposta ancora Pending.
    /// </summary>
    private const int ConcurrentTaps = 8;

    [Fact]
    public async Task ConcurrentConfirms_OfAnAdvanceTurn_AdvanceOnce()
    {
        var proposalId = await ProposeTheTurnAsync();

        var taps = await Task.WhenAll(Enumerable.Range(0, ConcurrentTaps).Select(_ => PostAsync("confirm", proposalId)));

        taps.Should().OnlyContain(tap => tap.Status == HttpStatusCode.OK, "ogni tap dopo il primo riceve lo stato attuale, non un errore");
        (await SessionAsync()).CurrentTurnIndex.Should().Be(2, "il turno è passato una volta sola");
        (await StoredAsync(proposalId)).Status.Should().Be(AssistantProposalStatus.Accepted);
    }

    /// <summary>
    /// Su un punteggio la doppia scrittura non si vede dalla cella, perché <c>RecordScore</c> sostituisce: 7 più 5
    /// fa 12 anche se la scrittura parte due volte. Si vede dagli eventi trasmessi ai partecipanti.
    /// </summary>
    [Fact]
    public async Task ConcurrentConfirms_OfAScore_WriteOnce_CountedOnTheBroadcast()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var taps = await Task.WhenAll(Enumerable.Range(0, ConcurrentTaps).Select(_ => PostAsync("confirm", proposalId)));

        taps.Should().OnlyContain(tap => tap.Status == HttpStatusCode.OK);
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
        (await MarcoFirstRoundAsync()).Should().Be(12);
    }

    [Fact]
    public async Task ASecondConfirm_AfterTheFirst_ReturnsTheSameState_AndWritesNothingMore()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        await PostAsync("confirm", proposalId);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.OK, body);
        Json(body).GetProperty("status").GetString().Should().Be("Accepted");
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
    }

    // ─── Il rifiuto ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_ClosesTheProposal_AndItCanNoLongerBeConfirmed()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();

        var (reject, rejectBody) = await PostAsync("reject", proposalId);
        var (confirm, confirmBody) = await PostAsync("confirm", proposalId);

        reject.Should().Be(HttpStatusCode.OK, rejectBody);
        Json(rejectBody).GetProperty("status").GetString().Should().Be("Rejected");
        confirm.Should().Be(HttpStatusCode.Conflict, confirmBody);
        confirmBody.Should().Contain("proposal_not_pending");
        (await MarcoFirstRoundAsync()).Should().Be(7);
        host.Hub.Count(ScoreConfirmed).Should().Be(0);
    }

    [Fact]
    public async Task AProposalExpiredByTheNextTurn_CanNoLongerBeConfirmed()
    {
        var proposalId = await ProposeFivePointsToMarcoAsync();
        host.Llm.Reset(Says("Tocca a Marco."));
        await SessionAssistantTestData.AskAsync(_client, _token, _table.SessionId, "a chi tocca?", Ct);

        var (status, body) = await PostAsync("confirm", proposalId);

        status.Should().Be(HttpStatusCode.Conflict, body);
        body.Should().Contain("proposal_not_pending");
        (await MarcoFirstRoundAsync()).Should().Be(7);
    }

    // ─── Più proposte uguali (spec §7.1) ────────────────────────────────────────

    /// <summary>
    /// Il modello ripete la stessa chiamata e lascia due card uguali. Il sink non le unisce (le conta il
    /// set di valutazione), quindi a proteggere la partita è lo stato atteso: confermata la prima, la cella
    /// non è più quella che la seconda si aspettava.
    /// </summary>
    [Fact]
    public async Task OfSeveralIdenticalProposals_OnlyOneCanBeConfirmed()
    {
        host.Llm.Reset(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Proposta pronta: confermala dalla card."));
        var (asked, events, askedBody) = await SessionAssistantTestData.AskAsync(_client, _token, _table.SessionId, "Marco fa 5 punti", Ct);
        asked.Should().Be(HttpStatusCode.OK, askedBody);
        var proposals = events
            .Where(e => e.GetProperty("type").GetString() == "proposal")
            .Select(e => e.GetProperty("proposal").GetProperty("id").GetGuid())
            .ToList();
        proposals.Should().HaveCount(2, "il sink non unisce due chiamate uguali");

        var (first, firstBody) = await PostAsync("confirm", proposals[0]);
        var (second, secondBody) = await PostAsync("confirm", proposals[1]);

        first.Should().Be(HttpStatusCode.OK, firstBody);
        second.Should().Be(HttpStatusCode.Conflict, secondBody);
        secondBody.Should().Contain("proposal_stale");
        (await MarcoFirstRoundAsync()).Should().Be(12, "l'incremento è entrato una volta sola");
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
        (await StoredAsync(proposals[1])).Status.Should().Be(AssistantProposalStatus.Stale);
    }

    // ─── Due proposte diverse confermate una dentro l'altra ─────────────────────

    /// <summary>Le proposte lasciate da un turno in cui il modello chiama due volte uno strumento.</summary>
    private async Task<List<Guid>> ProposeTwiceAsync(string tool, string firstArguments, string secondArguments, string question)
    {
        host.Llm.Reset(Calls(tool, firstArguments), Calls(tool, secondArguments), Says("Proposte pronte: confermale dalle card."));
        var (status, events, body) = await SessionAssistantTestData.AskAsync(_client, _token, _table.SessionId, question, Ct);
        status.Should().Be(HttpStatusCode.OK, body);
        var proposals = events
            .Where(e => e.GetProperty("type").GetString() == "proposal")
            .Select(e => e.GetProperty("proposal").GetProperty("id").GetGuid())
            .ToList();
        proposals.Should().HaveCount(2);
        return proposals;
    }

    /// <summary>
    /// Dalla review dell'intero branch: il controllo dello stato atteso e la scrittura erano due letture
    /// distinte. La conferma della seconda proposta supera il suo controllo e si ferma un attimo prima di
    /// scrivere; nel mezzo passa, intera, la conferma della prima. Il turno deve passare una volta sola.
    /// </summary>
    [Fact]
    public async Task TwoTurnProposals_ConfirmedAcrossEachOther_AdvanceOnce()
    {
        var proposals = await ProposeTwiceAsync("proponi_turno", "{}", "{}", "passa il turno");

        var held = host.WriteGate.HoldNextWrite();
        var late = PostAsync("confirm", proposals[1]);
        await held.Entered.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        var (first, firstBody) = await PostAsync("confirm", proposals[0]);
        held.Release();
        var (second, secondBody) = await late;

        first.Should().Be(HttpStatusCode.OK, firstBody);
        second.Should().Be(HttpStatusCode.Conflict, secondBody);
        secondBody.Should().Contain("proposal_stale");
        (await SessionAsync()).CurrentTurnIndex.Should().Be(2, "il turno è passato una volta sola");
        (await StoredAsync(proposals[0])).Status.Should().Be(AssistantProposalStatus.Accepted);
        (await StoredAsync(proposals[1])).Status.Should().Be(AssistantProposalStatus.Stale);
    }

    /// <summary>
    /// Lo stesso incrocio su una cella: «+5» e «+3» a Marco nello stesso round. Senza il controllo dentro
    /// la scrittura vince l'ultima, e un incremento sparisce con tutte e due le card verdi.
    /// </summary>
    [Fact]
    public async Task TwoScoreProposalsOnTheSameCell_ConfirmedAcrossEachOther_KeepTheFirstAndRefuseTheSecond()
    {
        var proposals = await ProposeTwiceAsync(
            "proponi_punteggio", """{"giocatore":"Marco","punti":5}""", """{"giocatore":"Marco","punti":3}""", "Marco fa 5 punti e 3 di bonus");

        var held = host.WriteGate.HoldNextWrite();
        var late = PostAsync("confirm", proposals[1]);
        await held.Entered.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        var (first, firstBody) = await PostAsync("confirm", proposals[0]);
        held.Release();
        var (second, secondBody) = await late;

        first.Should().Be(HttpStatusCode.OK, firstBody);
        second.Should().Be(HttpStatusCode.Conflict, secondBody);
        secondBody.Should().Contain("proposal_stale");
        (await MarcoFirstRoundAsync()).Should().Be(12, "resta l'incremento confermato per primo: il secondo non lo sovrascrive");
        host.Hub.Count(ScoreConfirmed).Should().Be(1);
        (await StoredAsync(proposals[1])).Status.Should().Be(AssistantProposalStatus.Stale);
    }
}
