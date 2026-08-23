using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.Tests.Services;

/// <summary>
/// La riga di diagnostica <c>[RAG-TUNE-GAME]</c> rende osservabile l'arena PER-GIOCO (#3768).
/// </summary>
/// <remarks>
/// <para>
/// <b>Perché esiste.</b> Il dump <c>[RAG-TUNE]</c> di
/// <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.MultiGameHybridSearchService"/>
/// mostra i candidati che <i>arrivano</i> alla fusione globale, mai quelli che il per-gioco ha
/// <i>rifiutato</i>. Su staging, per <c>catan-setup-it</c>, il chunk con le regole di setup è rango 1
/// del braccio vettoriale dentro Catan (cosine 0.80544) e non arriva: al suo posto arriva il
/// colophon <c>catan.com ®</c> (cosine 0.77997, rango 14). Diagnosticarlo è costato una lettura del
/// DB per ogni ipotesi, e due ipotesi sbagliate — le due penalità moltiplicative, entrambe a zero
/// su quel chunk. La causa era il termine additivo <c>headingBoost</c>, che nessuno stava guardando.
/// </para>
/// <para>
/// <b>Cosa pinnano questi test.</b> Lo strumento, non il difetto: che la riga esca a Debug e costi
/// zero quando Debug è spento, che il prefisso sia stabile (il consumatore fa <c>grep -F</c>), e che
/// il payload porti la scomposizione del punteggio — inclusi i due boost additivi separati — più il
/// punteggio reale, che è l'àncora con cui la replica offline si valida.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridSearchServicePerGameTuningLogTests
{
    private const string QueryNamingTheGame =
        "Come si prepara il tabellone e si piazzano i due insediamenti e le strade iniziali in Catan?";

    [Fact]
    public void ThePerGameLogPrefixIsStable()
    {
        // Il consumatore offline filtra su questa costante. Cambiarla rompe il banco.
        HybridSearchService.PerGameTuningLogPrefix.Should().Be("[RAG-TUNE-GAME]");
    }

    [Fact]
    public async Task WhenDebugIsDisabled_NoPerGameLineIsEmitted()
    {
        // La proprietà che rende accettabile la strumentazione in produzione: la guardia IsEnabled
        // precede ogni serializzazione. `ask/global` chiama questo percorso una volta PER GIOCO —
        // ~130 righe a query — quindi un payload costruito e poi scartato non sarebbe gratuito.
        var (service, logger) = CreateService(debugEnabled: false);

        await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid);

        PerGameLines(logger).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenDebugIsEnabled_ExactlyOnePerGameLinePerSearch()
    {
        var (service, logger) = CreateService(debugEnabled: true);

        await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid);

        PerGameLines(logger).Should().ContainSingle(
            "il consumatore associa una riga a un gioco: due righe romperebbero l'associazione");
    }

    [Fact]
    public async Task ThePayloadSeparatesTheHeadingBoostFromTheRestOfTheScore()
    {
        // È il campo per cui il dump esiste. Senza `hb` separato, l'unica cosa osservabile è il
        // punteggio finale, e da lì il boost non si distingue da una cosine alta.
        var (service, logger) = CreateService(debugEnabled: true);

        await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid);

        var candidates = Candidates(PerGameLines(logger).Single());

        var rules = candidates.Single(c => c.GetProperty("i").GetInt32() == 388);
        var colophon = candidates.Single(c => c.GetProperty("i").GetInt32() == 410);

        rules.GetProperty("hb").GetSingle().Should().Be(0f,
            "la heading \"I\" non contiene alcun termine della query lungo almeno 3 caratteri");
        colophon.GetProperty("hb").GetSingle().Should().Be(FusionSignals.HeadingMatchBoost,
            "la heading \"catan.com\" contiene il termine \"catan\", che viene dalla query grezza");
    }

    [Fact]
    public async Task ThePayloadCarriesTheRealHybridScoreAsValidationAnchor()
    {
        // La replica offline ricostruisce il punteggio da vr/kr/lg/nn/rb/hb e lo confronta con `s`.
        // Se `s` fosse ricalcolato dal logger invece che letto dalla fusione, la validazione
        // confermerebbe se stessa: qui si pinna che venga dal risultato fuso.
        var (service, logger) = CreateService(debugEnabled: true);

        var results = await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid);

        var candidates = Candidates(PerGameLines(logger).Single());
        var top = results[0];

        candidates
            .Single(c => c.GetProperty("i").GetInt32() == top.ChunkIndex)
            .GetProperty("s").GetSingle()
            .Should().Be(top.HybridScore);
    }

    [Fact]
    public async Task ThePayloadCarriesTheHeadingTermsActuallyUsed()
    {
        // Senza i termini, la replica offline dovrebbe riprodurre ExtractHeadingMatchTerms +
        // l'espansione per sinonimi — cioè indovinare l'input del boost che sta valutando.
        var (service, logger) = CreateService(debugEnabled: true);

        await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid);

        var payload = JsonDocument.Parse(PerGameLines(logger).Single()).RootElement;
        var terms = payload.GetProperty("t").EnumerateArray().Select(t => t.GetString()).ToList();

        terms.Should().Contain("catan", "è il termine che accende il boost sul colophon");
        terms.Should().Contain("tabellone");
    }

    [Fact]
    public async Task ThePayloadCarriesTheLanguageSoItCanDriveTheGlobalReplicaAlone()
    {
        // La fusione globale applica una correzione per lingua (#3740): senza `l` questo payload
        // andrebbe ri-unito al dump globale per valutare un cambiamento end-to-end.
        var (service, logger) = CreateService(debugEnabled: true, candidateLanguage: "it");

        await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid);

        Candidates(PerGameLines(logger).Single())
            .Should().OnlyContain(c => c.GetProperty("l").GetString() == "it");
    }

    [Fact]
    public async Task ThePayloadCoversEveryFusedCandidate_NotJustTheReturnedTop()
    {
        // Il chunk pertinente è, per definizione del difetto, FUORI dal top restituito: un dump
        // troncato al risultato non mostrerebbe mai ciò che serve capire.
        var (service, logger) = CreateService(debugEnabled: true);

        var results = await service.SearchAsync(QueryNamingTheGame, Guid.NewGuid(), SearchMode.Hybrid, limit: 1);

        results.Should().HaveCount(1);
        Candidates(PerGameLines(logger).Single()).Should().HaveCount(2);
    }

    /// <summary>Le righe [RAG-TUNE-GAME] emesse a Debug, come le legge il consumatore.</summary>
    private static List<string> PerGameLines(Mock<ILogger<HybridSearchService>> logger) =>
        logger.Invocations
            .Where(i => string.Equals(i.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal)
                        && i.Arguments.Count > 2
                        && i.Arguments[0] is LogLevel.Debug)
            .Select(i => i.Arguments[2]?.ToString() ?? string.Empty)
            .Where(m => m.Contains(HybridSearchService.PerGameTuningLogPrefix, StringComparison.Ordinal))
            .Select(m => m[(m.IndexOf('{', StringComparison.Ordinal))..])
            .ToList();

    private static List<JsonElement> Candidates(string payload) =>
        JsonDocument.Parse(payload).RootElement.GetProperty("c").EnumerateArray().ToList();

    /// <summary>
    /// Due candidati di uno stesso gioco che riproducono l'inversione misurata su staging: le regole
    /// (cosine più alta, heading senza il nome del gioco) contro il colophon (cosine più bassa,
    /// heading <c>catan.com</c>).
    /// </summary>
    private static (HybridSearchService Service, Mock<ILogger<HybridSearchService>> Logger) CreateService(
        bool debugEnabled,
        string candidateLanguage = "en")
    {
        var logger = new Mock<ILogger<HybridSearchService>>();
        logger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(debugEnabled);

        var pdfId = Guid.NewGuid();

        var embeddings = new Mock<IEmbeddingService>();
        embeddings
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[8] }));
        embeddings.Setup(x => x.GetEmbeddingDimensions()).Returns(8);
        embeddings.Setup(x => x.GetModelName()).Returns("test-model");

        var keyword = new Mock<IKeywordSearchService>();
        keyword
            .Setup(k => k.ResolveFtsConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("english");
        keyword
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KbEntities.ScoredEmbedding>
            {
                Scored(pdfId, chunkIndex: 388, score: 0.80544, heading: "I", language: candidateLanguage,
                    text: "Choose the First Player. Each player rolls the dice and takes the roads and buildings."),
                Scored(pdfId, chunkIndex: 410, score: 0.77997, heading: "catan.com", language: candidateLanguage,
                    text: "catan.com  ®"),
            });

        var service = new HybridSearchService(
            keyword.Object,
            embeddings.Object,
            vectorStore.Object,
            logger.Object,
            Options.Create(new HybridSearchConfiguration()));

        return (service, logger);
    }

    private static KbEntities.ScoredEmbedding Scored(
        Guid pdfDocumentId, int chunkIndex, double score, string heading, string text, string language = "en")
    {
        var embedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: text,
            vector: Vector.CreatePlaceholder(8),
            model: "test-model",
            chunkIndex: chunkIndex,
            pageNumber: 1,
            language: language,
            roleTags: (int)GameBookRole.None,
            pdfDocumentId: pdfDocumentId,
            heading: heading);

        return new KbEntities.ScoredEmbedding(embedding, score);
    }
}
