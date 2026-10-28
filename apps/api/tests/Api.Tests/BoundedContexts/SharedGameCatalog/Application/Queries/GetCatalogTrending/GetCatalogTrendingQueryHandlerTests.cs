using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

/// <summary>
/// Unit tests for <see cref="GetCatalogTrendingQueryHandler"/>.
///
/// Issue #2290: pin the contract that <see cref="TrendingGameDto.HasKnowledgeBase"/>
/// is populated from the joined <see cref="SharedGameEntity.HasKnowledgeBase"/>
/// projection, so the Discover Row 1 KB badge can render without an N+1 lookup
/// on <see cref="SharedGameDto"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2290")]
public sealed class GetCatalogTrendingQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IHybridCacheService> _cacheMock;
    // #4085: cover resolution reads the blob storage AFTER the cache boundary (see
    // TrendingGameDto.CoverUrl). Loose mock — the seeded games here carry no raw cover
    // key, so the resolver short-circuits to the placeholder path without calling it.
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<ILogger<GetCatalogTrendingQueryHandler>> _loggerMock;
    private readonly GetCatalogTrendingQueryHandler _handler;

    public GetCatalogTrendingQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _cacheMock = new Mock<IHybridCacheService>();
        _blobStorageMock = new Mock<IBlobStorageService>();
        _loggerMock = new Mock<ILogger<GetCatalogTrendingQueryHandler>>();

        // Cache pass-through: invoke factory directly so we test handler logic, not cache.
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<List<TrendingGameDto>>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));

        _handler = new GetCatalogTrendingQueryHandler(_db, _cacheMock.Object, _blobStorageMock.Object, _loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_ProjectsHasKnowledgeBase_FromSharedGameEntity_Issue2290()
    {
        // Arrange — two games with distinct HasKnowledgeBase values and one
        // analytics event per game so both surface in the trending result set.
        var gameA = SeedGame(hasKnowledgeBase: true, title: "AI-Ready Game");
        var gameB = SeedGame(hasKnowledgeBase: false, title: "Plain Game");

        SeedEvent(gameA.Id, GameEventType.Play);   // weight 10 → ranks first
        SeedEvent(gameB.Id, GameEventType.Search); // weight 3  → ranks second
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2, "both games have at least one event in the last 7 days");

        var aiReady = result.Single(r => r.GameId == gameA.Id);
        aiReady.HasKnowledgeBase.Should().BeTrue(
            "GameA has HasKnowledgeBase=true on the entity — the new projection must surface it");
        aiReady.Title.Should().Be("AI-Ready Game");

        var plain = result.Single(r => r.GameId == gameB.Id);
        plain.HasKnowledgeBase.Should().BeFalse(
            "GameB has HasKnowledgeBase=false on the entity — the projection must preserve the negative case");
        plain.Title.Should().Be("Plain Game");
    }

    [Fact]
    public async Task Handle_OmitsAnEvent_WhoseGameRowIsGone_Issue4659()
    {
        // Arrange — one analytics event but no SharedGameEntity row (orphan event row, e.g. game
        // deleted after the event was recorded). Before #4659 this surfaced as a card titled
        // "Unknown Game" whose link opens a detail that answers 404; the rail now leaves it out.
        // The handler must also not throw on the missing row (#2290).
        var orphanGameId = Guid.NewGuid();
        SeedEvent(orphanGameId, GameEventType.View);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty("a game that no longer exists is not a card the rail can open");
    }

    /// <summary>
    /// #4659. Il dettaglio che apre una card risponde 404 a chi non è admin per i giochi non pubblicati, e la
    /// lista del catalogo mostra solo i <c>Published</c>: la rail fa lo stesso. Gli eventi di un gioco che era
    /// pubblicato e poi è stato archiviato o cancellato restano nei sette giorni di calcolo.
    /// </summary>
    [Fact]
    public async Task Handle_RanksOnlyPublishedGames_Issue4659()
    {
        var published = SeedGame(hasKnowledgeBase: false, title: "Published");
        var draft = SeedGame(hasKnowledgeBase: false, title: "Draft", status: GameStatus.Draft);
        var pending = SeedGame(hasKnowledgeBase: false, title: "Pending", status: GameStatus.PendingApproval);
        var archived = SeedGame(hasKnowledgeBase: false, title: "Archived", status: GameStatus.Archived);
        var deleted = SeedGame(hasKnowledgeBase: false, title: "Deleted", isDeleted: true);
        foreach (var game in new[] { draft, pending, archived, deleted, published })
        {
            SeedEvent(game.Id, GameEventType.Play);
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        result.Select(r => r.Title).Should().Equal("Published");
    }

    /// <summary>
    /// La posizione riparte da 1 dopo l'esclusione: un gioco archiviato in testa alla classifica non lascia un
    /// buco (la rail mostrerebbe 2, 3 e nessun 1).
    /// </summary>
    [Fact]
    public async Task Handle_TheRankStartsAtOneAfterTheExclusion_Issue4659()
    {
        var archivedTop = SeedGame(hasKnowledgeBase: false, title: "Archived top", status: GameStatus.Archived);
        var first = SeedGame(hasKnowledgeBase: false, title: "First published");
        var second = SeedGame(hasKnowledgeBase: false, title: "Second published");
        SeedEvent(archivedTop.Id, GameEventType.Play);
        SeedEvent(archivedTop.Id, GameEventType.Play);
        SeedEvent(first.Id, GameEventType.Play);
        SeedEvent(second.Id, GameEventType.Search);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        result.Select(r => (r.Rank, r.Title)).Should().Equal((1, "First published"), (2, "Second published"));
    }

    /// <summary>
    /// Il filtro sta PRIMA del tetto della voce in cache (50 righe, <c>MaxCacheLimit</c>): se i giochi non
    /// pubblicati si scartassero dopo il <c>Take</c>, cinquanta archiviati più in vista dei pubblicati ne
    /// occuperebbero tutti i posti e la rail resterebbe vuota, pur avendo dati per riempirla.
    /// </summary>
    [Fact]
    public async Task Handle_TheCacheCapAppliesAfterThePublishedFilter_Issue4659()
    {
        // Tanti archiviati quanto il tetto della voce in cache: il tetto lo dice l'handler, non un numero scritto qui.
        for (var i = 0; i < GetCatalogTrendingQueryHandler.MaxCacheLimit; i++)
        {
            var archived = SeedGame(hasKnowledgeBase: false, title: $"Archived {i}", status: GameStatus.Archived);
            SeedEvent(archived.Id, GameEventType.Play);
        }
        var first = SeedGame(hasKnowledgeBase: false, title: "First published");
        var second = SeedGame(hasKnowledgeBase: false, title: "Second published");
        SeedEvent(first.Id, GameEventType.View);
        SeedEvent(second.Id, GameEventType.Search);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        result.Select(r => r.Title).Should().Equal("Second published", "First published");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // #4085 — CoverUrl resolution. Resolved per-request, AFTER the cache read: see
    // TrendingGameDto.CoverUrl for why baking a 4h-expiring presign into this 12h cache
    // would be worse than no cover.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ResolvesCoverUrl_FromSeededPdfCoverKey()
    {
        var game = SeedGame(hasKnowledgeBase: false, title: "Wingspan");
        game.PdfCoverR2Key = "pdf-cover-db-key";
        SeedEvent(game.Id, GameEventType.View);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string resolvedUrl = "https://r2.example.test/covers/pdf/wingspan-preview.webp";
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(resolvedUrl);

        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].CoverUrl.Should().Be(resolvedUrl);
    }

    [Fact]
    public async Task Handle_ResolvesCoverUrl_FreshOnEachCall_EvenOnAGenuineCacheHit()
    {
        // #4085 — the whole point of resolving post-cache: a 12h cache HIT must still
        // produce a FRESH presigned URL, because the 4h-lived URL from 8+ hours ago would
        // already be dead. This re-points the cache mock (set up in the ctor to always
        // invoke the factory) so the SECOND call returns the first call's result WITHOUT
        // invoking the factory again — a genuine cache hit, where ComputeTrendingAsync
        // (and anything resolved inside it) does NOT run a second time. If CoverUrl were
        // resolved inside that cached factory instead of in Handle() after it, this second
        // call would never touch the blob-storage mock again and would still carry the
        // FIRST presign — failing the assertion below.
        var game = SeedGame(hasKnowledgeBase: false, title: "Wingspan");
        game.PdfCoverR2Key = "pdf-cover-db-key";
        SeedEvent(game.Id, GameEventType.View);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _blobStorageMock
            .SetupSequence(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://r2.example.test/covers/pdf/first-presign.webp")
            .ReturnsAsync("https://r2.example.test/covers/pdf/second-presign-after-first-expired.webp");

        List<TrendingGameDto>? cachedValue = null;
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (
                string _,
                Func<CancellationToken, Task<List<TrendingGameDto>>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) =>
            {
                if (cachedValue is null)
                {
                    cachedValue = await factory(ct).ConfigureAwait(false); // miss: compute + store
                }
                return cachedValue; // hit: factory NOT invoked again
            });

        var query = new GetCatalogTrendingQuery { Limit = 10 };
        var first = await _handler.Handle(query, TestContext.Current.CancellationToken);
        var second = await _handler.Handle(query, TestContext.Current.CancellationToken);

        first[0].CoverUrl.Should().Be("https://r2.example.test/covers/pdf/first-presign.webp");
        second[0].CoverUrl.Should().Be(
            "https://r2.example.test/covers/pdf/second-presign-after-first-expired.webp",
            "#4085: the second call hit the cache (factory not re-invoked) — CoverUrl must " +
            "still be resolved fresh in Handle(), not inherited from the cached DTO");
    }

    private SharedGameEntity SeedGame(
        bool hasKnowledgeBase,
        string title,
        GameStatus status = GameStatus.Published,
        bool isDeleted = false)
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
            CreatedAt = DateTime.UtcNow,
            HasKnowledgeBase = hasKnowledgeBase,
            IsDeleted = isDeleted,
        };
        _db.SharedGames.Add(entity);
        return entity;
    }

    private void SeedEvent(Guid gameId, GameEventType eventType)
    {
        _db.Set<GameAnalyticsEventEntity>().Add(new GameAnalyticsEventEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            EventType = (int)eventType,
            UserId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddHours(-1),
        });
    }
}
