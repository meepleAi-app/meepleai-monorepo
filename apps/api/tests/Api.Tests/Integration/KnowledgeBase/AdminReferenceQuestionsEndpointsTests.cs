using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>La fixture di <see cref="AdminReferenceQuestionsEndpointsTests"/>: l'host vero, nessun doppio.</summary>
public sealed class AdminReferenceQuestionsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "ref_q_api");

/// <summary>
/// #4470 (ADR-095 fetta 7b, D8): l'elenco in sola lettura delle domande di riferimento, nella sezione
/// Qualità dell'admin. Si filtra per gioco, scelto per nome dall'elenco dei giochi, e per gate.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4470")]
public sealed class AdminReferenceQuestionsEndpointsTests
    : IClassFixture<AdminReferenceQuestionsHostFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/admin/kb/reference-questions";

    private readonly AdminReferenceQuestionsHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _adminToken = null!;
    private Guid _catan;
    private Guid _europe;

    public AdminReferenceQuestionsEndpointsTests(AdminReferenceQuestionsHostFixture hostFixture)
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
        _catan = await TestSessionHelper.SeedSharedGameAsync(db, "Catan");
        _europe = await TestSessionHelper.SeedSharedGameAsync(db, "Ticket to Ride: Europe");

        var now = DateTime.UtcNow;
        db.ReferenceQuestions.AddRange(
            ReferenceQuestion.Create("CAT-02", Content(_catan, "Più carte Sviluppo in un turno?", inGate: true), now),
            ReferenceQuestion.Create("CAT-01", Content(_catan, "Posso commerciare fuori turno?", outOfCorpus: true), now),
            ReferenceQuestion.Create("TTR-18", Content(_europe, "I biglietti tenuti si scartano?"), now));
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnAdmin_SeesEveryQuestionWithItsGame_AndTheGamesWithHowManyQuestionsTheyHave()
    {
        using var body = await OkJsonAsync(await SendAsync(Route, _adminToken));

        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("code").GetString()).Should().Equal("CAT-01", "CAT-02", "TTR-18");

        var first = items[0];
        first.GetProperty("gameId").GetGuid().Should().Be(_catan);
        first.GetProperty("gameName").GetString().Should().Be("Catan");
        first.GetProperty("question").GetString().Should().Be("Posso commerciare fuori turno?");
        first.GetProperty("expectedAnswer").GetString().Should().Be("Risposta attesa.");
        first.GetProperty("category").GetString().Should().Be("Commercio");
        first.GetProperty("difficulty").GetString().Should().Be("Medium");
        first.GetProperty("questionType").GetString().Should().Be("Procedurale");
        first.GetProperty("sourceReference").GetString().Should().Be("Manuale - Commercio");
        first.GetProperty("outOfCorpus").GetBoolean().Should().BeTrue();
        first.GetProperty("inGate").GetBoolean().Should().BeFalse();

        body.RootElement.GetProperty("games").EnumerateArray()
            .Select(g => (g.GetProperty("gameId").GetGuid(), g.GetProperty("gameName").GetString(), g.GetProperty("questionCount").GetInt32()))
            .Should().Equal((_catan, "Catan", 2), (_europe, "Ticket to Ride: Europe", 1));
    }

    [Fact]
    public async Task TheList_FiltersByGame_AndByGate_WhileTheGamesStayAll()
    {
        using var europe = await OkJsonAsync(await SendAsync($"{Route}?gameId={_europe}", _adminToken));
        using var inGate = await OkJsonAsync(await SendAsync($"{Route}?inGate=true", _adminToken));
        using var outOfGate = await OkJsonAsync(await SendAsync($"{Route}?gameId={_catan}&inGate=false", _adminToken));

        Codes(europe).Should().Equal("TTR-18");
        Codes(inGate).Should().Equal("CAT-02");
        Codes(outOfGate).Should().Equal("CAT-01");
        europe.RootElement.GetProperty("games").GetArrayLength().Should().Be(2,
            "i giochi servono a scegliere il filtro: non si restringono con il filtro");
    }

    [Fact]
    public async Task AQuestionWhoseGameWasSoftDeleted_StillShowsTheGameTitle()
    {
        // Il seeder non risolve più un gioco cancellato in modo logico, ma le domande già caricate restano
        // legate a lui: l'admin deve poter capire di quale gioco sono (review di #4495).
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.SharedGames.IgnoreQueryFilters()
                .Where(g => g.Id == _europe)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsDeleted, true), Ct);
        }

        using var body = await OkJsonAsync(await SendAsync(Route, _adminToken));

        body.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == "TTR-18")
            .GetProperty("gameName").GetString().Should().Be("Ticket to Ride: Europe");
        body.RootElement.GetProperty("games").EnumerateArray()
            .Single(g => g.GetProperty("gameId").GetGuid() == _europe)
            .GetProperty("gameName").GetString().Should().Be("Ticket to Ride: Europe");
    }

    [Fact]
    public async Task AUserWhoIsNotAnAdmin_CannotReadTheQuestions()
    {
        string userToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, userToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var response = await SendAsync(Route, userToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static ReferenceQuestionContent Content(
        Guid gameId, string question, bool outOfCorpus = false, bool inGate = false) =>
        new(gameId, question, "Risposta attesa.", "Commercio", "Medium", "Procedurale",
            "Manuale - Commercio", outOfCorpus, inGate);

    private static List<string?> Codes(JsonDocument body) =>
        body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ToList();

    private async Task<HttpResponseMessage> SendAsync(string url, string token) =>
        await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, url, token), Ct);

    private static async Task<JsonDocument> OkJsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
        return JsonDocument.Parse(raw);
    }
}
