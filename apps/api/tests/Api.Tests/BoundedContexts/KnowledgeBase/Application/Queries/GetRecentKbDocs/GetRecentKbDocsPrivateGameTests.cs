using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocs;

/// <summary>
/// Issue #4244. <c>GET /api/v1/kb-docs/recent</c> è il binario «documenti recenti» di Discover: lo
/// vede chiunque abbia una sessione, e il risultato sta in una cache condivisa. Elenca ogni PDF
/// arrivato a <c>Ready</c>. Finché il PDF di un gioco privato non si poteva indicizzare non ci
/// arrivava mai; ora ci arriva, e il suo nome non deve finire in quell'elenco.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed class GetRecentKbDocsPrivateGameTests : IDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly GetRecentKbDocsQueryHandler _sut;

    public GetRecentKbDocsPrivateGameTests()
    {
        // La cache esegue sempre la sua fabbrica: qui si prova il filtro, non la cache.
        var cache = new Mock<IHybridCacheService>();
        cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<RecentKbDocDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<RecentKbDocDto>>>, string[]?, TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        var sharedGames = new Mock<ISharedGameRepository>();
        sharedGames
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _sut = new GetRecentKbDocsQueryHandler(
            _db, sharedGames.Object, cache.Object, NullLogger<GetRecentKbDocsQueryHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task TheReadyPdfOfAPrivateGame_IsNotListed()
    {
        var privatePdf = await SeedReadyPdfAsync("il-mio-prototipo.pdf", sharedGameId: null, privateGameId: Guid.NewGuid());

        var recent = await _sut.Handle(new GetRecentKbDocsQuery(10), TestContext.Current.CancellationToken);

        recent.Should().NotContain(d => d.Id == privatePdf);
        recent.Select(d => d.Title).Should().NotContain("il-mio-prototipo");
    }

    /// <summary>Il controllo: il PDF di un gioco del catalogo resta nell'elenco.</summary>
    [Fact]
    public async Task TheReadyPdfOfASharedGame_IsListed()
    {
        var sharedPdf = await SeedReadyPdfAsync("regolamento.pdf", sharedGameId: Guid.NewGuid(), privateGameId: null);
        await SeedReadyPdfAsync("il-mio-prototipo.pdf", sharedGameId: null, privateGameId: Guid.NewGuid());

        var recent = await _sut.Handle(new GetRecentKbDocsQuery(10), TestContext.Current.CancellationToken);

        recent.Should().ContainSingle().Which.Id.Should().Be(sharedPdf);
    }

    private async Task<Guid> SeedReadyPdfAsync(string fileName, Guid? sharedGameId, Guid? privateGameId)
    {
        var id = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = fileName,
            FilePath = $"/tmp/{fileName}",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }
}
