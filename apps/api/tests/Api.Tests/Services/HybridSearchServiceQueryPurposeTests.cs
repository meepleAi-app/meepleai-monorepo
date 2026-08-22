using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Moq;
using Xunit;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.Tests.Services;

/// <summary>
/// The retrieval path must embed its question as a QUERY, not as a passage (issue #3737).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HybridSearchService"/> is the single vector-arm entry point for both per-game and
/// cross-game retrieval, so this one call site decided the prefix for every search in the
/// product. It asked for the passage side, which is what #3737 measured: on the real corpus
/// (56.367 chunk, 127 manuali) the best chunk of the manual named by the canonical
/// <c>catan-setup</c> query sat at cosine rank 10 instead of 1.
/// </para>
/// <para>
/// The assertion is on the purpose the service <i>requests</i>, not on the resulting vector: the
/// prefix is applied inside the embedding service, so the request is the only thing this layer
/// controls and the only thing that can regress here.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HybridSearchServiceQueryPurposeTests
{
    [Fact]
    public async Task SearchAsync_Hybrid_EmbedsTheQuestionWithQueryPurpose()
    {
        var embeddings = new MockEmbeddingService();
        var service = CreateService(embeddings);

        await service.SearchAsync(
            "How do I set up the board and place the two initial settlements and roads in Catan?",
            Guid.NewGuid(),
            SearchMode.Hybrid);

        embeddings.RequestedPurposes.Should().ContainSingle()
            .Which.Should().Be(EmbeddingPurpose.Query);
    }

    [Fact]
    public async Task SearchAsync_Semantic_EmbedsTheQuestionWithQueryPurpose()
    {
        // Semantic-only shares ExecuteVectorSearchAsync with hybrid; pinned so a future split
        // of the two modes cannot reintroduce the passage prefix on one of them.
        var embeddings = new MockEmbeddingService();
        var service = CreateService(embeddings);

        await service.SearchAsync("come funziona il commercio?", Guid.NewGuid(), SearchMode.Semantic);

        embeddings.RequestedPurposes.Should().ContainSingle()
            .Which.Should().Be(EmbeddingPurpose.Query);
    }

    [Fact]
    public async Task SearchAsync_Keyword_DoesNotEmbedAtAll()
    {
        // Guard against the opposite mistake: keyword-only must not pay for an embedding.
        var embeddings = new MockEmbeddingService();
        var service = CreateService(embeddings);

        await service.SearchAsync("catan setup", Guid.NewGuid(), SearchMode.Keyword);

        embeddings.RequestedPurposes.Should().BeEmpty();
    }

    private static HybridSearchService CreateService(MockEmbeddingService embeddings)
    {
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
            .ReturnsAsync(new List<KbEntities.ScoredEmbedding>());

        return new HybridSearchService(
            keyword.Object,
            embeddings,
            vectorStore.Object,
            NullLogger<HybridSearchService>.Instance,
            Options.Create(new HybridSearchConfiguration()));
    }
}
