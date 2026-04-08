# Seed Catalog Blob Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace filesystem-based PDF seeding with blob-store-based seeding backed by `meepleai-seeds` bucket, and bundle four runtime bug fixes (SharedGameId linkage, empty participants, fragile Enum.Parse, KB status lookup).

**Architecture:** `PdfSeeder` becomes blob-only, driven by a new `ISeedBlobReader` abstraction with S3 and NoOp implementations. `SeedManifestGame` gains four optional fields (`PdfBlobKey`, `PdfSha256`, `PdfVersion`). A forward-only EF migration backfills `SharedGameId` on existing pdf_documents/vector_documents/text_chunks. Four separate bug fixes in `UploadPdfCommandHandler`, `StartGameNightSessionCommandHandler`, `GameNightEventRepository`, and the missing `SEED_PROFILE=Staging` in `compose.staging.yml`.

**Tech Stack:** .NET 9, EF Core 9, AWS SDK for .NET (S3 client), xUnit + Moq + Testcontainers, bash + yq + aws-cli for developer scripts.

---

## Prerequisite knowledge

Before touching code, the implementer should skim:

- `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs` — orchestrates all catalog layers. Currently static, must stay static.
- `apps/api/src/Api/Infrastructure/Seeders/Catalog/PdfSeeder.cs` — the file being refactored. Static class.
- `apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs` — contains `SeedManifestGame` (the class we're extending — **not** `SeedManifestEntry`).
- `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` — target entity. Note: `FilePath` is NOT NULL, `VersionLabel` (not `VersionNumber`), `DocumentCategory = "Rulebook"`, `DocumentType = "base"`.
- `apps/api/src/Api/Services/Pdf/IBlobStorageService.cs` — signature is `StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct) → Task<BlobStorageResult>`. Do not confuse argument order.
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs` — three `Enum.Parse` sites at approximately lines 182, 217, 234 (exact line numbers may shift; locate by searching for `Enum.Parse<`).
- `docs/superpowers/specs/2026-04-08-seed-catalog-blob-migration-design.md` — the spec this plan implements.

The existing manifest files (`Manifests/staging.yml`, `Manifests/dev.yml`, `Manifests/prod.yml`) are generated resources. Do not hand-edit them in this plan — the `patch-manifest-from-hashes.sh` script rewrites them from `.seed-hashes.tsv` in Task 14. The developer runs that script locally before merging.

---

## File Structure

### Files to create

| Path | Purpose |
|------|---------|
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/ISeedBlobReader.cs` | Readonly interface for seed bucket |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReader.cs` | S3/R2 implementation |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/NoOpSeedBlobReader.cs` | Graceful fallback when bucket not configured |
| `apps/api/src/Api/Infrastructure/Migrations/20260408_BackfillSharedGameIdFromGames.cs` | Forward-only SQL backfill |
| `infra/scripts/upload-seed-pdfs.sh` | One-time uploader |
| `infra/scripts/patch-manifest-from-hashes.sh` | Rewrites YAML manifests from `.seed-hashes.tsv` |
| `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/PdfSeederBlobTests.cs` | Unit tests for blob-based PdfSeeder |
| `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReaderTests.cs` | Unit tests for S3 reader |
| `apps/api/tests/Api.Tests/Integration/SeedCatalogBlobIntegrationTests.cs` | End-to-end seed integration test |
| `apps/api/tests/Api.Tests/Integration/BackfillSharedGameIdMigrationTests.cs` | Migration test |

### Files to modify

| Path | Change |
|------|--------|
| `apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs` | Add 4 fields to `SeedManifestGame` |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/PdfSeeder.cs` | Full refactor to blob-only |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs` | Pass `ISeedBlobReader` + `IBlobStorageService` to `PdfSeeder.SeedAsync` |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs` | Resolve new services from DI and forward |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Register `ISeedBlobReader` |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs` | Bug #2: set `SharedGameId` when `gameId` matches a shared_games row |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs` | Bug #3: build organizer as owner participant |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs` | Bug #4: three `Enum.TryParse` helpers |
| `infra/compose.staging.yml` | Add `SEED_PROFILE: Staging` + `SEED_BUCKET_*` env vars |
| `infra/secrets/storage.secret.example` | Document `SEED_BUCKET_*` keys |
| Existing tests in `UploadPdfCommandHandlerTests`, `StartGameNightSessionCommandHandlerTests`, `GameNightEventRepositoryTests` | Add regression guards |

---

## Task 1: Extend SeedManifestGame with blob fields

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs` (class `SeedManifestGame`, around line 70)

- [ ] **Step 1: Add the four new optional properties**

Open `apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs`, locate `SeedManifestGame`, and append the new properties after the existing `Publishers` list (keep the existing `Pdf` field — it stays for one deploy then gets removed in a follow-up PR).

```csharp
internal sealed class SeedManifestGame
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? Pdf { get; set; }              // DEPRECATED: kept for one deploy, remove after manifest migration
    public bool SeedAgent { get; set; }
    public string? FallbackImageUrl { get; set; }
    public string? FallbackThumbnailUrl { get; set; }
    public bool BggEnhanced { get; set; }
    public string? Description { get; set; }
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? PlayingTime { get; set; }
    public int? MinAge { get; set; }
    public double? AverageRating { get; set; }
    public double? AverageWeight { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? RulesUrl { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? Mechanics { get; set; }
    public List<string>? Designers { get; set; }
    public List<string>? Publishers { get; set; }

    // Blob-based PDF seeding (new)
    public string? PdfBlobKey { get; set; }
    public string? PdfSha256 { get; set; }
    public string? PdfVersion { get; set; }
}
```

- [ ] **Step 2: Build the API project**

Run: `cd apps/api/src/Api && dotnet build --nologo`
Expected: `Compilazione completata. Avvisi: 0. Errori: 0`.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs
git commit -m "feat(seed): add blob fields to SeedManifestGame"
```

---

## Task 2: Create ISeedBlobReader interface and NoOp implementation

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/ISeedBlobReader.cs`
- Create: `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/NoOpSeedBlobReader.cs`

- [ ] **Step 1: Create the directory**

```bash
mkdir -p apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob
```

- [ ] **Step 2: Write the interface**

Create `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/ISeedBlobReader.cs`:

```csharp
namespace Api.Infrastructure.Seeders.Catalog.SeedBlob;

/// <summary>
/// Readonly accessor for the seed PDF bucket (meepleai-seeds).
/// Separate from IBlobStorageService because it uses different credentials,
/// is readonly, and points to an external source-of-truth bucket.
/// </summary>
internal interface ISeedBlobReader
{
    /// <summary>
    /// True when backed by a real seed bucket. False for NoOp fallback.
    /// PdfSeeder uses this to skip entirely instead of logging per-entry errors.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Opens a read stream to the blob at the given key.
    /// Throws if the key does not exist or the reader is a NoOp.
    /// Caller must dispose the returned stream.
    /// </summary>
    Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct);

    /// <summary>
    /// Returns true if the blob exists, false otherwise.
    /// NoOp fallback returns false without logging.
    /// </summary>
    Task<bool> ExistsAsync(string blobKey, CancellationToken ct);
}
```

- [ ] **Step 3: Write the NoOp implementation**

Create `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/NoOpSeedBlobReader.cs`:

```csharp
namespace Api.Infrastructure.Seeders.Catalog.SeedBlob;

/// <summary>
/// Fallback ISeedBlobReader used when SEED_BUCKET_* env vars are absent
/// (e.g., local dev without a configured seed bucket). Does not log per call;
/// PdfSeeder checks IsConfigured and emits a single informational message.
/// </summary>
internal sealed class NoOpSeedBlobReader : ISeedBlobReader
{
    public bool IsConfigured => false;

    public Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct) =>
        throw new InvalidOperationException(
            "Seed bucket not configured. Set SEED_BUCKET_NAME / SEED_BUCKET_ENDPOINT / "
            + "SEED_BUCKET_ACCESS_KEY / SEED_BUCKET_SECRET_KEY to enable PDF seeding.");

    public Task<bool> ExistsAsync(string blobKey, CancellationToken ct) =>
        Task.FromResult(false);
}
```

- [ ] **Step 4: Build to verify**

Run: `cd apps/api/src/Api && dotnet build --nologo`
Expected: `Compilazione completata. Avvisi: 0. Errori: 0`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/
git commit -m "feat(seed): add ISeedBlobReader interface and NoOp fallback"
```

---

## Task 3: Implement S3SeedBlobReader

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReader.cs`
- Create: `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReaderTests.cs`

- [ ] **Step 1: Write the failing test file**

Create `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReaderTests.cs`:

```csharp
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using FluentAssertions;
using Moq;

namespace Api.Tests.Infrastructure.Seeders.Catalog.SeedBlob;

public class S3SeedBlobReaderTests
{
    private readonly Mock<IAmazonS3> _s3Mock = new();
    private readonly S3SeedBlobReader _sut;

    public S3SeedBlobReaderTests()
    {
        _sut = new S3SeedBlobReader(_s3Mock.Object, "meepleai-seeds");
    }

    [Fact]
    public void IsConfigured_AlwaysTrue()
    {
        _sut.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ObjectPresent_ReturnsTrue()
    {
        _s3Mock
            .Setup(x => x.GetObjectMetadataAsync(
                "meepleai-seeds",
                "rulebooks/v1/test.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _sut.ExistsAsync("rulebooks/v1/test.pdf", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ObjectMissing_ReturnsFalse()
    {
        _s3Mock
            .Setup(x => x.GetObjectMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

        var result = await _sut.ExistsAsync("rulebooks/v1/missing.pdf", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsResponseStream()
    {
        var payload = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
        _s3Mock
            .Setup(x => x.GetObjectAsync(
                "meepleai-seeds",
                "rulebooks/v1/test.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = payload });

        await using var stream = await _sut.OpenReadAsync("rulebooks/v1/test.pdf", CancellationToken.None);
        var buffer = new byte[4];
        await stream.ReadExactlyAsync(buffer);

        buffer.Should().Equal(0x25, 0x50, 0x44, 0x46);
    }
}
```

- [ ] **Step 2: Create the empty directory and run tests to see them fail**

```bash
mkdir -p apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/SeedBlob
cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~S3SeedBlobReaderTests" --nologo 2>&1 | tail -10
```

Expected: Build failure — `S3SeedBlobReader` type does not exist.

- [ ] **Step 3: Write the implementation**

Create `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReader.cs`:

```csharp
using System.Net;
using Amazon.S3;

namespace Api.Infrastructure.Seeders.Catalog.SeedBlob;

/// <summary>
/// ISeedBlobReader backed by an S3-compatible client (AWS S3 or Cloudflare R2).
/// Uses the readonly credentials provided via SEED_BUCKET_* env vars.
/// </summary>
internal sealed class S3SeedBlobReader : ISeedBlobReader
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3SeedBlobReader(IAmazonS3 client, string bucket)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
    }

    public bool IsConfigured => true;

    public async Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct)
    {
        var response = await _client
            .GetObjectAsync(_bucket, blobKey, ct)
            .ConfigureAwait(false);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string blobKey, CancellationToken ct)
    {
        try
        {
            await _client
                .GetObjectMetadataAsync(_bucket, blobKey, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the tests — expect pass**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~S3SeedBlobReaderTests" --nologo 2>&1 | tail -5`
Expected: `Superato! Failed: 0 Passed: 4`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReader.cs \
        apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/SeedBlob/S3SeedBlobReaderTests.cs
git commit -m "feat(seed): add S3SeedBlobReader with AWS SDK client"
```

---

## Task 4: DI registration for ISeedBlobReader

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` (after the OpenRouterFileLogger registration block)

- [ ] **Step 1: Locate the insertion point**

Open the file and find `// Issue #5073: Rotating JSONL file logger for OpenRouter requests`. The new registration goes in the same method, just after the OpenRouterFileLogger block.

- [ ] **Step 2: Add the using statements and registration**

At the top of the file, add `using Amazon.Runtime;`, `using Amazon.S3;`, and `using Api.Infrastructure.Seeders.Catalog.SeedBlob;` if not already present.

Insert the following just after the OpenRouter logger registration:

```csharp
// Seed PDF bucket — readonly accessor for the meepleai-seeds bucket
services.AddSingleton<ISeedBlobReader>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<NoOpSeedBlobReader>>();

    var bucket = config["SEED_BUCKET_NAME"];
    var endpoint = config["SEED_BUCKET_ENDPOINT"];
    var accessKey = config["SEED_BUCKET_ACCESS_KEY"];
    var secretKey = config["SEED_BUCKET_SECRET_KEY"];

    if (string.IsNullOrWhiteSpace(bucket)
        || string.IsNullOrWhiteSpace(endpoint)
        || string.IsNullOrWhiteSpace(accessKey)
        || string.IsNullOrWhiteSpace(secretKey))
    {
        logger.LogInformation(
            "SEED_BUCKET_* env vars not fully set; using NoOpSeedBlobReader (PDF seeding disabled)");
        return new NoOpSeedBlobReader();
    }

    var s3Config = new AmazonS3Config
    {
        ServiceURL = endpoint,
        ForcePathStyle = bool.TryParse(
            config["SEED_BUCKET_FORCE_PATH_STYLE"], out var fps) && fps,
    };

    var credentials = new BasicAWSCredentials(accessKey, secretKey);
    var client = new AmazonS3Client(credentials, s3Config);
    return new S3SeedBlobReader(client, bucket);
});
```

- [ ] **Step 3: Build to verify**

Run: `cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -10`
Expected: zero errors.

- [ ] **Step 4: Smoke-test DI resolution**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~KnowledgeBaseServiceExtensionsTests" --nologo 2>&1 | tail -5`
Expected: existing tests still pass (no new test added yet; this is a DI smoke check).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs
git commit -m "feat(seed): register ISeedBlobReader in DI"
```

---

## Task 5: PdfSeeder blob-only refactor — failing tests first

**Files:**
- Create: `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/PdfSeederBlobTests.cs`

- [ ] **Step 1: Scaffold the test file with the scenarios listed in the spec**

Create `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/PdfSeederBlobTests.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Tests.Infrastructure.Seeders.Catalog;

public class PdfSeederBlobTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IBlobStorageService> _primaryBlob = new();
    private readonly Mock<ISeedBlobReader> _seedBlob = new();
    private readonly Guid _systemUserId = Guid.NewGuid();

    public PdfSeederBlobTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private static SeedManifest ManifestWith(params SeedManifestGame[] games)
    {
        var m = new SeedManifest();
        m.Catalog.Games.AddRange(games);
        return m;
    }

    [Fact]
    public async Task SeedAsync_NoOpReader_SkipsEntirelyAndReturnsEarly()
    {
        _seedBlob.SetupGet(x => x.IsConfigured).Returns(false);

        var manifest = ManifestWith(new SeedManifestGame
        {
            Title = "Mage Knight",
            BggId = 96848,
            PdfBlobKey = "rulebooks/v1/mage-knight_rulebook.pdf",
            PdfSha256 = "abc",
        });

        await PdfSeeder.SeedAsync(
            _db, manifest, new Dictionary<int, Guid> { [96848] = Guid.NewGuid() },
            _systemUserId, _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        _seedBlob.Verify(
            x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _db.PdfDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_ManifestWithoutBlobKey_DoesNothing()
    {
        _seedBlob.SetupGet(x => x.IsConfigured).Returns(true);

        var manifest = ManifestWith(new SeedManifestGame
        {
            Title = "Legacy",
            BggId = 1,
            Pdf = "legacy_rulebook.pdf",
            // PdfBlobKey intentionally null
        });

        await PdfSeeder.SeedAsync(
            _db, manifest, new Dictionary<int, Guid> { [1] = Guid.NewGuid() },
            _systemUserId, _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        _db.PdfDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_NewBlobEntry_CreatesPdfDocumentWithGameIdAndHash()
    {
        var gameId = Guid.NewGuid();
        _seedBlob.SetupGet(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync("rulebooks/v1/barrage.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync("rulebooks/v1/barrage.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        _primaryBlob
            .Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                "barrage.pdf",
                gameId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString(),
                FilePath: "/blobs/abc",
                FileSizeBytes: 4));

        var manifest = ManifestWith(new SeedManifestGame
        {
            Title = "Barrage",
            BggId = 251247,
            Language = "en",
            PdfBlobKey = "rulebooks/v1/barrage.pdf",
            PdfSha256 = "deadbeef",
            PdfVersion = "1.1",
        });

        await PdfSeeder.SeedAsync(
            _db, manifest, new Dictionary<int, Guid> { [251247] = gameId },
            _systemUserId, _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        var saved = await _db.PdfDocuments.SingleAsync();
        saved.GameId.Should().Be(gameId);
        saved.FileName.Should().Be("barrage.pdf");
        saved.ContentHash.Should().Be("deadbeef");
        saved.VersionLabel.Should().Be("1.1");
        saved.Language.Should().Be("en");
        saved.DocumentCategory.Should().Be("Rulebook");
        saved.DocumentType.Should().Be("base");
        saved.ProcessingState.Should().Be("Pending");
        saved.UploadedByUserId.Should().Be(_systemUserId);
    }

    [Fact]
    public async Task SeedAsync_ExistingMatchingHash_Skips()
    {
        var gameId = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            FileName = "barrage.pdf",
            FilePath = "/blobs/existing",
            FileSizeBytes = 4,
            UploadedByUserId = _systemUserId,
            ContentHash = "same-hash",
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = "Ready",
        });
        await _db.SaveChangesAsync();

        _seedBlob.SetupGet(x => x.IsConfigured).Returns(true);

        var manifest = ManifestWith(new SeedManifestGame
        {
            Title = "Barrage",
            BggId = 251247,
            PdfBlobKey = "rulebooks/v1/barrage.pdf",
            PdfSha256 = "same-hash",
        });

        await PdfSeeder.SeedAsync(
            _db, manifest, new Dictionary<int, Guid> { [251247] = gameId },
            _systemUserId, _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        _primaryBlob.Verify(
            x => x.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                              It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _db.PdfDocuments.Should().HaveCount(1);
    }

    [Fact]
    public async Task SeedAsync_HashDrift_DeletesOldCascadeAndReinserts()
    {
        var gameId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = oldId,
            GameId = gameId,
            FileName = "barrage.pdf",
            FilePath = "/blobs/old",
            FileSizeBytes = 4,
            UploadedByUserId = _systemUserId,
            ContentHash = "old-hash",
            DocumentType = "base",
            DocumentCategory = "Rulebook",
            ProcessingState = "Ready",
        });
        await _db.SaveChangesAsync();

        _seedBlob.SetupGet(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _seedBlob.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25 }));

        _primaryBlob
            .Setup(x => x.StoreAsync(
                It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString(),
                FilePath: "/blobs/new",
                FileSizeBytes: 1));

        _primaryBlob.Setup(x => x.DeleteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var manifest = ManifestWith(new SeedManifestGame
        {
            Title = "Barrage",
            BggId = 251247,
            PdfBlobKey = "rulebooks/v1/barrage.pdf",
            PdfSha256 = "new-hash",
        });

        await PdfSeeder.SeedAsync(
            _db, manifest, new Dictionary<int, Guid> { [251247] = gameId },
            _systemUserId, _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        _primaryBlob.Verify(x => x.DeleteAsync(
            It.IsAny<string>(), gameId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        var doc = await _db.PdfDocuments.SingleAsync();
        doc.Id.Should().NotBe(oldId);
        doc.ContentHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task SeedAsync_BlobMissing_SkipsEntryAndDoesNotInsert()
    {
        _seedBlob.SetupGet(x => x.IsConfigured).Returns(true);
        _seedBlob.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var manifest = ManifestWith(new SeedManifestGame
        {
            Title = "Barrage",
            BggId = 251247,
            PdfBlobKey = "rulebooks/v1/barrage.pdf",
            PdfSha256 = "abc",
        });

        await PdfSeeder.SeedAsync(
            _db, manifest, new Dictionary<int, Guid> { [251247] = Guid.NewGuid() },
            _systemUserId, _primaryBlob.Object, _seedBlob.Object,
            NullLogger.Instance, CancellationToken.None);

        _db.PdfDocuments.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests — expect compile failure**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~PdfSeederBlobTests" --nologo 2>&1 | tail -15
```

Expected: build failure — `PdfSeeder.SeedAsync` signature mismatch (the old method has 6 parameters, the new tests call it with 8).

- [ ] **Step 3: Commit the failing tests**

```bash
git add apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/PdfSeederBlobTests.cs
git commit -m "test(seed): add failing PdfSeeder blob-based tests"
```

---

## Task 6: PdfSeeder blob-only implementation

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Catalog/PdfSeeder.cs` (full rewrite)

- [ ] **Step 1: Replace the file contents**

Overwrite `apps/api/src/Api/Infrastructure/Seeders/Catalog/PdfSeeder.cs` with:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds PDF rulebook documents from the meepleai-seeds S3 bucket using the
/// YAML manifest. Creates PdfDocumentEntity records in Pending state; the
/// existing PdfProcessingQuartzJob (polls every 10s) drives them through the
/// RAG pipeline (extract → chunk → embed → index).
///
/// Idempotent: skips PDFs where (GameId, FileName, ContentHash) already match.
/// On hash drift, the old record and primary blob are deleted and the entry
/// is re-seeded.
/// </summary>
internal static class PdfSeeder
{
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        Guid systemUserId,
        IBlobStorageService primaryBlob,
        ISeedBlobReader seedBlob,
        ILogger logger,
        CancellationToken ct)
    {
        if (!seedBlob.IsConfigured)
        {
            logger.LogInformation(
                "PdfSeeder: ISeedBlobReader is NoOp (SEED_BUCKET_* env vars unset). Skipping PDF seeding.");
            return;
        }

        var pdfEntries = manifest.Catalog.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.PdfBlobKey) && g.BggId is > 0)
            .ToList();

        if (pdfEntries.Count == 0)
        {
            logger.LogInformation("PdfSeeder: no blob PDF entries in manifest. Skipping.");
            return;
        }

        logger.LogInformation(
            "PdfSeeder: processing {Count} blob PDF entries from manifest",
            pdfEntries.Count);

        // Idempotency index: (GameId, FileName) → (PdfId, ContentHash)
        var existing = await db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId != null)
            .Select(p => new { p.Id, p.GameId, p.FileName, p.ContentHash })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingByKey = new Dictionary<string, (Guid Id, string? Hash)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existing)
        {
            existingByKey[$"{row.GameId}:{row.FileName}"] = (row.Id, row.ContentHash);
        }

        var seeded = 0; var skipped = 0; var drifted = 0;

        foreach (var entry in pdfEntries)
        {
            try
            {
                if (!gameMap.TryGetValue(entry.BggId!.Value, out var gameId))
                {
                    logger.LogWarning(
                        "PdfSeeder: no GameEntity for BggId={BggId} ('{Title}'). Skipping.",
                        entry.BggId, entry.Title);
                    skipped++;
                    continue;
                }

                var fileName = Path.GetFileName(entry.PdfBlobKey!);
                var indexKey = $"{gameId}:{fileName}";

                if (existingByKey.TryGetValue(indexKey, out var existingRow))
                {
                    if (string.Equals(existingRow.Hash, entry.PdfSha256, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    logger.LogInformation(
                        "PdfSeeder: hash drift for '{FileName}' (BggId {BggId}). Re-seeding.",
                        fileName, entry.BggId);
                    await DeletePdfCascadeAsync(db, primaryBlob, existingRow.Id, gameId, logger, ct)
                        .ConfigureAwait(false);
                    existingByKey.Remove(indexKey);
                    drifted++;
                }

                if (!await seedBlob.ExistsAsync(entry.PdfBlobKey!, ct).ConfigureAwait(false))
                {
                    logger.LogWarning(
                        "PdfSeeder: seed blob missing at '{BlobKey}' for BggId {BggId}. Skipping.",
                        entry.PdfBlobKey, entry.BggId);
                    skipped++;
                    continue;
                }

                await using var stream = await seedBlob
                    .OpenReadAsync(entry.PdfBlobKey!, ct)
                    .ConfigureAwait(false);

                var result = await primaryBlob
                    .StoreAsync(stream, fileName, gameId.ToString(), ct)
                    .ConfigureAwait(false);

                if (!result.Success || string.IsNullOrEmpty(result.FileId))
                {
                    logger.LogWarning(
                        "PdfSeeder: primary blob store failed for '{FileName}': {Error}",
                        fileName, result.ErrorMessage);
                    skipped++;
                    continue;
                }

                var pdfEntity = new PdfDocumentEntity
                {
                    Id = Guid.Parse(result.FileId!),
                    GameId = gameId,
                    FileName = fileName,
                    FilePath = result.FilePath ?? string.Empty,
                    FileSizeBytes = result.FileSizeBytes,
                    ContentType = "application/pdf",
                    Language = entry.Language ?? "en",
                    ProcessingState = nameof(PdfProcessingState.Pending),
                    IsPublic = true,
                    DocumentType = "base",
                    DocumentCategory = "Rulebook",
                    VersionLabel = entry.PdfVersion ?? "1.0",
                    ContentHash = entry.PdfSha256,
                    IsActiveForRag = true,
                    ProcessingPriority = "Normal",
                    SortOrder = 0,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = systemUserId,
                };

                db.PdfDocuments.Add(pdfEntity);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                existingByKey[indexKey] = (pdfEntity.Id, entry.PdfSha256);
                seeded++;

                logger.LogInformation(
                    "PdfSeeder: queued '{FileName}' for game '{Title}' ({Size} bytes)",
                    fileName, entry.Title, pdfEntity.FileSizeBytes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "PdfSeeder: failed to seed BggId {BggId} ('{Title}'). Continuing.",
                    entry.BggId, entry.Title);
                db.ChangeTracker.Clear();
                skipped++;
            }
        }

        logger.LogInformation(
            "PdfSeeder completed: {Seeded} queued, {Drifted} re-seeded (drift), {Skipped} skipped",
            seeded, drifted, skipped);
    }

    private static async Task DeletePdfCascadeAsync(
        MeepleAiDbContext db,
        IBlobStorageService primaryBlob,
        Guid pdfDocumentId,
        Guid gameId,
        ILogger logger,
        CancellationToken ct)
    {
        var doc = await db.PdfDocuments
            .FindAsync(new object[] { pdfDocumentId }, ct)
            .ConfigureAwait(false);

        if (doc is null) return;

        try
        {
            await primaryBlob
                .DeleteAsync(doc.Id.ToString("N"), gameId.ToString(), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PdfSeeder: best-effort delete of primary blob failed for {PdfId}. Continuing cascade.",
                doc.Id);
        }

        db.PdfDocuments.Remove(doc);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Build — it will fail because CatalogSeeder still calls the old signature**

Run: `cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -15`
Expected: error — no overload for `PdfSeeder.SeedAsync` matching the call in `CatalogSeeder.cs`. This is expected; Task 7 fixes the call site.

- [ ] **Step 3: Do NOT commit yet — Task 7 must complete first so the codebase compiles**

---

## Task 7: Update CatalogSeeder and CatalogSeedLayer to pass new services

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs`
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs`

- [ ] **Step 1: Update `CatalogSeeder.SeedAsync` signature and PdfSeeder call**

Open `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs`. Add two `using` statements at the top:

```csharp
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
```

Change the signature and the `PdfSeeder.SeedAsync` call:

```csharp
public static async Task SeedAsync(
    SeedProfile profile,
    MeepleAiDbContext db,
    IBggApiService bggService,
    Guid systemUserId,
    IBlobStorageService primaryBlob,
    ISeedBlobReader seedBlob,
    ILogger logger,
    CancellationToken ct,
    IEmbeddingService? embeddingService = null,
    IConfiguration? configuration = null)
{
    var manifest = LoadManifest(profile);
    logger.LogInformation("Catalog: {Count} games from {Profile}.yml",
        manifest.Catalog.Games.Count, profile);

    var gameMap = await GameSeeder.SeedAsync(db, bggService, systemUserId, manifest, logger, ct)
        .ConfigureAwait(false);

    await PdfSeeder.SeedAsync(
        db, manifest, gameMap, systemUserId,
        primaryBlob, seedBlob,
        logger, ct).ConfigureAwait(false);

    if (profile >= SeedProfile.Dev)
    {
        await AgentSeeder.SeedAsync(db, manifest, gameMap, logger, ct)
            .ConfigureAwait(false);
    }
    else
    {
        logger.LogInformation("Catalog: skipping agent seeding (profile: {Profile})", profile);
    }

    var seedingEnabled = configuration?.GetValue("Seeding:EnableStrategyPatterns", true) ?? true;
    if (seedingEnabled)
    {
        logger.LogInformation("Seeding strategy patterns for common game openings...");
        await StrategyPatternSeeder.SeedAsync(db, logger, embeddingService, ct)
            .ConfigureAwait(false);
    }

    logger.LogInformation("Catalog seeding complete: {GameCount} games mapped", gameMap.Count);
}
```

- [ ] **Step 2: Update `CatalogSeedLayer.SeedAsync` to resolve and forward the new services**

Open `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs`. Add the new `using` statements at the top:

```csharp
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
```

Replace the `SeedAsync` method body:

```csharp
public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
{
    var config = context.Services.GetService<IConfiguration>();
    var bggService = context.Services.GetRequiredService<IBggApiService>();
    var embeddingService = context.Services.GetService<IEmbeddingService>();
    var primaryBlob = context.Services.GetRequiredService<IBlobStorageService>();
    var seedBlob = context.Services.GetRequiredService<ISeedBlobReader>();

    await CatalogSeeder.SeedAsync(
        context.Profile,
        context.DbContext,
        bggService,
        context.SystemUserId,
        primaryBlob,
        seedBlob,
        context.Logger,
        cancellationToken,
        embeddingService,
        config).ConfigureAwait(false);
}
```

- [ ] **Step 3: Build to verify the whole tree compiles**

Run: `cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -10`
Expected: zero errors.

- [ ] **Step 4: Run the PdfSeederBlobTests from Task 5**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "FullyQualifiedName~PdfSeederBlobTests" --nologo 2>&1 | tail -5`
Expected: `Passed: 6`.

- [ ] **Step 5: Run the full unit-test suite to catch regressions**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "Category!=Integration" --nologo 2>&1 | tail -10`
Expected: zero failures from test code touching `PdfSeeder` / `CatalogSeeder`.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/PdfSeeder.cs \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs
git commit -m "feat(seed): refactor PdfSeeder to blob-only and update call sites"
```

---

## Task 8: Bug #2 — UploadPdfCommandHandler resolves SharedGameId

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandlerTests.cs` (or the matching test file — locate first)

- [ ] **Step 1: Locate the existing handler test file**

```bash
find apps/api/tests/Api.Tests -name "UploadPdfCommandHandlerTests*" 2>/dev/null
```

Use the first result as the test file path in the steps below.

- [ ] **Step 2: Add a failing regression test**

Append two new test methods to `UploadPdfCommandHandlerTests`:

```csharp
[Fact]
public async Task Handle_GameIdMatchesSharedGame_SetsSharedGameIdAndClearsGameId()
{
    var sharedGameId = Guid.NewGuid();
    _db.SharedGames.Add(new SharedGameEntity
    {
        Id = sharedGameId,
        Title = "Mage Knight Board Game",
        // Minimum fields required by SharedGameEntity non-null columns
    });
    await _db.SaveChangesAsync();

    var cmd = new UploadPdfCommand(
        Stream: new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }),
        FileName: "rules.pdf",
        GameId: sharedGameId,
        UserId: _testUserId,
        GameName: "Mage Knight Board Game",
        VersionType: "base",
        Language: "en",
        VersionNumber: "1.0");

    var result = await _sut.Handle(cmd, CancellationToken.None);

    var saved = await _db.PdfDocuments.SingleAsync(p => p.Id == Guid.Parse(result.DocumentId));
    saved.SharedGameId.Should().Be(sharedGameId);
    saved.GameId.Should().BeNull();
}

[Fact]
public async Task Handle_GameIdMatchesPrivateGame_RetainsLegacyBehavior()
{
    var gameId = Guid.NewGuid();
    _db.Games.Add(new GameEntity
    {
        Id = gameId,
        Name = "Private Custom Game",
        SharedGameId = null,
    });
    await _db.SaveChangesAsync();

    var cmd = new UploadPdfCommand(
        Stream: new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }),
        FileName: "rules.pdf",
        GameId: gameId,
        UserId: _testUserId,
        GameName: "Private Custom Game",
        VersionType: "base",
        Language: "en",
        VersionNumber: "1.0");

    var result = await _sut.Handle(cmd, CancellationToken.None);

    var saved = await _db.PdfDocuments.SingleAsync(p => p.Id == Guid.Parse(result.DocumentId));
    saved.GameId.Should().Be(gameId);
    saved.SharedGameId.Should().BeNull();
}
```

If the existing test class uses a different constructor pattern (`_sut`, `_db`, `_testUserId`), adapt the names to match. The test intent is the same: first case checks SharedGame resolution, second checks legacy path is untouched.

- [ ] **Step 3: Run the tests — expect failure**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~UploadPdfCommandHandlerTests&(FullyQualifiedName~SharedGame|FullyQualifiedName~PrivateGame)" --nologo 2>&1 | tail -10
```

Expected: `Handle_GameIdMatchesSharedGame_...` fails (SharedGameId not set), private-game test may pass already.

- [ ] **Step 4: Apply the fix**

Open `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`. Locate the block that constructs the `PdfDocumentEntity`. Before the `new PdfDocumentEntity { ... }` block, add:

```csharp
// Bug #2 fix: detect whether GameId refers to a SharedGame (catalog) or a private GameEntity.
// When it matches a SharedGame row, populate SharedGameId and clear GameId so downstream
// queries (/knowledge-base/{sharedGameId}/status) can resolve via the shared_games → games bridge.
var sharedGameMatch = await _db.SharedGames
    .AsNoTracking()
    .Where(sg => sg.Id == request.GameId)
    .Select(sg => sg.Id)
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);
```

Then in the entity construction, replace the existing `GameId = request.GameId` with:

```csharp
GameId = sharedGameMatch == Guid.Empty ? request.GameId : (Guid?)null,
SharedGameId = sharedGameMatch == Guid.Empty ? (Guid?)null : sharedGameMatch,
```

Keep every other field exactly as it was. If the existing handler sets `GameId` via a different property (e.g., `PrivateGameId`), follow that same pattern — the change is: when a SharedGame is matched, `SharedGameId` is set and `GameId` is null; otherwise the legacy path runs unchanged.

- [ ] **Step 5: Run the tests — expect pass**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~UploadPdfCommandHandlerTests&(FullyQualifiedName~SharedGame|FullyQualifiedName~PrivateGame)" --nologo 2>&1 | tail -5
```

Expected: both tests pass.

- [ ] **Step 6: Run the full `UploadPdfCommandHandlerTests` class to catch regressions**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~UploadPdfCommandHandlerTests" --nologo 2>&1 | tail -5
```

Expected: zero failures.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandlerTests.cs
git commit -m "fix(upload): resolve SharedGameId when gameId matches shared_games (#2)"
```

---

## Task 9: Bug #3 — StartGameNightSessionCommandHandler adds organizer as owner participant

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs`
- Modify: the matching test file

- [ ] **Step 1: Locate the test file**

```bash
find apps/api/tests/Api.Tests -name "StartGameNightSessionCommandHandlerTests*" 2>/dev/null
```

- [ ] **Step 2: Add a failing regression test**

Append to the test class:

```csharp
[Fact]
public async Task Handle_CreatesSessionWithOrganizerAsOwnerParticipant()
{
    // Arrange
    var userId = Guid.NewGuid();
    var gameNightId = Guid.NewGuid();
    var gameId = Guid.NewGuid();

    _db.Users.Add(new UserEntity
    {
        Id = userId,
        DisplayName = "Test Organizer",
        Email = "organizer@example.com",
        Role = "user",
    });
    _db.GameNightEvents.Add(new GameNightEventEntity
    {
        Id = gameNightId,
        OrganizerId = userId,
        Title = "Test Night",
        Status = "Published",
        // include whatever other non-null fields the entity requires
    });
    await _db.SaveChangesAsync();

    List<ParticipantDto>? dispatchedParticipants = null;
    _mediatorMock
        .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
        .Callback<IRequest<CreateSessionResult>, CancellationToken>((cmd, _) =>
        {
            dispatchedParticipants = ((CreateSessionCommand)cmd).Participants;
        })
        .ReturnsAsync(new CreateSessionResult(Guid.NewGuid(), "CODE123"));

    var command = new StartGameNightSessionCommand(
        gameNightId, gameId, "Mage Knight Board Game", userId);

    // Act
    await _sut.Handle(command, CancellationToken.None);

    // Assert
    dispatchedParticipants.Should().NotBeNull();
    dispatchedParticipants!.Should().ContainSingle();
    dispatchedParticipants![0].UserId.Should().Be(userId);
    dispatchedParticipants![0].IsOwner.Should().BeTrue();
    dispatchedParticipants![0].DisplayName.Should().Be("Test Organizer");
}
```

If the existing test class uses different mocks/fixture names, rename accordingly. The essential assertions are: exactly one participant, matches the organizer, `IsOwner == true`.

- [ ] **Step 3: Run the test — expect validation failure**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~StartGameNightSessionCommandHandlerTests.Handle_CreatesSessionWithOrganizer" --nologo 2>&1 | tail -10
```

Expected: either `Participants is empty` or validation error from `CreateSessionCommandValidator`.

- [ ] **Step 4: Apply the fix**

Open `StartGameNightSessionCommandHandler.cs`. Add `using Microsoft.EntityFrameworkCore;` if absent. Replace the line that constructs the `CreateSessionCommand` (currently `new List<ParticipantDto>()`) with:

```csharp
var organizer = await _db.Users
    .AsNoTracking()
    .Where(u => u.Id == command.UserId)
    .Select(u => new { u.DisplayName })
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);

var participants = new List<ParticipantDto>
{
    new ParticipantDto
    {
        UserId = command.UserId,
        DisplayName = organizer?.DisplayName ?? "Organizer",
        IsOwner = true,
        JoinOrder = 0,
    }
};

var createResult = await _mediator.Send(new CreateSessionCommand(
    command.UserId,
    command.GameId,
    "GameSpecific",
    DateTime.UtcNow,
    null,
    participants), cancellationToken).ConfigureAwait(false);
```

The handler class already has `IMediator _mediator` via constructor injection. If `_db` (MeepleAiDbContext) is not already injected, add it: check the constructor, add a `MeepleAiDbContext _db` field and parameter, and propagate through DI (the class is resolved automatically by MediatR scanning, so no explicit DI change is needed).

- [ ] **Step 5: Run the test — expect pass**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~StartGameNightSessionCommandHandlerTests" --nologo 2>&1 | tail -5
```

Expected: zero failures.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Commands/GameNights/StartGameNightSessionCommandHandlerTests.cs
git commit -m "fix(game-night): add organizer as owner participant on session start (#3)"
```

---

## Task 10: Bug #4 — GameNightEventRepository safe Enum.TryParse

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs`
- Modify: matching test file

- [ ] **Step 1: Locate the test file**

```bash
find apps/api/tests/Api.Tests -name "GameNightEventRepositoryTests*" 2>/dev/null
```

- [ ] **Step 2: Add a failing regression test**

Append to the test class:

```csharp
[Fact]
public async Task MapToDomain_UnknownSessionStatus_DefaultsToPendingAndLogsWarning()
{
    // Arrange
    var gameNight = new GameNightEventEntity
    {
        Id = Guid.NewGuid(),
        OrganizerId = Guid.NewGuid(),
        Title = "Test",
        Status = "Published",
    };
    gameNight.Sessions.Add(new GameNightSessionEntity
    {
        Id = Guid.NewGuid(),
        GameNightEventId = gameNight.Id,
        GameId = Guid.NewGuid(),
        GameTitle = "X",
        PlayOrder = 1,
        Status = "CorruptedValueThatDoesNotExist",
    });
    _db.GameNightEvents.Add(gameNight);
    await _db.SaveChangesAsync();

    // Act — MapToDomain used to throw on unknown enum string
    var result = await _sut.GetByIdAsync(gameNight.Id, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result!.Sessions.Should().ContainSingle()
        .Which.Status.Should().Be(GameNightSessionStatus.Pending);
}
```

- [ ] **Step 3: Run the test — expect InvalidArgumentException**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GameNightEventRepositoryTests.MapToDomain_UnknownSessionStatus" --nologo 2>&1 | tail -10
```

Expected: failure with `Enum.Parse` exception.

- [ ] **Step 4: Apply the three-site fix**

Open `GameNightEventRepository.cs`. Search for `Enum.Parse<` — there should be three occurrences. Add an `ILogger<GameNightEventRepository> _logger` field and constructor parameter if absent (it is likely already present; if not, add it).

Add these three private helpers at the bottom of the class:

```csharp
private GameNightStatus ParseGameNightStatus(string raw)
{
    if (Enum.TryParse<GameNightStatus>(raw, ignoreCase: true, out var v)) return v;
    _logger.LogWarning("Unknown GameNightStatus '{Raw}', defaulting to Draft", raw);
    return GameNightStatus.Draft;
}

private RsvpStatus ParseRsvpStatus(string raw)
{
    if (Enum.TryParse<RsvpStatus>(raw, ignoreCase: true, out var v)) return v;
    _logger.LogWarning("Unknown RsvpStatus '{Raw}', defaulting to Pending", raw);
    return RsvpStatus.Pending;
}

private GameNightSessionStatus ParseSessionStatus(string raw)
{
    if (Enum.TryParse<GameNightSessionStatus>(raw, ignoreCase: true, out var v)) return v;
    _logger.LogWarning("Unknown GameNightSessionStatus '{Raw}', defaulting to Pending", raw);
    return GameNightSessionStatus.Pending;
}
```

Then replace each `Enum.Parse<...>(rawString)` call site with the corresponding helper:
- `Enum.Parse<GameNightStatus>(entity.Status)` → `ParseGameNightStatus(entity.Status)`
- `Enum.Parse<RsvpStatus>(r.Status)` → `ParseRsvpStatus(r.Status)`
- `Enum.Parse<GameNightSessionStatus>(s.Status)` → `ParseSessionStatus(s.Status)`

If the repository class is currently `internal class GameNightEventRepository { public GameNightEventRepository(MeepleAiDbContext db) { ... } }` with no logger injection, extend the constructor:

```csharp
private readonly MeepleAiDbContext _db;
private readonly ILogger<GameNightEventRepository> _logger;

public GameNightEventRepository(
    MeepleAiDbContext db,
    ILogger<GameNightEventRepository> logger)
{
    _db = db ?? throw new ArgumentNullException(nameof(db));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

The DI container injects `ILogger<T>` automatically; no explicit registration is needed.

- [ ] **Step 5: Build + run tests**

```bash
cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -5
cd ../../tests/Api.Tests && dotnet test --filter "FullyQualifiedName~GameNightEventRepositoryTests" --nologo 2>&1 | tail -5
```

Expected: zero build errors, new test passes, other existing tests still pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepositoryTests.cs
git commit -m "fix(game-night): safe Enum.TryParse in MapToDomain (#4)"
```

---

## Task 11: Backfill EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/20260408_BackfillSharedGameIdFromGames.cs` (generated, then edited)
- Create: `apps/api/tests/Api.Tests/Integration/BackfillSharedGameIdMigrationTests.cs`

- [ ] **Step 1: Generate the migration scaffold**

```bash
cd apps/api/src/Api
dotnet ef migrations add BackfillSharedGameIdFromGames --no-build
```

This creates two files under `Infrastructure/Migrations/`:
- `20260408..._BackfillSharedGameIdFromGames.cs`
- `20260408..._BackfillSharedGameIdFromGames.Designer.cs`

- [ ] **Step 2: Replace the migration body with the backfill SQL**

Open the non-Designer file and replace the `Up`/`Down` methods with:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        UPDATE pdf_documents pd
        SET ""SharedGameId"" = g.""SharedGameId""
        FROM games g
        WHERE pd.""GameId"" = g.""Id""
          AND pd.""SharedGameId"" IS NULL
          AND g.""SharedGameId"" IS NOT NULL;

        UPDATE vector_documents vd
        SET shared_game_id = pd.""SharedGameId""
        FROM pdf_documents pd
        WHERE vd.""PdfDocumentId"" = pd.""Id""
          AND vd.shared_game_id IS NULL
          AND pd.""SharedGameId"" IS NOT NULL;

        UPDATE text_chunks tc
        SET ""SharedGameId"" = pd.""SharedGameId""
        FROM pdf_documents pd
        WHERE tc.""PdfDocumentId"" = pd.""Id""
          AND tc.""SharedGameId"" IS NULL
          AND pd.""SharedGameId"" IS NOT NULL;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Forward-only: the backfill is non-destructive (only populates NULL columns).
    // A rollback is a no-op; data integrity is preserved.
}
```

- [ ] **Step 3: Write integration test**

Create `apps/api/tests/Api.Tests/Integration/BackfillSharedGameIdMigrationTests.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class BackfillSharedGameIdMigrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Up_PopulatesSharedGameIdFromGamesBridge()
    {
        // Arrange: insert rows directly via raw SQL to bypass EF defaults
        var sharedGameId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        await Db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO shared_games (id, title, approval_status) VALUES ({0}, 'Test', 2);
            INSERT INTO games (""Id"", ""Name"", ""CreatedAt"", ""SharedGameId"", approval_status)
                VALUES ({1}, 'Test', NOW(), {0}, 2);
            INSERT INTO pdf_documents (""Id"", ""GameId"", ""FileName"", ""FilePath"", ""FileSizeBytes"",
                                        ""ContentType"", ""UploadedByUserId"", ""UploadedAt"",
                                        processing_state, ""IsPublic"", ""DocumentType"", document_category,
                                        ""Language"")
                VALUES ({2}, {1}, 'test.pdf', '/blobs/test', 100, 'application/pdf',
                        {3}, NOW(), 'Ready', true, 'base', 'Rulebook', 'en');
            ",
            sharedGameId, gameId, pdfId, SystemUserId);

        // Act: the migration has already run in the test setup — re-run the
        // backfill SQL explicitly to assert behavior
        await Db.Database.ExecuteSqlRawAsync(@"
            UPDATE pdf_documents pd
            SET ""SharedGameId"" = g.""SharedGameId""
            FROM games g
            WHERE pd.""GameId"" = g.""Id""
              AND pd.""SharedGameId"" IS NULL
              AND g.""SharedGameId"" IS NOT NULL;
        ");

        // Assert
        var pdfSharedId = await Db.PdfDocuments
            .Where(p => p.Id == pdfId)
            .Select(p => p.SharedGameId)
            .FirstOrDefaultAsync();
        pdfSharedId.Should().Be(sharedGameId);
    }
}
```

If `IntegrationTestBase` with `Db` and `SystemUserId` properties does not exist, use whatever base class the existing integration tests use (e.g., `TestcontainerBase`, `PostgresIntegrationTestBase`). Locate an existing integration test in `apps/api/tests/Api.Tests/Integration/` and copy its setup pattern.

- [ ] **Step 4: Run the integration test**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~BackfillSharedGameIdMigrationTests" --nologo 2>&1 | tail -10
```

Expected: test passes.

- [ ] **Step 5: Build to confirm the migration compiles**

```bash
cd apps/api/src/Api && dotnet build --nologo 2>&1 | tail -5
```

Expected: zero errors.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/*BackfillSharedGameIdFromGames* \
        apps/api/tests/Api.Tests/Integration/BackfillSharedGameIdMigrationTests.cs
git commit -m "feat(db): backfill SharedGameId on pdf_documents/vector_documents/text_chunks"
```

---

## Task 12: Infra config — compose.staging.yml and storage.secret.example

**Files:**
- Modify: `infra/compose.staging.yml`
- Modify: `infra/secrets/storage.secret.example`

- [ ] **Step 1: Update compose.staging.yml**

Open `infra/compose.staging.yml`. Under the `api:` service, in the `environment:` block, add the `SEED_*` variables:

```yaml
  api:
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
      ASPNETCORE_URLS: http://+:8080
      SEED_PROFILE: Staging
      SEED_BUCKET_NAME: ${SEED_BUCKET_NAME}
      SEED_BUCKET_ENDPOINT: ${SEED_BUCKET_ENDPOINT}
      SEED_BUCKET_REGION: ${SEED_BUCKET_REGION:-auto}
      SEED_BUCKET_FORCE_PATH_STYLE: ${SEED_BUCKET_FORCE_PATH_STYLE:-false}
      # SEED_BUCKET_ACCESS_KEY and SEED_BUCKET_SECRET_KEY come from storage.secret
      POSTGRES_HOST: postgres
      # ... existing env vars unchanged ...
```

Preserve all existing env vars; only add the six new `SEED_*` ones plus `SEED_PROFILE`. Do NOT change indentation of other services.

- [ ] **Step 2: Update storage.secret.example**

Open `infra/secrets/storage.secret.example`. Append at the end:

```bash
# --- Seed bucket (readonly) ---
# Used by the API at startup to fetch rulebook PDFs for seeding.
# Create a readonly access key for the meepleai-seeds bucket.
SEED_BUCKET_NAME=meepleai-seeds
SEED_BUCKET_ENDPOINT=https://<your-account>.r2.cloudflarestorage.com
SEED_BUCKET_REGION=auto
SEED_BUCKET_FORCE_PATH_STYLE=false
SEED_BUCKET_ACCESS_KEY=<readonly_access_key>
SEED_BUCKET_SECRET_KEY=<readonly_secret_key>
```

- [ ] **Step 3: Verify compose file parses**

```bash
cd infra
docker compose -f docker-compose.yml -f compose.staging.yml config > /dev/null 2>&1 && echo "OK" || echo "FAIL"
```

Expected: `OK` (compose file is valid YAML and resolves env vars without error).

- [ ] **Step 4: Commit**

```bash
git add infra/compose.staging.yml infra/secrets/storage.secret.example
git commit -m "chore(infra): add SEED_PROFILE and SEED_BUCKET_* config to staging"
```

---

## Task 13: Upload script — upload-seed-pdfs.sh

**Files:**
- Create: `infra/scripts/upload-seed-pdfs.sh`

- [ ] **Step 1: Write the script**

Create `infra/scripts/upload-seed-pdfs.sh`:

```bash
#!/usr/bin/env bash
# Uploads rulebook PDFs to the meepleai-seeds bucket with SHA256 object metadata.
# Idempotent: skips uploads whose remote sha256 metadata already matches local.
#
# Usage:
#   ./infra/scripts/upload-seed-pdfs.sh [LOCAL_DIR]
#
# Requires:
#   - aws-cli >= 2.x
#   - SEED_BUCKET_* env vars OR infra/secrets/storage.secret sourced
#   - Write credentials (not the runtime readonly ones)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
LOCAL_DIR="${1:-${REPO_ROOT}/data/rulebook}"
OUT_TSV="${LOCAL_DIR}/.seed-hashes.tsv"

# Load secrets if env not preset
if [[ -z "${SEED_BUCKET_NAME:-}" ]]; then
  if [[ -f "${REPO_ROOT}/infra/secrets/storage.secret" ]]; then
    # shellcheck disable=SC1091
    source "${REPO_ROOT}/infra/secrets/storage.secret"
  fi
fi

: "${SEED_BUCKET_NAME:?SEED_BUCKET_NAME is required}"
: "${SEED_BUCKET_ENDPOINT:?SEED_BUCKET_ENDPOINT is required}"
: "${SEED_BUCKET_ACCESS_KEY:?SEED_BUCKET_ACCESS_KEY is required}"
: "${SEED_BUCKET_SECRET_KEY:?SEED_BUCKET_SECRET_KEY is required}"

export AWS_ACCESS_KEY_ID="${SEED_BUCKET_ACCESS_KEY}"
export AWS_SECRET_ACCESS_KEY="${SEED_BUCKET_SECRET_KEY}"
export AWS_DEFAULT_REGION="${SEED_BUCKET_REGION:-auto}"

PREFIX="rulebooks/v1"
BUCKET="${SEED_BUCKET_NAME}"
ENDPOINT="${SEED_BUCKET_ENDPOINT}"

if [[ ! -d "${LOCAL_DIR}" ]]; then
  echo "ERROR: local directory not found: ${LOCAL_DIR}" >&2
  exit 1
fi

> "${OUT_TSV}"  # truncate

uploaded=0
skipped=0
for file in "${LOCAL_DIR}"/*_rulebook.pdf; do
  [[ -f "$file" ]] || continue
  base="$(basename "$file")"
  slug="${base%_rulebook.pdf}"

  local_sha="$(sha256sum "$file" | cut -d' ' -f1)"
  if [[ -z "$local_sha" ]]; then
    echo "ERROR: failed to compute sha256 for $file" >&2
    exit 1
  fi

  key="${PREFIX}/${base}"

  remote_sha="$(aws s3api head-object \
      --endpoint-url "${ENDPOINT}" \
      --bucket "${BUCKET}" \
      --key "${key}" \
      --query 'Metadata.sha256' \
      --output text 2>/dev/null || echo "")"

  if [[ "$remote_sha" == "$local_sha" ]]; then
    echo "  SKIP  ${slug} (sha matches)"
    skipped=$((skipped + 1))
  else
    aws s3 cp "$file" "s3://${BUCKET}/${key}" \
      --endpoint-url "${ENDPOINT}" \
      --metadata "sha256=${local_sha}" \
      --no-progress
    echo "  UP    ${slug} (${local_sha:0:12}...)"
    uploaded=$((uploaded + 1))
  fi

  printf "%s\t%s\n" "${slug}" "${local_sha}" >> "${OUT_TSV}"
done

echo ""
echo "Summary: ${uploaded} uploaded, ${skipped} already up to date"
echo "Wrote ${OUT_TSV}"
```

- [ ] **Step 2: Make executable and verify syntax**

```bash
chmod +x infra/scripts/upload-seed-pdfs.sh
bash -n infra/scripts/upload-seed-pdfs.sh && echo "syntax OK"
```

Expected: `syntax OK`.

- [ ] **Step 3: Commit**

```bash
git add infra/scripts/upload-seed-pdfs.sh
git commit -m "feat(seed): add upload-seed-pdfs.sh script"
```

---

## Task 14: Patch manifest script — patch-manifest-from-hashes.sh

**Files:**
- Create: `infra/scripts/patch-manifest-from-hashes.sh`

- [ ] **Step 1: Write the script**

Create `infra/scripts/patch-manifest-from-hashes.sh`:

```bash
#!/usr/bin/env bash
# Rewrites the YAML manifests in
# apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/ by converting
# existing `pdf:` entries to `pdfBlobKey:` + `pdfSha256:` based on
# the .seed-hashes.tsv file produced by upload-seed-pdfs.sh.
#
# For slugs that exist in .seed-hashes.tsv but have no matching manifest entry,
# appends a skeleton entry at the bottom of staging.yml for the developer to
# fill in manually.
#
# Usage:
#   ./infra/scripts/patch-manifest-from-hashes.sh [HASHES_TSV]
#
# Requires: yq (mikefarah/yq v4+)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

HASHES_TSV="${1:-${REPO_ROOT}/data/rulebook/.seed-hashes.tsv}"
MANIFEST_DIR="${REPO_ROOT}/apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests"
STAGING="${MANIFEST_DIR}/staging.yml"
DEV="${MANIFEST_DIR}/dev.yml"
PROD="${MANIFEST_DIR}/prod.yml"

if [[ ! -f "${HASHES_TSV}" ]]; then
  echo "ERROR: hashes file not found: ${HASHES_TSV}" >&2
  exit 1
fi

if ! command -v yq >/dev/null; then
  echo "ERROR: yq (mikefarah/yq v4+) is required" >&2
  exit 1
fi

# Build a slug→hash lookup from the TSV
declare -A HASH_FOR
while IFS=$'\t' read -r slug sha; do
  [[ -n "$slug" && -n "$sha" ]] || continue
  HASH_FOR["$slug"]="$sha"
done < "${HASHES_TSV}"

patch_file() {
  local yml="$1"
  [[ -f "$yml" ]] || { echo "SKIP (missing): $yml"; return; }

  echo "Patching: $yml"
  local matched=0

  for slug in "${!HASH_FOR[@]}"; do
    local hash="${HASH_FOR[$slug]}"
    local expected_pdf="${slug}_rulebook.pdf"
    local blob_key="rulebooks/v1/${expected_pdf}"

    # Find the entry by matching the legacy pdf field (just the filename)
    local idx
    idx="$(yq eval "(.catalog.games[] | select(.pdf == \"${expected_pdf}\")) | path | .[-1]" "$yml" 2>/dev/null || echo "")"

    if [[ -n "$idx" ]]; then
      yq eval -i "
        .catalog.games[${idx}].pdfBlobKey = \"${blob_key}\" |
        .catalog.games[${idx}].pdfSha256  = \"${hash}\" |
        .catalog.games[${idx}].pdfVersion = \"1.0\" |
        del(.catalog.games[${idx}].pdf)
      " "$yml"
      matched=$((matched + 1))
    fi
  done

  echo "  matched ${matched} entries"
}

append_missing() {
  local yml="$1"
  local appended=0

  for slug in "${!HASH_FOR[@]}"; do
    local hash="${HASH_FOR[$slug]}"
    local blob_key="rulebooks/v1/${slug}_rulebook.pdf"

    # Skip if already present via pdfBlobKey
    if yq eval "[.catalog.games[] | select(.pdfBlobKey == \"${blob_key}\")] | length" "$yml" | grep -q '^0$'; then
      # Not present — append skeleton
      yq eval -i "
        .catalog.games += [{
          \"title\": \"TODO: fill from manifest.json\",
          \"bggId\": 0,
          \"language\": \"en\",
          \"pdfBlobKey\": \"${blob_key}\",
          \"pdfSha256\":  \"${hash}\",
          \"pdfVersion\": \"1.0\"
        }]
      " "$yml"
      appended=$((appended + 1))
    fi
  done
  echo "  appended ${appended} skeleton entries to $(basename "$yml")"
}

patch_file "${STAGING}"
patch_file "${DEV}"
patch_file "${PROD}"

# Skeleton append is staging-only (developer completes metadata there first)
append_missing "${STAGING}"

echo ""
echo "Done. Review diffs with: git diff apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/"
```

- [ ] **Step 2: Verify syntax**

```bash
chmod +x infra/scripts/patch-manifest-from-hashes.sh
bash -n infra/scripts/patch-manifest-from-hashes.sh && echo "syntax OK"
```

Expected: `syntax OK`.

- [ ] **Step 3: Commit**

```bash
git add infra/scripts/patch-manifest-from-hashes.sh
git commit -m "feat(seed): add patch-manifest-from-hashes.sh script"
```

---

## Task 15: Developer runs the scripts and updates manifests

This task is a **manual operation** that the developer running the plan must perform locally on their dev machine. It produces the committed manifest files.

- [ ] **Step 1: Verify local PDFs are present**

```bash
ls data/rulebook/*_rulebook.pdf | wc -l
```

Expected: 130+ files.

- [ ] **Step 2: Create the meepleai-seeds bucket**

Using the Cloudflare R2 (or AWS S3) console or CLI, create a bucket named `meepleai-seeds` in a region of choice. Generate two credential pairs:
- **Write-capable**: used for the upload script (this session only, discard after).
- **Readonly**: used by the API container at runtime, stored in the server's `storage.secret`.

- [ ] **Step 3: Run the upload script with write credentials**

Temporarily set the write credentials and run:

```bash
SEED_BUCKET_NAME=meepleai-seeds \
SEED_BUCKET_ENDPOINT=https://<account>.r2.cloudflarestorage.com \
SEED_BUCKET_ACCESS_KEY=<write_key> \
SEED_BUCKET_SECRET_KEY=<write_secret> \
./infra/scripts/upload-seed-pdfs.sh
```

Expected: all PDFs uploaded, `.seed-hashes.tsv` written.

- [ ] **Step 4: Run the patch-manifest script**

```bash
./infra/scripts/patch-manifest-from-hashes.sh
```

Expected: `staging.yml`, `dev.yml`, `prod.yml` updated; skeleton entries appended at the end of `staging.yml` for any unmatched slugs.

- [ ] **Step 5: Fill in the skeleton metadata**

Open `apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/staging.yml` and find the appended entries (those with `title: "TODO: fill from manifest.json"`). For each one, copy the relevant fields from `data/rulebook/manifest.json` (created in PR #267) — specifically `name` → `title`, `bggId`, `yearPublished`, `minPlayers`, `maxPlayers`, `playingTime`, `designers`, `publishers`, `categories`, `mechanics`.

Mirror the filled entries into `dev.yml` and `prod.yml` if they are meant to carry the same data.

- [ ] **Step 6: Validate the YAML**

```bash
cd apps/api/src/Api
dotnet build --nologo 2>&1 | tail -5
```

The build embeds the YAML as a resource; if deserialization would fail, the build succeeds but a later test will catch it. To proactively validate, run:

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~SeedManifestLoader" --nologo 2>&1 | tail -5
```

Expected: existing manifest loader tests still pass.

- [ ] **Step 7: Upload readonly credentials to the deploy server**

SSH to the staging deploy server and update `/opt/meepleai/secrets/storage.secret` with the readonly credentials from Step 2. Do not commit these to git.

- [ ] **Step 8: Commit the updated manifests**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/staging.yml \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/prod.yml
git commit -m "chore(seed): migrate manifests to blob-based PDF references"
```

---

## Task 16: Integration test — end-to-end seed cycle

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/SeedCatalogBlobIntegrationTests.cs`

- [ ] **Step 1: Write the integration test**

Create `apps/api/tests/Api.Tests/Integration/SeedCatalogBlobIntegrationTests.cs`:

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Tests.Integration;

[Trait("Category", "Integration")]
public class SeedCatalogBlobIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task PdfSeeder_RunsEndToEnd_WithBlobReaderAndPersistsEntity()
    {
        // Arrange — insert a SharedGame + games bridge so GameSeeder is not needed
        var sharedGameId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        Db.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Mage Knight Board Game",
            BggId = 96848,
        });
        Db.Games.Add(new GameEntity
        {
            Id = gameId,
            Name = "Mage Knight Board Game",
            CreatedAt = DateTime.UtcNow,
            SharedGameId = sharedGameId,
            BggId = 96848,
        });
        await Db.SaveChangesAsync();

        var manifest = new SeedManifest();
        manifest.Catalog.Games.Add(new SeedManifestGame
        {
            Title = "Mage Knight Board Game",
            BggId = 96848,
            Language = "en",
            PdfBlobKey = "rulebooks/v1/mage-knight_rulebook.pdf",
            PdfSha256 = "test-hash-123",
            PdfVersion = "1.0",
        });

        var seedBlobMock = new Mock<ISeedBlobReader>();
        seedBlobMock.SetupGet(x => x.IsConfigured).Returns(true);
        seedBlobMock.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        seedBlobMock.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        var primaryBlobMock = new Mock<IBlobStorageService>();
        primaryBlobMock
            .Setup(x => x.StoreAsync(
                It.IsAny<Stream>(),
                "mage-knight_rulebook.pdf",
                gameId.ToString(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: Guid.NewGuid().ToString(),
                FilePath: "/blobs/xyz",
                FileSizeBytes: 4));

        // Act
        await PdfSeeder.SeedAsync(
            Db, manifest, new Dictionary<int, Guid> { [96848] = gameId },
            SystemUserId, primaryBlobMock.Object, seedBlobMock.Object,
            NullLogger.Instance, CancellationToken.None);

        // Assert
        var saved = await Db.PdfDocuments.SingleAsync(p => p.GameId == gameId);
        saved.FileName.Should().Be("mage-knight_rulebook.pdf");
        saved.ContentHash.Should().Be("test-hash-123");
        saved.ProcessingState.Should().Be("Pending");
        saved.DocumentCategory.Should().Be("Rulebook");
    }
}
```

Adjust `IntegrationTestBase`, `Db`, and `SystemUserId` references to match the existing integration test fixture in the project.

- [ ] **Step 2: Run the integration test**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~SeedCatalogBlobIntegrationTests" --nologo 2>&1 | tail -10
```

Expected: test passes.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/SeedCatalogBlobIntegrationTests.cs
git commit -m "test(seed): add end-to-end integration for blob-based PdfSeeder"
```

---

## Task 17: Full build and test sweep

- [ ] **Step 1: Clean build the whole API**

```bash
cd apps/api/src/Api
dotnet build --nologo 2>&1 | tail -15
```

Expected: zero errors, zero warnings.

- [ ] **Step 2: Run all unit tests**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category!=Integration" --nologo 2>&1 | tail -10
```

Expected: all passing.

- [ ] **Step 3: Run all integration tests**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category=Integration" --nologo 2>&1 | tail -10
```

Expected: all passing (barring pre-existing flaky tests noted in PR #267).

- [ ] **Step 4: Push the branch**

```bash
git push -u origin feature/seed-catalog-blob-migration
```

Expected: push succeeds, pre-push checks pass.

- [ ] **Step 5: Open PR**

```bash
gh pr create --base main-dev --head feature/seed-catalog-blob-migration \
  --title "feat(seed): blob-based PDF seeding + game-night bug fixes" \
  --body "$(cat <<'EOF'
## Summary

- Migrates `PdfSeeder` from filesystem-based to S3/R2 blob-based seeding via `meepleai-seeds` bucket.
- Adds `ISeedBlobReader` with `S3SeedBlobReader` + `NoOpSeedBlobReader` fallback.
- Extends `SeedManifestGame` with `PdfBlobKey`, `PdfSha256`, `PdfVersion`.
- Fixes four runtime bugs discovered in PR #267 end-to-end testing:
  - `UploadPdfCommandHandler`: resolve SharedGameId when gameId matches a shared_games row
  - `StartGameNightSessionCommandHandler`: add organizer as owner participant
  - `GameNightEventRepository`: safe `Enum.TryParse` helpers on all three status parses
  - `compose.staging.yml`: explicit `SEED_PROFILE=Staging`
- Forward-only backfill migration populates `SharedGameId` on existing rows.
- Developer scripts: `upload-seed-pdfs.sh` + `patch-manifest-from-hashes.sh`.

See: `docs/superpowers/specs/2026-04-08-seed-catalog-blob-migration-design.md`

## Test plan
- [ ] Unit tests green for PdfSeeder, S3SeedBlobReader, bug fixes
- [ ] Integration test end-to-end seed cycle passes
- [ ] Backfill migration integration test passes
- [ ] Staging deploy: verify `/api/v1/shared-games?search=...` returns results after ~30 min
- [ ] Staging deploy: verify `/api/v1/knowledge-base/{id}/status` returns Completed for a sampled game

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR created, URL printed.

---

## Self-Review Checklist (post-plan)

After all tasks above are implemented, manually verify:

1. **Spec coverage** — every "Scope Summary" row in the spec has a corresponding task:
   - `meepleai-seeds` bucket → Task 15 (developer op)
   - `upload-seed-pdfs.sh` → Task 13
   - `patch-manifest-from-hashes.sh` → Task 14
   - Manifest files migrated → Task 15
   - `SeedManifest.cs` extended → Task 1
   - `PdfSeeder.cs` rewritten → Tasks 5-6
   - `ISeedBlobReader` + implementations → Tasks 2-3
   - DI registration → Task 4
   - `CatalogSeeder` + `CatalogSeedLayer` updated → Task 7
   - Bug #2 (`UploadPdfCommandHandler`) → Task 8
   - Bug #3 (`StartGameNightSessionCommandHandler`) → Task 9
   - Bug #4 (`GameNightEventRepository`) → Task 10
   - Backfill migration → Task 11
   - `compose.staging.yml` / `storage.secret.example` → Task 12
   - Integration test → Task 16
   - Full sweep + PR → Task 17

2. **Type consistency** — all method signatures, property names, and class names in later tasks match what was defined in earlier tasks:
   - `SeedManifestGame` (not `SeedManifestEntry`) everywhere
   - `PdfSeeder.SeedAsync(db, manifest, gameMap, systemUserId, primaryBlob, seedBlob, logger, ct)` — same 8-arg signature in Tasks 5, 6, 7, 16
   - `BlobStorageResult(Success, FileId, FilePath, FileSizeBytes, ErrorMessage?)` positional record — same in Tasks 5, 6, 16
   - `ISeedBlobReader.IsConfigured` / `ExistsAsync` / `OpenReadAsync` — same in Tasks 2, 3, 5, 6, 16
   - `PdfDocumentEntity` fields: `FilePath`, `VersionLabel`, `DocumentType = "base"`, `DocumentCategory = "Rulebook"`, `IsActiveForRag`, `ProcessingPriority` — consistent in Tasks 5, 6, 16

3. **Bug fix line counts** — Task 10 covers all three `Enum.Parse<T>` call sites (one for each of `GameNightStatus`, `RsvpStatus`, `GameNightSessionStatus`), not just one.

4. **Rollback** — the backfill migration's `Down` method is a no-op (documented in Task 11 Step 2), consistent with the spec's forward-only claim.
