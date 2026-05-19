using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Lightweight integration test that exercises the full blob-based seeding pipeline
/// using an in-memory DbContext with realistic SharedGame + GameEntity rows pre-seeded,
/// and mocked blob services. This verifies the contract between GameSeeder (which populates
/// gameMap) and PdfSeeder (which consumes it), without requiring Testcontainers.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class SeedCatalogBlobIntegrationTests
{
    private readonly Mock<IBlobStorageService> _primaryBlob = new();
    private readonly Mock<ISeedBlobReader> _seedBlob = new();
    private readonly Guid _systemUserId = Guid.NewGuid();

    [Fact]
    public async Task PdfSeeder_EndToEndSingleGame_CreatesPdfDocumentLinkedToGameEntity()
    {
        // Arrange — real in-memory DbContext + pre-seeded GameEntity bridge
        // (GameSeeder creates both SharedGame + GameEntity in prod; for this test
        //  we only care about the bridge row PdfSeeder queries via gameMap)
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var gameEntityId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        const int bggId = 96848;

        // PdfSeeder filters Games by `SharedGameId != null` (see Catalog/PdfSeeder.cs#L72),
        // so the test must mirror what GameSeeder produces in prod: a SharedGameEntity
        // row linked from GameEntity.SharedGameId. Without it, gameIdToSharedId is empty
        // and every manifest entry gets skipped → 0 PdfDocumentEntity rows created.
        db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Mage Knight Board Game",
        });
        db.Games.Add(new GameEntity
        {
            Id = gameEntityId,
            Name = "Mage Knight Board Game",
            CreatedAt = DateTime.UtcNow,
            BggId = bggId,
            SharedGameId = sharedGameId,
        });
        await db.SaveChangesAsync();

        var manifest = new SeedManifest
        {
            Profile = "Staging",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new()
                    {
                        Title = "Mage Knight Board Game",
                        BggId = bggId,
                        Language = "en",
                        PdfBlobKey = "rulebooks/v1/mage-knight_rulebook.pdf",
                        PdfSha256 = "sha256-deadbeef-integration-test",
                        PdfVersion = "1.0",
                    }
                }
            }
        };

        // gameMap is what GameSeeder would produce — BggId → GameEntity.Id
        var gameMap = new Dictionary<int, Guid> { [bggId] = gameEntityId };

        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync(
                "rulebooks/v1/mage-knight_rulebook.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync(
                "rulebooks/v1/mage-knight_rulebook.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        var pdfFileId = Guid.NewGuid();
        _primaryBlob
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: pdfFileId.ToString(),
                FilePath: $"/blobs/{gameEntityId}/{pdfFileId:N}",
                FileSizeBytes: 4));

        // Act
        await PdfSeeder.SeedAsync(
            db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        // Assert — full pipeline produced exactly one PdfDocumentEntity with all expected fields
        var savedPdfs = db.PdfDocuments.ToList();
        savedPdfs.Should().ContainSingle();

        var pdf = savedPdfs[0];
        // PdfDocumentEntity.SharedGameId resolves to the SharedGameEntity.Id
        // (not GameEntity.Id) — PdfSeeder reads gameIdToSharedId[gameId] from
        // Games.SharedGameId. Post-2026-04-19 migration this is the canonical
        // foreign key on pdf_documents.
        pdf.SharedGameId.Should().Be(sharedGameId,
            "PdfSeeder links the PDF to the SharedGameEntity catalog entry");
        pdf.FileName.Should().Be("mage-knight_rulebook.pdf");
        pdf.ContentHash.Should().Be("sha256-deadbeef-integration-test");
        pdf.Language.Should().Be("en");
        pdf.VersionLabel.Should().Be("1.0");
        pdf.DocumentType.Should().Be("base");
        pdf.DocumentCategory.Should().Be("Rulebook");
        pdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        pdf.UploadedByUserId.Should().Be(_systemUserId);
        pdf.IsActiveForRag.Should().BeTrue();
        pdf.ProcessingPriority.Should().Be("Normal");
        pdf.FilePath.Should().NotBeNullOrEmpty();

        // Verify the games bridge row is intact (the backfill migration
        // propagates SharedGameId to pdf_documents.SharedGameId at deploy time).
        var game = await db.Games.FindAsync(gameEntityId);
        game.Should().NotBeNull();
    }

    [Fact]
    public async Task PdfSeeder_EndToEndMultipleGames_ProcessesAllAndSkipsMissingBggId()
    {
        // Arrange — 3 games: 2 in manifest with gameMap, 1 in manifest without
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var bcGameId = Guid.NewGuid();
        var mkGameId = Guid.NewGuid();
        var bcSharedId = Guid.NewGuid();
        var mkSharedId = Guid.NewGuid();

        // PdfSeeder requires Game.SharedGameId to be populated — see EndToEndSingleGame test for rationale.
        db.SharedGames.AddRange(
            new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity { Id = bcSharedId, Title = "Barrage" },
            new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity { Id = mkSharedId, Title = "Mage Knight Board Game" });
        db.Games.AddRange(
            new GameEntity { Id = bcGameId, Name = "Barrage", CreatedAt = DateTime.UtcNow, BggId = 251247, SharedGameId = bcSharedId },
            new GameEntity { Id = mkGameId, Name = "Mage Knight Board Game", CreatedAt = DateTime.UtcNow, BggId = 96848, SharedGameId = mkSharedId });
        await db.SaveChangesAsync();

        var manifest = new SeedManifest
        {
            Profile = "Staging",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new() { Title = "Barrage", BggId = 251247, Language = "en",
                            PdfBlobKey = "rulebooks/v1/barrage_rulebook.pdf", PdfSha256 = "sha-barrage" },
                    new() { Title = "Mage Knight", BggId = 96848, Language = "en",
                            PdfBlobKey = "rulebooks/v1/mage-knight_rulebook.pdf", PdfSha256 = "sha-mk" },
                    new() { Title = "Unknown Game", BggId = 999999, Language = "en",
                            PdfBlobKey = "rulebooks/v1/unknown_rulebook.pdf", PdfSha256 = "sha-unknown" },
                }
            }
        };

        var gameMap = new Dictionary<int, Guid>
        {
            [251247] = bcGameId,
            [96848] = mkGameId,
            // 999999 intentionally missing
        };

        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        _primaryBlob
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString(),
                FilePath: "/blobs/x",
                FileSizeBytes: 4));

        // Act
        await PdfSeeder.SeedAsync(
            db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        // Assert — 2 entities created (Barrage + Mage Knight), Unknown skipped
        var pdfs = db.PdfDocuments.ToList();
        pdfs.Should().HaveCount(2);
        pdfs.Should().Contain(p => p.FileName == "barrage_rulebook.pdf");
        pdfs.Should().Contain(p => p.FileName == "mage-knight_rulebook.pdf");
        pdfs.Should().NotContain(p => p.FileName == "unknown_rulebook.pdf");

        // The missing gameMap entry did not cause any blob store call
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), "unknown_rulebook.pdf", It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PdfSeeder_Reseed_IsIdempotentAndDoesNotCreateDuplicates()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var gameEntityId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        // PdfSeeder requires Game.SharedGameId to be populated — see EndToEndSingleGame test for rationale.
        db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Barrage",
        });
        db.Games.Add(new GameEntity
        {
            Id = gameEntityId,
            Name = "Barrage",
            CreatedAt = DateTime.UtcNow,
            BggId = 251247,
            SharedGameId = sharedGameId,
        });
        await db.SaveChangesAsync();

        var manifest = new SeedManifest
        {
            Profile = "Staging",
            Catalog = new SeedManifestCatalog
            {
                Games = new List<SeedManifestGame>
                {
                    new()
                    {
                        Title = "Barrage",
                        BggId = 251247,
                        Language = "en",
                        PdfBlobKey = "rulebooks/v1/barrage_rulebook.pdf",
                        PdfSha256 = "sha-barrage-stable",
                    }
                }
            }
        };
        var gameMap = new Dictionary<int, Guid> { [251247] = gameEntityId };

        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        _primaryBlob
            .Setup(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString(),
                FilePath: "/blobs/x",
                FileSizeBytes: 4));

        // Act — run the seeder twice
        await PdfSeeder.SeedAsync(
            db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        var firstRunCount = db.PdfDocuments.Count();

        await PdfSeeder.SeedAsync(
            db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        var secondRunCount = db.PdfDocuments.Count();

        // Assert
        firstRunCount.Should().Be(1);
        secondRunCount.Should().Be(1, "re-seeding with unchanged hash must not create duplicates");

        // StoreAsync invoked exactly once — second call short-circuits on hash match
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
