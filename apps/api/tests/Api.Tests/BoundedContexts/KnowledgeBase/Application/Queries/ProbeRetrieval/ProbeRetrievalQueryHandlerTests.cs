using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SearchResult = Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;

/// <summary>
/// #4482: la sonda di solo recupero fa ciò che fa la chat per un gioco — lingua dal resolver, versione
/// pubblicata del profilo, recupero con i suoi numeri — e mostra il pool prima del reranker e i finali.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4482")]
public sealed class ProbeRetrievalQueryHandlerTests
{
    private static readonly Guid Game = Guid.Parse("00000000-0000-0000-0000-0000000044a2");
    private static readonly Guid VersionId = Guid.Parse("00000000-0000-0000-0000-0000000044b7");
    private const string Question = "Brigante su più giocatori: a chi rubo?";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Mock<ILanguageDetectionService> _detector = new();

    public ProbeRetrievalQueryHandlerTests()
    {
        // Come per una domanda corta: il rilevatore non è sicuro, e decide l'Accept-Language.
        _detector.Setup(d => d.Detect(It.IsAny<string>())).Returns(new LanguageDetection("en", IsConfident: false));
        _detector.Setup(d => d.IsSupportedLanguage(It.IsAny<string?>()))
            .Returns<string?>(l => l is "en" or "it" or "de" or "fr" or "es");
    }

    private ProbeRetrievalQueryHandler Handler(
        FusedSearchStub search, AgentProfileContent profile, ICrossEncoderReranker? reranker = null) => new(
        ProfileReader(profile),
        _detector.Object,
        AnswerPipelineTestDoubles.Retrieval(search.Handler, reranker ?? AnswerRetrievalTests.ReverseReranker()),
        NullLogger<ProbeRetrievalQueryHandler>.Instance);

    private static IPublishedAgentProfileReader ProfileReader(AgentProfileContent profile)
    {
        var reader = new Mock<IPublishedAgentProfileReader>();
        reader.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishedAgentProfile(VersionId, 7, profile));
        return reader.Object;
    }

    [Fact]
    public async Task Handle_SearchesWithThePublishedProfilesNumbers_AndReportsThem()
    {
        var profile = ProbeProfiles.With(
            candidatePoolSize: 12, finalTopK: 3, minScore: 0.4m, vectorWeight: 0.6m, keywordWeight: 0.4m, rerankerEnabled: true);
        var search = new FusedSearchStub(FusedSearchStub.Chunks(15));

        var probe = await Handler(search, profile).Handle(new ProbeRetrievalQuery(Game, Question), Ct);

        // Lo stesso recupero della chat: il pool del profilo a ciascun braccio, la sua soglia sul
        // braccio vettoriale e i suoi pesi sulla fusione.
        search.Embeddings.Verify(e => e.SearchByVectorWithScoresAsync(
            Game, It.IsAny<Vector>(), 12, 0.4, null, It.IsAny<CancellationToken>()), Times.Once);
        search.Keywords.Verify(k => k.SearchAsync(
            Question, Game, 12, It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Once);
        search.Fusion.Verify(f => f.FuseResults(
            It.IsAny<List<SearchResult>>(), It.IsAny<List<SearchResult>>(), It.IsAny<int>(), It.IsAny<GameBookRole>(),
            It.IsAny<IReadOnlyList<string>?>(), 0.6f, 0.4f), Times.Once);

        probe.ProfileVersionId.Should().Be(VersionId);
        probe.ProfileVersionNumber.Should().Be(7);
        probe.Parameters.CandidatePoolSize.Should().Be(12);
        probe.Parameters.FinalTopK.Should().Be(3);
        probe.Parameters.MinScore.Should().Be(0.4m);
        probe.Parameters.VectorWeight.Should().Be(0.6m);
        probe.Parameters.KeywordWeight.Should().Be(0.4m);
        probe.Parameters.RerankerEnabled.Should().BeTrue();
        probe.Parameters.SearchMode.Should().Be("hybrid");
    }

    [Fact]
    public async Task Handle_ReturnsThePoolInFusionOrder_AndTheFinalsInRerankerOrder()
    {
        var profile = ProbeProfiles.With(candidatePoolSize: 12, finalTopK: 3);
        var search = new FusedSearchStub(FusedSearchStub.Chunks(15));

        var probe = await Handler(search, profile).Handle(new ProbeRetrievalQuery(Game, Question), Ct);

        probe.FusedCount.Should().Be(15);
        probe.Pool.Select(c => c.Position).Should().Equal(Enumerable.Range(1, 12));
        probe.Pool.Select(c => c.Text).Should().Equal(Enumerable.Range(1, 12).Select(n => $"chunk {n}"));
        probe.Pool.Should().AllSatisfy(c =>
        {
            c.PoolPosition.Should().BeNull();
            c.RerankScore.Should().BeNull();
        });

        probe.Reranking.Should().Be("Applied");
        probe.Final.Select(c => c.Position).Should().Equal(1, 2, 3);
        probe.Final.Select(c => c.PoolPosition).Should().Equal(12, 11, 10);
        probe.Final.Select(c => c.RerankScore).Should().Equal(1.0, 0.9, 0.8);
        probe.Final.Select(c => c.Text).Should().Equal("chunk 12", "chunk 11", "chunk 10");
    }

    [Fact]
    public async Task Handle_CarriesTheIdentityAndTheScoresOfEachChunk()
    {
        var profile = ProbeProfiles.With(candidatePoolSize: 4, finalTopK: 2);
        var search = new FusedSearchStub(FusedSearchStub.Chunks(4));

        var probe = await Handler(search, profile).Handle(new ProbeRetrievalQuery(Game, Question), Ct);

        var second = probe.Pool[1];
        var expected = FusedSearchStub.Chunk(2);
        second.TextChunkId.Should().Be(FusedSearchStub.TextChunkId(2));
        second.VectorDocumentId.Should().Be(FusedSearchStub.VectorDocumentId.ToString());
        second.PdfDocumentId.Should().Be(FusedSearchStub.PdfDocumentId.ToString());
        second.ChunkIndex.Should().Be(20);
        second.PageNumber.Should().Be(2);
        second.Language.Should().Be("en");
        second.RelevanceScore.Should().Be(expected.RelevanceScore.Value);
        second.HybridScore.Should().Be(expected.Signals.HybridScore);
        second.VectorScore.Should().Be(expected.Signals.VectorScore);
        second.KeywordScore.Should().Be(0.1f);
        second.MatchedTerms.Should().Equal("robber");
        second.SearchMethod.Should().Be("hybrid");
    }

    [Fact]
    public async Task Handle_WithoutAcceptLanguage_UsesItalian_ThroughTheChatsResolver()
    {
        var search = new FusedSearchStub(FusedSearchStub.Chunks(3));

        var probe = await Handler(search, ProbeProfiles.With()).Handle(new ProbeRetrievalQuery(Game, Question), Ct);

        probe.AcceptLanguage.Should().Be("it");
        probe.Language.Should().Be("it");
        probe.LanguageSource.Should().Be(nameof(AnswerLanguageSource.AcceptLanguage));
        // La lingua guida il recupero come nella chat: è quella con cui si calcola l'embedding.
        search.Embedder.Verify(e => e.GenerateEmbeddingAsync(
            Question, "it", EmbeddingPurpose.Query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheQuestionSaysItsLanguage_TheDetectionWins_AsInTheChat()
    {
        _detector.Setup(d => d.Detect(It.IsAny<string>())).Returns(new LanguageDetection("it", IsConfident: true));
        var search = new FusedSearchStub(FusedSearchStub.Chunks(3));

        var probe = await Handler(search, ProbeProfiles.With())
            .Handle(new ProbeRetrievalQuery(Game, Question, AcceptLanguage: "en-US,en;q=0.9"), Ct);

        probe.AcceptLanguage.Should().Be("en-US,en;q=0.9");
        probe.Language.Should().Be("it");
        probe.LanguageSource.Should().Be(nameof(AnswerLanguageSource.Detected));
    }

    [Fact]
    public async Task Handle_WhenTheRerankerFails_SaysTheFinalsAreTheFirstOfThePool()
    {
        var reranker = new Mock<ICrossEncoderReranker>();
        reranker
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("reranker down"));
        var search = new FusedSearchStub(FusedSearchStub.Chunks(10));

        var probe = await Handler(search, ProbeProfiles.With(candidatePoolSize: 10, finalTopK: 3), reranker.Object)
            .Handle(new ProbeRetrievalQuery(Game, Question), Ct);

        probe.Reranking.Should().Be("FellBack");
        probe.Final.Select(c => c.PoolPosition).Should().Equal(1, 2, 3);
        probe.Final.Should().AllSatisfy(c => c.RerankScore.Should().BeNull());
    }

    [Fact]
    public async Task Handle_WithoutAPublishedProfile_Refuses503_AndSearchesNothing()
    {
        var reader = new Mock<IPublishedAgentProfileReader>();
        reader.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new AgentProfileNotPublishedException());
        var search = new FusedSearchStub(FusedSearchStub.Chunks(3));
        var handler = new ProbeRetrievalQueryHandler(
            reader.Object, _detector.Object,
            AnswerPipelineTestDoubles.Retrieval(search.Handler, AnswerRetrievalTests.ReverseReranker()),
            NullLogger<ProbeRetrievalQueryHandler>.Instance);

        var act = () => handler.Handle(new ProbeRetrievalQuery(Game, Question), Ct);

        var refused = (await act.Should().ThrowAsync<HttpException>()).Which;
        refused.StatusCode.Should().Be(503);
        refused.ErrorCode.Should().Be(AgentProfileNotPublishedException.Code);
        search.Fusion.VerifyNoOtherCalls();
    }
}
