using System.Net;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using static Api.Tests.Integration.KnowledgeBase.SessionAssistant.ScriptedChatGateway;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// #4372 (A4a) — <c>GET /live-sessions/{id}/assistant</c>: ciò che il pannello mostra al montaggio e dopo un
/// ricaricamento. Se l'assistente risponde (con lo stesso codice del turno, se no), la conversazione e le
/// proposte. Dalla richiesta HTTP al database, con il gateway LLM a copione.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4372")]
public sealed class SessionAssistantPanelEndpointTests(SessionAssistantHostFixture host)
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
        host.WriteGate.Reset();
        _client = host.Factory.CreateClient();
        (_owner, _token) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<LiveTable> StartedTableAsync() =>
        SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: true, Ct, "Marco", "Giulia");

    private async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> GetPanelAsync(Guid sessionId, string token)
    {
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/live-sessions/{sessionId}/assistant?language=it", token), Ct);
        var raw = await response.Content.ReadAsStringAsync(Ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, default, raw);
        }

        using var document = JsonDocument.Parse(raw);
        return (response.StatusCode, document.RootElement.Clone(), raw);
    }

    [Fact]
    public async Task AfterATurn_ThePanelReturnsTheConversation_AndTheOpenProposalOfTheAnswer()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(
            Calls("proponi_punteggio", """{"giocatore":"Marco","punti":5}"""),
            Says("Ho preparato la proposta: +5 a Marco."));
        var (askStatus, events, askBody) = await SessionAssistantTestData.AskAsync(
            _client, _token, table.SessionId, "Marco fa 5 punti", Ct);
        askStatus.Should().Be(HttpStatusCode.OK, askBody);
        var proposalId = events.Single(e => e.GetProperty("type").GetString() == "proposal")
            .GetProperty("proposal").GetProperty("id").GetGuid();

        // Il ricaricamento della pagina.
        var (status, panel, raw) = await GetPanelAsync(table.SessionId, _token);

        status.Should().Be(HttpStatusCode.OK, raw);
        panel.GetProperty("availability").GetProperty("enabled").GetBoolean().Should().BeTrue();
        panel.GetProperty("availability").GetProperty("code").ValueKind.Should().Be(JsonValueKind.Null);

        var messages = panel.GetProperty("messages").EnumerateArray().ToList();
        messages.Select(m => m.GetProperty("role").GetString()).Should().Equal("user", "assistant");
        messages[0].GetProperty("content").GetString().Should().Be("Marco fa 5 punti");
        messages[1].GetProperty("content").GetString().Should().Contain("+5 a Marco");

        var proposal = panel.GetProperty("proposals").EnumerateArray().Should().ContainSingle().Subject;
        proposal.GetProperty("id").GetGuid().Should().Be(proposalId);
        proposal.GetProperty("status").GetString().Should().Be("Pending");
        proposal.GetProperty("kind").GetString().Should().Be("Score");
        proposal.GetProperty("resultingValue").GetInt32().Should().Be(5);
        proposal.GetProperty("messageId").GetGuid().Should().Be(
            messages[1].GetProperty("id").GetGuid(), "la card sta sotto il messaggio dell'assistente che l'ha prodotta");
    }

    /// <summary>
    /// #4397: il turno fallito resta nella conversazione che vede l'host, e non torna al modello. Il segno
    /// passa dal database: il secondo turno rilegge il thread salvato dal primo.
    /// </summary>
    [Fact]
    public async Task AFailedTurn_StaysInThePanel_ButDoesNotGoBackToTheModel()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset();   // copione vuoto: il gateway fallisce
        var (failedStatus, failed, failedBody) = await SessionAssistantTestData.AskAsync(
            _client, _token, table.SessionId, "Marco fa 5 punti", Ct);
        failedStatus.Should().Be(HttpStatusCode.OK, failedBody);
        failed[^1].GetProperty("code").GetString().Should().Be("assistant_unavailable");

        host.Llm.Reset(Says("Tocca a Marco."));
        var (askStatus, _, askBody) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);

        askStatus.Should().Be(HttpStatusCode.OK, askBody);
        var sent = host.Llm.Requests.Single().Messages;
        sent.Select(m => m.Role).Should().Equal("system", "system", "user");
        sent[2].Content.Should().Be("a chi tocca?");

        var (status, panel, raw) = await GetPanelAsync(table.SessionId, _token);
        status.Should().Be(HttpStatusCode.OK, raw);
        var messages = panel.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("content").GetString()).ToList();
        messages.Should().HaveCount(4);
        messages[0].Should().Be("Marco fa 5 punti");
        messages[1].Should().StartWith("L'assistente non è disponibile", "l'host vede ancora il turno fallito");
        messages[3].Should().Be("Tocca a Marco.");
    }

    [Fact]
    public async Task WithTheAssistantOff_ThePanelSaysSo_WithTheCodeTheTurnWouldReturn()
    {
        await SessionAssistantTestData.DisableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();

        var (status, panel, raw) = await GetPanelAsync(table.SessionId, _token);

        status.Should().Be(HttpStatusCode.OK, raw);
        panel.GetProperty("availability").GetProperty("enabled").GetBoolean().Should().BeFalse();
        panel.GetProperty("availability").GetProperty("code").GetString().Should().Be("assistant_disabled");
        panel.GetProperty("messages").GetArrayLength().Should().Be(0, "senza turni non c'è un thread");
        panel.GetProperty("proposals").GetArrayLength().Should().Be(0);

        // Lo stesso codice che dà il turno: la regola è una sola.
        var (askStatus, _, askBody) = await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);
        askStatus.Should().Be(HttpStatusCode.Conflict);
        askBody.Should().Contain("assistant_disabled");
    }

    [Fact]
    public async Task AfterTheGameIsOver_ThePanelSaysSessionStateInvalid_AndKeepsTheConversation()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var table = await StartedTableAsync();
        host.Llm.Reset(Says("Tocca a Marco."));
        await SessionAssistantTestData.AskAsync(_client, _token, table.SessionId, "a chi tocca?", Ct);
        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(table.SessionId), Ct);

        var (status, panel, raw) = await GetPanelAsync(table.SessionId, _token);

        status.Should().Be(HttpStatusCode.OK, raw);
        panel.GetProperty("availability").GetProperty("enabled").GetBoolean().Should().BeFalse();
        panel.GetProperty("availability").GetProperty("code").GetString().Should().Be("session_state_invalid");
        panel.GetProperty("messages").GetArrayLength().Should().Be(2, "la conversazione resta leggibile a partita finita");
    }

    [Fact]
    public async Task AParticipantWhoIsNotTheOwner_IsForbidden()
    {
        await SessionAssistantTestData.EnableAssistantAsync(host.Factory, Ct);
        var (guest, guestToken) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, _owner, start: false, Ct, "Marco");
        await SessionAssistantTestData.SendAsync(host.Factory, new AddPlayerToLiveSessionCommand(
            table.SessionId, "Giulia", Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor.Blue, UserId: guest), Ct);

        var (status, _, raw) = await GetPanelAsync(table.SessionId, guestToken);

        status.Should().Be(HttpStatusCode.Forbidden, raw);
    }

    [Fact]
    public async Task ASessionThatDoesNotExist_IsNotFound()
    {
        var (status, _, raw) = await GetPanelAsync(Guid.NewGuid(), _token);

        status.Should().Be(HttpStatusCode.NotFound, raw);
    }
}
