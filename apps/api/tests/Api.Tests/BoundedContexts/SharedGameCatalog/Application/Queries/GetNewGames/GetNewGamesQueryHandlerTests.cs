using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Issue #4085 — <see cref="GetNewGamesQueryHandler"/> must resolve <c>ImageUrl</c> via
/// <c>CoverUrlResolver</c> instead of the <c>SharedGameEntity.ImageUrl</c>/<c>ThumbnailUrl</c>
/// tombstone columns (#2123: always empty since the BGG user-side asset ban). No behavior
/// tests existed for this handler before this issue.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4085")]
public sealed class GetNewGamesQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<ILogger<GetNewGamesQueryHandler>> _loggerMock;
    private readonly GetNewGamesQueryHandler _handler;

    public GetNewGamesQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _cacheMock = new Mock<IHybridCacheService>();
        _blobStorageMock = new Mock<IBlobStorageService>();
        _loggerMock = new Mock<ILogger<GetNewGamesQueryHandler>>();

        // Cache pass-through: invoke factory directly so we test handler logic, not cache.
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<NewGameDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<List<NewGameDto>>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));

        _handler = new GetNewGamesQueryHandler(_db, _cacheMock.Object, _blobStorageMock.Object, _loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private SharedGameEntity SeedGame(
        string title,
        string? pdfCoverR2Key = null,
        GameStatus status = GameStatus.Published,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var entity = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = 2024,
            Description = "Test game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = (int)status,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = createdAt ?? DateTime.UtcNow,
            IsDeleted = isDeleted,
            PdfCoverR2Key = pdfCoverR2Key,
        };
        _db.SharedGames.Add(entity);
        return entity;
    }

    /// <summary>
    /// #4659. La rail è pubblica per ogni utente autenticato, e il dettaglio che apre una card risponde 404 a
    /// chi non è admin per i giochi non pubblicati (<c>HandleGetGameById</c>): bozze, giochi in approvazione e
    /// archiviati non devono comparire, come non compaiono nella lista del catalogo (default <c>Published</c>).
    /// </summary>
    [Fact]
    public async Task Handle_ListsOnlyPublishedGames()
    {
        SeedGame("Draft", status: GameStatus.Draft);
        SeedGame("Pending", status: GameStatus.PendingApproval);
        SeedGame("Archived", status: GameStatus.Archived);
        SeedGame("Deleted", status: GameStatus.Published, isDeleted: true);
        SeedGame("Published");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetNewGamesQuery(Limit: 10),
            TestContext.Current.CancellationToken);

        result.Select(g => g.Name).Should().Equal("Published");
    }

    /// <summary>
    /// Il filtro sta nella query, prima del tetto di 50 righe della voce in cache: se stesse dopo, una
    /// cinquantina di bozze recenti spingerebbero fuori ogni gioco pubblicato e la rail resterebbe vuota.
    /// </summary>
    [Fact]
    public async Task Handle_TheStatusFilterComesBeforeTheCacheCap_SoNewerDraftsDoNotCrowdOutPublishedGames()
    {
        var now = DateTime.UtcNow;
        // Più bozze del tetto della voce in cache: il tetto lo dice l'handler, non un numero scritto qui.
        for (var i = 0; i <= GetNewGamesQueryHandler.MaxCacheLimit; i++)
        {
            SeedGame($"Draft {i}", status: GameStatus.Draft, createdAt: now.AddMinutes(-i));
        }
        SeedGame("Older published game", createdAt: now.AddDays(-30));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetNewGamesQuery(Limit: 10),
            TestContext.Current.CancellationToken);

        result.Select(g => g.Name).Should().Equal("Older published game");
    }

    [Fact]
    public async Task Handle_ResolvesImageUrl_FromSeededPdfCoverKey()
    {
        SeedGame("Wingspan", pdfCoverR2Key: "pdf-cover-db-key");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string resolvedUrl = "https://r2.example.test/covers/pdf/wingspan-preview.webp";
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(resolvedUrl);

        var result = await _handler.Handle(
            new GetNewGamesQuery(Limit: 10),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].ImageUrl.Should().Be(resolvedUrl);
    }

    [Fact]
    public async Task Handle_NoCoverKeyAnywhere_ReturnsNullImageUrl_NotTheTombstone()
    {
        // #4085 regression guard: before the fix, this handler's projection fell back to
        // SharedGameEntity.ImageUrl/ThumbnailUrl when both are empty strings (the real
        // #2123 state) — ImageUrl would then be `null` in BOTH the buggy and fixed code
        // for this exact case, which is why Handle_ResolvesImageUrl_FromSeededPdfCoverKey
        // above is the test that actually discriminates. This test exists to pin the
        // "no key → null, not an empty-string tombstone value" contract explicitly.
        SeedGame("Codenames");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetNewGamesQuery(Limit: 10),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].ImageUrl.Should().BeNull();
        _blobStorageMock.Verify(
            b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()),
            Times.Never);
    }
}
