using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SearchResult = Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4482: una ricerca ibrida vera (<see cref="SearchQueryHandler"/>) i cui bracci sono doppi e la cui
/// fusione restituisce la lista data, nell'ordine dato. Così un test sceglie i candidati e legge con
/// quali numeri sono stati chiesti: il TopK e la soglia arrivano ai due bracci, i pesi alla fusione.
/// </summary>
internal sealed class FusedSearchStub
{
    public Mock<IEmbeddingRepository> Embeddings { get; } = new();
    public Mock<IEmbeddingService> Embedder { get; } = new();
    public Mock<IKeywordSearchService> Keywords { get; } = new();
    public Mock<RrfFusionDomainService> Fusion { get; } = new();
    public SearchQueryHandler Handler { get; }

    /// <param name="fused">
    /// La lista che la fusione restituisce. Null: la fusione vera, sopra ciò che i bracci restituiscono
    /// (<see cref="VectorArmReturns"/>).
    /// </param>
    public FusedSearchStub(IReadOnlyList<SearchResult>? fused)
    {
        Embedder.Setup(e => e.GetModelName()).Returns(EmbeddingModel);
        Embedder
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new[] { 0.1f, 0.2f, 0.3f } } });
        Embeddings
            .Setup(e => e.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoredEmbedding>());
        Keywords
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(),
                It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());
        if (fused is not null)
        {
            IReadOnlyList<SearchResult> given = fused;
            Fusion
                .Setup(f => f.FuseResults(
                    It.IsAny<List<SearchResult>>(), It.IsAny<List<SearchResult>>(), It.IsAny<int>(), It.IsAny<GameBookRole>(),
                    It.IsAny<IReadOnlyList<string>?>(), It.IsAny<float>(), It.IsAny<float>()))
                .Returns(() => given.ToList());
        }

        Handler = new SearchQueryHandler(
            Embeddings.Object,
            new Mock<VectorSearchDomainService>().Object,
            fused is null ? new RrfFusionDomainService() : Fusion.Object,
            Embedder.Object,
            Keywords.Object,
            Mock.Of<IRagAccessService>(),
            NullLogger<SearchQueryHandler>.Instance);
    }

    /// <summary>Il nome che l'embedding di prova dichiara.</summary>
    public const string EmbeddingModel = "test-embedding";

    /// <summary>Il braccio vettoriale restituisce questi chunk, con coseno 0,8.</summary>
    public FusedSearchStub VectorArmReturns(params Embedding[] embeddings)
    {
        Embeddings
            .Setup(e => e.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings.Select(e => new ScoredEmbedding(e, 0.8)).ToList());
        return this;
    }

    /// <summary>La query pgvector del braccio vettoriale fallisce: la ricerca ibrida prosegue solo lessicale.</summary>
    public FusedSearchStub VectorArmFails()
    {
        Embeddings
            .Setup(e => e.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pgvector down"));
        return this;
    }

    /// <summary>Un embedding del PDF di prova, originale o traduzione.</summary>
    public static Embedding StoredEmbedding(int chunkIndex, string text, bool isTranslation = false) => new(
        id: Guid.NewGuid(),
        vectorDocumentId: VectorDocumentId,
        textContent: text,
        vector: new Vector(new[] { 0.1f, 0.2f, 0.3f }),
        model: EmbeddingModel,
        chunkIndex: chunkIndex,
        pageNumber: 4,
        language: isTranslation ? "en" : "it",
        isTranslation: isTranslation,
        pdfDocumentId: PdfDocumentId);

    /// <summary>
    /// Il candidato numero <paramref name="n"/> (da 1): testo «chunk n», con l'identità del chunk e i
    /// segnali della fusione, così che ogni campo che la sonda riporta abbia un valore riconoscibile.
    /// </summary>
    public static SearchResult Chunk(int n) => new(
        id: Guid.NewGuid(),
        vectorDocumentId: VectorDocumentId,
        textContent: $"chunk {n}",
        pageNumber: n,
        relevanceScore: new Confidence(Math.Max(0.0, 0.9 - (n * 0.01))),
        rank: n,
        searchMethod: "hybrid",
        pdfDocumentId: PdfDocumentId,
        chunkIndex: n * 10,
        signals: new RetrievalSignals(
            HybridScore: 1.0f / n,
            VectorScore: 0.8f - (n * 0.001f),
            KeywordScore: n % 2 == 0 ? 0.1f : null,
            Language: "en",
            MatchedTerms: n % 2 == 0 ? ["robber"] : null),
        textChunkId: TextChunkId(n));

    public static List<SearchResult> Chunks(int count) => Enumerable.Range(1, count).Select(Chunk).ToList();

    public static readonly Guid VectorDocumentId = Guid.Parse("00000000-0000-0000-0000-00000000c4a1");
    public static readonly Guid PdfDocumentId = Guid.Parse("00000000-0000-0000-0000-00000000d4a1");

    public static Guid TextChunkId(int n) => new($"00000000-0000-0000-0000-{n:D12}");
}
