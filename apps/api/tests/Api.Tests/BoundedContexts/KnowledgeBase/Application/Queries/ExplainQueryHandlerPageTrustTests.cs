using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Infrastructure.Entities;
using Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DomainSearchResult = Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4819: the citations of an explanation carry the page the search found, except for a document that cannot vouch
/// for the pages of its passages (#4782), which is cited without one. The search is a real
/// <see cref="SearchQueryHandler"/> over doubles, as in the Ask handler tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4819")]
public sealed class ExplainQueryHandlerPageTrustTests
{
    private readonly SearchQueryHandler _searchHandler;
    private readonly Mock<RrfFusionDomainService> _rrf = new();

    public ExplainQueryHandlerPageTrustTests()
    {
        var embeddingRepo = new Mock<IEmbeddingRepository>();
        var vectorSearch = new Mock<VectorSearchDomainService>();
        var searchEmbedding = new Mock<IEmbeddingService>();
        var keywordSearch = new Mock<IKeywordSearchService>();

        searchEmbedding
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = [new float[768]] });
        embeddingRepo
            .Setup(r => r.SearchByVectorAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());
        embeddingRepo
            .Setup(r => r.SearchByVectorWithScoresAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());
        vectorSearch
            .Setup(v => v.ValidateSearchParameters(It.IsAny<int>(), It.IsAny<double>()))
            .Callback((int _, double _) => { });
        vectorSearch
            .Setup(v => v.Search(It.IsAny<Vector>(), It.IsAny<List<Embedding>>(), It.IsAny<int>(), It.IsAny<double>()))
            .Returns(new List<DomainSearchResult>());
        keywordSearch
            .Setup(h => h.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        var access = new Mock<IRagAccessService>();
        access
            .Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _searchHandler = new SearchQueryHandler(
            embeddingRepo.Object,
            vectorSearch.Object,
            _rrf.Object,
            searchEmbedding.Object,
            keywordSearch.Object,
            access.Object,
            Mock.Of<ILogger<SearchQueryHandler>>());
    }

    private void SearchFinds(Guid pdfDocumentId, int page) => _rrf
        .Setup(r => r.FuseResults(
            It.IsAny<List<DomainSearchResult>>(),
            It.IsAny<List<DomainSearchResult>>(),
            It.IsAny<int>(),
            It.IsAny<GameBookRole>(),
            It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<float>(),
            It.IsAny<float>()))
        .Returns(
        [
            new DomainSearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: "Si pescano quattro tessere.",
                pageNumber: page,
                relevanceScore: new Confidence(0.85),
                rank: 1,
                searchMethod: "hybrid",
                pdfDocumentId: pdfDocumentId),
        ]);

    private ExplainQueryHandler Handler(ICitedPageTrust trust) =>
        new(_searchHandler, trust, NullLogger<ExplainQueryHandler>.Instance);

    private static ExplainQuery Query() =>
        new(GameId: Guid.NewGuid(), Topic: "Come si pescano le tessere", UserId: Guid.NewGuid(), UserRole: "User");

    [Fact]
    public async Task Handle_CitesADocumentThatCannotVouchForItsPagesWithoutOne()
    {
        var flat = Guid.NewGuid();
        SearchFinds(flat, page: 4);

        var result = await Handler(CitedPageTrustTestDoubles.WithoutAPage(flat.ToString()))
            .Handle(Query(), TestContext.Current.CancellationToken);

        result.Citations.Should().ContainSingle().Which.PageNumber.Should().BeNull();
        // The script is text built from the same passages: its heading does not name the page either.
        result.Script.Should().Contain("### Section 1");
        result.Script.Should().NotContain("Page ");
    }

    [Fact]
    public async Task Handle_CitesTheDeclaredPageOfADocumentThatCanVouchForIt()
    {
        var measured = Guid.NewGuid();
        SearchFinds(measured, page: 4);

        var result = await Handler(CitedPageTrustTestDoubles.WithoutAPage(Guid.NewGuid().ToString()))
            .Handle(Query(), TestContext.Current.CancellationToken);

        result.Citations.Should().ContainSingle().Which.PageNumber.Should().Be(4);
        result.Script.Should().Contain("### Section 1 (Page 4)");
    }
}
