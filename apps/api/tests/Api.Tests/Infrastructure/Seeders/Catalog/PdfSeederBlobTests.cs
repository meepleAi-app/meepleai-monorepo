using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

        // Post-migration (2026-04-19): seeder stores under pdfs/{pdfId}/ bucket, not pdfs/{gameId}/.
        // pdfId is generated inside the seeder, so match any bucket key here.
        _primaryBlob.Setup(x => x.StoreAsync(It.IsAny<Stream>(), "gloomhaven.pdf", It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "file123", "/blobs/file123", 4));

        var manifest = CreateManifest(CreateBlobEntry());
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Seed required parent entities for navigation properties.
        // Post-migration (2026-04-19): PdfSeeder resolves GameEntity.SharedGameId → PDF.SharedGameId.
        // Use gameId as both GameEntity.Id and SharedGameId so assertions remain on a single Guid.
        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gloomhaven" });
        await db.SaveChangesAsync();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert
        var docs = db.PdfDocuments.ToList();
        docs.Should().HaveCount(1);

        var doc = docs[0];
        doc.SharedGameId.Should().Be(gameId);
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

        // Regression guard for PR #XXX: PdfSeeder must also create a ProcessingJob
        // in Queued state so PdfProcessingQuartzJob picks the PDF up automatically.
        // Without this, seeded PDFs stay in Pending forever.
        var jobs = db.ProcessingJobs.Include(j => j.Steps).ToList();
        jobs.Should().HaveCount(1);
        var job = jobs[0];
        job.PdfDocumentId.Should().Be(doc.Id);
        job.UserId.Should().Be(userId);
        job.Status.Should().Be(nameof(JobStatus.Queued));
        job.Priority.Should().Be(0);
        job.MaxRetries.Should().Be(3);
        job.RetryCount.Should().Be(0);
        job.Steps.Should().HaveCount(5);
        job.Steps.Select(s => s.StepName).Should().BeEquivalentTo(new[]
        {
            nameof(ProcessingStepType.Upload),
            nameof(ProcessingStepType.Extract),
            nameof(ProcessingStepType.Chunk),
            nameof(ProcessingStepType.Embed),
            nameof(ProcessingStepType.Index),
        });
        job.Steps.Should().OnlyContain(s => s.Status == "Pending");
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

        // Pre-seed an existing document with matching hash.
        // Post-migration: GameEntity.SharedGameId drives PdfSeeder resolution.
        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
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
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

        // Post-migration (2026-04-19): seeder stores under pdfs/{pdfId}/ bucket, not pdfs/{gameId}/.
        // pdfId is generated inside the seeder, so match any bucket key here.
        _primaryBlob.Setup(x => x.StoreAsync(It.IsAny<Stream>(), "gloomhaven.pdf", It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "newfile", "/blobs/newfile", 4));
        _primaryBlob.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var manifest = CreateManifest(CreateBlobEntry(pdfSha256: "newhash"));
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        // Pre-seed with old hash. Post-migration: GameEntity.SharedGameId drives PdfSeeder resolution.
        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = oldDocId,
            SharedGameId = gameId,
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
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.PdfDocuments.Should().BeEmpty();
    }

    // ===============================================================
    // Repair mode (#2666): DB-only snapshot restore leaves the record
    // intact but the blob missing from the runtime bucket. The seeder
    // must verify blob presence before the idempotency skip and re-upload
    // from the seed bucket using the EXISTING id when the blob is gone.
    // ===============================================================

    // ---------------------------------------------------------------
    // Repair (a): record exists + blob PRESENT in runtime → plain skip
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_ExistingHash_BlobPresentInRuntime_SkipsWithoutReupload()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);

        // Runtime blob is present → seeder must NOT re-upload nor mutate the record.
        _primaryBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var manifest = CreateManifest(CreateBlobEntry(pdfSha256: "samehash"));
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = existingId,
            SharedGameId = gameId,
            FileName = "gloomhaven.pdf",
            // FilePath carries the fileId (deadbeef) so ExtractFileIdFromPath can build the ExistsAsync probe.
            FilePath = $"pdfs/{PdfStorageKey.ForPdf(existingId)}/deadbeef_gloomhaven.pdf",
            ContentHash = "samehash",
            ProcessingState = nameof(PdfProcessingState.Ready),
            UploadedByUserId = userId,
            DocumentType = "base",
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert — presence check happened, but no re-upload and no state change.
        _primaryBlob.Verify(x => x.ExistsAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _seedBlob.Verify(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var doc = db.PdfDocuments.Single(p => p.Id == existingId);
        doc.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready));
        db.ProcessingJobs.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Repair (b): record exists + blob ABSENT in runtime + present in
    //             seed → repair: re-upload against existing id, reset to
    //             Pending, clear error fields, re-enqueue ProcessingJob.
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_ExistingHash_BlobMissingInRuntimeButInSeed_RepairsInPlace()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);

        // Runtime blob is GONE (snapshot DB-only restore), but the seed bucket still has it.
        _primaryBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _seedBlob.Setup(x => x.ExistsAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        string? capturedResourceKey = null;
        _primaryBlob.Setup(x => x.StoreAsync(It.IsAny<Stream>(), "gloomhaven.pdf", BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, BlobCategory, string, CancellationToken>((_, _, _, rk, _) => capturedResourceKey = rk)
            .ReturnsAsync(new BlobStorageResult(true, "newfile", "pdfs/repaired/newfile_gloomhaven.pdf", 4));

        var manifest = CreateManifest(CreateBlobEntry(pdfSha256: "samehash"));
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = existingId,
            SharedGameId = gameId,
            FileName = "gloomhaven.pdf",
            FilePath = $"pdfs/{PdfStorageKey.ForPdf(existingId)}/deadbeef_gloomhaven.pdf",
            FileSizeBytes = 999,
            ContentHash = "samehash",
            // The exact broken state observed on staging.
            ProcessingState = nameof(PdfProcessingState.Failed),
            ProcessingError = "PDF file not found in blob storage",
            ErrorCategory = "Service",
            FailedAtState = "Extracting",
            RetryCount = 2,
            UploadedByUserId = userId,
            DocumentType = "base",
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync();

        // Act
        await PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert — re-uploaded against the EXISTING id's bucket key (NOT a new Guid).
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), "gloomhaven.pdf", BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedResourceKey.Should().Be(PdfStorageKey.ForPdf(existingId));

        // Record is repaired in place: same id, reset to Pending, error fields cleared, new blob path/size.
        var docs = db.PdfDocuments.ToList();
        docs.Should().HaveCount(1);
        var doc = docs[0];
        doc.Id.Should().Be(existingId);
        doc.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        doc.FilePath.Should().Be("pdfs/repaired/newfile_gloomhaven.pdf");
        doc.FileSizeBytes.Should().Be(4);
        doc.ProcessingError.Should().BeNull();
        doc.ErrorCategory.Should().BeNull();
        doc.FailedAtState.Should().BeNull();
        doc.RetryCount.Should().Be(0);

        // A fresh ProcessingJob is enqueued for the repaired PDF (5 Queued steps).
        var jobs = db.ProcessingJobs.Include(j => j.Steps).ToList();
        jobs.Should().HaveCount(1);
        var job = jobs[0];
        job.PdfDocumentId.Should().Be(existingId);
        job.UserId.Should().Be(userId);
        job.Status.Should().Be(nameof(JobStatus.Queued));
        job.Steps.Should().HaveCount(5);
        job.Steps.Select(s => s.StepName).Should().BeEquivalentTo(new[]
        {
            nameof(ProcessingStepType.Upload),
            nameof(ProcessingStepType.Extract),
            nameof(ProcessingStepType.Chunk),
            nameof(ProcessingStepType.Embed),
            nameof(ProcessingStepType.Index),
        });
        job.Steps.Should().OnlyContain(s => s.Status == "Pending");
    }

    // ---------------------------------------------------------------
    // Repair (c): record exists + blob ABSENT in BOTH runtime and seed
    //             → log warning, no crash, no re-upload, no state change.
    // ---------------------------------------------------------------
    [Fact]
    public async Task SeedAsync_ExistingHash_BlobMissingInRuntimeAndSeed_LeavesRecordUntouched()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        _seedBlob.Setup(x => x.IsConfigured).Returns(true);

        // Blob gone from BOTH buckets → irrecoverable, must not crash and must not mutate.
        _primaryBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _seedBlob.Setup(x => x.ExistsAsync("rulebooks/v1/gloomhaven.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var manifest = CreateManifest(CreateBlobEntry(pdfSha256: "samehash"));
        var gameMap = new Dictionary<int, Guid> { { 174430, gameId } };
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        db.Users.Add(new UserEntity { Id = userId, Email = "system@test.com", PasswordHash = "hash" });
        db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gloomhaven" });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = existingId,
            SharedGameId = gameId,
            FileName = "gloomhaven.pdf",
            FilePath = $"pdfs/{PdfStorageKey.ForPdf(existingId)}/deadbeef_gloomhaven.pdf",
            ContentHash = "samehash",
            ProcessingState = nameof(PdfProcessingState.Failed),
            ProcessingError = "PDF file not found in blob storage",
            UploadedByUserId = userId,
            DocumentType = "base",
            DocumentCategory = "Rulebook",
        });
        await db.SaveChangesAsync();

        // Act
        Func<Task> act = () => PdfSeeder.SeedAsync(db, manifest, gameMap, userId,
            _primaryBlob.Object, _seedBlob.Object, _logger.Object, CancellationToken.None);

        // Assert — no crash, no re-upload, record untouched, no job enqueued.
        await act.Should().NotThrowAsync();
        _seedBlob.Verify(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _primaryBlob.Verify(x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var doc = db.PdfDocuments.Single(p => p.Id == existingId);
        doc.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        doc.ProcessingError.Should().Be("PDF file not found in blob storage");
        db.ProcessingJobs.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // ExtractFileIdFromPath edge cases (review finding #3 follow-up)
    // ---------------------------------------------------------------

    [Theory]
    // Happy paths — S3 forward-slash + Windows backslash layouts
    [InlineData("pdf_uploads/gameId/abc123_rulebook.pdf", "abc123")]
    [InlineData("pdf_uploads\\gameId\\abc123_rulebook.pdf", "abc123")]
    [InlineData("C:\\storage\\pdf_uploads\\gameId\\fedcba_doc.pdf", "fedcba")]
    // GUID-N fileId (32 hex chars, no underscores, no hyphens) — production shape
    [InlineData("pdf_uploads/game/abcdef01234567890123456789abcdef_file.pdf", "abcdef01234567890123456789abcdef")]
    // Filename contains underscore — only FIRST underscore in last segment splits fileId/filename
    [InlineData("pdf_uploads/gameId/abc123_my_file_v2.pdf", "abc123")]
    public void ExtractFileIdFromPath_ValidPaths_ReturnsFileId(string filePath, string expectedFileId)
    {
        var result = PdfSeeder.ExtractFileIdFromPath(filePath);
        result.Should().Be(expectedFileId);
    }

    [Theory]
    // Null / empty
    [InlineData(null)]
    [InlineData("")]
    // No path separator → can't isolate last segment
    [InlineData("singleSegment_file.pdf")]
    [InlineData("noSeparatorNorUnderscore")]
    // Trailing slash → no filename after last separator
    [InlineData("pdf_uploads/gameId/")]
    [InlineData("pdf_uploads\\gameId\\")]
    // Last segment has no underscore → can't isolate fileId from filename
    [InlineData("pdf_uploads/gameId/justAFile.pdf")]
    [InlineData("pdf_uploads/gameId/no-underscore-here")]
    // Leading underscore in last segment → fileId would be empty
    [InlineData("pdf_uploads/gameId/_leadingUnderscore.pdf")]
    public void ExtractFileIdFromPath_InvalidPaths_ReturnsNull(string? filePath)
    {
        var result = PdfSeeder.ExtractFileIdFromPath(filePath!);
        result.Should().BeNull();
    }
}
