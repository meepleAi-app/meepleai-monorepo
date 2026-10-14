using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.PerGameSearchStub;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for MultiGameHybridSearchService.
/// Tests parallel-per-game orchestration + aggregation + deterministic ordering.
/// Issue #1661: cross-game KB search.
/// </summary>
/// <remarks>
/// #4168 (5e-c-2): il passo per gioco è una <see cref="SearchQuery"/> mandata al mediator — lo stesso
/// recupero della risposta per gioco —, quindi questi test simulano il mediator
/// (<see cref="PerGameSearchStub"/>) e guardano la query che parte.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class MultiGameHybridSearchServiceTests
{
    private readonly PerGameSearchStub _perGame = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();

    // #3786: l'embedding della query e' ora calcolato UNA VOLTA a monte del fan-out, quindi il
    // servizio ne dipende. Questi test non lo riguardano — coprono orchestrazione, lingua e
    // diagnostica — e il mock centralizzato (#2599) li lascia invariati: restituisce sempre un
    // embedding deterministico. Il conteggio delle chiamate e' asserito in
    // MultiGameHybridSearchServiceQueryEmbeddingTests.

    // Each per-game search resolves the mediator from its OWN DI scope (own DbContext).
    private MultiGameHybridSearchService CreateSut()
    {
        var scopeFactory = _perGame.ScopeFactory();
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(() => scopeFactory.CreateScope());
        return new(_scopeFactoryMock.Object, new MockEmbeddingService(), NullLogger<MultiGameHybridSearchService>.Instance);
    }

    private Task<IReadOnlyList<MultiGameSearchResultItem>> SearchAsync(
        MultiGameHybridSearchService sut,
        string query,
        IReadOnlyList<Guid> gameIds,
        int limit,
        SearchMode mode = SearchMode.Hybrid,
        double minScore = 0.0,
        IReadOnlyList<Guid>? pdfDocumentIds = null,
        CancellationToken cancellationToken = default) =>
        sut.SearchAsync(query, gameIds, limit, Seed, mode, minScore, pdfDocumentIds, cancellationToken);

    // ---------------------------------------------------------------------------
    // EC-1: empty gameIds → empty result, no per-game search
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EmptyGameIds_ReturnsEmpty_WithoutAnyPerGameSearch()
    {
        var sut = CreateSut();

        var result = await SearchAsync(sut, "catan rules", Array.Empty<Guid>(), limit: 10);

        result.Should().BeEmpty();
        _perGame.Received.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // #4168 (5e-c-2): il passo per gioco è il recupero della risposta, con i numeri del profilo
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SingleGame_SendsTheAnswerRetrieval_WithTheProfileParameters()
    {
        var gameId = Guid.NewGuid();
        _perGame.Returns(gameId, BuildPassages(count: 3, baseScore: 0.9f));
        var sut = CreateSut();
        var perGame = new PerGameRetrievalParameters(VectorMinScore: 0.62, VectorWeight: 0.4f, KeywordWeight: 0.6f);

        var result = await sut.SearchAsync("test query", new[] { gameId }, 10, perGame);

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.GameId.Should().Be(gameId));
        var sent = _perGame.For(gameId).Should().ContainSingle().Subject;
        sent.Query.Should().Be("test query");
        sent.SearchMode.Should().Be("hybrid");
        sent.TopK.Should().Be(10, "ogni gioco porta al più `limit` candidati");
        sent.MinScore.Should().Be(0.62, "la soglia del braccio vettoriale è quella del profilo");
        sent.VectorWeight.Should().Be(0.4f);
        sent.KeywordWeight.Should().Be(0.6f);
        sent.Access.Should().Be(SearchAccess.AlreadyChecked, "i giochi arrivano già filtrati per accesso");
        sent.QueryVector.Should().NotBeNullOrEmpty("l'embedding della domanda è calcolato una volta, a monte");
    }

    [Fact]
    public async Task PerGameResults_BeyondThePerGameCap_AreDropped()
    {
        // In modo ibrido il recupero per gioco restituisce l'unione dei due bracci, fino a 2×TopK:
        // il taglio per gioco è reale. Sotto i 50 lo nasconde il taglio finale a `limit`, quindi lo
        // si osserva solo oltre il tetto per gioco.
        var gameId = Guid.NewGuid();
        _perGame.Returns(gameId, BuildPassages(count: 80, baseScore: 0.9f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId }, limit: 60);

        result.Should().HaveCount(50, "ogni gioco porta al più min(limit, 50) candidati");
    }

    // ---------------------------------------------------------------------------
    // Multi-game: parallel calls (once per gameId)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_SearchesEachGameOnce()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();
        _perGame.Returns(gameId1, BuildPassages(count: 2, baseScore: 0.8f));
        _perGame.Returns(gameId2, BuildPassages(count: 2, baseScore: 0.7f));
        _perGame.Returns(gameId3, BuildPassages(count: 2, baseScore: 0.6f));
        var sut = CreateSut();

        await SearchAsync(sut, "test", new[] { gameId1, gameId2, gameId3 }, limit: 20);

        _perGame.For(gameId1).Should().ContainSingle();
        _perGame.For(gameId2).Should().ContainSingle();
        _perGame.For(gameId3).Should().ContainSingle();
        _perGame.Received.Should().HaveCount(3);
    }

    // ---------------------------------------------------------------------------
    // Regression (#2480): each per-game search runs in its OWN DI scope so it gets
    // its OWN DbContext. Sharing the request-scoped DbContext across the concurrent
    // per-game searches threw "A second operation was started on this context
    // instance…" and made every cross-game search return 0 results.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_CreatesOneDiScopePerGame()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();
        _perGame.Returns(gameId1, BuildPassages(count: 1, baseScore: 0.8f));
        _perGame.Returns(gameId2, BuildPassages(count: 1, baseScore: 0.7f));
        _perGame.Returns(gameId3, BuildPassages(count: 1, baseScore: 0.6f));
        var sut = CreateSut();

        await SearchAsync(sut, "test", new[] { gameId1, gameId2, gameId3 }, limit: 20);

        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Exactly(3));
    }

    // ---------------------------------------------------------------------------
    // Aggregation: results from all games are combined
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_AggregatesResultsAcrossGames()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _perGame.Returns(gameId1, BuildPassages(count: 5, baseScore: 0.9f));
        _perGame.Returns(gameId2, BuildPassages(count: 5, baseScore: 0.8f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 20);

        result.Should().HaveCount(10);
        result.Count(r => r.GameId == gameId1).Should().Be(5);
        result.Count(r => r.GameId == gameId2).Should().Be(5);
    }

    // ---------------------------------------------------------------------------
    // EC-4: deterministic ordering — score DESC, then ChunkIndex ASC
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_OrdersByScoreDescThenChunkIndexAsc()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        // game1 → scores 0.5, 0.3
        _perGame.Returns(gameId1,
            Passage(chunkIndex: 2, hybridScore: 0.3f, vectorScore: 0.3f),
            Passage(chunkIndex: 1, hybridScore: 0.5f, vectorScore: 0.5f));
        // game2 → score 0.5 (tie with game1 chunk), chunkIndex 0
        _perGame.Returns(gameId2, Passage(chunkIndex: 0, hybridScore: 0.5f, vectorScore: 0.5f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 10);

        // #3735: il VALORE di HybridScore in uscita non è più quello per-gioco in ingresso — è il
        // punteggio RRF globale. Ciò che il contratto promette, e che qui si verifica, è
        // l'ORDINAMENTO: i due chunk a pari cosine restano appaiati (ranking a scaglioni) e il
        // pareggio è rotto da ChunkIndex ASC, non dall'ordine dei gameId.
        result.Should().HaveCount(3);
        result[0].ChunkIndex.Should().Be(0);  // tie broken by chunkIndex ASC
        result[1].ChunkIndex.Should().Be(1);
        result[2].ChunkIndex.Should().Be(2);  // cosine più bassa → scaglione inferiore
        result[0].HybridScore.Should().Be(result[1].HybridScore,
            "pari cosine deve dare pari rango, quindi termine RRF identico");
        result[2].HybridScore.Should().BeLessThan(result[1].HybridScore);
    }

    // ---------------------------------------------------------------------------
    // #2568: cross-game ranking degeneracy. Per-game RRF is rank-only, so EVERY
    // game's rank-1 chunk gets the IDENTICAL fused HybridScore (0.7/(60+1) ≈ 0.01148).
    // The tie MUST be broken by the globally-comparable raw vector cosine
    // (VectorScore) DESC so the chunk actually most relevant to THIS query wins.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TiedHybridScore_BrokenByVectorScoreDesc_NotChunkIndex()
    {
        var lowRelevanceGame = Guid.NewGuid();
        var highRelevanceGame = Guid.NewGuid();
        const float tiedHybrid = 0.01148f; // the rank-1 RRF value every game ties on

        // low-relevance game: chunkIndex 0 → would WIN the legacy ChunkIndex-ASC tiebreak.
        _perGame.Returns(lowRelevanceGame, Passage(chunkIndex: 0, hybridScore: tiedHybrid, vectorScore: 0.45f));
        // high-relevance game: chunkIndex 7 → would LOSE the legacy tiebreak, but a strong cosine.
        _perGame.Returns(highRelevanceGame, Passage(chunkIndex: 7, hybridScore: tiedHybrid, vectorScore: 0.92f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { lowRelevanceGame, highRelevanceGame }, limit: 10);

        result.Should().HaveCount(2);
        result[0].GameId.Should().Be(highRelevanceGame,
            "the higher raw cosine (VectorScore) must win a HybridScore tie, not the lower ChunkIndex");
        result[0].VectorScore.Should().Be(0.92f);
        result[1].GameId.Should().Be(lowRelevanceGame);
    }

    [Fact]
    public async Task TiedHybridScoreAndVectorScore_FallsBackToChunkIndexAsc()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        const float tiedHybrid = 0.5f;
        const float tiedVector = 0.6f;
        _perGame.Returns(gameId1, Passage(chunkIndex: 3, hybridScore: tiedHybrid, vectorScore: tiedVector));
        _perGame.Returns(gameId2, Passage(chunkIndex: 1, hybridScore: tiedHybrid, vectorScore: tiedVector));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 10);

        result.Should().HaveCount(2);
        result[0].ChunkIndex.Should().Be(1);
        result[1].ChunkIndex.Should().Be(3);
    }

    // ---------------------------------------------------------------------------
    // EC-7: limit truncates final result
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Limit_TruncatesFinalResultSet()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _perGame.Returns(gameId1, BuildPassages(count: 10, baseScore: 0.9f));
        _perGame.Returns(gameId2, BuildPassages(count: 10, baseScore: 0.8f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 7);

        result.Should().HaveCount(7);
    }

    // ---------------------------------------------------------------------------
    // Essential: each result carries the origin gameId
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EachResult_CarriesItsOriginGameId()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        _perGame.Returns(gameId1, BuildPassages(count: 3, baseScore: 0.9f));
        _perGame.Returns(gameId2, BuildPassages(count: 3, baseScore: 0.8f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 20);

        result.Should().AllSatisfy(r => r.GameId.Should().NotBe(Guid.Empty));
        var foundGameIds = result.Select(r => r.GameId).Distinct().ToList();
        foundGameIds.Should().Contain(gameId1);
        foundGameIds.Should().Contain(gameId2);
    }

    /// <summary>
    /// #4168 (5e-c-2): l'identità del chunk e i punteggi grezzi arrivano dal recupero per gioco, che
    /// FuseGlobally e il dump di taratura leggono.
    /// </summary>
    [Fact]
    public async Task EachResult_CarriesTheChunkIdentityAndTheRawScoresOfThePerGameRetrieval()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        _perGame.Returns(gameId, Passage(pdfDocumentId: pdfId, chunkIndex: 4, hybridScore: 0.02f,
            vectorScore: 0.81f, keywordScore: 2.25f, language: "it", content: "Preparazione", page: 3,
            matchedTerms: new[] { "preparazione" }));
        var sut = CreateSut();

        var item = (await SearchAsync(sut, "preparazione", new[] { gameId }, limit: 10)).Should().ContainSingle().Subject;

        item.ChunkId.Should().Be($"{pdfId}_4");
        item.PdfDocumentId.Should().Be(pdfId.ToString());
        item.ChunkIndex.Should().Be(4);
        item.PageNumber.Should().Be(3);
        item.Content.Should().Be("Preparazione");
        item.VectorScore.Should().Be(0.81f);
        item.KeywordScore.Should().Be(2.25f, "il ts_rank_cd grezzo, che supera 1");
        item.Language.Should().Be("it");
        item.MatchedTerms.Should().Equal("preparazione");
        item.Mode.Should().Be(SearchMode.Hybrid);
    }

    // ---------------------------------------------------------------------------
    // CancellationToken: propagated to all parallel calls
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CancellationToken_Propagated_ToAllParallelCalls()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var sut = CreateSut();

        await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 10, cancellationToken: cts.Token);

        _perGame.Mediator.Verify(
            m => m.Send(It.Is<SearchQuery>(q => q.GameId == gameId1), cts.Token), Times.Once);
        _perGame.Mediator.Verify(
            m => m.Send(It.Is<SearchQuery>(q => q.GameId == gameId2), cts.Token), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Resilience: single-game exception → logs warn, continues with other games
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SingleGameException_DoesNotFailWholeSearch_ReturnsOtherGamesResults()
    {
        var failingGameId = Guid.NewGuid();
        var goodGameId = Guid.NewGuid();
        _perGame.Throws(failingGameId, new InvalidOperationException("game has no content"));
        _perGame.Returns(goodGameId, BuildPassages(count: 4, baseScore: 0.7f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { failingGameId, goodGameId }, limit: 10);

        result.Should().HaveCount(4);
        result.Should().AllSatisfy(r => r.GameId.Should().Be(goodGameId));
    }

    // ---------------------------------------------------------------------------
    // minScore: results below threshold are excluded from aggregation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MinScore_FiltersOutLowScoreResults()
    {
        var gameId = Guid.NewGuid();
        _perGame.Returns(gameId,
            Passage(chunkIndex: 0, hybridScore: 0.9f, vectorScore: 0.9f),
            Passage(chunkIndex: 1, hybridScore: 0.4f, vectorScore: 0.4f),  // below 0.5 threshold
            Passage(chunkIndex: 2, hybridScore: 0.6f, vectorScore: 0.6f));
        var sut = CreateSut();

        var result = await SearchAsync(sut, "test", new[] { gameId }, limit: 10, minScore: 0.5);

        // #3735: minScore filtra il punteggio PER-GIOCO in ingresso, mentre HybridScore in uscita è
        // quello globale: asserire sull'uscita confronterebbe due grandezze diverse. Si verifica
        // quindi CHI è sopravvissuto, che è ciò che la soglia deve garantire.
        result.Should().HaveCount(2);
        result.Select(r => r.ChunkIndex).Should().BeEquivalentTo(new[] { 0, 2 },
            "il chunk con punteggio per-gioco 0.4 è sotto la soglia 0.5 e va escluso");
        _perGame.For(gameId).Should().ContainSingle().Which.MinScore.Should().Be(Seed.VectorMinScore,
            "la soglia della ricerca multi-gioco non è quella del braccio vettoriale per gioco");
    }

    // ---------------------------------------------------------------------------
    // Issue #1686 — documentIds parameter is forwarded to each per-game search
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DocumentIds_PassedThrough_ToPerGameSearch()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var documentIds = new List<Guid> { docA, docB };
        var sut = CreateSut();

        await SearchAsync(sut, "test", new[] { gameId1, gameId2 }, limit: 10, pdfDocumentIds: documentIds);

        _perGame.Received.Should().HaveCount(2).And.AllSatisfy(q =>
            q.PdfDocumentIds.Should().BeEquivalentTo(new[] { docA, docB }));
    }

    [Fact]
    public async Task DocumentIds_Null_ForwardedAsNull_BackwardCompatible()
    {
        var gameId = Guid.NewGuid();
        _perGame.Returns(gameId, BuildPassages(count: 1, baseScore: 0.8f));
        var sut = CreateSut();

        await SearchAsync(sut, "test", new[] { gameId }, limit: 10);

        _perGame.For(gameId).Should().ContainSingle().Which.PdfDocumentIds.Should().BeNull(
            "senza filtro la ricerca per gioco non riceve un filtro (D-3)");
    }

    // ---------------------------------------------------------------------------
    // #3735: il punteggio per-gioco NON è comparabile fra giochi
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Il difetto che #3735 misura sul corpus reale (9 query canoniche su 11 recuperano il manuale
    /// sbagliato), ridotto al suo nucleo.
    ///
    /// <para>
    /// <c>HybridScore</c> è calcolato <b>dentro</b> ogni gioco come somma di termini RRF
    /// <c>1/(k+rank)</c>. Un chunk che compare in <b>entrambe</b> le liste (vettoriale e lessicale)
    /// riceve due termini; uno che compare in una sola ne riceve uno — anche se la sua similarità
    /// semantica è nettamente superiore. Fra giochi diversi quei valori vengono poi confrontati
    /// direttamente, benché misurino il rango locale e non la pertinenza.
    /// </para>
    /// <para>
    /// Effetto: un manuale che contiene le parole generiche della query («setup», «board»,
    /// «place» — presenti in ogni regolamento) batte il manuale che la query nomina
    /// esplicitamente. Il tiebreak su <c>VectorScore</c> di #2568 non interviene, perché i due
    /// <c>HybridScore</c> non sono uguali: sono diversi <i>e</i> nell'ordine sbagliato.
    /// </para>
    /// </summary>
    [Fact]
    public async Task CrossGame_TheChunkMostRelevantToTheQuery_OutranksTheGenericOnes()
    {
        // Il campo realistico: molti giochi, come nel corpus vero (133 manuali).
        var expectedGame = Guid.NewGuid();                                   // il manuale che la query nomina
        var genericGames = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();

        // Il chunk atteso è nettamente il più pertinente, ma compare SOLO nella lista vettoriale.
        _perGame.Returns(expectedGame, Passage(chunkIndex: 0,
            hybridScore: 0.0164f,   // ≈ 1/(60+1): un termine
            vectorScore: 0.93f,     // ma è il più simile alla query, di molto
            keywordScore: null));

        // Ogni manuale generico contiene le parole comuni della query, quindi il suo chunk compare
        // in ENTRAMBE le liste per-gioco: due termini, punteggio per-gioco doppio.
        foreach (var (gameId, i) in genericGames.Select((g, i) => (g, i)))
        {
            var noise = i * 0.005f;
            _perGame.Returns(gameId, Passage(chunkIndex: 0,
                hybridScore: 0.0328f,          // ≈ 2/(60+1): il doppio dell'atteso
                vectorScore: 0.71f - noise,    // semanticamente molto più debole
                keywordScore: 0.12f - noise));
        }

        var sut = CreateSut();

        var results = await SearchAsync(sut,
            "How do I set up the board and place the two initial settlements?",
            genericGames.Append(expectedGame).ToArray(),   // l'atteso è l'ULTIMO: nessun vantaggio d'ordine
            limit: 30);

        results[0].GameId.Should().Be(expectedGame,
            "il ranking cross-gioco deve riflettere la pertinenza alla query, non quante liste "
            + "per-gioco hanno prodotto il chunk (#3735)");

        // Perché venti giochi e non due: con due soli candidati i ranghi vettoriali adiacenti
        // distano 0.7/61 − 0.7/62 ≈ 0.000185, mentre un secondo segnale aggiunge 0.3/61 ≈ 0.0049 —
        // trenta volte tanto. L'RRF è rank-based per costruzione e non può esprimere un divario di
        // cosine fra due posizioni contigue: il calcolo è in #3735.
    }

    private static SearchResultDto[] BuildPassages(int count, float baseScore) =>
        Enumerable.Range(0, count)
            .Select(i => Passage(chunkIndex: i, hybridScore: baseScore - i * 0.01f, vectorScore: baseScore - i * 0.01f))
            .ToArray();
}
