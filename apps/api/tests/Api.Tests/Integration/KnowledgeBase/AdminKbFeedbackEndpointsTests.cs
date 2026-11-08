using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>La fixture di <see cref="AdminKbFeedbackEndpointsTests"/>: l'host vero, nessun doppio.</summary>
public sealed class AdminKbFeedbackHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "kb_feedback");

/// <summary>
/// ADR-095 fetta 7a (#4469): i 👍👎 si leggono per versione del profilo, e senza digitare l'UUID del
/// gioco. Da #4168 ogni feedback porta la versione della risposta che valuta, ma nessuno la leggeva:
/// la pagina admin chiedeva l'UUID del gioco e mostrava solo esito, id del messaggio, data e commento.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4469")]
public sealed class AdminKbFeedbackEndpointsTests : IClassFixture<AdminKbFeedbackHostFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/admin/kb/feedback";
    private const string Answer = "Ogni giocatore pesca cinque carte durante la preparazione.";

    private readonly AdminKbFeedbackHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _adminToken = null!;
    private Guid _userId;
    private Guid _gameId;

    public AdminKbFeedbackEndpointsTests(AdminKbFeedbackHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Coloni del feedback");
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Summary_CountsEachVersion_AndKeepsTheFeedbackWithoutAVersionApart()
    {
        var (previous, current) = await PublishANewVersionAsync();
        await SeedFeedbackAsync(current, "helpful", "helpful", "helpful", "not_helpful");
        await SeedFeedbackAsync(previous.Id, "not_helpful", "not_helpful");
        await SeedFeedbackAsync(null, "helpful");

        using var body = await OkJsonAsync(await SendAsync($"{Route}/summary", _adminToken));

        var versions = body.RootElement.GetProperty("versions").EnumerateArray().ToList();
        versions.Select(v => (Version: VersionNumberOf(v), Helpful: v.GetProperty("helpful").GetInt32(), NotHelpful: v.GetProperty("notHelpful").GetInt32()))
            .Should().Equal(
                (previous.Number + 1, 3, 1),
                (previous.Number, 0, 2),
                ((int?)null, 1, 0));
        versions[0].GetProperty("status").GetString().Should().Be("Published");
        versions[1].GetProperty("status").GetString().Should().Be("Archived");
        versions[2].GetProperty("profileVersionId").ValueKind.Should().Be(JsonValueKind.Null, "un feedback senza versione non va dentro una versione");

        body.RootElement.GetProperty("games").EnumerateArray()
            .Select(g => (g.GetProperty("gameId").GetGuid(), g.GetProperty("gameName").GetString()))
            .Should().Equal((_gameId, "Coloni del feedback"));
    }

    [Fact]
    public async Task List_FiltersByVersion_WithoutAGameId_AndShowsTheAnswerItJudges()
    {
        var (previous, current) = await PublishANewVersionAsync();
        var (threadId, messageId) = await SeedAnswerAsync(current);
        await SeedFeedbackAsync(current, threadId, messageId, "not_helpful", "Non risponde alla domanda");
        await SeedFeedbackAsync(previous.Id, "helpful"); // su un thread che non esiste
        await SeedFeedbackAsync(null, "helpful");

        using var byVersion = await OkJsonAsync(await SendAsync($"{Route}?version={current}", _adminToken));
        using var gone = await OkJsonAsync(await SendAsync($"{Route}?version={previous.Id}", _adminToken));
        using var unversioned = await OkJsonAsync(await SendAsync($"{Route}?version=none", _adminToken));
        using var all = await OkJsonAsync(await SendAsync($"{Route}?gameId={_gameId}", _adminToken));

        byVersion.RootElement.GetProperty("total").GetInt32().Should().Be(1);
        var item = byVersion.RootElement.GetProperty("items")[0];
        item.GetProperty("messageId").GetGuid().Should().Be(messageId);
        item.GetProperty("gameName").GetString().Should().Be("Coloni del feedback");
        item.GetProperty("versionNumber").GetInt32().Should().Be(previous.Number + 1);
        item.GetProperty("outcome").GetString().Should().Be("not_helpful");
        item.GetProperty("comment").GetString().Should().Be("Non risponde alla domanda");
        item.GetProperty("answerExcerpt").GetString().Should().Be(Answer);

        gone.RootElement.GetProperty("items")[0].GetProperty("answerExcerpt").ValueKind
            .Should().Be(JsonValueKind.Null, "il thread non esiste più: la riga resta, la risposta no");
        unversioned.RootElement.GetProperty("items").EnumerateArray()
            .Should().ContainSingle().Which.GetProperty("profileVersionId").ValueKind.Should().Be(JsonValueKind.Null);
        all.RootElement.GetProperty("total").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task List_RefusesAVersionThatIsNeitherAnIdNorNone()
    {
        var response = await SendAsync($"{Route}?version=latest", _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AUserWhoIsNotAnAdmin_CannotReadTheFeedback()
    {
        string userToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, userToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var summary = await SendAsync($"{Route}/summary", userToken);
        var list = await SendAsync(Route, userToken);

        summary.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static int? VersionNumberOf(JsonElement version) =>
        version.GetProperty("versionNumber").ValueKind == JsonValueKind.Null
            ? null
            : version.GetProperty("versionNumber").GetInt32();

    /// <summary>
    /// Pubblica una versione nuova del profilo: quella che era pubblicata diventa archiviata.
    /// </summary>
    private async Task<((Guid Id, int Number) Previous, Guid Current)> PublishANewVersionAsync()
    {
        (Guid, int) previous;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(Ct);
            previous = (profile!.Published.Id, profile.Published.VersionNumber);
        }

        await AgentProfilePublisher.PublishAsync(
            _hostFixture.Factory.Services, c => AgentProfilePublisher.WithFusionWeights(c, 0.6m, 0.4m), Ct);

        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(Ct);
            return (previous, profile!.Published.Id);
        }
    }

    private async Task<(Guid ThreadId, Guid MessageId)> SeedAnswerAsync(Guid profileVersionId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var thread = new ChatThread(Guid.NewGuid(), _userId, _gameId, "Feedback");
        thread.AddUserMessage("Quante carte pesca ogni giocatore?");
        thread.AddAssistantMessage(Answer, profileVersionId, "it");
        await scope.ServiceProvider.GetRequiredService<IChatThreadRepository>().AddAsync(thread, Ct);
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);
        return (thread.Id, thread.Messages.Single(m => m.IsAssistantMessage).Id);
    }

    private Task SeedFeedbackAsync(Guid? profileVersionId, params string[] outcomes) =>
        SeedRowsAsync(outcomes.Select(o => KbUserFeedback.Create(
            _userId, _gameId, Guid.NewGuid(), Guid.NewGuid(), o, comment: null, profileVersionId, "it"))
            .ToArray());

    private Task SeedFeedbackAsync(Guid profileVersionId, Guid threadId, Guid messageId, string outcome, string comment) =>
        SeedRowsAsync(KbUserFeedback.Create(_userId, _gameId, threadId, messageId, outcome, comment, profileVersionId, "it"));

    private async Task SeedRowsAsync(params KbUserFeedback[] rows)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.KbUserFeedbacks.AddRange(rows);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string token) =>
        await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, url, token), Ct);

    private static async Task<JsonDocument> OkJsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        return JsonDocument.Parse(raw);
    }
}
