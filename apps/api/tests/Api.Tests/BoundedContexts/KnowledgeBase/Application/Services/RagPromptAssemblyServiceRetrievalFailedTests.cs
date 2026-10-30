using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4751 — <see cref="Api.BoundedContexts.KnowledgeBase.Application.Models.AssembledPrompt.RetrievalFailed"/>:
/// a retrieval that stopped on an error is told apart from one that ran and found nothing. Both return an empty
/// context and no citation (the outer catch of <c>RetrieveRagContextAsync</c> flattens a failure into that); only
/// the flag lets a caller say «could not consult the rulebook» instead of «nothing relevant in it».
/// </summary>
/// <remarks>
/// In the same collection as the other files that build <see cref="RagPromptAssemblyService"/>: they share the
/// process-wide retrieval counters (see <c>RagPromptAssemblyServiceRetrievalEmptyMetricsTests</c>, #3994).
/// </remarks>
[Collection("RetrievalEmptyMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4751")]
public sealed class RagPromptAssemblyServiceRetrievalFailedTests
{
    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly float[] TestEmbedding = [0.1f, 0.2f, 0.3f];

    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<IEmbeddingRepository> _embeddingRepositoryMock = new();
    private readonly Mock<ITextChunkSearchService> _textSearchMock = new();
    private readonly Mock<ILlmService> _llmMock = new();
    private readonly Mock<IExpansionGameResolver> _expansionResolverMock = new();

    public RagPromptAssemblyServiceRetrievalFailedTests()
    {
        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = false });
        _expansionResolverMock
            .Setup(r => r.GetExpansionGameIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>());
        _embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoredEmbedding>());
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([TestEmbedding]));
    }

    private RagPromptAssemblyService CreateService()
        => new(
            _embeddingMock.Object,
            _embeddingRepositoryMock.Object,
            new Mock<ICrossEncoderReranker>().Object,
            _llmMock.Object,
            _textSearchMock.Object,
            _expansionResolverMock.Object,
            new Mock<IRagEnhancementService>().Object,
            new Mock<IQueryComplexityClassifier>().Object,
            new Mock<IRetrievalRelevanceEvaluator>().Object,
            new Mock<IQueryExpander>().Object,
            new Mock<IGraphRetrievalService>().Object,
            NullLogger<RagPromptAssemblyService>.Instance);

    private Task<Api.BoundedContexts.KnowledgeBase.Application.Models.AssembledPrompt> Assemble()
        => CreateService().AssemblePromptAsync(
            "tutor", "Catan", null, "Il ladro si sposta subito?",
            TestGameId, null, null, "it", CancellationToken.None,
            retrievalPolicy: RetrievalPolicy.LiveSession);

    [Fact]
    public async Task WhenTheQueryEmbeddingFails_TheRetrievalIsMarkedFailed()
    {
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("embedding service down"));

        var assembled = await Assemble();

        assembled.RetrievalFailed.Should().BeTrue();
        assembled.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenAStageOfTheRetrievalThrows_TheRetrievalIsMarkedFailed()
    {
        _embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("vector store unavailable"));

        var assembled = await Assemble();

        assembled.RetrievalFailed.Should().BeTrue();
        assembled.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheRetrievalRunsAndFindsNothing_ItIsNotMarkedFailed()
    {
        var assembled = await Assemble();

        assembled.RetrievalFailed.Should().BeFalse("it ran to the end: an empty result is not a failure");
        assembled.Citations.Should().BeEmpty();
    }
}
