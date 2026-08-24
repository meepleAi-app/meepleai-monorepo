using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Observability;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics.Metrics;
using Xunit;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.Tests.Services;

/// <summary>
/// Il braccio vettoriale dichiara il proprio esito, e «non ho potuto cercare» non si confonde più
/// con «non ho trovato niente» (#3786).
/// </summary>
/// <remarks>
/// <para>
/// <b>Perché.</b> I due casi erano entrambi una lista vuota. Una richiesta cross-gioco poteva quindi
/// rispondere su un retrieval dimezzato senza che nulla lo segnalasse: su staging sono state
/// misurate <b>428 ricerche per-gioco su 1759</b> senza braccio vettoriale, per una corsa sul
/// <c>DbContext</c> che non lasciava traccia nel risultato. La diagnosi è costata due giorni proprio
/// perché il segnale andava letto dai log a <c>Debug</c>, che in produzione non sono attivi.
/// </para>
/// <para>
/// <b>Cosa pinnano questi test.</b> I tre esiti sono distinguibili, e in particolare il percorso di
/// <c>catch</c> — quello che prima taceva — emette <c>failed</c>. È la sola parte per cui la metrica
/// esiste: <c>hit</c> ed <c>empty</c> si potevano già dedurre dal risultato, <c>failed</c> no.
/// </para>
/// </remarks>
[Collection("VectorArmMetrics")]  // #3786: contatore condiviso, vedi VectorArmMetricsCollection
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridSearchServiceVectorArmMetricTests
{
    [Fact]
    public async Task AFailedVectorSearchIsRecordedAsFailed_NotAsEmpty()
    {
        // Il caso per cui la metrica esiste: l'eccezione viene catturata per non far cadere la
        // ricerca cross-gioco (scelta corretta), ma senza questo segnale la degradazione è invisibile.
        using var probe = new OutcomeProbe();
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "A second operation was started on this context instance"));

        await CreateService(vectorStore).SearchAsync("catan setup", Guid.NewGuid(), SearchMode.Hybrid);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(1);
        probe.Count(MeepleAiMetrics.RagArmOutcome.Empty).Should().Be(0,
            "una ricerca fallita non è una ricerca senza risultati: confonderle è il difetto #3786");
    }

    [Fact]
    public async Task AFailedEmbeddingIsRecordedAsFailed()
    {
        // Secondo percorso di degradazione: l'embedding non arriva, la ricerca vettoriale non parte.
        using var probe = new OutcomeProbe();
        var embeddings = new Mock<IEmbeddingService>();
        embeddings
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("embedding service unavailable"));
        embeddings.Setup(x => x.GetEmbeddingDimensions()).Returns(8);
        embeddings.Setup(x => x.GetModelName()).Returns("test-model");

        await CreateService(embeddings: embeddings).SearchAsync("catan setup", Guid.NewGuid(), SearchMode.Hybrid);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(1);
    }

    [Fact]
    public async Task AnEmptyButSuccessfulSearchIsRecordedAsEmpty()
    {
        // Esito legittimo, non un difetto: un gioco senza contenuto indicizzato — su staging ~34
        // giochi su 161 non hanno PDF — o senza chunk sopra minScore. Se finisse in `failed`,
        // l'allarme sarebbe rumoroso e verrebbe ignorato.
        using var probe = new OutcomeProbe();

        await CreateService().SearchAsync("catan setup", Guid.NewGuid(), SearchMode.Hybrid);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Empty).Should().Be(1);
        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(0);
    }

    [Fact]
    public async Task ASearchWithResultsIsRecordedAsHit()
    {
        using var probe = new OutcomeProbe();
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore
            .Setup(v => v.SearchWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KbEntities.ScoredEmbedding> { Scored() });

        await CreateService(vectorStore).SearchAsync("catan setup", Guid.NewGuid(), SearchMode.Hybrid);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Hit).Should().Be(1);
    }

    [Fact]
    public async Task ExactlyOneOutcomePerSearch()
    {
        // Il conteggio è il denominatore della copertura: due registrazioni per una ricerca
        // falserebbero il rapporto senza che nulla lo segnali.
        using var probe = new OutcomeProbe();

        await CreateService().SearchAsync("catan setup", Guid.NewGuid(), SearchMode.Hybrid);

        probe.Total.Should().Be(1);
    }

    /// <summary>Ascolta il contatore reale e somma per valore del tag <c>outcome</c>.</summary>
    private sealed class OutcomeProbe : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly Dictionary<string, long> _byOutcome = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public OutcomeProbe()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (string.Equals(instrument.Name, "meepleai.rag.vector_arm.outcomes", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
            {
                var outcome = "?";
                foreach (var tag in tags)
                {
                    if (string.Equals(tag.Key, "outcome", StringComparison.Ordinal))
                    {
                        outcome = tag.Value?.ToString() ?? "?";
                    }
                }

                lock (_gate)
                {
                    _byOutcome[outcome] = _byOutcome.GetValueOrDefault(outcome) + measurement;
                }
            });
            _listener.Start();
        }

        public long Count(string outcome)
        {
            lock (_gate)
            {
                return _byOutcome.GetValueOrDefault(outcome);
            }
        }

        public long Total
        {
            get { lock (_gate) { return _byOutcome.Values.Sum(); } }
        }

        public void Dispose() => _listener.Dispose();
    }

    private static HybridSearchService CreateService(
        Mock<IVectorStoreAdapter>? vectorStore = null,
        Mock<IEmbeddingService>? embeddings = null)
    {
        // Solo se il test NON ne passa uno: un mock gia' configurato non va ri-configurato, o il
        // suo setup viene sovrascritto (Invocations conta le CHIAMATE, non i setup).
        if (vectorStore is null)
        {
            vectorStore = new Mock<IVectorStoreAdapter>();
            vectorStore
                .Setup(v => v.SearchWithScoresAsync(
                    It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                    It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<KbEntities.ScoredEmbedding>());
        }

        if (embeddings is null)
        {
            embeddings = new Mock<IEmbeddingService>();
            embeddings
                .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[8] }));
            embeddings.Setup(x => x.GetEmbeddingDimensions()).Returns(8);
            embeddings.Setup(x => x.GetModelName()).Returns("test-model");
        }

        var keyword = new Mock<IKeywordSearchService>();
        keyword
            .Setup(k => k.ResolveFtsConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("english");
        keyword
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        return new HybridSearchService(
            keyword.Object,
            embeddings.Object,
            vectorStore.Object,
            NullLogger<HybridSearchService>.Instance,
            Options.Create(new HybridSearchConfiguration()));
    }

    private static KbEntities.ScoredEmbedding Scored()
    {
        var embedding = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "Choose the First Player.",
            vector: Vector.CreatePlaceholder(8),
            model: "test-model",
            chunkIndex: 1,
            pageNumber: 1,
            pdfDocumentId: Guid.NewGuid());

        return new KbEntities.ScoredEmbedding(embedding, 0.8);
    }
}
