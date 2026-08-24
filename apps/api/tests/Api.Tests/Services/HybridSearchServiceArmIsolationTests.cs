using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.Tests.Services;

/// <summary>
/// I due bracci della ricerca ibrida non devono toccare il database nello stesso momento (#3786).
/// </summary>
/// <remarks>
/// <para>
/// <b>Perché.</b> Entrambi risolvono dallo stesso scope — quello creato per gioco da
/// <c>MultiGameHybridSearchService.SearchGameSafeAsync</c> — quindi condividono la stessa istanza di
/// <c>MeepleAiDbContext</c>: il vettoriale via <c>GetDbConnection()</c> in
/// <c>PgVectorStoreAdapter</c>, il lessicale via <c>SqlQueryRaw</c> in
/// <c>KeywordSearchService</c>. <c>DbContext</c> non è thread-safe, e lanciarli con
/// <c>Task.WhenAll</c> produceva:
/// </para>
/// <code>
/// System.InvalidOperationException: A second operation was started on this context instance
/// before a previous operation completed.
/// </code>
/// <para>
/// Misurato su staging: <b>267</b> eccezioni e <b>428</b> ricerche per-gioco con
/// <c>vectorCount=0</c> su 1759, in una sola raccolta di 11 query. Il braccio vettoriale eccepiva,
/// l'eccezione veniva catturata, e la ricerca proseguiva <b>solo lessicale</b> — senza alcun segnale
/// nel risultato. Da qui l'intermittenza: è una corsa, quindi cambia a ogni esecuzione.
/// </para>
/// <para>
/// <b>Nota su #2480.</b> La correzione di allora («own scope → own DbContext» in
/// <c>SearchGameSafeAsync</c>) è valida e separa i <i>giochi</i> fra loro. Il difetto residuo era
/// <i>dentro</i> il singolo gioco, fra i due bracci — un caso che quella correzione non copriva.
/// </para>
/// <para>
/// <b>Limite del test.</b> Verifica una proprietà di concorrenza con dei mock che si mettono in
/// attesa per aprire una finestra di sovrapposizione: può produrre falsi <i>negativi</i> (non
/// accorgersi di una corsa), mai falsi positivi. Se fallisce, la sovrapposizione c'è davvero.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridSearchServiceArmIsolationTests
{
    /// <summary>Conta quanti bracci sono dentro il DB nello stesso istante.</summary>
    private sealed class ConcurrencyProbe
    {
        private int _current;
        public int Max { get; private set; }
        private readonly object _gate = new();

        public async Task<T> EnterAsync<T>(T result)
        {
            lock (_gate)
            {
                _current++;
                if (_current > Max) Max = _current;
            }

            // Tiene "aperta" la chiamata abbastanza da lasciare entrare l'altro braccio, se il
            // codice li esegue davvero in parallelo. Non è un'attesa su cui si asserisce: la prova
            // è il contatore.
            await Task.Delay(60).ConfigureAwait(false);

            lock (_gate)
            {
                _current--;
            }

            return result;
        }
    }

    [Fact]
    public async Task TheTwoArmsNeverTouchTheDatabaseAtTheSameTime()
    {
        var probe = new ConcurrencyProbe();
        var service = CreateService(probe);

        await service.SearchAsync("come si prepara il tabellone in Catan?", Guid.NewGuid(), SearchMode.Hybrid);

        probe.Max.Should().Be(1,
            "i due bracci condividono la stessa istanza di DbContext, che non è thread-safe: "
            + "sovrapporli fa eccepire il vettoriale e degrada la ricerca a solo-lessicale (#3786)");
    }

    [Fact]
    public async Task BothArmsStillRun()
    {
        // La serializzazione non deve diventare «ne eseguo uno solo»: il contatore a 1 sarebbe
        // soddisfatto anche da un braccio mancante.
        var probe = new ConcurrencyProbe();
        var keyword = new Mock<IKeywordSearchService>();
        var vectorStore = new Mock<IVectorStoreAdapter>();
        var service = CreateService(probe, keyword, vectorStore);

        await service.SearchAsync("come si prepara il tabellone in Catan?", Guid.NewGuid(), SearchMode.Hybrid);

        keyword.Verify(k => k.SearchAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
            It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Once);

        vectorStore.Verify(v => v.SearchWithScoresAsync(
            It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
            It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static HybridSearchService CreateService(
        ConcurrencyProbe probe,
        Mock<IKeywordSearchService>? keyword = null,
        Mock<IVectorStoreAdapter>? vectorStore = null)
    {
        keyword ??= new Mock<IKeywordSearchService>();
        vectorStore ??= new Mock<IVectorStoreAdapter>();

        keyword
            .Setup(k => k.ResolveFtsConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("english");
        keyword
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns(() => probe.EnterAsync(new List<KeywordSearchResult>()));

        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .Returns(() => probe.EnterAsync(new List<KbEntities.ScoredEmbedding>()));

        var embeddings = new Mock<IEmbeddingService>();
        embeddings
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[8] }));
        embeddings.Setup(x => x.GetEmbeddingDimensions()).Returns(8);
        embeddings.Setup(x => x.GetModelName()).Returns("test-model");

        return new HybridSearchService(
            keyword.Object,
            embeddings.Object,
            vectorStore.Object,
            NullLogger<HybridSearchService>.Instance,
            Options.Create(new HybridSearchConfiguration()));
    }
}
