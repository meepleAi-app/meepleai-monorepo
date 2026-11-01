using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4782 — a document that has several pages and whose passages all claim the same one was indexed without the page
/// of each passage (a document the chunker had no structure for gets a single section on page 1). Its pages are not
/// facts, and a source that says «p. 1» for every question sends the reader to the wrong place.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetDocumentsWithUnreliablePageNumbersQueryHandlerTests
{
    private readonly MeepleAiDbContext _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly GetDocumentsWithUnreliablePageNumbersQueryHandler _sut;

    public GetDocumentsWithUnreliablePageNumbersQueryHandlerTests()
    {
        _sut = new GetDocumentsWithUnreliablePageNumbersQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ADocumentOfManyPagesWhoseChunksAllSayPageOne_IsUnreliable()
    {
        var rulebook = await SeedAsync(pageCount: 12, chunkPages: [1, 1, 1, 1]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([rulebook]), CancellationToken.None);

        result.Should().Equal(rulebook);
    }

    [Fact]
    public async Task Handle_ADocumentWhoseChunksTellPagesApart_IsReliable()
    {
        var rulebook = await SeedAsync(pageCount: 12, chunkPages: [1, 3, 3, 7]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([rulebook]), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TwoPagesTwoNumbers_IsReliable_TheSmallestDocumentThatCanTellThemApart()
    {
        var leaflet = await SeedAsync(pageCount: 2, chunkPages: [1, 2]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([leaflet]), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ADocumentOfOnePage_IsReliableEvenIfEveryChunkSaysPageOne()
    {
        // Nothing to confuse: a one-page document is on page 1 throughout, and that is true.
        var card = await SeedAsync(pageCount: 1, chunkPages: [1, 1, 1]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([card]), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ADocumentWhosePageCountIsNotKnown_IsGivenTheBenefitOfTheDoubt()
    {
        // Without the count there is nothing to say the numbers are wrong.
        var unknown = await SeedAsync(pageCount: null, chunkPages: [1, 1, 1]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([unknown]), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ADocumentOfManyPagesWhoseChunksCarryNoPage_IsUnreliable()
    {
        var rulebook = await SeedAsync(pageCount: 8, chunkPages: [null, null, null]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([rulebook]), CancellationToken.None);

        result.Should().Equal(rulebook);
    }

    [Fact]
    public async Task Handle_OnlyTheDocumentsAskedAbout_AreAnswered_EachOnItsOwn()
    {
        var flat = await SeedAsync(pageCount: 40, chunkPages: [1, 1]);
        var withPages = await SeedAsync(pageCount: 40, chunkPages: [2, 9]);
        var notAsked = await SeedAsync(pageCount: 40, chunkPages: [1, 1]);

        var result = await _sut.Handle(
            new GetDocumentsWithUnreliablePageNumbersQuery([flat, withPages, flat]),
            CancellationToken.None);

        result.Should().Equal(flat);
        result.Should().NotContain(notAsked);
    }

    [Fact]
    public async Task Handle_NoDocumentAsked_AnswersNothing()
    {
        await SeedAsync(pageCount: 40, chunkPages: [1, 1]);

        var result = await _sut.Handle(new GetDocumentsWithUnreliablePageNumbersQuery([]), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ADocumentThatIsNotThere_IsNotInTheAnswer()
    {
        var result = await _sut.Handle(
            new GetDocumentsWithUnreliablePageNumbersQuery([Guid.NewGuid()]),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    private async Task<Guid> SeedAsync(int? pageCount, int?[] chunkPages)
    {
        var id = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"{id:N}.pdf",
            FilePath = $"/tmp/{id:N}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            IsPublic = true,
            PageCount = pageCount,
        });
        for (var i = 0; i < chunkPages.Length; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = id,
                Content = $"passage {i}",
                ChunkIndex = i,
                CharacterCount = 9,
                PageNumber = chunkPages[i],
                ElementType = "NarrativeText",
                Level = 1,
            });
        }

        await _dbContext.SaveChangesAsync();
        return id;
    }
}
