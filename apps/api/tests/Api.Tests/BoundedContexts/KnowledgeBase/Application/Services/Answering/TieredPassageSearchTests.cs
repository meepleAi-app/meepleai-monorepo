using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4410 (E1): il passo «recupero → citazioni con tier» della pipeline, riusabile da cerca_regole.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4410")]
public sealed class TieredPassageSearchTests
{
    private static readonly Guid Game = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid Asker = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid VectorDoc = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Pdf = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    private readonly Mock<IVectorDocumentRepository> _vectorDocuments = new();
    private readonly Mock<ICopyrightTierResolver> _tiers = new();

    public TieredPassageSearchTests()
    {
        _vectorDocuments.Setup(v => v.GetByGameIdAsync(Game, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { new(VectorDoc, Game, Pdf, "en", totalChunks: 1) });
        _tiers.Setup(t => t.ResolveAsync(It.IsAny<IReadOnlyList<ChunkCitation>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkCitation> citations, Guid _, CancellationToken _) =>
                citations.Select(c => c with { CopyrightTier = CopyrightTier.Full }).ToList());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Il recupero non serve a CiteAsync: un'istanza valida e inerte.
    private TieredPassageSearch Search() => new(
        AnswerPipelineTestDoubles.RetrievalAcrossGames(Mock.Of<IMultiGameHybridSearchService>()),
        _vectorDocuments.Object,
        _tiers.Object);

    private static SearchResultDto Result(string vectorDocumentId, string text = "Si pescano 4 tessere.", int page = 3) =>
        new(vectorDocumentId, text, page, 0.9, 1, "hybrid",
            BoundingBoxesJson: "[{\"page\":3}]", CharStart: 10, CharEnd: 30);

    [Fact]
    public async Task CiteAsync_PutsThePdfIdInTheCitation_AndTheVectorIdWhenItCannotResolveIt()
    {
        var unknown = Guid.NewGuid().ToString();

        var citations = await Search().CiteAsync(Game, [Result(VectorDoc.ToString()), Result(unknown)], Asker, Ct);

        citations.Select(c => c.DocumentId).Should().Equal(Pdf.ToString(), unknown);
    }

    [Fact]
    public async Task CiteAsync_ResolvesTheTierForTheUserWhoAsks()
    {
        await Search().CiteAsync(Game, [Result(VectorDoc.ToString())], Asker, Ct);

        _tiers.Verify(t => t.ResolveAsync(It.IsAny<IReadOnlyList<ChunkCitation>>(), Asker, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Il testo letterale resta in ogni citazione (è il contesto del modello); regioni e offset viaggiano fino a
    // chi costruisce la forma per il client, che li limita al tier Full.
    [Fact]
    public async Task CiteAsync_KeepsTheTextAndTheRegionCarriers_ForTheCallerToGate()
    {
        var citation = (await Search().CiteAsync(Game, [Result(VectorDoc.ToString())], Asker, Ct)).Single();

        citation.SnippetPreview.Should().Be("Si pescano 4 tessere.");
        citation.PageNumber.Should().Be(3);
        citation.BoundingBoxesJson.Should().Be("[{\"page\":3}]");
        citation.CharStart.Should().Be(10);
        citation.CharEnd.Should().Be(30);
    }
}
