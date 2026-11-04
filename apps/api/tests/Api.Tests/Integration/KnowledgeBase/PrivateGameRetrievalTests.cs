using System.Net;
using System.Text.Json;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #4244: una domanda su un gioco PRIVATO trova i passaggi del suo PDF, con entrambi i
/// bracci del recupero, e cita il PDF.
/// </summary>
/// <remarks>
/// <para>
/// Il gioco privato sta in una colonna sua (<c>PrivateGameId</c>) su <c>text_chunks</c> e
/// <c>vector_documents</c>, perché <c>GameId</c> è una chiave esterna verso <c>shared_games</c>.
/// Chi legge «i documenti del gioco» deve guardare entrambe le colonne: il braccio lessicale, la
/// risoluzione del PDF per le citazioni, il controllo dei documenti ancora in elaborazione.
/// </para>
/// <para>
/// Su Postgres, via HTTP: il provider in memoria non applica chiavi esterne, ed è per questo che i
/// test precedenti su questo percorso passavano.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed class PrivateGameRetrievalTests
    : IClassFixture<AgentProfileFieldEffectHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";

    private const string BothArms = "At setup each player draws five cards and keeps them hidden.";
    private const string LexicalOnly = "Each player draws two extra cards at setup when the long variant is used.";
    private const string VectorOnly = "Deal a starting hand of five to everybody.";

    private readonly AgentProfileFieldEffectHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _privateGameId;
    private Guid _pdfId;

    public PrivateGameRetrievalTests(AgentProfileFieldEffectHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _hostFixture.Llm.Reset();
        _hostFixture.Reranker.Reset();
        _hostFixture.Cache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);

        _privateGameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = _privateGameId,
            OwnerId = _userId,
            Title = "Il mio prototipo",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.StreamingResponses",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4244)",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = _userId,
        });
        await db.SaveChangesAsync(Ct);

        _pdfId = await IndexPrivatePdfAsync(db, processingState: "Ready",
            (BothArms, QueryVectorEmbeddings.Axis(1f, 0f)),
            (LexicalOnly, QueryVectorEmbeddings.Axis(0f, 1f)),
            (VectorOnly, QueryVectorEmbeddings.Axis(1f, 0f)));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheOwner_GetsAnAnswerFromBothArms()
    {
        var events = await AskAsync(_token);

        ErrorsOf(events).Should().BeEmpty();
        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("il modello è stato interrogato")
            .And.Contain(BothArms)
            .And.Contain(LexicalOnly, "il braccio lessicale legge i passaggi del gioco privato")
            .And.Contain(VectorOnly, "il braccio vettoriale legge gli embedding del gioco privato");
    }

    /// <summary>
    /// La citazione porta l'id del PDF, che è ciò che il visualizzatore apre. Senza il documento
    /// vettoriale del gioco privato, la citazione ricadeva sull'id del vettore e il PDF non si apriva.
    /// </summary>
    [Fact]
    public async Task TheCitations_PointToThePdf()
    {
        var events = await AskAsync(_token);

        var citations = events
            .Where(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations)
            .SelectMany(e => e.GetProperty("data").GetProperty("citations").EnumerateArray())
            .ToList();
        citations.Should().NotBeEmpty();
        citations.Should().OnlyContain(c => c.GetProperty("source").GetString() == $"PDF:{_pdfId}");
    }

    /// <summary>
    /// Un PDF del gioco privato ancora in elaborazione ferma la domanda, come per un gioco del
    /// catalogo. Prima il controllo non vedeva i PDF privati e passava sempre.
    /// </summary>
    [Fact]
    public async Task APdfStillBeingProcessed_StopsTheQuestion()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Extracting");
        }

        var events = await AskAsync(_token);

        ErrorsOf(events).Should().Equal("DOCUMENT_PROCESSING");
    }

    /// <summary>
    /// #4408: un PDF fallito è finito, non «in lavorazione». Prima bloccava ogni domanda sul gioco
    /// con «N of M documents are still being processed» finché un admin non lo cancellava; ora la
    /// domanda risponde dai PDF pronti.
    /// </summary>
    [Fact]
    [Trait("Issue", "4408")]
    public async Task AFailedPdf_DoesNotStopTheQuestion()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Failed");
        }

        var events = await AskAsync(_token);

        ErrorsOf(events).Should().BeEmpty();
        _hostFixture.Llm.LastUserPrompt.Should().Contain(BothArms, "the answer comes from the PDF that is ready");
    }

    /// <summary>#4408, la domanda singola (<c>/knowledge-base/ask</c>): ha la sua copia del controllo.</summary>
    [Fact]
    [Trait("Issue", "4408")]
    public async Task AFailedPdf_DoesNotStopTheSingleQuestionEither()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Failed");
        }

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/knowledge-base/ask", _token,
            new { gameId = _privateGameId.ToString(), query = Question }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("answer").GetString().Should().NotContain("still being processed", body);
        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("the question reaches the model");
    }

    /// <summary>
    /// #4408: un PDF fallito non entra nel conteggio dei documenti in lavorazione. Con uno ancora in
    /// estrazione la domanda si ferma come prima, ma il messaggio conta solo quello.
    /// </summary>
    [Fact]
    [Trait("Issue", "4408")]
    public async Task AFailedPdf_IsNotCountedAmongThoseBeingProcessed()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Failed");
            await IndexPrivatePdfAsync(db, processingState: "Extracting");
        }

        var events = await AskAsync(_token);

        ErrorsOf(events).Should().Equal("DOCUMENT_PROCESSING");
        ErrorMessagesOf(events).Single().Should().StartWith("1 of 3 documents are still being processed");
    }

    /// <summary>
    /// #4408: se tutti i PDF del gioco sono falliti non c'è niente da cui rispondere, e il messaggio
    /// lo dice invece di promettere un'elaborazione che non arriverà.
    /// </summary>
    [Fact]
    [Trait("Issue", "4408")]
    public async Task OnlyFailedPdfs_SayTheyFailed_NotThatTheyAreBeingProcessed()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.PdfDocuments.Where(p => p.Id == _pdfId)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.ProcessingState, "Failed"), Ct);
        }

        var events = await AskAsync(_token);

        ErrorsOf(events).Should().Equal("DOCUMENTS_FAILED");
        ErrorMessagesOf(events).Single().Should().NotContain("still being processed");
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("la domanda si ferma prima del modello");
    }

    /// <summary>Il controllo d'accesso: il gioco privato di un altro non risponde.</summary>
    [Fact]
    public async Task AnotherUser_GetsNothingFromThePrivateGame()
    {
        string strangerToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var events = await AskAsync(strangerToken);

        ErrorsOf(events).Should().NotBeEmpty();
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("a un estraneo non si costruisce nessun prompt");
    }

    /// <summary>
    /// La domanda singola (<c>/knowledge-base/ask</c>) ha la sua copia del controllo «documenti in
    /// elaborazione». Guardava solo il catalogo: per un gioco privato non trovava PDF e rispondeva
    /// da un contenuto parziale.
    /// </summary>
    [Fact]
    public async Task APdfStillBeingProcessed_StopsTheSingleQuestionToo()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Extracting");
        }

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/knowledge-base/ask", _token,
            new { gameId = _privateGameId.ToString(), query = Question }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        // La rotta non espone il codice d'errore: il rifiuto si riconosce dal testo e dal fatto
        // che il modello non viene chiamato.
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("answer").GetString().Should().Contain("still being processed", body);
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("la domanda si ferma prima del modello");
    }

    /// <summary>
    /// <c>GET /games/{id}/knowledge-base</c> non controlla chi chiede: per un gioco del catalogo
    /// dice a chiunque se c'è una KB. Di un gioco privato non deve dire nulla a un estraneo — né
    /// che è indicizzato, né quanti documenti e passaggi ha.
    /// </summary>
    [Fact]
    public async Task AnotherUser_LearnsNothingAboutTheKbOfThePrivateGame()
    {
        string strangerToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/games/{_privateGameId}/knowledge-base", strangerToken), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("isIndexed").GetBoolean().Should().BeFalse(body);
        json.RootElement.GetProperty("documentCount").GetInt32().Should().Be(0, body);
    }

    /// <summary>
    /// Il braccio lessicale toglie dalla domanda le parole del titolo del gioco (#3768): dentro il
    /// corpus di quel gioco non distinguono nulla, e portano in alto copertina e colophon. Il titolo
    /// veniva cercato solo nel catalogo, quindi per un gioco privato restavano nella ricerca.
    /// </summary>
    [Fact]
    public async Task TheLexicalArm_KnowsTheTitleOfThePrivateGame()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var keywordSearch = scope.ServiceProvider.GetRequiredService<Api.Services.IKeywordSearchService>();

        var title = await keywordSearch.ResolveGameTitleAsync(_privateGameId, Ct);

        title.Should().Be("Il mio prototipo");
    }

    /// <summary>Un gioco privato cancellato non ha più un titolo da leggere.</summary>
    [Fact]
    public async Task TheLexicalArm_DoesNotReadTheTitleOfADeletedPrivateGame()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var deleted = await db.PrivateGames.AsTracking().SingleAsync(g => g.Id == _privateGameId, Ct);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(Ct);

        var keywordSearch = scope.ServiceProvider.GetRequiredService<Api.Services.IKeywordSearchService>();
        var title = await keywordSearch.ResolveGameTitleAsync(_privateGameId, Ct);

        title.Should().BeNull();
    }

    // ─── Le altre rotte che restituiscono i passaggi di un gioco (#4294) ───

    /// <summary>
    /// La spiegazione di un argomento cerca nella KB del gioco e ne restituisce i passaggi. Al
    /// proprietario arrivano quelli del suo PDF: è la prova che questa rotta legge il gioco privato,
    /// e quindi che il rifiuto del test successivo protegge qualcosa.
    /// </summary>
    [Fact]
    public async Task Explain_GivesTheOwnerThePassagesOfThePrivatePdf()
    {
        var response = await PostExplainAsync(_token);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var json = JsonDocument.Parse(body);
        var passages = json.RootElement.GetProperty("citations").EnumerateArray()
            .Select(c => c.GetProperty("text").GetString())
            .ToList();
        passages.Should().Contain(BothArms);
    }

    [Fact]
    public async Task Explain_RefusesAnotherUser()
    {
        var response = await PostExplainAsync(await CreateStrangerAsync());

        await AssertRefusedAsync(response);
    }

    /// <summary>Il suggerimento di mossa restituisce i passaggi in <c>sources</c>: stesso cancello.</summary>
    [Fact]
    public async Task PlayerModeSuggest_RefusesAnotherUser()
    {
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/player-mode/suggest", await CreateStrangerAsync(),
            new
            {
                gameId = _privateGameId.ToString(),
                gameState = new { players = new[] { new { name = "Anna", score = 3 } } },
                query = Question,
            }), Ct);

        await AssertRefusedAsync(response);
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("a un estraneo non si costruisce nessun prompt");
    }

    private Task<HttpResponseMessage> PostExplainAsync(string token) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/explain", token,
            new { gameId = _privateGameId.ToString(), topic = "setup" }), Ct);

    private async Task<string> CreateStrangerAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        return token;
    }

    private async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetString().Should().Be("RAG_OWNERSHIP_REQUIRED");
        body.Should().NotContain(BothArms).And.NotContain(LexicalOnly).And.NotContain(VectorOnly);
    }

    // ─── Il thread sul gioco privato (#4244, consegna B) ───
    // `ChatThreads.GameId` è una chiave esterna verso il catalogo: il gioco privato sta nella colonna
    // sua. Per chi legge il thread il gioco resta uno, condiviso o privato che sia.

    private const string ThreadSecret = "ZAFFIRO-7731";

    [Fact]
    public async Task TheOwner_CanOpenAThreadOnThePrivateGame()
    {
        var response = await PostThreadAsync(_token);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("gameId").GetGuid().Should().Be(_privateGameId, "il gioco del thread, come lo legge il client");
        json.RootElement.GetProperty("privateGameId").GetGuid().Should().Be(_privateGameId, "e il client deve sapere che è privato");

        var row = await ThreadRowAsync(json.RootElement.GetProperty("id").GetGuid());
        row.GameId.Should().BeNull("GameId è una chiave esterna verso shared_games");
        row.PrivateGameId.Should().Be(_privateGameId);
    }

    /// <summary>Il gioco privato di un altro non dice nemmeno di esistere (#4230).</summary>
    [Fact]
    public async Task AnotherUser_CannotOpenAThreadOnThePrivateGame()
    {
        var response = await PostThreadAsync(await CreateStrangerAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task AQuestionInTheThread_UsesItsHistoryAndThePrivatePdf()
    {
        var threadId = await CreateThreadWithMessageAsync($"La nostra variante si chiama {ThreadSecret}.");

        await AskAsync(_token, chatId: threadId);

        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("il modello è stato interrogato")
            .And.Contain(ThreadSecret, "la cronologia del thread entra nel prompt")
            .And.Contain(BothArms, "insieme ai passaggi del PDF privato");
    }

    /// <summary>
    /// Ogni scrittura sul thread ricostruisce la riga dal dominio: una colonna che il mapper
    /// dimentica verrebbe azzerata al primo messaggio, e il thread resterebbe senza gioco.
    /// </summary>
    [Fact]
    public async Task AddingAMessage_KeepsTheThreadOnThePrivateGame()
    {
        var threadId = await CreateThreadWithMessageAsync("Prima domanda.");

        var row = await ThreadRowAsync(threadId);
        row.PrivateGameId.Should().Be(_privateGameId);
        row.GameId.Should().BeNull();
    }

    [Fact]
    public async Task TheThread_IsListedAmongTheThreadsOfThePrivateGame()
    {
        var threadId = await CreateThreadWithMessageAsync("Prima domanda.");

        var byGame = await GetJsonAsync($"/api/v1/chat-threads?gameId={_privateGameId}");
        byGame.GetProperty("threads").EnumerateArray().Select(t => t.GetProperty("id").GetGuid())
            .Should().Contain(threadId);

        var mine = await GetJsonAsync($"/api/v1/chat-threads/my?gameId={_privateGameId}");
        mine.ToString().Should().Contain(threadId.ToString(), "anche l'elenco dei propri thread filtra per gioco");
    }

    /// <summary>
    /// La cronologia di un thread vale per il suo gioco: nominata in una domanda su un altro gioco,
    /// non entra nel prompt (#4242). Qui l'altro gioco è un secondo gioco privato dello stesso utente.
    /// </summary>
    [Fact]
    public async Task TheHistoryOfTheThread_IsNotUsedForAQuestionOnAnotherGame()
    {
        var threadId = await CreateThreadWithMessageAsync($"La nostra variante si chiama {ThreadSecret}.");
        var otherGameId = await SeedAnotherPrivateGameWithAPdfAsync();

        await AskAsync(_token, chatId: threadId, gameId: otherGameId);

        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("la domanda sull'altro gioco riceve la sua risposta")
            .And.NotContain(ThreadSecret);
    }

    private Task<HttpResponseMessage> PostThreadAsync(string token) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/chat-threads", token,
            new { gameId = _privateGameId, title = "Chat: Il mio prototipo" }), Ct);

    /// <summary>Un thread del proprietario sul gioco privato, con un messaggio dentro.</summary>
    private async Task<Guid> CreateThreadWithMessageAsync(string content)
    {
        var created = await PostThreadAsync(_token);
        var createdBody = await created.Content.ReadAsStringAsync(Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created, createdBody);
        using var thread = JsonDocument.Parse(createdBody);
        var threadId = thread.RootElement.GetProperty("id").GetGuid();

        var added = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/messages", _token,
            new { content, role = "user" }), Ct);
        added.IsSuccessStatusCode.Should().BeTrue(await added.Content.ReadAsStringAsync(Ct));
        return threadId;
    }

    private async Task<(Guid? GameId, Guid? PrivateGameId)> ThreadRowAsync(Guid threadId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var row = await db.ChatThreads.AsNoTracking().SingleAsync(t => t.Id == threadId, Ct);
        return (row.GameId, row.PrivateGameId);
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, path, _token), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    /// <summary>Un secondo gioco privato dello stesso utente, con un suo PDF indicizzato.</summary>
    private async Task<Guid> SeedAnotherPrivateGameWithAPdfAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var otherGameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = otherGameId,
            OwnerId = _userId,
            Title = "Un altro prototipo",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        await db.SaveChangesAsync(Ct);
        await IndexPdfOfPrivateGameAsync(db, otherGameId, processingState: "Ready",
            ("In the other prototype each player draws seven tiles at setup.", QueryVectorEmbeddings.Axis(1f, 0f)));
        return otherGameId;
    }

    // ─── I pesi della fusione vengono dal profilo (#4168, 5e-c-1) ───
    // Il corpus ha un passaggio in entrambi i bracci, uno solo nel lessicale e uno solo nel
    // vettoriale. Il reranker di prova non riordina, quindi l'ordine dei passaggi nel prompt è quello
    // della fusione per gioco.
    //
    // Il corpus presuppone la soglia vettoriale del seed (0,55): è lei che tiene fuori dal braccio
    // vettoriale il passaggio solo lessicale, che ha un vettore ortogonale alla domanda. Con una soglia
    // a 0 entrerebbe anche lì, e l'ordine cambierebbe per una ragione che non sono i pesi.

    /// <summary>
    /// Caratterizzazione: coi pesi del seed (0,7 e 0,3, gli stessi che la fusione aveva scritti nel
    /// codice) l'ordine resta quello di prima.
    /// </summary>
    [Fact]
    public async Task WithTheSeedWeights_TheFusionKeepsItsOrder()
    {
        await AskAsync(_token);

        PassageOrder().Should().Equal(BothArms, VectorOnly, LexicalOnly);
    }

    [Fact]
    public async Task AHigherKeywordWeight_PutsTheLexicalPassageBeforeTheVectorOne()
    {
        await AgentProfilePublisher.PublishAsync(_hostFixture.Factory.Services,
            c => AgentProfilePublisher.WithFusionWeights(c, vectorWeight: 0.1m, keywordWeight: 0.9m), Ct);

        await AskAsync(_token);

        PassageOrder().Should().Equal(BothArms, LexicalOnly, VectorOnly);
    }

    /// <summary>I tre passaggi del corpus, nell'ordine in cui compaiono nel prompt.</summary>
    private List<string> PassageOrder()
    {
        var prompt = _hostFixture.Llm.LastUserPrompt;
        prompt.Should().NotBeNull("il modello è stato interrogato");
        return new[] { BothArms, LexicalOnly, VectorOnly }
            .Where(p => prompt!.Contains(p, StringComparison.Ordinal))
            .OrderBy(p => prompt!.IndexOf(p, StringComparison.Ordinal))
            .ToList();
    }

    // ─── La lista dei PDF del gioco privato (#4348) ───
    // `PrivateGameHub` la legge al caricamento della pagina: vuota, ricaricata la pagina il
    // regolamento spariva. La rotta non guardava chi chiede, quindi i PDF privati li vede solo il
    // proprietario: per chiunque altro il gioco risponde come uno inesistente.

    [Fact]
    public async Task TheOwner_FindsThePdfOfThePrivateGameInItsList()
    {
        var json = await GetJsonAsync($"/api/v1/games/{_privateGameId}/pdfs");

        json.GetProperty("pdfs").EnumerateArray()
            .Select(p => p.GetProperty("id").GetGuid())
            .Should().Equal(_pdfId);
    }

    [Fact]
    public async Task AnotherUser_GetsForThePrivateGameWhatANonexistentGameGets()
    {
        var stranger = await CreateStrangerAsync();

        var (privateStatus, privateBody) = await GetAsync($"/api/v1/games/{_privateGameId}/pdfs", stranger);
        var (missingStatus, missingBody) = await GetAsync($"/api/v1/games/{Guid.NewGuid()}/pdfs", stranger);

        privateStatus.Should().Be(missingStatus);
        privateBody.Should().Be(missingBody, "dire di più direbbe che il gioco esiste");
        privateBody.Should().NotContain(_pdfId.ToString());
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private async Task<(HttpStatusCode Status, string Body)> GetAsync(string path, string token)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, path, token), Ct);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<List<JsonElement>> AskAsync(string token, Guid? chatId = null, Guid? gameId = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["gameId"] = (gameId ?? _privateGameId).ToString(),
            ["query"] = Question,
        };
        if (chatId is not null) payload["chatId"] = chatId;

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", token, payload), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(trimmed["data:".Length..].Trim());
            events.Add(doc.RootElement.Clone());
        }

        return events;
    }

    private static List<string?> ErrorMessagesOf(IEnumerable<JsonElement> events) =>
        events
            .Where(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error)
            .Select(e => e.GetProperty("data").GetProperty("errorMessage").GetString())
            .ToList();

    private static List<string?> ErrorsOf(IEnumerable<JsonElement> events) =>
        events
            .Where(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error)
            .Select(e => e.GetProperty("data").GetProperty("errorCode").GetString())
            .ToList();

    /// <summary>
    /// Il PDF di un gioco privato come lo lascia l'indicizzazione: il gioco nella colonna
    /// <c>PrivateGameId</c> di <c>text_chunks</c> e <c>vector_documents</c> (con <c>GameId</c> null),
    /// e nel <c>game_id</c> unico degli embedding.
    /// </summary>
    private Task<Guid> IndexPrivatePdfAsync(
        MeepleAiDbContext db, string processingState, params (string Text, float[] Vector)[] chunks) =>
        IndexPdfOfPrivateGameAsync(db, _privateGameId, processingState, chunks);

    private Task<Guid> IndexPdfOfPrivateGameAsync(
        MeepleAiDbContext db, Guid privateGameId, string processingState, params (string Text, float[] Vector)[] chunks) =>
        PrivateGameKbSeed.IndexPdfAsync(db, _userId, privateGameId, processingState, Ct, chunks);
}
