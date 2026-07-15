# Issue #2947 — Unify R2 Cover Convention (BGG L2.5 + Backfill L4 + Pipeline L4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every cover write-site produce a *deterministic* R2 physical key that `CoverUrlResolver` can reconstruct from the DB-persisted key, so BGG (L2.5) and PDF-cover (L4 pipeline + L4 backfill) covers resolve instead of falling through to placeholder.

**Architecture:** The proven pattern (`CoverR2UploadPipeline` for Wikidata, `PdfCoverUploadPipeline` for PDF materialization) talks directly to `IAmazonS3.PutObjectAsync` with a *raw, deterministic* object key, and the resolver reconstructs that exact key at read time via `IBlobStorageService.GetPresignedUrlForRawKeyAsync(rawKey)`. Today the three remaining write-sites (`BggCoverDownloader`, `BackfillPdfCoversJob`, `PdfProcessingPipelineService`) instead call `IBlobStorageService.StoreAsync`, which mints a random `Guid.NewGuid()` fileId (`S3BlobStorageService.cs:69`) → the physical key `game-images/{resourceKey}/{guid}_{file}` is non-reconstructible from the DB-persisted base key. This plan introduces two dedicated raw-S3 upload pipelines (`BggCoverUploadPipeline`, and reuses the existing `PdfCoverUploadPipeline` for the two PDF write-sites), rewires the three write-sites to persist a deterministic key, and switches the resolver's L2.5 BGG branch from the legacy `GetPresignedDownloadUrlAsync` to `GetPresignedUrlForRawKeyAsync`.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + MediatR (CQRS), AWSSDK.S3 (`IAmazonS3`), EF Core (InMemory for unit tests), xUnit + Moq + FluentAssertions, Quartz (backfill job).

## Global Constraints

- **Branch name:** `feature/issue-2947-r2-cover-convention` — created FROM `main-dev` (HEAD = `963ebbd65`, merge of #2943). Run the pre-creation safety check: `git branch --show-current` MUST print `main-dev`, `git status` MUST be clean of source changes, then `git checkout -b feature/issue-2947-r2-cover-convention`.
- **PR target:** parent branch `main-dev` (NOT `main`). After creating the branch: `git config branch.feature/issue-2947-r2-cover-convention.parent main-dev`.
- **Commit convention:** `feat|fix|refactor|test|chore(scope): description`. Scope = `catalog` for BGG, `pdf`/`docproc` for PDF pipeline/backfill.
- **GOTCHA — Meziantou MA0025:** `throw new NotImplementedException()` stubs are a BUILD ERROR. Every task does REAL TDD: red test → minimal REAL implementation. Never scaffold with a throwing stub.
- **GOTCHA — SonarAnalyzer S1135:** a `// TODO(...)` comment in C# is a BUILD ERROR. Use `// Follow-up:` if you must annotate deferred work.
- **GOTCHA — CQRS:** endpoints call ONLY `IMediator.Send()`; never inject a service directly into an endpoint. (This plan touches no endpoints — services + a Quartz job + a static resolver — so no endpoint edits.)
- **GOTCHA — DDD:** entities have private setters + factory methods; value objects are immutable records with validation in their factory. (This plan adds no new entity/VO; `SharedGameEntity.BggCoverR2Key` is an existing EF persistence entity with a public setter — leave it as-is.)
- **GOTCHA — Exceptions:** use `ConflictException` (409) / `NotFoundException` (404); NEVER `InvalidOperationException` (500) for domain/validation errors. Argument validation on new pipelines uses `ArgumentException` (matches `CoverR2UploadPipeline`/`PdfCoverUploadPipeline`).
- **GOTCHA — DI:** register BOTH the interface `IService` and its implementation. New pipeline is registered as a singleton via a factory delegate that lazily builds the `AmazonS3Client` from `S3_*` config (mirror `RegisterCoverR2UploadPipeline` / `RegisterPdfCoverUploadPipeline` verbatim, including `s3Client.BeforeRequestEvent += BlobStorageServiceFactory.StripUnsupportedR2Headers`).
- **GOTCHA — test namespace `Unit` collision:** `Api.Tests.Unit` exists; if you `using MediatR;` (which exposes `Unit`) in a test that also references the `Unit` type, fully-qualify. This plan's tests do not need MediatR's `Unit`. When mocking an `ICommand : IRequest` handler you would use `.Returns(Task.CompletedTask)` NOT `.ReturnsAsync(Unit.Value)` — not needed here (no command handler is mocked).
- **GOTCHA — testhost:** kill any lingering testhost before running tests: `tasklist | grep testhost` → `taskkill //PID <PID> //F`. Percentages/culture: use `$"{val*100:0}%"` (culture-independent) — not applicable in this plan (no percentage formatting).
- **Test commands (this repo):** BE `cd apps/api/src/Api && dotnet test --filter "<FullyQualifiedName~...>"` (run from the API project dir; the test project is referenced). FE not used (BE-only issue).
- **Integration-test caveat (documented, NOT a blocker):** the Testcontainers-MinIO end-to-end R2 test skips locally because MinIO-over-HTTP rejects `DisablePayloadSigning`. This plan is **unit-first**: every write-site is covered by a `Mock<IAmazonS3>`-based unit test that asserts the exact deterministic `PutObjectRequest.Key`, plus a resolver unit test asserting the reconstructed raw key. Task 6 adds a **skippable** MinIO integration test guarded exactly like the existing gated integration tests so CI runs it where MinIO/R2-HTTPS is available and it is inert locally.

---

## File Structure

**New files:**
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverUploadPipeline.cs` — contract for the BGG raw-S3 upload pipeline (returns the exact physical key persisted to `BggCoverR2Key`).
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipeline.cs` — `IAmazonS3` PutObject to `bgg-covers/{bggId}/cover{ext}`; returns that full key verbatim (BGG images keep their original extension; the resolver passes the key with NO suffix appended).
- `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs` — unit tests (Mock<IAmazonS3>) asserting deterministic key, content-type, cache-control, validation, cancellation, S3-error rethrow.
- `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/CoverR2ConventionIntegrationTests.cs` — gated MinIO/R2 end-to-end round-trip (write via each pipeline → resolve via `GetPresignedUrlForRawKeyAsync` → HTTP-GET the presigned URL → assert 200 + bytes).

**Modified files:**
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs` — inject `IBggCoverUploadPipeline` instead of `IBlobStorageService`; on HTTP success, buffer bytes and delegate to the pipeline; return the pipeline's raw key.
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs:91-110` — L2.5 BGG branch (comment starts line 91, `if` block ends line 110) resolves via `GetPresignedUrlForRawKeyAsync(sharedGame.BggCoverR2Key)` (no suffix), not the legacy `GetPresignedDownloadUrlAsync(...)`.
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs` — register `IBggCoverUploadPipeline` singleton; adjust the `AddHttpClient<IBggCoverDownloader, BggCoverDownloader>` registration (typed client still works — the pipeline is resolved from DI, not the HttpClient factory).
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJob.cs:121-235` — replace the two `blob.StoreAsync` calls (`ProcessOneAsync` Generated branch) with a single `IPdfCoverUploadPipeline.UploadAsync(dbKey, previewBytes, ct)`; inject the pipeline via the DI scope; persist the deterministic dbKey.
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:517-609` — replace the two `_blobStorageService.StoreAsync` calls in `ExtractCoverImageAsync` with `_pdfCoverUploadPipeline.UploadAsync(dbKey, previewBytes, ct)`; add the optional `IPdfCoverUploadPipeline?` ctor param; persist the deterministic dbKey.
- `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs` — update mocks from `IBlobStorageService.StoreAsync` to `IBggCoverUploadPipeline.UploadAsync`.
- `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolverTests.cs` — L2.5 BGG metric test switches expectation to `GetPresignedUrlForRawKeyAsync`; add a slash/dot BGG key regression test.
- `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJobTests.cs` — replace `StoreAsync` verifications with `IPdfCoverUploadPipeline.UploadAsync` verifications; assert the persisted deterministic key.

**Key conventions locked by this plan:**
- **BGG physical + DB key (L2.5):** physical R2 object = `bgg-covers/{bggId}/cover{ext}` (ext preserved, e.g. `.jpg`). DB `BggCoverR2Key` = that SAME full key verbatim. Resolver calls `GetPresignedUrlForRawKeyAsync(BggCoverR2Key)` with **no** suffix. (Asymmetry vs L4/L2 which append a suffix — documented in the resolver + interface XML doc.)
- **PDF cover DB key (L4, both write-sites):** DB `CoverR2Key`/`PdfCoverR2Key` = `covers/pdf/{pdfId:D}/cover` (no suffix). Physical R2 object = `covers/pdf/{pdfId:D}/cover-preview.webp` (the `-preview.webp` suffix is appended by `PdfCoverUploadPipeline.UploadAsync` AND by the resolver's L4 branch — they must match). Only the **preview** size is uploaded to R2 (the resolver only ever reads `-preview.webp`; the thumbnail size was never read by the resolver and is dropped from the R2 write to remove the non-deterministic second blob).

---

### Task 1: BGG raw-S3 upload pipeline (`IBggCoverUploadPipeline` + impl)

De-risking slice: a self-contained new pipeline with no consumers yet. Mirrors `PdfCoverUploadPipeline` exactly (raw `IAmazonS3`, no `IBlobStorageService` indirection).

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverUploadPipeline.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipeline.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs`

**Interfaces:**
- Consumes: `Amazon.S3.IAmazonS3`, `Api.Services.Pdf.S3StorageOptions` (existing options record with `.BucketName`), `Amazon.S3.Model.PutObjectRequest`/`PutObjectResponse`.
- Produces: `internal interface IBggCoverUploadPipeline { Task<string> UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct); }` — returns the full physical key `bgg-covers/{bggId}/cover{extension}` (verbatim, persisted to `SharedGameEntity.BggCoverR2Key`). `extension` is a dot-prefixed lowercase extension (e.g. `.jpg`); the pipeline normalizes a null/empty/invalid extension to `.jpg`.

- [ ] **Step 1: Write the failing test file**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggCoverUploadPipelineTests : IDisposable
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<ILogger<BggCoverUploadPipeline>> _mockLogger;
    private readonly S3StorageOptions _options;
    private readonly BggCoverUploadPipeline _sut;

    public BggCoverUploadPipelineTests()
    {
        _mockS3Client = new Mock<IAmazonS3>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<BggCoverUploadPipeline>>();
        _options = new S3StorageOptions
        {
            Endpoint = "https://test.r2.cloudflarestorage.com",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "test-bucket",
            Region = "auto",
            PresignedUrlExpirySeconds = 3600,
            EnableEncryption = true,
            ForcePathStyle = false
        };
        _sut = new BggCoverUploadPipeline(_mockS3Client.Object, _options, _mockLogger.Object);
    }

    public void Dispose() => _mockS3Client.Reset();

    [Fact]
    public async Task UploadAsync_ValidBytes_PutsObjectWithDeterministicKeyIncludingExtension()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        PutObjectRequest? captured = null;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK, ETag = "\"abc\"" });

        var key = await _sut.UploadAsync(13, bytes, ".jpg", CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Key.Should().Be("bgg-covers/13/cover.jpg", "physical R2 key is deterministic + keeps the source extension");
        captured.BucketName.Should().Be("test-bucket");
        key.Should().Be("bgg-covers/13/cover.jpg", "the returned DB key is the exact physical key (resolver appends no suffix)");
    }

    [Fact]
    public async Task UploadAsync_NullOrEmptyExtension_DefaultsToJpg()
    {
        var bytes = new byte[] { 0x01 };
        var keys = new List<string>();
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => keys.Add(req.Key))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var k1 = await _sut.UploadAsync(7, bytes, null!, CancellationToken.None);
        var k2 = await _sut.UploadAsync(8, bytes, "", CancellationToken.None);

        k1.Should().Be("bgg-covers/7/cover.jpg");
        k2.Should().Be("bgg-covers/8/cover.jpg");
        keys.Should().BeEquivalentTo(new[] { "bgg-covers/7/cover.jpg", "bgg-covers/8/cover.jpg" });
    }

    [Fact]
    public async Task UploadAsync_SetsCacheControlImmutable1Year()
    {
        var bytes = new byte[] { 0x01 };
        PutObjectRequest? captured = null;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        await _sut.UploadAsync(1, bytes, ".png", CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Headers.CacheControl.Should().Be("public, max-age=31536000, immutable");
    }

    [Fact]
    public async Task UploadAsync_NullBytes_ThrowsArgumentException()
    {
        Func<Task> act = async () => await _sut.UploadAsync(1, null!, ".jpg", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*imageBytes*");
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_EmptyBytes_ThrowsArgumentException()
    {
        Func<Task> act = async () => await _sut.UploadAsync(1, Array.Empty<byte>(), ".jpg", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*imageBytes*");
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_Cancellation_RethrowsOperationCanceledException()
    {
        var bytes = new byte[] { 0x01 };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Func<Task> act = async () => await _sut.UploadAsync(1, bytes, ".jpg", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UploadAsync_S3Exception_Rethrows()
    {
        var bytes = new byte[] { 0x01 };
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("boom") { StatusCode = HttpStatusCode.InternalServerError, ErrorCode = "InternalError" });

        Func<Task> act = async () => await _sut.UploadAsync(1, bytes, ".jpg", CancellationToken.None);

        await act.Should().ThrowAsync<AmazonS3Exception>();
    }

    [Fact]
    public void Ctor_NullS3Client_ThrowsArgumentNullException()
    {
        Action act = () => new BggCoverUploadPipeline(null!, _options, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("s3Client");
    }

    [Fact]
    public void Ctor_NullOptions_ThrowsArgumentNullException()
    {
        Action act = () => new BggCoverUploadPipeline(_mockS3Client.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Ctor_NullLogger_ThrowsArgumentNullException()
    {
        Action act = () => new BggCoverUploadPipeline(_mockS3Client.Object, _options, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
```

- [ ] **Step 2: Run test to verify it fails (does not compile)**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BggCoverUploadPipelineTests"`
Expected: BUILD FAIL — `The type or namespace name 'BggCoverUploadPipeline' could not be found` (the interface + impl do not exist yet).

- [ ] **Step 3: Create the interface**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverUploadPipeline.cs`:

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2947 — uploads a downloaded BGG cover image to R2 under a
/// deterministic key so <see cref="CoverUrlResolver"/> can reconstruct it.
/// <para>
/// Unlike <c>CoverR2UploadPipeline</c> (Wikidata) and <c>PdfCoverUploadPipeline</c>
/// which store a suffix-stripped DB key and append a suffix at read time, the
/// BGG cover keeps its ORIGINAL image extension (BGG serves jpg/png, not webp).
/// The returned key is therefore the FULL physical object key
/// (<c>bgg-covers/{bggId}/cover{extension}</c>) and the resolver's L2.5 branch
/// passes it verbatim to <c>GetPresignedUrlForRawKeyAsync</c> with NO suffix.
/// </para>
/// </summary>
internal interface IBggCoverUploadPipeline
{
    /// <summary>
    /// Uploads <paramref name="imageBytes"/> to R2 as
    /// <c>bgg-covers/{bggId}/cover{extension}</c> with an immutable cache-control
    /// header, then returns that exact key — the value to persist on
    /// <c>SharedGameEntity.BggCoverR2Key</c>.
    /// </summary>
    /// <param name="bggId">BoardGameGeek game id; the cover namespace.</param>
    /// <param name="imageBytes">The downloaded cover image bytes. Must be non-null and non-empty.</param>
    /// <param name="extension">Dot-prefixed lowercase extension (e.g. <c>.jpg</c>);
    /// null/empty/invalid normalizes to <c>.jpg</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full deterministic physical key persisted to the DB.</returns>
    /// <exception cref="System.ArgumentException">When <paramref name="imageBytes"/> is null or empty.</exception>
    /// <exception cref="Amazon.S3.AmazonS3Exception">When the underlying S3 client fails.</exception>
    /// <exception cref="System.OperationCanceledException">When <paramref name="ct"/> signals cancellation.</exception>
    Task<string> UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct);
}
```

- [ ] **Step 4: Create the implementation**

Create `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipeline.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Issue #2947 — R2 upload pipeline for BGG-downloaded cover images. Mirrors
/// <see cref="CoverR2UploadPipeline"/> / <c>PdfCoverUploadPipeline</c>: talks
/// directly to <see cref="IAmazonS3"/> with a raw deterministic key rather than
/// going through <c>IBlobStorageService.StoreAsync</c> (which mints a random
/// fileId the resolver cannot reconstruct).
/// </summary>
internal sealed class BggCoverUploadPipeline : IBggCoverUploadPipeline
{
    // Cover assets are immutable for 1 year; re-uploads reuse the same key so
    // Cloudflare CDN cache stays warm (mirrors CoverR2UploadPipeline).
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private const string DefaultExtension = ".jpg";

    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<BggCoverUploadPipeline> _logger;

    public BggCoverUploadPipeline(
        IAmazonS3 s3Client,
        S3StorageOptions options,
        ILogger<BggCoverUploadPipeline> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException("imageBytes must be non-null and non-empty.", nameof(imageBytes));
        }

        var ext = NormalizeExtension(extension);
        var contentType = ContentTypeFor(ext);
        // Deterministic physical key = DB key (resolver appends NO suffix for L2.5).
        var objectKey = $"bgg-covers/{bggId.ToString(CultureInfo.InvariantCulture)}/cover{ext}";

        using var stream = new MemoryStream(imageBytes, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            // Required for S3-compatible providers (R2/MinIO) that don't support
            // STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER (mirrors CoverR2UploadPipeline).
            DisablePayloadSigning = true,
        };
        request.Headers.CacheControl = ImmutableCacheControl;

        // OperationCanceledException propagates naturally from PutObjectAsync.
        try
        {
            var response = await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Uploaded BGG cover to R2: BggId={BggId}, Key={Key}, Size={Size} bytes, ETag={ETag}",
                bggId, objectKey, imageBytes.Length, response.ETag);
            return objectKey;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(
                ex,
                "R2 BGG cover upload failed: BggId={BggId}, Key={Key}, StatusCode={Status}, ErrorCode={ErrorCode}",
                bggId, objectKey, ex.StatusCode, ex.ErrorCode);
            throw;
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return DefaultExtension;
        }

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        ext = ext.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => ext,
            _ => DefaultExtension,
        };
    }

    private static string ContentTypeFor(string normalizedExtension) => normalizedExtension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BggCoverUploadPipelineTests"`
Expected: PASS (10 tests).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverUploadPipeline.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipeline.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs
git commit -m "feat(catalog): add deterministic BggCoverUploadPipeline (raw R2 PutObject)"
```

---

### Task 2: Rewire `BggCoverDownloader` to the new pipeline + resolver L2.5 branch

Switches the BGG write-site off `IBlobStorageService.StoreAsync` and the resolver's read-side off the legacy `GetPresignedDownloadUrlAsync`. This is the "smallest self-contained slice" from the triage.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs:9-94`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs:91-110`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolverTests.cs`

**Interfaces:**
- Consumes: `IBggCoverUploadPipeline.UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct) → Task<string>` (Task 1). `IBlobStorageService.GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null) → Task<string?>` (existing, `IBlobStorageService.cs:158`).
- Produces: `BggCoverDownloader.DownloadAndUploadAsync(int bggId, string remoteImageUrl, CancellationToken ct) → Task<string?>` now returns the pipeline's full raw key (e.g. `bgg-covers/13/cover.jpg`) instead of `bgg-cover-{bggId}`. The value persisted to `SharedGameEntity.BggCoverR2Key` by `CreateSharedGameFromPdfCommandHandler.cs:174` therefore changes shape — the resolver is updated in the same task to match.

- [ ] **Step 1: Update the resolver test for the new L2.5 raw-key call**

In `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolverTests.cs`, replace the body of `ResolvePublicAsync_EmitsR2BggMetric_WhenL25BggCoverWins` (currently lines 216-230, using `GetPresignedDownloadUrlAsync`) with:

```csharp
    [Fact]
    public async Task ResolvePublicAsync_EmitsR2BggMetric_WhenL25BggCoverWins()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity { BggCoverR2Key = "bgg-covers/13/cover.jpg" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("bgg-covers/13/cover.jpg", null))
             .ReturnsAsync("https://r2/bgg.jpg");

        await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.cover.resolution.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["source"] as string, "r2_bgg", StringComparison.Ordinal));
    }
```

Then add a new regression test directly below it:

```csharp
    [Fact]
    public async Task ResolvePublicAsync_L25BggSlashKey_ResolvesViaRawKeyMethodNoSuffix()
    {
        // Issue #2947: BGG DB key is the FULL physical key (contains '/' and a
        // dot-extension) and the resolver passes it verbatim to
        // GetPresignedUrlForRawKeyAsync with NO appended suffix. The legacy
        // GetPresignedDownloadUrlAsync path (PathSecurity.ValidateIdentifier)
        // would have rejected the '/' and '.'.
        var sg = new SharedGameEntity { BggCoverR2Key = "bgg-covers/42/cover.png" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("bgg-covers/42/cover.png", null))
             .ReturnsAsync("https://r2/bgg-42.png");

        var url = await CoverUrlResolver.ResolvePublicAsync(sg, _blob.Object);

        url.Should().Be("https://r2/bgg-42.png");
        _blob.Verify(b => b.GetPresignedDownloadUrlAsync(
            It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }
```

- [ ] **Step 2: Run resolver test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CoverUrlResolverTests.ResolvePublicAsync_L25BggSlashKey_ResolvesViaRawKeyMethodNoSuffix"`
Expected: FAIL — the resolver still calls `GetPresignedDownloadUrlAsync` (the `GetPresignedUrlForRawKeyAsync` setup is never hit) so `url` is `null` and the `GetPresignedDownloadUrlAsync ... Times.Never` verify fails.

- [ ] **Step 3: Update the resolver L2.5 branch**

In `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs`, replace lines 91-110 (the L2.5 BGG block) with:

```csharp
        // L2.5 BGG re-uploaded cover (Gap G2 / Issue #2947)
        // The DB key is the FULL deterministic physical object key composed by
        // BggCoverUploadPipeline (bgg-covers/{bggId}/cover{ext}). Unlike L4/L2,
        // NO suffix is appended: BGG keeps its original image extension, so the
        // stored key IS the physical key. Resolved via the raw-key method (the
        // legacy GetPresignedDownloadUrlAsync validated the key with
        // PathSecurity.ValidateIdentifier, which rejects '/' and '.').
        if (!string.IsNullOrWhiteSpace(sharedGame.BggCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedUrlForRawKeyAsync(sharedGame.BggCoverR2Key)
                .ConfigureAwait(false);
            if (url is not null)
            {
                EmitResolution("r2_bgg");
                return url;
            }
        }
```

Also update the class-level XML doc: in the `<summary>` block (`CoverUrlResolver.cs:22-26`), replace the sentence beginning "L2.5 (BGG) is intentionally UNCHANGED..." through "...pending a follow-up." with:

```csharp
/// L2.5 (BGG) now resolves via <see cref="IBlobStorageService.GetPresignedUrlForRawKeyAsync"/>
/// using the full deterministic key written by
/// <c>BggCoverUploadPipeline</c> (Issue #2947): <c>bgg-covers/{bggId}/cover{ext}</c>,
/// with no suffix appended (BGG keeps its original image extension).
```

- [ ] **Step 4: Run resolver tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CoverUrlResolverTests"`
Expected: PASS (all resolver tests, including the updated BGG metric test + the new slash-key regression test).

- [ ] **Step 5: Update the BggCoverDownloader test to mock the pipeline**

Replace `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs` in full:

```csharp
using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

public sealed class BggCoverDownloaderTests
{
    private readonly Mock<IBggCoverUploadPipeline> _pipelineMock = new();
    private readonly Mock<ILogger<BggCoverDownloader>> _loggerMock = new();

    // RFC 5737 TEST-NET-3 literal keeps the SSRF DNS check offline/deterministic.
    private const string PublicHttpsUrl = "https://203.0.113.10/abc.jpg";

    [Fact]
    public async Task DownloadAndUploadAsync_OnSuccess_ReturnsPipelineKey()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        _pipelineMock
            .Setup(p => p.UploadAsync(13, It.IsAny<byte[]>(), ".jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bgg-covers/13/cover.jpg");

        var sut = new BggCoverDownloader(httpClient, _pipelineMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        result.Should().Be("bgg-covers/13/cover.jpg");
        _pipelineMock.Verify(p => p.UploadAsync(13, It.IsAny<byte[]>(), ".jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_PassesUrlExtensionToPipeline()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x01 });
        _pipelineMock
            .Setup(p => p.UploadAsync(99, It.IsAny<byte[]>(), ".png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("bgg-covers/99/cover.png");

        var sut = new BggCoverDownloader(httpClient, _pipelineMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(99, "https://203.0.113.10/image.PNG", CancellationToken.None);

        result.Should().Be("bgg-covers/99/cover.png");
        _pipelineMock.Verify(p => p.UploadAsync(99, It.IsAny<byte[]>(), ".png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnHttpError_ReturnsNull()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.NotFound);
        var sut = new BggCoverDownloader(httpClient, _pipelineMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        result.Should().BeNull();
        _pipelineMock.Verify(p => p.UploadAsync(
            It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnPipelineThrows_ReturnsNull()
    {
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x01 });
        _pipelineMock
            .Setup(p => p.UploadAsync(It.IsAny<int>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Amazon.S3.AmazonS3Exception("S3 unavailable"));

        var sut = new BggCoverDownloader(httpClient, _pipelineMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, PublicHttpsUrl, CancellationToken.None);

        result.Should().BeNull();
    }

    // ---- SSRF guard (#2655 finding #10) — unchanged behaviour ----

    [Theory]
    [InlineData("http://203.0.113.10/abc.jpg")]
    [InlineData("ftp://203.0.113.10/abc.jpg")]
    public async Task DownloadAndUploadAsync_NonHttpsUrl_BlockedWithoutFetching(string url)
    {
        var handler = TrackingHandler(HttpStatusCode.OK, new byte[] { 0x01 });
        var sut = new BggCoverDownloader(new HttpClient(handler.Object), _pipelineMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, url, CancellationToken.None);

        result.Should().BeNull("a non-HTTPS URL must be blocked by the SSRF guard");
        VerifyNoHttpCall(handler);
    }

    [Theory]
    [InlineData("https://127.0.0.1/abc.jpg")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("https://10.0.0.5/abc.jpg")]
    public async Task DownloadAndUploadAsync_PrivateOrReservedIp_BlockedWithoutFetching(string url)
    {
        var handler = TrackingHandler(HttpStatusCode.OK, new byte[] { 0x01 });
        var sut = new BggCoverDownloader(new HttpClient(handler.Object), _pipelineMock.Object, _loggerMock.Object);

        var result = await sut.DownloadAndUploadAsync(13, url, CancellationToken.None);

        result.Should().BeNull("a URL resolving to a private/reserved IP must be blocked by the SSRF guard");
        VerifyNoHttpCall(handler);
    }

    private static HttpClient BuildHttpClient(HttpStatusCode statusCode, byte[]? content = null)
        => new(TrackingHandler(statusCode, content).Object);

    private static Mock<HttpMessageHandler> TrackingHandler(HttpStatusCode statusCode, byte[]? content = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = content is null ? null : new ByteArrayContent(content)
            });
        return handler;
    }

    private static void VerifyNoHttpCall(Mock<HttpMessageHandler> handler)
        => handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
}
```

- [ ] **Step 6: Run downloader test to verify it fails (does not compile)**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BggCoverDownloaderTests"`
Expected: BUILD FAIL — `BggCoverDownloader` constructor still takes `IBlobStorageService`, not `IBggCoverUploadPipeline`.

- [ ] **Step 7: Rewire `BggCoverDownloader`**

Replace `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs` in full:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal sealed class BggCoverDownloader : IBggCoverDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IBggCoverUploadPipeline _uploadPipeline;
    private readonly ILogger<BggCoverDownloader> _logger;

    public BggCoverDownloader(
        HttpClient httpClient,
        IBggCoverUploadPipeline uploadPipeline,
        ILogger<BggCoverDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _uploadPipeline = uploadPipeline ?? throw new ArgumentNullException(nameof(uploadPipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> DownloadAndUploadAsync(
        int bggId,
        string remoteImageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteImageUrl))
        {
            return null;
        }

        // SSRF guard (#2655 finding #10): only fetch HTTPS URLs that resolve to public IPs.
        // Fails closed — an invalid scheme or a private/reserved target aborts the download.
        try
        {
            SsrfSafeHttpClient.ValidateUrlScheme(remoteImageUrl);
            await SsrfSafeHttpClient.ValidateResolvedIpAsync(remoteImageUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "BGG cover download blocked by SSRF guard: BggId={BggId}, Url={Url}",
                bggId, remoteImageUrl);
            return null;
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(remoteImageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BGG cover download failed: BggId={BggId}, Url={Url}, Status={Status}",
                    bggId, remoteImageUrl, response.StatusCode);
                return null;
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (imageBytes.Length == 0)
            {
                _logger.LogWarning("BGG cover download returned empty body: BggId={BggId}", bggId);
                return null;
            }

            var extension = GetExtension(remoteImageUrl);

            // Issue #2947: raw R2 PutObject to a deterministic key so
            // CoverUrlResolver can reconstruct it via GetPresignedUrlForRawKeyAsync.
            var r2Key = await _uploadPipeline
                .UploadAsync(bggId, imageBytes, extension, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "BGG cover uploaded successfully: BggId={BggId}, R2Key={Key}",
                bggId, r2Key);
            return r2Key;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error in BGG cover download/upload: BggId={BggId}", bggId);
            return null;
        }
    }

    private static string GetExtension(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) || ext.Length > 5 ? ".jpg" : ext.ToLowerInvariant();
        }
        catch
        {
            return ".jpg";
        }
    }
}
```

- [ ] **Step 8: Run downloader tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BggCoverDownloaderTests"`
Expected: PASS (6 tests: success, extension-passthrough, http-error, pipeline-throws, 2 SSRF theories).

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolverTests.cs
git commit -m "refactor(catalog): BGG L2.5 writes deterministic key + resolver reconstructs via raw-key lookup"
```

---

### Task 3: Register `IBggCoverUploadPipeline` in DI + wire into the typed `BggCoverDownloader`

Without this, the app fails at startup resolving `BggCoverDownloader`'s new constructor dependency. Mirrors `RegisterCoverR2UploadPipeline` verbatim.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs:188-235` (add registration call) + a new private `RegisterBggCoverUploadPipeline` method near `RegisterCoverR2UploadPipeline` (`:300-339`).
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs` (add a DI-resolution smoke test in the SAME file — no new file).

**Interfaces:**
- Consumes: `IServiceCollection`, `IConfiguration` (`S3_ENDPOINT`/`S3_ACCESS_KEY`/`S3_SECRET_KEY`/`S3_BUCKET_NAME`/`S3_REGION`/`S3_FORCE_PATH_STYLE`), `Api.Services.Pdf.BlobStorageServiceFactory.StripUnsupportedR2Headers`, `Amazon.S3.AmazonS3Client`.
- Produces: `services.AddSingleton<IBggCoverUploadPipeline>(...)` resolvable from the DI container. The typed `AddHttpClient<IBggCoverDownloader, BggCoverDownloader>` already resolves `IBggCoverUploadPipeline` + `ILogger<BggCoverDownloader>` from DI automatically.

- [ ] **Step 1: Write the failing DI-resolution test**

Append to `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs` a new nested test class at the end of the file (after the closing brace of `BggCoverUploadPipelineTests`), so it lives in the same file but is independently discoverable:

```csharp

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggCoverUploadPipelineDiTests
{
    [Fact]
    public void AddSharedGameCatalogContext_RegistersBggCoverUploadPipeline()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["S3_ENDPOINT"] = "https://test.r2.cloudflarestorage.com",
                ["S3_ACCESS_KEY"] = "ak",
                ["S3_SECRET_KEY"] = "sk",
                ["S3_BUCKET_NAME"] = "bucket",
                ["S3_REGION"] = "auto",
            })
            .Build();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        // The pipeline factory resolves IConfiguration from DI
        // (sp.GetRequiredService<IConfiguration>()), so the built config MUST be
        // registered on the same collection — otherwise resolution throws
        // "No service for type 'IConfiguration'" instead of returning the pipeline.
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(config);
        Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection.SharedGameCatalogServiceExtensions
            .RegisterBggCoverUploadPipelineForTests(services);

        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetService<Api.BoundedContexts.SharedGameCatalog.Application.Services.IBggCoverUploadPipeline>();

        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<BggCoverUploadPipeline>();
    }
}
```

Add the `using Microsoft.Extensions.DependencyInjection;` import at the top of the test file (needed for `GetService`/`BuildServiceProvider`).

- [ ] **Step 2: Run the DI test to verify it fails (does not compile)**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BggCoverUploadPipelineDiTests"`
Expected: BUILD FAIL — `RegisterBggCoverUploadPipelineForTests` does not exist.

- [ ] **Step 3: Add the registration method + call it, and expose a test seam**

In `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs`, add the registration call immediately after the existing `RegisterCoverR2UploadPipeline(services);` line (currently `:235`):

```csharp
        // Issue #2947 — BGG cover R2 upload pipeline (deterministic key). Same
        // S3_* config + lifetime as RegisterCoverR2UploadPipeline. Register both
        // the interface and the concrete type (CLAUDE.md pitfall #2565).
        RegisterBggCoverUploadPipeline(services);
```

Then add, directly after the closing brace of `RegisterCoverR2UploadPipeline` (currently ends `:339`), two methods:

```csharp
    /// <summary>
    /// Issue #2947 — registers <see cref="IBggCoverUploadPipeline"/> as a
    /// singleton backed by a lazily-constructed <see cref="Amazon.S3.IAmazonS3"/>
    /// client. Mirrors <see cref="RegisterCoverR2UploadPipeline"/> (same S3_* env
    /// vars, same R2 header-strip hook, same singleton lifetime).
    /// </summary>
    private static void RegisterBggCoverUploadPipeline(IServiceCollection services)
    {
        services.AddSingleton<IBggCoverUploadPipeline>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<BggCoverUploadPipeline>>();
            return BuildBggCoverUploadPipeline(config, logger);
        });
    }

    /// <summary>
    /// Test seam (Issue #2947): registers only the BGG cover pipeline against a
    /// caller-supplied service collection so a DI-resolution unit test can verify
    /// the registration without spinning up the whole catalog context. The caller
    /// MUST have already registered an <see cref="IConfiguration"/> on the same
    /// collection (the singleton factory resolves it via
    /// <c>sp.GetRequiredService&lt;IConfiguration&gt;()</c>).
    /// </summary>
    internal static void RegisterBggCoverUploadPipelineForTests(IServiceCollection services)
    {
        services.AddLogging();
        RegisterBggCoverUploadPipeline(services);
    }

    private static BggCoverUploadPipeline BuildBggCoverUploadPipeline(
        IConfiguration config,
        ILogger<BggCoverUploadPipeline> logger)
    {
        var options = new S3StorageOptions
        {
            Endpoint = config["S3_ENDPOINT"] ?? throw new InvalidOperationException("S3_ENDPOINT is required for BggCoverUploadPipeline"),
            AccessKey = config["S3_ACCESS_KEY"] ?? throw new InvalidOperationException("S3_ACCESS_KEY is required for BggCoverUploadPipeline"),
            SecretKey = config["S3_SECRET_KEY"] ?? throw new InvalidOperationException("S3_SECRET_KEY is required for BggCoverUploadPipeline"),
            BucketName = config["S3_BUCKET_NAME"] ?? throw new InvalidOperationException("S3_BUCKET_NAME is required for BggCoverUploadPipeline"),
            Region = config["S3_REGION"] ?? "auto",
            ForcePathStyle = bool.TryParse(config["S3_FORCE_PATH_STYLE"], out var forcePathStyle) && forcePathStyle,
        };

        var s3Config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
        };

        if (!string.Equals(options.Region, "auto", StringComparison.Ordinal)
            && Amazon.RegionEndpoint.GetBySystemName(options.Region) != null)
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
        }

        var credentials = new Amazon.Runtime.BasicAWSCredentials(options.AccessKey, options.SecretKey);
        var s3Client = new Amazon.S3.AmazonS3Client(credentials, s3Config);

        // Issue #1357: R2 rejects x-amz-tagging-directive; strip it defensively.
        s3Client.BeforeRequestEvent += BlobStorageServiceFactory.StripUnsupportedR2Headers;

        return new BggCoverUploadPipeline(s3Client, options, logger);
    }
```

At the top of the file, ensure the namespace `Api.BoundedContexts.SharedGameCatalog.Application.Services` (for `IBggCoverUploadPipeline`) and `Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services` (for `BggCoverUploadPipeline`) are in scope. Check the existing `using` block; the file already lives in the `Infrastructure.DependencyInjection` namespace and references `CoverR2UploadPipeline` (Infrastructure.Services) + `BggCoverDownloader` (Application.Services), so both namespaces are already imported — verify with a quick read before adding; add whichever is missing.

- [ ] **Step 4: Run the DI test + the pipeline unit tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BggCoverUploadPipeline"`
Expected: PASS (10 pipeline unit tests + 1 DI-resolution test).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Services/BggCoverUploadPipelineTests.cs
git commit -m "chore(catalog): register IBggCoverUploadPipeline singleton in DI"
```

---

### Task 4: Rewire `BackfillPdfCoversJob` L4 to `IPdfCoverUploadPipeline`

Replaces the two non-deterministic `blob.StoreAsync` calls in the Generated branch with a single deterministic `IPdfCoverUploadPipeline.UploadAsync`, and persists the deterministic dbKey.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJob.cs:60-235`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJobTests.cs`

**Interfaces:**
- Consumes: `IPdfCoverUploadPipeline.UploadAsync(string dbKey, byte[] webpBytes, CancellationToken cancellationToken) → Task<string>` (existing, returns `dbKey` unchanged; writes physical `{dbKey}-preview.webp`). `IBlobStorageService.RetrieveAsync(...)` still used to LOAD the source PDF bytes (unchanged). `IPdfCoverExtractor.ExtractAsync(...)` unchanged.
- Produces: `BackfillPdfCoversJob.RunBatchAsync(MeepleAiDbContext db, IPdfCoverExtractor extractor, IBlobStorageService blob, IPdfCoverUploadPipeline coverUploadPipeline, IDomainEventCollector? eventCollector, CancellationToken ct)` — the internal batch runner gains a new `coverUploadPipeline` parameter (inserted after `blob`). `PdfDocument.CoverR2Key` is persisted as the deterministic `covers/pdf/{pdfId:D}/cover` (no suffix). `PdfCoverGeneratedEvent.coverR2Key` carries the SAME deterministic dbKey.

- [ ] **Step 1: Update the backfill test for the new pipeline param + deterministic key**

In `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJobTests.cs`:

Add a field + resolve import at the top of the class (after `private readonly Mock<IBlobStorageService> _blob = new();`, line 25):

```csharp
    private readonly Mock<IPdfCoverUploadPipeline> _coverPipeline = new();
```

Update `RunBatchAsync` call-sites: every `.RunBatchAsync(_db, _extractor.Object, _blob.Object, _eventCollector.Object, default)` becomes `.RunBatchAsync(_db, _extractor.Object, _blob.Object, _coverPipeline.Object, _eventCollector.Object, default)`. (9 call-sites: lines 78, 102, 115, 140, 173, 197, 221, 246, 276 in the pre-edit file — insert `_coverPipeline.Object` after `_blob.Object` at each. Line numbers shift as you edit; a global find/replace of the exact `_blob.Object, _eventCollector.Object, default` fragment is the reliable way to catch all 9.)

Replace the body of `RunBatchAsync_ExtractGenerated_UploadsBothSizesAndPersistsKeyAndEmitsEvent` (currently lines 124-158) with:

```csharp
    [Fact]
    public async Task RunBatchAsync_ExtractGenerated_UploadsPreviewViaPipelineAndPersistsDeterministicKeyAndEmitsEvent()
    {
        var sharedGameId = Guid.NewGuid();
        var pdf = SeedPdf(sharedGameId: sharedGameId);

        ConfigureBlobReturningStream();
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PdfCoverExtractionResult
                  {
                      Outcome = PdfCoverExtractionOutcome.Generated,
                      ThumbnailWebp = new byte[] { 1, 2, 3 },
                      PreviewWebp = new byte[] { 4, 5, 6, 7 },
                      SelectedPageIndex = 1,
                  });

        var expectedKey = $"covers/pdf/{pdf.Id:D}/cover";
        _coverPipeline
            .Setup(p => p.UploadAsync(expectedKey, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedKey);

        await CreateJob().RunBatchAsync(_db, _extractor.Object, _blob.Object, _coverPipeline.Object, _eventCollector.Object, default);

        // Only the PREVIEW size is uploaded (the resolver only reads -preview.webp).
        _coverPipeline.Verify(p => p.UploadAsync(
            expectedKey,
            It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 4, 5, 6, 7 })),
            It.IsAny<CancellationToken>()), Times.Once);
        // StoreAsync must NOT be used for the cover write anymore.
        _blob.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameImage, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var refreshed = _db.PdfDocuments.AsNoTracking().Single(p => p.Id == pdf.Id);
        refreshed.CoverGenerationStatus.Should().Be(nameof(PdfCoverGenerationStatus.Generated));
        refreshed.CoverR2Key.Should().Be(expectedKey);
        refreshed.CoverPageIndex.Should().Be(1);
        refreshed.CoverGenerationError.Should().BeNull();

        _collectedEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PdfCoverGeneratedEvent>()
            .Which.CoverR2Key.Should().Be(expectedKey);
    }
```

Update `RunBatchAsync_ExtractSkipped_SetsSkippedStatusAndNoUpload` (currently lines 160-182): change the `_blob.Verify(b => b.StoreAsync(...))` assertion to also verify the pipeline is never called — replace the two `_blob.Verify(...StoreAsync...)` lines (179-180) with:

```csharp
        _coverPipeline.Verify(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
```

Add `using System.Linq;` at the top of the test file if not already present (needed for `SequenceEqual`). Add `using Api.BoundedContexts.DocumentProcessing.Application.Services;` — already present (line 2).

- [ ] **Step 2: Run the backfill test to verify it fails (does not compile)**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BackfillPdfCoversJobTests"`
Expected: BUILD FAIL — `RunBatchAsync` has no overload taking `IPdfCoverUploadPipeline`.

- [ ] **Step 3: Rewire `BackfillPdfCoversJob`**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJob.cs`:

Add the `using` for the pipeline interface (top of file, after line 1 `using Api.BoundedContexts.DocumentProcessing.Application.Services;` — that namespace already covers `IPdfCoverUploadPipeline`, so no new using needed).

Replace `Execute` (lines 60-73) body's service resolution + `RunBatchAsync` call:

```csharp
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("BackfillPdfCoversJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var extractor = scope.ServiceProvider.GetRequiredService<IPdfCoverExtractor>();
        var blob = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        var coverUploadPipeline = scope.ServiceProvider.GetRequiredService<IPdfCoverUploadPipeline>();
        var eventCollector = scope.ServiceProvider.GetService<IDomainEventCollector>();

        await RunBatchAsync(db, extractor, blob, coverUploadPipeline, eventCollector, ct).ConfigureAwait(false);
    }
```

Update `RunBatchAsync` signature (line 79-84) + the `ProcessOneAsync` call (line 112):

```csharp
    internal async Task RunBatchAsync(
        MeepleAiDbContext db,
        IPdfCoverExtractor extractor,
        IBlobStorageService blob,
        IPdfCoverUploadPipeline coverUploadPipeline,
        IDomainEventCollector? eventCollector,
        CancellationToken ct)
    {
```

...and inside the `for` loop replace the `ProcessOneAsync(pdf, db, extractor, blob, eventCollector, ct)` call (line 112) with:

```csharp
            await ProcessOneAsync(pdf, db, extractor, blob, coverUploadPipeline, eventCollector, ct).ConfigureAwait(false);
```

Update `ProcessOneAsync` signature (lines 121-127) to add the pipeline param after `blob`:

```csharp
    private async Task ProcessOneAsync(
        PdfDocumentEntity pdf,
        MeepleAiDbContext db,
        IPdfCoverExtractor extractor,
        IBlobStorageService blob,
        IPdfCoverUploadPipeline coverUploadPipeline,
        IDomainEventCollector? eventCollector,
        CancellationToken ct)
    {
```

Replace the `case PdfCoverExtractionOutcome.Generated:` block (lines 147-177) with the deterministic-key + single preview upload:

```csharp
                case PdfCoverExtractionOutcome.Generated:
                    {
                        // Issue #2947: deterministic DB key; the pipeline writes the
                        // physical R2 object "{dbKey}-preview.webp" that the resolver
                        // reconstructs. Only the preview size is uploaded (the
                        // resolver never reads the thumbnail size).
                        var dbKey = $"covers/pdf/{pdf.Id:D}/cover";

                        var persistedKey = await coverUploadPipeline
                            .UploadAsync(dbKey, result.PreviewWebp!, ct)
                            .ConfigureAwait(false);

                        pdf.CoverR2Key = persistedKey;
                        pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Generated);
                        pdf.CoverPageIndex = result.SelectedPageIndex;
                        pdf.CoverGenerationError = null;

                        eventCollector?.Collect(new PdfCoverGeneratedEvent(
                            pdfDocumentId: pdf.Id,
                            sharedGameId: pdf.SharedGameId,
                            coverR2Key: persistedKey,
                            coverPageIndex: result.SelectedPageIndex ?? 0));

                        _logger.LogInformation(
                            "BackfillPdfCoversJob: cover generated for PDF {PdfId} from page {PageIndex} (dbKey={DbKey})",
                            pdf.Id, result.SelectedPageIndex, persistedKey);
                        break;
                    }
```

Finally, update the orphan-hint comment + `resourceKey` in the outer `catch` (lines 208-223). The old comment references `game-images/pdf-cover-{Id}/` (the `StoreAsync` layout). Replace the `resourceKey` computation and its log/error text (lines 216-223) with:

```csharp
            // Operator-recovery hint: when UploadAsync succeeded BUT the subsequent
            // SaveChangesAsync threw (transient DB error, RowVersion conflict, etc.),
            // R2 will contain an orphan preview under the deterministic key. The
            // entity ends up Failed without a CoverR2Key so cleanup can't reach it.
            // Embed the prospective physical key so operators can grep the bucket.
            var orphanPhysicalKey = $"covers/pdf/{pdf.Id:D}/cover-preview.webp";
            _logger.LogWarning(ex,
                "BackfillPdfCoversJob: unexpected error processing PDF {PdfId}; marking Failed. " +
                "Inspect R2 key {OrphanKey} for an orphan preview blob and clean up manually if present.",
                pdf.Id, orphanPhysicalKey);
            pdf.CoverGenerationStatus = nameof(PdfCoverGenerationStatus.Failed);
            var detail = ex.GetType().Name + ": orphan-check-key=" + orphanPhysicalKey;
            pdf.CoverGenerationError = detail.Length > 500 ? detail[..500] : detail;
```

(The existing test `RunBatchAsync_ExtractorThrows_MarksFailedAndContinuesNextItem` asserts `CoverGenerationError` contains `nameof(InvalidOperationException)` — still true — and contains `$"pdf-cover-{first.Id}"`. That substring changes: update that test's assertion in Step 1's file to `refreshedFirst.CoverGenerationError.Should().Contain($"covers/pdf/{first.Id:D}/cover-preview.webp");` — apply this edit now as part of the same test-file update if not already done in Step 1. Re-run confirms.)

Apply the `RunBatchAsync_ExtractorThrows...` assertion fix: in the test file, change line 228 `refreshedFirst.CoverGenerationError.Should().Contain($"pdf-cover-{first.Id}");` to:

```csharp
        refreshedFirst.CoverGenerationError.Should().Contain($"covers/pdf/{first.Id:D}/cover-preview.webp");
```

- [ ] **Step 4: Run the backfill tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BackfillPdfCoversJobTests"`
Expected: PASS (all backfill tests, including the rewritten Generated + Skipped + ExtractorThrows tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJob.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/BackfillPdfCoversJobTests.cs
git commit -m "refactor(pdf): backfill job writes deterministic cover key via IPdfCoverUploadPipeline"
```

---

### Task 5: Rewire `PdfProcessingPipelineService.ExtractCoverImageAsync` L4 to `IPdfCoverUploadPipeline`

Replaces the two non-deterministic `_blobStorageService.StoreAsync` calls in the pipeline's cover-store block with a single deterministic `IPdfCoverUploadPipeline.UploadAsync`.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:38-104` (ctor) + `:517-609` (`ExtractCoverImageAsync`)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs:178` (the `AddScoped<IPdfProcessingPipelineService, PdfProcessingPipelineService>()` auto-resolves the new ctor param from DI since `IPdfCoverUploadPipeline` is already registered at `:175` — verify only, likely no edit needed)
- Test: reuse existing `PdfProcessingPipelineService` unit tests (find the suite) — add ONE focused cover test if the existing suite doesn't already assert the store call; see Step 1.

**Interfaces:**
- Consumes: `IPdfCoverUploadPipeline.UploadAsync(string dbKey, byte[] webpBytes, CancellationToken cancellationToken) → Task<string>` (existing).
- Produces: `PdfProcessingPipelineService` gains an optional `IPdfCoverUploadPipeline? pdfCoverUploadPipeline = null` ctor param (LAST position, after `eventCollector`, so all existing positional test constructors keep compiling). `ExtractCoverImageAsync` persists `pdfDoc.CoverR2Key = "covers/pdf/{pdfDoc.Id:D}/cover"` and raises `PdfCoverGeneratedEvent` with that same key. When the pipeline is null (older unit-test constructors), cover generation is skipped exactly like when `_pdfCoverExtractor` is null.

- [ ] **Step 1: Find the existing pipeline test suite + write the failing cover test**

Locate the test class:
Run: `ls apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/ | grep -i PdfProcessingPipeline`

Open the primary suite (expected `PdfProcessingPipelineServiceTests.cs` or a `...CoverTests.cs` sibling). Add a new test that constructs the service WITH a mocked `IPdfCoverUploadPipeline` and asserts the deterministic key. Because the ctor is long, add a focused test in a NEW sibling file to avoid disturbing the large existing fixture:

Create `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineServiceCoverTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingPipelineServiceCoverTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfCoverExtractor> _coverExtractor = new();
    private readonly Mock<IPdfCoverUploadPipeline> _coverPipeline = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IDomainEventCollector> _eventCollector = new();
    private readonly List<IDomainEvent> _collected = new();

    public PdfProcessingPipelineServiceCoverTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"PdfPipelineCover_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _eventCollector.Setup(c => c.Collect(It.IsAny<IDomainEvent>()))
                       .Callback<IDomainEvent>(e => _collected.Add(e));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExtractCoverImageAsync_Generated_UploadsPreviewViaPipelineWithDeterministicKey()
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Extracting",
            CoverGenerationStatus = "Pending",
            SharedGameId = Guid.NewGuid(),
        };

        _blob.Setup(b => b.RetrieveAsync(It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        _coverExtractor.Setup(e => e.ExtractAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new PdfCoverExtractionResult
                       {
                           Outcome = PdfCoverExtractionOutcome.Generated,
                           ThumbnailWebp = new byte[] { 1 },
                           PreviewWebp = new byte[] { 9, 9, 9 },
                           SelectedPageIndex = 0,
                       });

        var expectedKey = $"covers/pdf/{pdf.Id:D}/cover";
        _coverPipeline.Setup(p => p.UploadAsync(expectedKey, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedKey);

        var sut = PdfProcessingPipelineServiceCoverTestFactory.Create(
            _db, _blob.Object, _coverExtractor.Object, _coverPipeline.Object, _eventCollector.Object);

        await sut.InvokeExtractCoverImageForTestAsync(pdf, "/tmp/rules.pdf", CancellationToken.None);

        _coverPipeline.Verify(p => p.UploadAsync(
            expectedKey,
            It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 9, 9, 9 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _blob.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.GameImage, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        pdf.CoverR2Key.Should().Be(expectedKey);
        pdf.CoverGenerationStatus.Should().Be("Generated");

        _collected.Should().ContainSingle()
            .Which.Should().BeOfType<PdfCoverGeneratedEvent>()
            .Which.CoverR2Key.Should().Be(expectedKey);
    }
}
```

This test needs a factory + a test seam to reach the private `ExtractCoverImageAsync`. To avoid weakening production visibility, expose the method for tests via an `internal` wrapper method on the service AND an `InternalsVisibleTo` (already present for `Api.Tests`). Add the factory + wrapper as part of Step 3 — but write the test first so it fails.

- [ ] **Step 2: Run the cover test to verify it fails (does not compile)**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PdfProcessingPipelineServiceCoverTests"`
Expected: BUILD FAIL — `PdfProcessingPipelineServiceCoverTestFactory` and `InvokeExtractCoverImageForTestAsync` do not exist, and the ctor has no `IPdfCoverUploadPipeline` param.

- [ ] **Step 3: Add the ctor param + rewire `ExtractCoverImageAsync` + add the test seam**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`:

Add the field after `_eventCollector` (line 57):

```csharp
    // Issue #2947: deterministic R2 cover writes. Optional so pre-#2947 unit-test
    // constructors compile; when null, cover generation is skipped like when
    // _pdfCoverExtractor is null.
    private readonly IPdfCoverUploadPipeline? _pdfCoverUploadPipeline;
```

Add the ctor parameter as the LAST positional param (after `IDomainEventCollector? eventCollector = null`, line 80) and assign it (after line 103):

```csharp
        IDomainEventCollector? eventCollector = null,
        IPdfCoverUploadPipeline? pdfCoverUploadPipeline = null)
```

```csharp
        _eventCollector = eventCollector;
        _pdfCoverUploadPipeline = pdfCoverUploadPipeline;
```

Update the `ExtractCoverImageAsync` guard (lines 522-526) to also require the pipeline:

```csharp
        if (_pdfCoverExtractor is null || _pdfCoverUploadPipeline is null)
        {
            // Cover services not registered (unit-test scenarios) — leave default Pending.
            return;
        }
```

Replace the `case PdfCoverExtractionOutcome.Generated:` block (lines 540-580) with:

```csharp
                case PdfCoverExtractionOutcome.Generated:
                    {
                        // Issue #2947: deterministic DB key; the pipeline writes the
                        // physical R2 object "{dbKey}-preview.webp" that the resolver
                        // reconstructs. Only the preview size is uploaded (the
                        // resolver never reads the thumbnail size).
                        var dbKey = $"covers/pdf/{pdfDoc.Id:D}/cover";

                        var persistedKey = await _pdfCoverUploadPipeline
                            .UploadAsync(dbKey, result.PreviewWebp!, cancellationToken)
                            .ConfigureAwait(false);

                        pdfDoc.CoverR2Key = persistedKey;
                        pdfDoc.CoverGenerationStatus = "Generated";
                        pdfDoc.CoverPageIndex = result.SelectedPageIndex;
                        pdfDoc.CoverGenerationError = null;

                        // Issue #1852 (Gap A): raise the propagation event so
                        // PdfCoverGeneratedEventHandler can populate SharedGame.PdfCoverR2Key.
                        _eventCollector?.Collect(new PdfCoverGeneratedEvent(
                            pdfDocumentId: pdfDoc.Id,
                            sharedGameId: pdfDoc.SharedGameId,
                            coverR2Key: persistedKey,
                            coverPageIndex: result.SelectedPageIndex ?? 0));

                        _logger.LogInformation(
                            "[PdfPipeline] Cover image generated for PDF {PdfId} from page {PageIndex} (dbKey={DbKey})",
                            pdfDoc.Id, result.SelectedPageIndex, persistedKey);
                        break;
                    }
```

Add an internal test-seam wrapper method at the end of the class (before the final closing brace) so the test can invoke the private method without reflection:

```csharp
    /// <summary>
    /// Issue #2947 test seam: exposes <see cref="ExtractCoverImageAsync"/> to the
    /// unit-test assembly (InternalsVisibleTo Api.Tests) so the deterministic
    /// cover-key behaviour can be asserted directly without driving the full
    /// ProcessAsync pipeline.
    /// </summary>
    internal Task InvokeExtractCoverImageForTestAsync(
        PdfDocumentEntity pdfDoc, string filePath, CancellationToken cancellationToken)
        => ExtractCoverImageAsync(pdfDoc, filePath, cancellationToken);
```

Add the test factory. Create `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineServiceCoverTestFactory.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Issue #2947 — builds a <see cref="PdfProcessingPipelineService"/> with only the
/// collaborators the cover path touches wired to real/mocked instances and all
/// other required constructor dependencies stubbed with permissive mocks. Keeps
/// the cover-focused test independent of the large main fixture.
/// </summary>
internal static class PdfProcessingPipelineServiceCoverTestFactory
{
    public static PdfProcessingPipelineService Create(
        MeepleAiDbContext db,
        IBlobStorageService blob,
        IPdfCoverExtractor coverExtractor,
        IPdfCoverUploadPipeline coverUploadPipeline,
        IDomainEventCollector eventCollector)
    {
        return new PdfProcessingPipelineService(
            db: db,
            pdfClaimService: Mock.Of<IPdfClaimService>(),
            pdfTextExtractor: Mock.Of<IPdfTextExtractor>(),
            tableExtractor: Mock.Of<IPdfTableExtractor>(),
            chunkingService: Mock.Of<ITextChunkingService>(),
            embeddingService: Mock.Of<IEmbeddingService>(),
            blobStorageService: blob,
            timeProvider: TimeProvider.System,
            logger: NullLogger<PdfProcessingPipelineService>.Instance,
            languageDetector: Mock.Of<ILanguageDetector>(),
            chunkTranslationService: Mock.Of<IChunkTranslationService>(),
            indexingPipeline: Mock.Of<IPdfIndexingPipeline>(),
            raptorIndexer: null,
            entityExtractor: null,
            vectorStore: null,
            featureFlagService: null,
            roleClassifier: null,
            pdfCoverExtractor: coverExtractor,
            eventCollector: eventCollector,
            pdfCoverUploadPipeline: coverUploadPipeline);
    }
}
```

> Note for the implementer: the exact interface names for the stubbed collaborators (`IPdfClaimService`, `IPdfTextExtractor`, `IPdfTableExtractor`, `ITextChunkingService`, `IEmbeddingService`, `ILanguageDetector`, `IChunkTranslationService`, `IPdfIndexingPipeline`) are read verbatim from the `PdfProcessingPipelineService` constructor (`PdfProcessingPipelineService.cs:61-80`). If any namespace `using` is missing when compiling the factory, add the one the compiler names — do not guess; the compiler error states the exact type and namespace.

- [ ] **Step 4: Run the cover test to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PdfProcessingPipelineServiceCoverTests"`
Expected: PASS (1 test). Then run the full pipeline suite to confirm no regression from the ctor change:
Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PdfProcessingPipelineService"`
Expected: PASS (existing suite + new cover test).

- [ ] **Step 5: Verify DI still resolves the pipeline service (no edit expected)**

Read `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs:178`. The `AddScoped<IPdfProcessingPipelineService, PdfProcessingPipelineService>()` uses constructor injection; `IPdfCoverUploadPipeline` is registered (`:175`) so the new optional param resolves automatically. No edit required — this step is a read-only confirmation. If the build's DI validation flags a missing dependency, only then add an explicit factory (it should not).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineServiceCoverTests.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineServiceCoverTestFactory.cs
git commit -m "refactor(pdf): pipeline cover-store writes deterministic key via IPdfCoverUploadPipeline"
```

---

### Task 6: Gated R2/MinIO end-to-end round-trip integration test

Adds the issue-mandated integration test, guarded so it runs where MinIO/R2-HTTPS is available and is inert locally (the documented `DisablePayloadSigning`-over-HTTP MinIO limitation). Proves the full write→resolve→GET loop for all three deterministic key shapes.

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/CoverR2ConventionIntegrationTests.cs`

**Interfaces:**
- Consumes: the existing Testcontainers/MinIO integration harness conventions (`[Trait("Category", "Integration")]` + a skip guard). `S3BlobStorageService.GetPresignedUrlForRawKeyAsync`, `BggCoverUploadPipeline.UploadAsync`, `PdfCoverUploadPipeline.UploadAsync`.
- Produces: an integration test class that is skipped locally and executes in the gated CI lane.

- [ ] **Step 1: Read one existing MinIO/S3 integration test to copy the exact harness + skip convention**

Run: `ls apps/api/tests/Api.Tests/Integration/ -R | grep -i -E "s3|minio|blob|storage"`
Open the closest match (e.g. `S3BlobStorageIntegrationTests.cs`) and copy: the Testcontainers MinIO container fixture, the `[Trait("Category", "Integration")]` attributes, and the exact skip mechanism used (the note in CLAUDE.md "Known Flaky Tests" references `S3BlobStorageIntegrationTests` with "11 skipped tests ... only require Docker"). Match that skip guard EXACTLY (same `Skip = "..."` string style or same `[Trait("Skip", ...)]` used by that file).

- [ ] **Step 2: Write the integration test**

Create `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/CoverR2ConventionIntegrationTests.cs`. The body must:
1. Spin up the MinIO Testcontainer + build an `IAmazonS3` client + `S3StorageOptions` pointing at it (copy from the harness read in Step 1).
2. Construct `S3BlobStorageService` (for `GetPresignedUrlForRawKeyAsync`), `BggCoverUploadPipeline`, and `PdfCoverUploadPipeline` against that client.
3. Three scenarios, each: upload via the pipeline → get the DB key → compute the physical key the resolver would request → call `GetPresignedUrlForRawKeyAsync` → HTTP-GET the URL → assert `200 OK` and body bytes equal the uploaded bytes.
   - BGG: `BggCoverUploadPipeline.UploadAsync(13, bytes, ".jpg", ct)` returns `bgg-covers/13/cover.jpg`; resolver requests `GetPresignedUrlForRawKeyAsync("bgg-covers/13/cover.jpg")` (no suffix).
   - PDF: `PdfCoverUploadPipeline.UploadAsync("covers/pdf/{guid:D}/cover", bytes, ct)` returns the dbKey; resolver requests `GetPresignedUrlForRawKeyAsync("covers/pdf/{guid:D}/cover-preview.webp")`.
   - Miss: `GetPresignedUrlForRawKeyAsync("bgg-covers/999/cover.jpg")` (never uploaded) returns `null` (fail-closed existence check).

Use the EXACT container/fixture/skip pattern from Step 1's file — do NOT invent a new harness. Structure (fill the harness specifics from Step 1):

```csharp
using Amazon.S3;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CoverR2ConventionIntegrationTests
    // : IClassFixture<MinioContainerFixture>  // <-- use the exact fixture type from Step 1
{
    // Copy the container/client/options setup from the harness read in Step 1.
    // Inject the fixture, expose `IAmazonS3 _s3`, `S3StorageOptions _options`,
    // and a helper to build S3BlobStorageService.

    [Fact] // apply the SAME skip guard as the existing S3 integration tests (Step 1)
    public async Task BggCover_Uploaded_ResolvesViaRawKeyNoSuffix_And200()
    {
        var bggPipeline = new BggCoverUploadPipeline(_s3, _options, NullLogger<BggCoverUploadPipeline>.Instance);
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };

        var dbKey = await bggPipeline.UploadAsync(13, bytes, ".jpg", CancellationToken.None);
        dbKey.Should().Be("bgg-covers/13/cover.jpg");

        var blob = BuildBlobService();
        var url = await blob.GetPresignedUrlForRawKeyAsync(dbKey);
        url.Should().NotBeNull();

        using var http = new HttpClient();
        using var resp = await http.GetAsync(url);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await resp.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(bytes);
    }

    [Fact] // same skip guard
    public async Task PdfCover_Uploaded_ResolvesViaPreviewSuffix_And200()
    {
        var pdfPipeline = new PdfCoverUploadPipeline(_s3, _options, NullLogger<PdfCoverUploadPipeline>.Instance);
        var id = Guid.NewGuid();
        var dbKey = $"covers/pdf/{id:D}/cover";
        var bytes = new byte[] { 0x52, 0x49, 0x46, 0x46 };

        var returned = await pdfPipeline.UploadAsync(dbKey, bytes, CancellationToken.None);
        returned.Should().Be(dbKey);

        var blob = BuildBlobService();
        var url = await blob.GetPresignedUrlForRawKeyAsync($"{dbKey}-preview.webp");
        url.Should().NotBeNull();

        using var http = new HttpClient();
        using var resp = await http.GetAsync(url);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await resp.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(bytes);
    }

    [Fact] // same skip guard
    public async Task MissingKey_ResolvesToNull_FailClosed()
    {
        var blob = BuildBlobService();
        var url = await blob.GetPresignedUrlForRawKeyAsync("bgg-covers/999/cover.jpg");
        url.Should().BeNull("fail-closed existence check must return null for a non-existent object");
    }

    // private S3BlobStorageService BuildBlobService() => ... (from Step 1 harness)
}
```

- [ ] **Step 3: Run the integration test to confirm it is discovered + skips locally**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CoverR2ConventionIntegrationTests"`
Expected: SKIPPED locally (3 tests skipped with the harness's skip reason), OR PASS if Docker/MinIO is available — either outcome is green (no failures). If it FAILS with a `DisablePayloadSigning`-over-HTTP error, the skip guard from Step 1 was not applied correctly — re-check against the existing `S3BlobStorageIntegrationTests` guard.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/SharedGameCatalog/CoverR2ConventionIntegrationTests.cs
git commit -m "test(catalog): gated MinIO end-to-end round-trip for deterministic cover keys"
```

---

### Task 7: Full-suite regression sweep for the touched contexts + doc update

Confirms no collateral breakage in the two bounded contexts and records the convention.

**Files:**
- Modify: `docs/for-developers/specs/2026-07-14-game-cover-configuration-design.md` (append a short "Issue #2947 — deterministic BGG/PDF cover keys" subsection documenting the three key shapes). If the file does not exist at that path, create `docs/for-developers/audits/2026-07-15-issue-2947-r2-cover-convention.md` instead with the same content.

**Interfaces:**
- Consumes: nothing new.
- Produces: green test suites for `SharedGameCatalog` + `DocumentProcessing`; a documented convention.

- [ ] **Step 1: Kill lingering testhost then run the SharedGameCatalog unit suite**

Run: `tasklist | grep testhost` → if any, `taskkill //PID <PID> //F`
Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=SharedGameCatalog&Category=Unit"`
Expected: PASS (0 failures). Fix any regression at its root cause (do NOT skip tests).

- [ ] **Step 2: Run the DocumentProcessing unit suite**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=DocumentProcessing&Category=Unit"`
Expected: PASS (0 failures).

- [ ] **Step 3: Document the convention**

Read the spec path. If `docs/for-developers/specs/2026-07-14-game-cover-configuration-design.md` exists, append this subsection at the end:

```markdown
## Issue #2947 — Deterministic R2 cover keys (BGG L2.5 + PDF L4)

All cover write-sites now compose a deterministic physical R2 key that
`CoverUrlResolver` reconstructs from the DB-persisted key via
`IBlobStorageService.GetPresignedUrlForRawKeyAsync` (the earlier
`IBlobStorageService.StoreAsync` path minted a random `Guid.NewGuid()` fileId
that could not be reconstructed).

| Layer | Writer | DB key (persisted) | Physical R2 object | Resolver read |
|-------|--------|--------------------|--------------------|---------------|
| L2 Wikidata | `CoverR2UploadPipeline` | `covers/{gameId}/cover` | `covers/{gameId}/cover.webp` | `{dbKey}.webp` |
| L2.5 BGG | `BggCoverUploadPipeline` | `bgg-covers/{bggId}/cover{ext}` | same as DB key | `{dbKey}` (no suffix) |
| L4 PDF (pipeline + backfill) | `PdfCoverUploadPipeline` | `covers/pdf/{pdfId:D}/cover` | `covers/pdf/{pdfId:D}/cover-preview.webp` | `{dbKey}-preview.webp` |

BGG is the only layer that does NOT append a suffix at read time: it keeps the
source image extension (BGG serves jpg/png, not webp), so the DB key IS the
physical key.
```

If the spec file is absent, create `docs/for-developers/audits/2026-07-15-issue-2947-r2-cover-convention.md` with a top-level `# Issue #2947 — Deterministic R2 cover keys` heading followed by the same table + prose.

- [ ] **Step 4: Commit**

```bash
git add docs/for-developers/specs/2026-07-14-game-cover-configuration-design.md
git commit -m "docs(catalog): document deterministic R2 cover-key convention (#2947)"
```

(If you created the audit file instead, `git add docs/for-developers/audits/2026-07-15-issue-2947-r2-cover-convention.md`.)

- [ ] **Step 5: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2947-r2-cover-convention
gh pr create --base main-dev --title "fix(catalog): unify deterministic R2 cover convention (BGG L2.5 + PDF L4) #2947" --body "$(cat <<'EOF'
## Summary
Resolves the P1 follow-up from #2943: all cover write-sites now compose deterministic R2 physical keys that `CoverUrlResolver` reconstructs from the DB-persisted key, so BGG (L2.5), PDF-pipeline (L4), and PDF-backfill (L4) covers resolve instead of falling through to placeholder.

- New `BggCoverUploadPipeline` (raw `IAmazonS3` PutObject → `bgg-covers/{bggId}/cover{ext}`), rewired `BggCoverDownloader`; resolver L2.5 now uses `GetPresignedUrlForRawKeyAsync`.
- `BackfillPdfCoversJob` + `PdfProcessingPipelineService` now write the single preview via `IPdfCoverUploadPipeline` to `covers/pdf/{pdfId:D}/cover-preview.webp`, persisting `covers/pdf/{pdfId:D}/cover`.
- Gated MinIO end-to-end round-trip test (skips locally per the documented `DisablePayloadSigning`-over-HTTP MinIO limit).

## Test plan
- `dotnet test --filter "BoundedContext=SharedGameCatalog&Category=Unit"` — green
- `dotnet test --filter "BoundedContext=DocumentProcessing&Category=Unit"` — green
- Integration round-trip runs in the gated MinIO/R2 lane.

Closes #2947

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review

**1. Spec coverage** (every scope item → a task):
- BGG write-site (`BggCoverDownloader.DownloadAndUploadAsync`, formerly `:72-87`) → **Task 1 + 2 + 3** (new pipeline, rewire, DI).
- Resolver L2.5 branch (`CoverUrlResolver.cs:97-110`) → **Task 2** (switched to `GetPresignedUrlForRawKeyAsync`).
- Backfill write-site (`BackfillPdfCoversJob.ProcessOneAsync`, formerly `:149-171`) → **Task 4**.
- Pipeline write-site (`PdfProcessingPipelineService`, formerly `:549-566`) → **Task 5**.
- `BggCoverDownloaderTests` + `CoverUrlResolverTests` updated → **Task 2** (explicitly rewritten with real code).
- Integration test (R2/MinIO end-to-end) → **Task 6** (gated, unit-first caveat honored).
- No new endpoint (CQRS untouched); no new entity/VO (DDD untouched). ✅ All scope items mapped.

**2. Placeholder scan:** No "TBD/TODO/handle edge cases/similar to Task N". Task 6's harness specifics are deferred to a READ of the existing `S3BlobStorageIntegrationTests` (an intentional "copy the exact existing convention" instruction, with a concrete structural skeleton provided) rather than an invented harness — this is a fidelity requirement, not a placeholder. Task 5's factory carries an explicit "read the ctor verbatim; add the using the compiler names" note rather than guessing namespaces. No `throw new NotImplementedException()` stubs (MA0025). No `// TODO(` comments (S1135) — used "// Follow-up:" style nowhere-needed. ✅

**3. Type consistency:**
- `IBggCoverUploadPipeline.UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct) → Task<string>` — identical in the interface (Task 1 Step 3), impl (Task 1 Step 4), downloader consumer (Task 2 Step 7), downloader tests (Task 2 Step 5), and DI build method (Task 3). ✅
- `IPdfCoverUploadPipeline.UploadAsync(string dbKey, byte[] webpBytes, CancellationToken cancellationToken) → Task<string>` — read verbatim from the existing interface; used identically in Task 4 + Task 5. ✅
- Deterministic keys are consistent everywhere: BGG `bgg-covers/{bggId}/cover{ext}`; PDF DB `covers/pdf/{pdfId:D}/cover`, physical `covers/pdf/{pdfId:D}/cover-preview.webp`. Resolver L4 appends `-preview.webp` (matches `PdfCoverUploadPipeline` write) and L2.5 appends nothing (matches `BggCoverUploadPipeline` returning the full key). ✅
- `RunBatchAsync` / `ProcessOneAsync` new `IPdfCoverUploadPipeline` param inserted after `blob` consistently in signature + all call-sites + all 8 test call-sites. ✅
- `PdfProcessingPipelineService` new ctor param is LAST + optional (`= null`), so existing positional constructors compile; guard skips cover work when null. ✅

**Cross-cutting caution documented:** Task 4/5 drop the thumbnail R2 write (only preview is uploaded) because the resolver only ever reads `-preview.webp`. This is called out in-code and in the docs table so a reviewer sees the intentional behaviour change (no orphan random-fileId thumbnail blob).
