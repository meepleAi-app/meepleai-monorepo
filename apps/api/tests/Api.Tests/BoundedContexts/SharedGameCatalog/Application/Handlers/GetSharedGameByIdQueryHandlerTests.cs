using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class GetSharedGameByIdQueryHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<ILogger<GetSharedGameByIdQueryHandler>> _loggerMock;

    public GetSharedGameByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<GetSharedGameByIdQueryHandler>>();
        _blobStorageMock = new Mock<IBlobStorageService>();
        // Default: presigned URL returns null (dev/local — no R2 configured).
        _blobStorageMock
            .Setup(b => b.GetPresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private GetSharedGameByIdQueryHandler CreateHandler(MeepleAiDbContext db) =>
        new(_repositoryMock.Object, db, _blobStorageMock.Object, CreateHybridCache(), CreateConfiguration(), _loggerMock.Object);

    [Fact]
    public async Task Handle_WithExistingGame_ReturnsDetailDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var game = SharedGame.Create(
            "Catan",
            1995,
            "Description",
            3,
            4,
            90,
            10,
            2.5m,
            7.8m,
            "https://example.com/catan.jpg",
            "https://example.com/thumb.jpg",
            GameRules.Create("Rules content", "en"),
            userId,
            13);

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(game.Id);
        result.Title.Should().Be("Catan");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(90);
        result.MinAge.Should().Be(10);
        result.ComplexityRating.Should().Be(2.5m);
        result.AverageRating.Should().Be(7.8m);
        result.BggId.Should().Be(13);
        result.Rules.Should().NotBeNull();
        result.Rules!.Content.Should().Be("Rules content");
        result.Rules.Language.Should().Be("en");
        result.CreatedBy.Should().Be(userId);
        // A.4 — empty DB → all aggregates default to 0/false.
        result.ToolkitsCount.Should().Be(0);
        result.AgentsCount.Should().Be(0);
        result.KbsCount.Should().Be(0);
        result.ContributorsCount.Should().Be(0);
        result.HasKnowledgeBase.Should().BeFalse();
        // AverageRating 7.8 < default TopRatedThreshold 4.0 on 0..5 scale? No: handler reads
        // configuration value but here the persisted AverageRating is on 0..10 BGG scale.
        // The handler treats AverageRating directly so 7.8 > 4.0 ⇒ IsTopRated=true.
        result.IsTopRated.Should().BeTrue();
        result.IsNew.Should().BeFalse();
        result.Toolkits.Should().NotBeNull().And.BeEmpty();
        result.Agents.Should().NotBeNull().And.BeEmpty();
        result.Kbs.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ResolvesHeroContextCover_HonoringAdminOverride()
    {
        // Epic #3470 Slice 2 AC-1: the detail page is the sole Hero surface. An admin
        // Hero override pinning Wikidata must win over the implicit precedence (PDF is
        // higher in the chain) — proving the handler resolves the HERO context AND
        // eager-loads CoverAssignments (without the Include the AsNoTracking entity has
        // no assignments and the override is silently ignored, yielding the PDF cover).
        var game = SharedGame.Create(
            "Catan", 1995, "Description", 3, 4, 90, 10, 2.5m, 7.8m,
            "https://example.com/catan.jpg", "https://example.com/thumb.jpg",
            GameRules.Create("Rules content", "en"), Guid.NewGuid(), 13);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
            .ReturnsAsync("https://r2/wiki-hero.webp");
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
            .ReturnsAsync("https://r2/pdf.webp");

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = game.Id,
            Title = "Catan",
            Description = "desc",
            Status = 1,
            PdfCoverR2Key = "pdf-key",        // would win the implicit precedence
            WikidataCoverR2Key = "wiki-key",  // pinned by the Hero override
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Context = CoverContext.Hero,
                    Source = CoverAssignmentSource.Wikidata,
                    CreatedBy = Guid.NewGuid(),
                },
            },
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetSharedGameByIdQuery(game.Id), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.CoverUrl.Should().Be(
            "https://r2/wiki-hero.webp",
            "the Hero admin override pins Wikidata; the PDF implicit precedence must NOT win");
    }

    [Fact]
    public async Task Handle_ResolvesSocialCover_IndependentlyOfHero()
    {
        // Epic #3470 Slice 2d AC-2: the detail DTO exposes a Social cover (for OG meta)
        // resolved for the Social context. A Social override must surface on SocialCoverUrl
        // WITHOUT affecting the Hero cover (CoverUrl), which here has no override and so
        // falls through to the implicit PDF precedence.
        var game = SharedGame.Create(
            "Catan", 1995, "Description", 3, 4, 90, 10, 2.5m, 7.8m,
            "https://example.com/catan.jpg", "https://example.com/thumb.jpg",
            GameRules.Create("Rules content", "en"), Guid.NewGuid(), 13);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync("wiki-key.webp", null))
            .ReturnsAsync("https://r2/wiki-social.webp");
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
            .ReturnsAsync("https://r2/pdf.webp");

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = game.Id,
            Title = "Catan",
            Description = "desc",
            Status = 1,
            PdfCoverR2Key = "pdf-key",        // implicit precedence winner (Hero, no override)
            WikidataCoverR2Key = "wiki-key",  // pinned by the Social override
            CoverAssignments = new List<GameCoverAssignmentEntity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Context = CoverContext.Social,
                    Source = CoverAssignmentSource.Wikidata,
                    CreatedBy = Guid.NewGuid(),
                },
            },
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetSharedGameByIdQuery(game.Id), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.SocialCoverUrl.Should().Be(
            "https://r2/wiki-social.webp",
            "the Social override pins Wikidata for the OG image");
        result.CoverUrl.Should().Be(
            "https://r2/pdf.webp",
            "the Hero cover has no override and must fall through to the implicit PDF precedence, unaffected by the Social override");
    }

    [Fact]
    public async Task Handle_WithGameWithoutRules_ReturnsNullRules()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = SharedGame.Create(
            "Simple Game",
            2020,
            "Description",
            2,
            4,
            60,
            8,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Rules.Should().BeNull();
        // No AverageRating ⇒ IsTopRated=false.
        result.IsTopRated.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SharedGameWithPdfCover_ProjectsCoverUrl()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        const string pdfKey = "pdf-cover-key";
        const string expectedUrl = "https://r2/pdf-cover-key-preview.webp";

        var game = SharedGame.Create(
            "Cover Game",
            2021,
            "Description",
            2,
            4,
            45,
            8,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        // Seed the EF entity with PdfCoverR2Key so CoverUrlResolver finds it.
        var sharedGameEntity = new SharedGameEntity
        {
            Id = gameId,
            Title = "Cover Game",
            YearPublished = 2021,
            Description = "Description",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)GameStatus.Published,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PdfCoverR2Key = pdfKey,
        };

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // CoverUrlResolver calls GetPresignedUrlForRawKeyAsync("{key}-preview.webp", null)
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(
                $"{pdfKey}-preview.webp",
                null))
            .ReturnsAsync(expectedUrl);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await db.SharedGames.AddAsync(sharedGameEntity);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.CoverUrl.Should().Be(expectedUrl);
    }
}
