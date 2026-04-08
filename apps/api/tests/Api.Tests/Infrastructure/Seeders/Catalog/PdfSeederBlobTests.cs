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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

[Trait("Category", TestCategories.Unit)]
public sealed class PdfSeederBlobTests
{
    private readonly Mock<IBlobStorageService> _primaryBlob = new();
    private readonly Mock<ISeedBlobReader> _seedBlob = new();
    private readonly Mock<ILogger> _logger = new();
    private readonly Guid _systemUserId = Guid.NewGuid();

    private static SeedManifest CreateManifest(params SeedManifestGame[] games)
    {
        return new SeedManifest
        {
            Profile = "Dev",
            Catalog = new SeedManifestCatalog { Games = games.ToList() }
        };
    }

    private static SeedManifestGame CreateBlobEntry(
        int bggId = 174430,
        string title = "Gloomhaven",
        string pdfBlobKey = "rulebooks/v1/gloomhaven.pdf",
        string pdfSha256 = "abc123hash",
        string? pdf = "gloomhaven.pdf",
        string? pdfVersion = "1.0")
    {
        return new SeedManifestGame
        {
            Title = title,
            BggId = bggId,
            Language = "en",
            Pdf = pdf,
            PdfBlobKey = pdfBlobKey,
            PdfSha256 = pdfSha256,
            PdfVersion = pdfVersion,
        };
    }

    // ---------------------------------------------------------------
    // Test 1: NoOp reader → skip entirely
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_NoOpReader_SkipsEntirelyAndReturnsEarly()
    {
        // Arrange
        _seedBlob.Setup(x => x.IsConfigured).Returns(false);
        var manifest = CreateManifest(CreateBlobEntry());
        var gameMap = new Dictionary<int, Guid> { { 174430, Guid.NewGuid() } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert — no ExistsAsync / OpenReadAsync calls, no PdfDocuments created
        _seedBlob.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _seedBlob.Verify(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.PdfDocuments.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 2: Entry has Pdf but no PdfBlobKey → skip
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_ManifestWithoutBlobKey_DoesNothing()
    {
        // Arrange
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        var entry = CreateBlobEntry();
        entry.PdfBlobKey = null; // no blob key
        var manifest = CreateManifest(entry);
        var gameMap = new Dictionary<int, Guid> { { 174430, Guid.NewGuid() } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert
        _seedBlob.Verify(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.PdfDocuments.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Test 3: Happy path — new blob entry creates PdfDocument
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_NewBlobEntry_CreatesPdfDocumentWithGameIdAndHash()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        _primaryBlob.Setup(x => x.StoreAsync(It.IsAny<Stream>(), "gloomhaven.pdf", gameId.ToString("N"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "file123", "/blobs/file123", 4));

        var manifest = CreateManifest(CreateBlobEntry());
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Seed required parent entities for navigation properties
        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.Games.Add(new GameEntity { Id = gameId, Name = "Gloomhaven" });
        await db.SaveChangesAsync();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert
        var docs = db.PdfDocuments.ToList();
        docs.Should().HaveCount(1);

        var doc = docs[0];
        doc.GameId.Should().Be(gameId);
        doc.FileName.Should().Be("gloomhaven.pdf");
        doc.FilePath.Should().Be("/blobs/file123");
        doc.ContentHash.Should().Be("abc123hash");
        doc.VersionLabel.Should().Be("1.0");
        doc.DocumentType.Should().Be("base");
        doc.DocumentCategory.Should().Be("Rulebook");
        doc.IsActiveForRag.Should().BeTrue();
        doc.ProcessingPriority.Should().Be("Normal");
        doc.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        doc.UploadedByUserId.Should().Be(userId);
        doc.FileSizeBytes.Should().Be(4);
    }

    // ---------------------------------------------------------------
    // Test 4: Existing matching hash → idempotent skip
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_ExistingMatchingHash_Skips()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);

        var manifest = CreateManifest(CreateBlobEntry(pdfSha256: "samehash"));
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Pre-seed an existing document with matching hash
        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.Games.Add(new GameEntity { Id = gameId, Name = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            FileName = "gloomhaven.pdf",
            FilePath = "/old/path",
            ContentHash = "samehash",
            UploadedByUserId = userId,
            DocumentType = "base",
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert — no store call, still only 1 doc
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.PdfDocuments.ToList().Should().HaveCount(1);
    }

    // ---------------------------------------------------------------
    // Test 5: Hash drift → delete old, create new
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_HashDrift_DeletesOldCascadeAndReinserts()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldDocId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        _primaryBlob.Setup(x => x.StoreAsync(It.IsAny<Stream>(), "gloomhaven.pdf", gameId.ToString("N"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "newfile", "/blobs/newfile", 4));
        _primaryBlob.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var manifest = CreateManifest(CreateBlobEntry(pdfSha256: "newhash"));
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Pre-seed with old hash
        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.Games.Add(new GameEntity { Id = gameId, Name = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = oldDocId,
            GameId = gameId,
            FileName = "gloomhaven.pdf",
            FilePath = "/old/path",
            FileSizeBytes = 100,
            ContentHash = "oldhash",
            UploadedByUserId = userId,
            DocumentType = "base",
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert — old doc deleted, new doc created
        var docs = db.PdfDocuments.ToList();
        docs.Should().HaveCount(1);
        docs[0].Id.Should().NotBe(oldDocId);
        docs[0].ContentHash.Should().Be("newhash");
        docs[0].FilePath.Should().Be("/blobs/newfile");
    }

    // ---------------------------------------------------------------
    // Test 6: Blob missing in seed bucket → skip gracefully
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_BlobMissing_SkipsEntryAndDoesNotInsert()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var manifest = CreateManifest(CreateBlobEntry());
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, _systemUserId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert
        _seedBlob.Verify(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.PdfDocuments.Should().BeEmpty();
    }
}
