using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Configuration;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Translation;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Phase D (D7) tests for <see cref="AskQuestionQueryHandler"/>: verifies the intent
/// classifier is invoked with the user question and the resulting <see cref="GameBookRole"/>
/// hint is threaded into the downstream <see cref="IHybridSearchService.SearchAsync"/> call.
///
/// Test strategy: spy on the <see cref="IIntentClassifierService"/> and on
/// <see cref="IHybridSearchService"/> to assert (a) the classifier was called with the question
/// text and (b) the boosted SearchQuery flowed the classifier output through to retrieval.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AskQuestionQueryHandlerIntentRoutingTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_InvokesIntentClassifierWithQuestion_AndThreadsHintToHybridSearch()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string question = "come si setupa per 4 giocatori"; // matches Tutorial|Setup in real classifier
        var expectedRoleHint = GameBookRole.Tutorial | GameBookRole.Setup;

        var intentClassifierMock = new Mock<IIntentClassifierService>();
        intentClassifierMock
            .Setup(c => c.ClassifyIntent(It.IsAny<string>()))
            .Returns(expectedRoleHint);

        // Spy on IHybridSearchService to capture the GameBookRole passed in by the downstream chain.
        GameBookRole capturedRoleHint = GameBookRole.None;
        var hybridSearchMock = new Mock<IHybridSearchService>();
        hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<double>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Guid, SearchMode, int, List<Guid>?, float, float, double, GameBookRole, CancellationToken>(
                (q, g, m, l, d, vw, kw, ms, rh, ct) => capturedRoleHint = rh)
            .ReturnsAsync(new List<HybridSearchResult>());

        var handler = BuildHandler(intentClassifierMock.Object, hybridSearchMock.Object);

        // Act
        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: question,
            SearchMode: "hybrid",
            Language: "it",
            BypassCache: true); // skip semantic cache so the full pipeline executes

        await handler.Handle(query, TestCancellationToken);

        // Assert
        // 1. Classifier received the raw question text.
        intentClassifierMock.Verify(c => c.ClassifyIntent(question), Times.AtLeastOnce);

        // 2. The role hint emitted by the classifier reached the hybrid search re-ranker.
        capturedRoleHint.Should().Be(expectedRoleHint);
    }

    [Fact]
    public async Task Handle_WithClassifierReturningNone_PropagatesNoneHintWithoutBoost()
    {
        // Arrange: classifier returns None (e.g., when the question matches no pattern in a
        // pathological scenario — the real service falls back to RulesReference, but a mock
        // can return None to exercise the no-op default path of D6).
        var gameId = Guid.NewGuid();
        const string question = "totally random gibberish abcxyz";

        var intentClassifierMock = new Mock<IIntentClassifierService>();
        intentClassifierMock
            .Setup(c => c.ClassifyIntent(question))
            .Returns(GameBookRole.None);

        GameBookRole capturedRoleHint = GameBookRole.RulesReference; // sentinel != None
        var hybridSearchMock = new Mock<IHybridSearchService>();
        hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<double>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Guid, SearchMode, int, List<Guid>?, float, float, double, GameBookRole, CancellationToken>(
                (q, g, m, l, d, vw, kw, ms, rh, ct) => capturedRoleHint = rh)
            .ReturnsAsync(new List<HybridSearchResult>());

        var handler = BuildHandler(intentClassifierMock.Object, hybridSearchMock.Object);

        // Act
        var query = new AskQuestionQuery(
            GameId: gameId,
            Question: question,
            SearchMode: "hybrid",
            BypassCache: true);

        await handler.Handle(query, TestCancellationToken);

        // Assert: None is faithfully propagated (the boost will be a no-op in D6).
        capturedRoleHint.Should().Be(GameBookRole.None);
    }

    private static AskQuestionQueryHandler BuildHandler(
        IIntentClassifierService intentClassifier,
        IHybridSearchService hybridSearchService)
    {
        // Build a real (lightweight) SearchQueryHandler with mocked dependencies so the
        // SearchQuery.QueryRoleHint actually flows through to the hybrid search spy.
        var embeddingRepoMock = new Mock<IEmbeddingRepository>();
        embeddingRepoMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(),
                It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());

        var searchEmbeddingServiceMock = new Mock<IEmbeddingService>();
        searchEmbeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[768] } });

        var ragAccessMock = new Mock<IRagAccessService>();
        ragAccessMock
            .Setup(r => r.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var searchHandler = new SearchQueryHandler(
            embeddingRepoMock.Object,
            new VectorSearchDomainService(),
            new RrfFusionDomainService(),
            searchEmbeddingServiceMock.Object,
            hybridSearchService,
            ragAccessMock.Object,
            new Mock<ILogger<SearchQueryHandler>>().Object);

        // The handler itself: most collaborators are no-op mocks. We exit early on empty
        // search results (no LLM call), which is what we want — this isolates the intent
        // classifier → SearchQuery wiring without dragging in LLM/Pricing/HouseRule paths
        // that have no bearing on D7.
        var pdfRepoMock = new Mock<IPdfDocumentRepository>();
        pdfRepoMock
            .Setup(r => r.FindByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PdfDocument>)Array.Empty<PdfDocument>());

        var qaTrackerMock = new Mock<IRagQualityTracker>();

        var pricingMock = new Mock<IPricingEngine>();
        pricingMock
            .Setup(p => p.ConsumeQuotaAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var routingMonitorMock = new Mock<IOptionsMonitor<LlmQueryComplexityRoutingOptions>>();
        routingMonitorMock.Setup(m => m.CurrentValue).Returns(new LlmQueryComplexityRoutingOptions());

        return new AskQuestionQueryHandler(
            searchHandler,
            new QualityTrackingDomainService(),
            new ChatContextDomainService(),
            new Mock<IChatThreadRepository>().Object,
            pdfRepoMock.Object,
            new Mock<ILlmService>().Object,
            new Mock<IPromptTemplateService>().Object,
            new Mock<IRagValidationPipelineService>().Object,
            ragAccessMock.Object,
            qaTrackerMock.Object,
            new QueryComplexityAnalyzer(),
            new Mock<ISemanticResponseCache>().Object,
            new Mock<IEmbeddingService>().Object,
            new Mock<IHouseRuleMatcher>().Object,
            pricingMock.Object,
            new Mock<IGenericTranslationService>().Object,
            intentClassifier,
            routingMonitorMock.Object,
            new Mock<ILogger<AskQuestionQueryHandler>>().Object);
    }
}
