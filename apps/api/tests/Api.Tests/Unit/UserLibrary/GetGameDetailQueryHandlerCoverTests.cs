using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// Issue #4085 — <see cref="GetGameDetailQueryHandler"/> must resolve <c>GameImageUrl</c>
/// via <c>CoverUrlResolver</c> instead of the <c>SharedGame.ImageUrl</c> tombstone column
/// (#2123: always empty since the BGG user-side asset ban).
///
/// <para>The domain <c>ISharedGameRepository</c> returns <c>SharedGame</c> (the domain
/// aggregate), which carries no raw cover-key columns — the resolver needs the EF
/// <see cref="SharedGameEntity"/>. These tests seed that entity into an in-memory
/// <c>MeepleAiDbContext</c> directly (bypassing the domain repository, which is exactly
/// what the handler itself now does for this one field).</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4085")]
public sealed class GetGameDetailQueryHandlerCoverTests
{
    private static GetGameDetailQueryHandler CreateHandler(
        Mock<IUserLibraryRepository> libraryRepo,
        Mock<ISharedGameRepository> sharedGameRepo,
        Mock<IGameLabelRepository> labelRepo,
        Api.Infrastructure.MeepleAiDbContext db,
        Mock<IBlobStorageService> blobStorage)
    {
        HybridCache cache = TestDbContextFactory.CreateInMemoryHybridCache();
        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.FindByUserIdAndGameIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatThread>());

        return new GetGameDetailQueryHandler(
            libraryRepo.Object,
            sharedGameRepo.Object,
            labelRepo.Object,
            chatThreadRepo.Object,
            db,
            blobStorage.Object,
            cache,
            NullLogger<GetGameDetailQueryHandler>.Instance);
    }

    private static (SharedGame domain, Guid gameId, Guid userId) SeedDomainGame(
        // SharedGame.Create.ValidateImageUrl rejects an empty string, so this can't
        // literally be "" even though that's the real #2123-tombstone value in the DB
        // column; the value is irrelevant here — these tests assert on the EF
        // SharedGameEntity path, never on this domain object's ImageUrl.
        string tombstoneImageUrl = "https://unused.example.test/placeholder.jpg")
    {
        var userId = Guid.NewGuid();
        var sharedGame = SharedGame.Create(
            title: "Wingspan",
            yearPublished: 2019,
            description: "A bird-themed engine builder.",
            minPlayers: 1,
            maxPlayers: 5,
            playingTimeMinutes: 70,
            minAge: 10,
            complexityRating: 2.4m,
            averageRating: 8.1m,
            imageUrl: tombstoneImageUrl,
            thumbnailUrl: tombstoneImageUrl,
            rules: null,
            createdBy: userId,
            bggId: 266192);
        return (sharedGame, sharedGame.Id, userId);
    }

    [Fact]
    public async Task Handle_SharedGameHasPdfCover_ReturnsResolvedUrl_NotTheTombstone()
    {
        // Arrange
        var (sharedGame, gameId, userId) = SeedDomainGame();
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var libraryRepo = new Mock<IUserLibraryRepository>();
        libraryRepo
            .Setup(r => r.GetUserGameWithStatsAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        var sharedGameRepo = new Mock<ISharedGameRepository>();
        sharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var labelRepo = new Mock<IGameLabelRepository>();
        labelRepo
            .Setup(r => r.GetLabelsForEntryAsync(libraryEntry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.UserLibrary.Domain.Entities.GameLabel>());

        // The EF entity the resolver actually reads — seeded directly, matching how
        // PdfCoverUploadPipeline persists PdfCoverR2Key on a real upload.
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Wingspan",
            PdfCoverR2Key = "pdf-cover-db-key",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        const string resolvedUrl = "https://r2.example.test/covers/pdf/wingspan-preview.webp";
        var blobStorage = new Mock<IBlobStorageService>();
        blobStorage
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(resolvedUrl);

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo, db, blobStorage);

        // Act
        var result = await handler.Handle(new GetGameDetailQuery(userId, gameId), CancellationToken.None);

        // Assert
        result.GameImageUrl.Should().Be(resolvedUrl,
            "#4085: GameImageUrl must come from CoverUrlResolver, not the always-empty ImageUrl tombstone");
    }

    [Fact]
    public async Task Handle_SharedGameHasNoCoverKeyAnywhere_ReturnsNull_NotTheTombstone()
    {
        // Arrange — #4085 regression guard: if someone reverted the fix back to
        // `sharedGame.ImageUrl`, this test wouldn't catch it on its own (both are empty-ish
        // here); it exists alongside the positive test above so a partial revert (DTO wiring
        // reverted, this call site left alone, or vice versa) shows up as a FAILURE on the
        // positive test instead of a false green here.
        var (sharedGame, gameId, userId) = SeedDomainGame();
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        var libraryRepo = new Mock<IUserLibraryRepository>();
        libraryRepo
            .Setup(r => r.GetUserGameWithStatsAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryEntry);

        var sharedGameRepo = new Mock<ISharedGameRepository>();
        sharedGameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        var labelRepo = new Mock<IGameLabelRepository>();
        labelRepo
            .Setup(r => r.GetLabelsForEntryAsync(libraryEntry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.UserLibrary.Domain.Entities.GameLabel>());

        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Wingspan",
            // No PdfCoverR2Key / BggCoverR2Key / WikidataCoverR2Key / ManualCoverR2Key set.
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var blobStorage = new Mock<IBlobStorageService>();
        // No Setup for GetPresignedUrlForRawKeyAsync: with no key present, the resolver
        // must never call it. MockBehavior default (Loose) would return null anyway, but
        // the Verify(Times.Never) below makes that explicit instead of accidental.

        var handler = CreateHandler(libraryRepo, sharedGameRepo, labelRepo, db, blobStorage);

        var result = await handler.Handle(new GetGameDetailQuery(userId, gameId), CancellationToken.None);

        result.GameImageUrl.Should().BeNull();
        blobStorage.Verify(
            b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()),
            Times.Never);
    }
}
