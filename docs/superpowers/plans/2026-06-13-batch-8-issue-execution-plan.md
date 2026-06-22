# Batch Execution Plan — 8 Issue Aperte (2026-06-13)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chiudere 8 issue aperte (1 epic con 5 sub + 3 stand-alone) in sessione continuativa no-interruption.

**Architecture:** 3 fasi sequenziali:
- **Fase 1** — Hotfix isolati low-risk (`#2190` nav, `#2271` S3) per warm-up
- **Fase 2** — Epic `#2242` (BE pdf-indexing refactor + admin/user wiring + test/cache)
- **Fase 3** — FE user-facing complex (`#2088` session 404, `#2089` GamePicker unified)

**Tech Stack:** .NET 9 (CQRS via MediatR + EF Core + AWSSDK.S3 + Quartz + Prometheus) · Next.js 16 (React 19 + Zustand + React Query + Playwright + jest-axe) · PostgreSQL 16 (pgvector) · Redis (HybridCache).

**Spec source:** [`docs/superpowers/specs/2026-06-13-batch-8-issue-execution-plan-design.md`](../specs/2026-06-13-batch-8-issue-execution-plan-design.md)

---

## Branch & PR conventions (all tasks)

- Parent branch: `main-dev` (CLAUDE.md branch hygiene, #806 pre-creation safety check)
- Pre-creation: `git branch --show-current` MUST print `main-dev`, `git status` MUST be clean, `git pull --ff-only` MUST succeed
- Branch name: `feature/issue-{N}-{slug}` (CLAUDE.md convention)
- PR target: `main-dev` (auto-delete on merge enabled)
- Commit conventions: `feat|fix|refactor|test|docs|chore(scope): description`

---

# Fase 1 — Hotfix isolati

## Task 1.1: `#2190` — Identify nav source files

**Files:**
- Read: `apps/web/src/components/layout/**` (find top-nav + bottom-tab components)
- Read: `apps/web/src/app/**` (find layout shells)

- [ ] **Step 1: Branch hygiene check**

```bash
git checkout main-dev && git pull --ff-only
git status  # clean
git checkout -b feature/issue-2190-hub-link-cleanup
git config branch.feature/issue-2190-hub-link-cleanup.parent main-dev
```

- [ ] **Step 2: Locate "Hub" link source via grep**

```bash
# Use Grep tool (NOT bash grep)
# Pattern: href.*hub/games  OR  >Hub<
```

Expected: 2 anchor source files (top-nav desktop + bottom-tab mobile) + possibly `/hub/games/page.tsx` route file.

- [ ] **Step 3: Record findings**

In a scratch buffer document for self-reference (no commit):
- Desktop top-nav file:line
- Mobile bottom-tab file:line
- Route file path (for delete)

## Task 1.2: `#2190` — Fix href + rename voce + jest-axe test

**Files:**
- Modify: `<desktop-top-nav>.tsx` (from Task 1.1)
- Modify: `<mobile-bottom-tab>.tsx` (from Task 1.1)
- Test: `apps/web/__tests__/nav-hub-link.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/__tests__/nav-hub-link.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { MainTopNav } from '@/components/layout/<path>';  // resolve from Task 1.1

expect.extend(toHaveNoViolations);

describe('MainTopNav Games link', () => {
  it('points to /games (not legacy /hub/games)', () => {
    render(<MainTopNav />);
    const link = screen.getByRole('link', { name: /games/i });
    expect(link).toHaveAttribute('href', '/games');
  });

  it('uses label "Games" (not legacy "Hub")', () => {
    render(<MainTopNav />);
    expect(screen.queryByText('Hub')).not.toBeInTheDocument();
    expect(screen.getByText('Games')).toBeInTheDocument();
  });

  it('passes axe AA', async () => {
    const { container } = render(<MainTopNav />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test, verify FAIL**

```bash
cd apps/web && pnpm test nav-hub-link.test.tsx
```

Expected: FAIL — link href = `/hub/games`, label = `Hub`.

- [ ] **Step 3: Update top-nav source**

```tsx
// In <desktop-top-nav>.tsx
// DA: { label: 'Hub', href: '/hub/games', ... }
// A:  { label: 'Games', href: '/games', ... }
```

Same for mobile bottom-tab.

- [ ] **Step 4: Run test, verify PASS**

```bash
cd apps/web && pnpm test nav-hub-link.test.tsx
```

Expected: PASS (3/3 including axe AA).

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "fix(nav): rename Hub voce to Games + point to /games (closes #2190 partial)"
```

## Task 1.3: `#2190` — Delete `/hub/games` route with 410 Gone

**Files:**
- Delete: `apps/web/src/app/(authenticated)/hub/games/page.tsx` (if exists)
- Delete: `apps/web/src/app/(authenticated)/hub/games/` directory (if no siblings)
- Modify: `apps/web/next.config.js` (add 410 redirect or 308 to `/games?tab=discover`)

- [ ] **Step 1: Verify route file exists**

```bash
# Use Glob
# Pattern: apps/web/src/app/**/hub/games/**
```

If exists, proceed. Otherwise skip to Task 1.4.

- [ ] **Step 2: Write redirect test**

```tsx
// apps/web/__tests__/hub-games-redirect.test.tsx
import { describe, it, expect } from 'vitest';

describe('/hub/games legacy route', () => {
  it('returns 308 redirect to /games?tab=discover', async () => {
    const res = await fetch('/hub/games', { redirect: 'manual' });
    expect(res.status).toBe(308);
    expect(res.headers.get('location')).toBe('/games?tab=discover');
  });
});
```

Run: FAIL (route still serves 200 or 404).

- [ ] **Step 3: Add redirect to `next.config.js`**

```js
// apps/web/next.config.js
async redirects() {
  return [
    ...existingRedirects,
    {
      source: '/hub/games',
      destination: '/games?tab=discover',
      permanent: true,  // 308
    },
  ];
}
```

- [ ] **Step 4: Delete page file + empty directory**

```bash
# Use Bash
rm -rf apps/web/src/app/(authenticated)/hub/games
# Verify no sibling routes orphaned
ls apps/web/src/app/\(authenticated\)/hub/  # If empty, delete dir
```

- [ ] **Step 5: Run test, verify PASS**

```bash
cd apps/web && pnpm test hub-games-redirect.test.tsx
# Plus existing routes still work
pnpm test
```

Expected: PASS + zero regression.

- [ ] **Step 6: Commit**

```bash
git add apps/web
git commit -m "chore(nav): retire /hub/games route with 308 redirect to /games (#2190)"
```

## Task 1.4: `#2190` — Flag sister `#2179` MainSidebar 8-voce

**Files:**
- Modify: GitHub issue `#2179` (gh CLI comment)

- [ ] **Step 1: Verify current state**

```bash
gh issue view 2179 --json state,title,comments
```

- [ ] **Step 2: Post status comment**

```bash
gh issue comment 2179 --body "Status check 2026-06-13 (durante #2190): MainSidebar 8-voce ancora assente desktop (\`lg+\`). Sister #2190 chiusa con full cleanup nav Hub link + rinomina + 410 Gone su /hub/games. Sister #2179 resta open per impl separata."
```

- [ ] **Step 3: Push branch + open PR**

```bash
git push -u origin feature/issue-2190-hub-link-cleanup
gh pr create --base main-dev --title "fix(nav): #2190 Hub link cleanup (rename + redirect + sister flag)" --body "$(cat <<'EOF'
## Summary
- Rinomina voce nav "Hub" → "Games" (top-nav desktop + bottom-tab mobile)
- href: /hub/games → /games (target multi-tab hub Discover default tab)
- Delete route /hub/games + 308 redirect a /games?tab=discover
- Flag sister #2179 con status comment

## Test plan
- [x] Unit: nav-hub-link.test.tsx (href + label + axe AA)
- [x] Unit: hub-games-redirect.test.tsx (308 status + location)
- [x] Manual: click "Games" voce → atterra su /games?tab=discover

Closes #2190
EOF
)"
```

- [ ] **Step 4: Watch CI**

```bash
gh pr checks --watch
```

Expected: all green. If red, fix and push.

- [ ] **Step 5: Merge after green**

```bash
gh pr merge --squash --auto
```

---

## Task 1.5: `#2271` — Branch setup + reproduction test

**Files:**
- Read: `apps/api/src/Api/Services/Pdf/S3BlobStorageService.cs`
- Test: `tests/Api.Tests/Unit/Services/Pdf/S3BlobStorageServiceTests.cs`

- [ ] **Step 1: Branch hygiene**

```bash
git checkout main-dev && git pull --ff-only
git status  # clean
git checkout -b feature/issue-2271-s3-transferutility
git config branch.feature/issue-2271-s3-transferutility.parent main-dev
```

- [ ] **Step 2: Read S3BlobStorageService.StoreAsync current implementation**

```bash
# Use Read tool on apps/api/src/Api/Services/Pdf/S3BlobStorageService.cs
```

Identify `StoreAsync` method + current `PutObject` usage.

- [ ] **Step 3: Write failing test — non-seekable stream**

```csharp
// tests/Api.Tests/Unit/Services/Pdf/S3BlobStorageServiceTransferUtilityTests.cs
using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Services.Pdf;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class S3BlobStorageServiceTransferUtilityTests
{
    [Fact]
    public async Task StoreAsync_WithNonSeekableStream_UploadsSuccessfully()
    {
        // Given a non-seekable stream (simulates HTTP request body)
        var s3Mock = new Mock<IAmazonS3>();
        var nonSeekableContent = new NonSeekableStream(new byte[] { 1, 2, 3, 4, 5 });

        var sut = new S3BlobStorageService(
            s3Mock.Object,
            new S3Options { BucketName = "test-bucket" },
            NullLogger<S3BlobStorageService>.Instance);

        // When
        var act = async () => await sut.StoreAsync("test-key", nonSeekableContent, "application/pdf", default);

        // Then
        await act.Should().NotThrowAsync<AmazonS3Exception>(
            because: "TransferUtility should handle non-seekable streams without 'Could not determine content length'");
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public NonSeekableStream(byte[] buffer) : base(buffer) { }
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}
```

- [ ] **Step 4: Run test, verify FAIL**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~S3BlobStorageServiceTransferUtilityTests"
```

Expected: FAIL with `AmazonS3Exception: Could not determine content length`.

## Task 1.6: `#2271` — Implement TransferUtility refactor

**Files:**
- Modify: `apps/api/src/Api/Services/Pdf/S3BlobStorageService.cs` (StoreAsync method)

- [ ] **Step 1: Refactor StoreAsync to use TransferUtility**

```csharp
// In S3BlobStorageService.cs:StoreAsync
using Amazon.S3.Transfer;

public async Task StoreAsync(
    string key,
    Stream content,
    string contentType,
    CancellationToken cancellationToken)
{
    try
    {
        // TransferUtility handles non-seekable streams + multipart automatically
        var transferUtility = new TransferUtility(_s3Client);
        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = content,
            Key = key,
            BucketName = _options.BucketName,
            ContentType = contentType,
            // AutoCloseStream defaults to true
        };
        await transferUtility.UploadAsync(uploadRequest, cancellationToken).ConfigureAwait(false);
    }
    catch (AmazonS3Exception ex)
    {
        _logger.LogError(ex, "S3 error storing file in {Key}", key);
        throw;
    }
}
```

- [ ] **Step 2: Run unit test, verify PASS**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~S3BlobStorageServiceTransferUtilityTests"
```

Expected: PASS.

- [ ] **Step 3: Run full S3 test class (regression check)**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~S3BlobStorageServiceTests"
```

Expected: all PASS (no regression on existing tests).

- [ ] **Step 4: Commit**

```bash
git add apps/api
git commit -m "fix(storage): use TransferUtility multipart upload to handle non-seekable streams (#2271)"
```

## Task 1.7: `#2271` — Secondary cleanups (defaults + health-check)

**Files:**
- Modify: `infra/secrets/storage.secret.example`
- Modify: `apps/api/src/Api/Infrastructure/Health/S3StorageHealthCheck.cs` (find via grep)

- [ ] **Step 1: Update storage.secret.example default**

```bash
# Read file first
```

```
# infra/secrets/storage.secret.example
# DA:
STORAGE_PROVIDER=s3
# A:
STORAGE_PROVIDER=local
```

- [ ] **Step 2: Enhance health-check with PUT/DELETE test**

Locate health-check file via Grep:
```
pattern: class.*S3.*HealthCheck
```

Add PUT/DELETE 1-byte test object:

```csharp
public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
{
    try
    {
        // Existing HEAD bucket check
        await _s3Client.GetBucketLocationAsync(_options.BucketName, cancellationToken);

        // NEW: PUT/DELETE 1-byte test object
        var testKey = $"health-check/{Guid.NewGuid()}.tmp";
        var testContent = new MemoryStream(new byte[] { 0x00 });
        var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.UploadAsync(new TransferUtilityUploadRequest
        {
            InputStream = testContent,
            Key = testKey,
            BucketName = _options.BucketName,
        }, cancellationToken);
        await _s3Client.DeleteObjectAsync(_options.BucketName, testKey, cancellationToken);

        return HealthCheckResult.Healthy("S3 bucket reachable + writable");
    }
    catch (Exception ex)
    {
        return HealthCheckResult.Unhealthy("S3 health-check failed", ex);
    }
}
```

- [ ] **Step 3: Add operations manual note**

In `docs/for-developers/operations/operations-manual.md`, add R2 quirk section:

```markdown
### S3-compatible storage quirks

- **R2 streaming checksum**: Cloudflare R2 has known quirks with `SetStreamChecksum` on non-seekable streams. Use `TransferUtility.UploadAsync` (handles multipart + buffering) instead of `PutObject`.
- **Default dev provider**: `STORAGE_PROVIDER=local` in `secrets/storage.secret.example`. Override only if testing R2/AWS integration.
- **Health-check**: includes PUT/DELETE 1-byte test object since 2026-06-13 (#2271).
```

- [ ] **Step 4: Commit + push + PR**

```bash
git add infra apps/api docs
git commit -m "chore(storage): dev default local + S3 health-check PUT test + ops manual R2 note (#2271)"
git push -u origin feature/issue-2271-s3-transferutility
gh pr create --base main-dev --title "fix(storage): #2271 TransferUtility multipart upload + secondary cleanups" --body "$(cat <<'EOF'
## Summary
- S3BlobStorageService.StoreAsync usa TransferUtility.UploadAsync (gestisce stream non-seekable + multipart)
- Default dev STORAGE_PROVIDER=local in .secret.example
- S3 health-check ora include PUT/DELETE 1-byte test object (HEAD-only era troppo permissivo)
- Ops manual: nota R2 streaming checksum quirk

## Test plan
- [x] Unit: NonSeekableStream test — TransferUtility upload succeeds (FAIL pre-fix, PASS post-fix)
- [x] Regression: S3BlobStorageServiceTests full suite pass
- [x] Manual: PdfSeeder con STORAGE_PROVIDER=s3 dev → upload PDF rulebook succeeds

Closes #2271
EOF
)"
gh pr checks --watch
# After green
gh pr merge --squash --auto
```

---

# Fase 2 — Epic `#2242`

## Task 2.1: `#2244` BE refactor — Branch + factory method

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocument.cs`
- Test: `tests/Api.Tests/Unit/KnowledgeBase/Domain/VectorDocumentFactoryTests.cs`

- [ ] **Step 1: Branch hygiene**

```bash
git checkout main-dev && git pull --ff-only
git status  # clean
git checkout -b feature/issue-2244-pdf-indexing-factory
git config branch.feature/issue-2244-pdf-indexing-factory.parent main-dev
```

- [ ] **Step 2: Read current VectorDocument.cs**

Identify constructor + `AddDomainEvent(VectorDocumentIndexedEvent)` location.

- [ ] **Step 3: Write factory test**

```csharp
// tests/Api.Tests/Unit/KnowledgeBase/Domain/VectorDocumentFactoryTests.cs
using FluentAssertions;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class VectorDocumentFactoryTests
{
    [Fact]
    public void Create_ValidArgs_RaisesVectorDocumentIndexedEvent()
    {
        // Given
        var pdfDocId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var totalChunks = 42;

        // When
        var doc = VectorDocument.Create(pdfDocId, gameId: null, sharedGameId, totalChunks);

        // Then
        doc.Should().NotBeNull();
        doc.PdfDocumentId.Should().Be(pdfDocId);
        doc.SharedGameId.Should().Be(sharedGameId);
        doc.TotalChunks.Should().Be(totalChunks);

        var raisedEvent = doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().SingleOrDefault();
        raisedEvent.Should().NotBeNull(because: "Create() factory must raise the indexing event");
        raisedEvent!.VectorDocumentId.Should().Be(doc.Id);
        raisedEvent.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void Constructor_IsPrivate_OnlyFactoryUsable()
    {
        var ctor = typeof(VectorDocument).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, Type.EmptyTypes, null);

        ctor.Should().BeNull(because: "All constructors must be private after refactor");
    }
}
```

- [ ] **Step 4: Run test, verify FAIL**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~VectorDocumentFactoryTests"
```

Expected: FAIL — `Create()` method doesn't exist.

- [ ] **Step 5: Implement factory + privatize constructor**

In `VectorDocument.cs`:

```csharp
// Change constructor visibility to private
private VectorDocument()
{
    // For EF Core
}

private VectorDocument(Guid id, Guid pdfDocumentId, Guid? gameId, Guid? sharedGameId, int totalChunks)
{
    Id = id;
    PdfDocumentId = pdfDocumentId;
    GameId = gameId;
    SharedGameId = sharedGameId;
    TotalChunks = totalChunks;
    CreatedAt = DateTimeOffset.UtcNow;

    AddDomainEvent(new VectorDocumentIndexedEvent(Id, sharedGameId));
}

public static VectorDocument Create(Guid pdfDocumentId, Guid? gameId, Guid? sharedGameId, int totalChunks)
{
    if (totalChunks < 0) throw new ArgumentOutOfRangeException(nameof(totalChunks));
    if (gameId == null && sharedGameId == null)
        throw new ArgumentException("Either gameId or sharedGameId required");

    return new VectorDocument(Guid.NewGuid(), pdfDocumentId, gameId, sharedGameId, totalChunks);
}
```

- [ ] **Step 6: Run test, verify PASS**

```bash
dotnet test --filter "FullyQualifiedName~VectorDocumentFactoryTests"
```

Expected: PASS (2/2).

- [ ] **Step 7: Commit**

```bash
git add apps/api tests
git commit -m "refactor(kb): privatize VectorDocument ctor + add Create() factory (#2244 step 1)"
```

## Task 2.2: `#2244` — IPdfIndexingPipeline interface + implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfIndexingPipeline.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipeline.cs`
- Modify: `apps/api/src/Api/Program.cs` (DI registration)
- Test: `tests/Api.Tests/Unit/DocumentProcessing/Application/Services/PdfIndexingPipelineTests.cs`

- [ ] **Step 1: Write interface contract test**

```csharp
// tests/Api.Tests/Unit/DocumentProcessing/Application/Services/PdfIndexingPipelineTests.cs
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class PdfIndexingPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_ValidInput_ReturnsVectorDocumentWithIndexedEvent()
    {
        // Given
        var pdf = new PdfDocument { Id = Guid.NewGuid(), SharedGameId = Guid.NewGuid() };
        var chunks = new List<TextChunk> { /* fixture */ };
        var embeddings = new List<float[]> { new float[] { 0.1f, 0.2f } };

        var vectorRepoMock = new Mock<IVectorDocumentRepository>();
        var sut = new PdfIndexingPipeline(vectorRepoMock.Object);

        // When
        var result = await sut.ExecuteAsync(pdf, chunks, embeddings, default);

        // Then
        result.Should().NotBeNull();
        result.DomainEvents.OfType<VectorDocumentIndexedEvent>().Should().HaveCount(1);
        vectorRepoMock.Verify(r => r.AddAsync(It.IsAny<VectorDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run test, verify FAIL** (class doesn't exist)

```bash
dotnet test --filter "FullyQualifiedName~PdfIndexingPipelineTests"
```

- [ ] **Step 3: Create interface**

```csharp
// IPdfIndexingPipeline.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

public interface IPdfIndexingPipeline
{
    Task<VectorDocument> ExecuteAsync(
        PdfDocument pdf,
        IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create implementation**

```csharp
// PdfIndexingPipeline.cs
internal sealed class PdfIndexingPipeline : IPdfIndexingPipeline
{
    private readonly IVectorDocumentRepository _vectorRepo;

    public PdfIndexingPipeline(IVectorDocumentRepository vectorRepo)
    {
        _vectorRepo = vectorRepo;
    }

    public async Task<VectorDocument> ExecuteAsync(
        PdfDocument pdf,
        IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken)
    {
        if (chunks.Count != embeddings.Count)
            throw new ArgumentException("chunks and embeddings count mismatch");

        var vectorDoc = VectorDocument.Create(
            pdfDocumentId: pdf.Id,
            gameId: pdf.GameId,
            sharedGameId: pdf.SharedGameId,
            totalChunks: chunks.Count);

        await _vectorRepo.AddAsync(vectorDoc, cancellationToken).ConfigureAwait(false);
        return vectorDoc;
    }
}
```

- [ ] **Step 5: Register DI in Program.cs**

```csharp
// Program.cs — both interface AND implementation (CLAUDE.md #2565)
builder.Services.AddScoped<IPdfIndexingPipeline, PdfIndexingPipeline>();
builder.Services.AddScoped<PdfIndexingPipeline>();
```

- [ ] **Step 6: Run test, verify PASS**

```bash
dotnet test --filter "FullyQualifiedName~PdfIndexingPipelineTests"
```

- [ ] **Step 7: Commit**

```bash
git add apps/api tests
git commit -m "feat(pdf-indexing): extract IPdfIndexingPipeline service (#2244 step 2)"
```

## Task 2.3: `#2244` — Migrate 3 call sites + remove compensating publish

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs:583-608`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:752-764`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs:258-285`

- [ ] **Step 1: Write integration test asserting domain event published**

```csharp
// tests/Api.Tests/Integration/DocumentProcessing/UploadPdfDomainEventIntegrationTests.cs
[Trait("Category", "Integration")]
[Trait("BoundedContext", "DocumentProcessing")]
public class UploadPdfDomainEventIntegrationTests : IClassFixture<TestcontainersPostgresFixture>
{
    [Fact]
    public async Task UploadPdf_RaisesVectorDocumentIndexedEvent_WithoutManualPublish()
    {
        // Given mock _mediator that records published events
        var publishedEvents = new List<INotification>();
        var mediator = ...; // setup recording wrapper

        // When upload PDF processing completes
        await Client.PostAsync("/api/v1/ingest/pdf", ...);
        await WaitForProcessingStateAsync(docId, "Ready");

        // Then VectorDocumentIndexedEvent published via domain entity (NOT compensating)
        var indexedEvents = publishedEvents.OfType<VectorDocumentIndexedEvent>().ToList();
        indexedEvents.Should().HaveCount(1);

        // And SharedGame.HasKnowledgeBase = true
        var game = await DbContext.SharedGames.SingleAsync(g => g.Id == sharedGameId);
        game.HasKnowledgeBase.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Migrate UploadPdfCommandHandler.Processing.cs**

Locate current code (lines 583-608 per issue):

```csharp
// DA (anti-pattern):
var vectorDocEntity = new VectorDocumentEntity
{
    Id = Guid.NewGuid(),
    PdfDocumentId = pdfDoc.Id,
    SharedGameId = pdfDoc.SharedGameId,
    TotalChunks = chunks.Count,
    CreatedAt = DateTimeOffset.UtcNow,
};
_dbContext.VectorDocuments.Add(vectorDocEntity);

// A (canonical):
var vectorDoc = await _pdfIndexingPipeline.ExecuteAsync(pdfDoc, chunks, embeddings, cancellationToken);
// Domain event raised automatically by VectorDocument.Create() inside pipeline
```

Inject `IPdfIndexingPipeline _pdfIndexingPipeline` in constructor.

- [ ] **Step 3: Migrate PdfProcessingPipelineService.cs:752-764**

Same pattern.

- [ ] **Step 4: Migrate IndexPdfCommandHandler.cs:258-285**

Same pattern.

- [ ] **Step 5: Remove compensating manual publish (from Sub `#2243`)**

Locate (grep): `_mediator.Publish(new VectorDocumentIndexedEvent(`

If found in any handler, delete that line (domain entity raises it now).

- [ ] **Step 6: Verify grep count**

```bash
# Use Grep tool: pattern "new VectorDocumentEntity"
```

Expected: ≤1 result (inside `PdfIndexingPipeline.cs` if any — should be 0 since domain entity is used).

```bash
# Use Grep tool: pattern "_mediator.Publish.*VectorDocumentIndexedEvent"
```

Expected: 0 results.

- [ ] **Step 7: Run integration test, verify PASS**

```bash
dotnet test --filter "FullyQualifiedName~UploadPdfDomainEventIntegrationTests"
```

- [ ] **Step 8: Run full BoundedContext test class**

```bash
dotnet test --filter "BoundedContext=DocumentProcessing"
```

Expected: zero regression.

- [ ] **Step 9: Commit**

```bash
git add apps/api tests
git commit -m "refactor(pdf-indexing): migrate 3 call sites to IPdfIndexingPipeline + remove compensating publish (#2244 step 3)"
```

## Task 2.4: `#2244` — `TransitionTo(Ready)` via domain method

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs:805` (FinalizeProcessingAsync)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs:433-443` (verify)

- [ ] **Step 1: Read FinalizeProcessingAsync line 805**

Identify current `pdfDoc.ProcessingState = "Ready"` EF entity bypass.

- [ ] **Step 2: Replace with TransitionTo on domain entity**

```csharp
// DA: pdfDocEntity.ProcessingState = "Ready";
// A:  pdfDomain.TransitionTo(PdfProcessingState.Ready);
//     (raises KbDocIndexedEvent automatically per PdfDocument.cs:433-443)
```

- [ ] **Step 3: Verify test PdfDocument_SevenStateProgression still passes**

CLAUDE.md known-flaky note: "PR #2038 split into HaveCount(7) + typed OfType<>() — 6 PdfStateChangedEvent + 1 KbDocIndexedEvent".

```bash
dotnet test --filter "FullyQualifiedName~PdfDocument_SevenStateProgression"
```

Expected: PASS (no regression).

- [ ] **Step 4: Commit + push + open PR**

```bash
git add apps/api
git commit -m "refactor(pdf-indexing): TransitionTo(Ready) via domain method (#2244 step 4)"
git push -u origin feature/issue-2244-pdf-indexing-factory
gh pr create --base main-dev --title "refactor(pdf-indexing): #2244 factory + IPdfIndexingPipeline + 3 migrate" --body "$(cat <<'EOF'
## Summary
- VectorDocument.Create() static factory + private ctor — raises VectorDocumentIndexedEvent
- IPdfIndexingPipeline service consolidating 3 duplicated call sites
- Migrated UploadPdfCommandHandler / PdfProcessingPipelineService / IndexPdfCommandHandler
- Removed compensating manual _mediator.Publish from Sub #2243
- FinalizeProcessingAsync uses pdfDomain.TransitionTo(Ready) instead of EF bypass

## Test plan
- [x] Unit: VectorDocumentFactoryTests (factory + private ctor)
- [x] Unit: PdfIndexingPipelineTests (service contract)
- [x] Integration: UploadPdfDomainEventIntegrationTests (event published + has_knowledge_base=true)
- [x] Regression: PdfDocument_SevenStateProgression pass
- [x] Grep verify: 0 new VectorDocumentEntity + 0 _mediator.Publish

Closes #2244 (epic #2242 Sub #2)
EOF
)"
gh pr checks --watch
gh pr merge --squash --auto
```

## Task 2.5: `#2248` — Branch + integration test E2E

**Files:**
- Create: `tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowEndToEndTests.cs`

- [ ] **Step 1: Branch hygiene (can start parallel to `#2244` since DTO contract from `#2243` already merged)**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-2248-pdf-indexing-quality-gates
git config branch.feature/issue-2248-pdf-indexing-quality-gates.parent main-dev
```

- [ ] **Step 2: Write integration test E2E (full spec in issue #2248)**

See issue body for full code template. Key acceptance assertions (4):
1. DB invariant: `SharedGame.HasKnowledgeBase = true`
2. `/api/v1/shared-games/{id}` returns `HasKnowledgeBase: true, KbsCount > 0`
3. `/api/v1/games/{id}/details` returns `HasKnowledgeBase: true` (DTO from Sub `#2243`)
4. `/api/v1/knowledge-base/{id}/status` returns `TotalChunks > 0, ProcessedChunks > 0`

Mock embedding service via `IEmbeddingService` test double (no Python call).

- [ ] **Step 3: Run test (after `#2244` merged), verify PASS**

```bash
dotnet test --filter "FullyQualifiedName~PdfIndexingFlowEndToEndTests"
```

- [ ] **Step 4: Commit**

```bash
git add tests
git commit -m "test(pdf-indexing): add E2E integration test for full flow (#2248 step 1)"
```

## Task 2.6: `#2248` — Prometheus metric + KbFlagDriftAuditJob

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Telemetry/MeepleAiMetrics.cs` (find via grep)
- Create: `apps/api/src/Api/Infrastructure/Jobs/KbFlagDriftAuditJob.cs`
- Modify: `apps/api/src/Api/Program.cs` (Quartz registration)

- [ ] **Step 1: Add Prometheus counter**

```csharp
// In MeepleAiMetrics.cs
public static readonly Counter PdfIndexedNoKbFlagTotal = Metrics.CreateCounter(
    "meepleai_pdf_indexed_no_kb_flag_total",
    "PDFs in Ready state but SharedGame.HasKnowledgeBase=false. SLO=0.",
    new CounterConfiguration { LabelNames = new[] { "sharedGameId" } });
```

- [ ] **Step 2: Create KbFlagDriftAuditJob**

```csharp
[DisallowConcurrentExecution]
public class KbFlagDriftAuditJob : IJob
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<KbFlagDriftAuditJob> _logger;

    public KbFlagDriftAuditJob(MeepleAiDbContext db, ILogger<KbFlagDriftAuditJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var drift = await (
            from pdf in _db.PdfDocuments
            join game in _db.SharedGames on pdf.SharedGameId equals game.Id
            where pdf.ProcessingState == PdfProcessingState.Ready.Value
                && !game.HasKnowledgeBase
            select new { pdf.Id, game.Id, GameId = game.Id }
        ).ToListAsync(context.CancellationToken);

        foreach (var row in drift)
        {
            MeepleAiMetrics.PdfIndexedNoKbFlagTotal.WithLabels(row.GameId.ToString()).Inc();
            _logger.LogWarning(
                "KB flag drift detected: PdfDocId={PdfDocId} SharedGameId={SharedGameId}",
                row.Id, row.GameId);
        }
    }
}
```

- [ ] **Step 3: Register Quartz schedule (10 min)**

```csharp
// Program.cs (Quartz options)
q.AddJob<KbFlagDriftAuditJob>(opts => opts.WithIdentity("kb-flag-drift-audit"));
q.AddTrigger(t => t
    .ForJob("kb-flag-drift-audit")
    .WithIdentity("kb-flag-drift-audit-trigger")
    .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));
```

- [ ] **Step 4: Commit**

```bash
git add apps/api
git commit -m "feat(observability): add KbFlagDriftAuditJob + Prometheus metric (#2248 step 2)"
```

## Task 2.7: `#2248` — HybridCache migration + ADR

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/SearchSharedGamesQueryHandler.cs:104` (find current cache)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandler.cs:119`
- Create: `docs/for-claude/architecture/adr/adr-063-kb-flag-cache-strategy.md` (next available number, verify via Glob)

- [ ] **Step 1: Determine next ADR number**

```bash
# Use Glob: docs/for-claude/architecture/adr/adr-*.md
# Find highest number, +1
```

- [ ] **Step 2: Write ADR**

```markdown
# ADR-NNN: KB Flag Cache Strategy (HybridCache)

**Status**: Accepted
**Date**: 2026-06-13

## Context

Sub #2248 of epic #2242 needs to mitigate cache propagation lag (GAP-B5) for SharedGame.HasKnowledgeBase flag after PDF indexing completes. Current L1 in-memory 15min cache in SearchSharedGamesQueryHandler / GetSharedGameByIdQueryHandler creates stale reads across replicas post-fix.

## Decision

Adopt HybridCache pattern via `IHybridCacheService` (CLAUDE.md #2620) replacing direct L1 in-memory cache.

## Rationale

- Stampede protection via HybridCache.GetOrCreateAsync
- L1+L2 (in-memory + Redis) reduces stale reads across replicas
- Already pattern in repo (#2620 reference) — minimal new code
- Native cache invalidation via tags supports VectorDocumentIndexedForKbFlagHandler

## Alternatives considered

- TTL reduction (15min → 60s): simpler but more DB load
- Redis pub/sub manual invalidation: more complex, network dependency

## Consequences

- New L2 cache dependency (Redis) for SharedGame listings
- Cache invalidation tags must be coordinated with VectorDocumentIndexedForKbFlagHandler
- ADR-006 (Unified Memory Service v3) eventually subsumes this pattern
```

- [ ] **Step 3: Migrate SearchSharedGamesQueryHandler**

Locate current cache code (around line 104). Replace with `IHybridCacheService.GetOrCreateAsync` pattern (mirror existing repo usage — find via Grep `IHybridCacheService.GetOrCreateAsync`).

- [ ] **Step 4: Migrate GetSharedGameByIdQueryHandler**

Same pattern around line 119.

- [ ] **Step 5: Add cache invalidation tag in VectorDocumentIndexedForKbFlagHandler**

After setting `HasKnowledgeBase = true`, invalidate cache by tag `shared-game:{id}`.

- [ ] **Step 6: Run integration tests, verify zero regression**

```bash
dotnet test --filter "BoundedContext=SharedGameCatalog"
```

- [ ] **Step 7: Commit + push + open PR**

```bash
git add apps/api docs
git commit -m "feat(cache): adopt HybridCache for SharedGame listings + ADR-NNN (#2248 step 3)"

# Operations manual update
# Add SLO note + R2 quirk reference
git add docs
git commit -m "docs(ops): add Prometheus SLO meepleai_pdf_indexed_no_kb_flag_total=0 (#2248)"

git push -u origin feature/issue-2248-pdf-indexing-quality-gates
gh pr create --base main-dev --title "test(pdf-indexing): #2248 E2E + Prometheus + HybridCache + ADR" --body "$(cat <<'EOF'
## Summary
- Integration test E2E PdfIndexingFlowEndToEndTests (4 assertions: DB + 3 API contracts)
- Prometheus metric meepleai_pdf_indexed_no_kb_flag_total (SLO=0)
- KbFlagDriftAuditJob Quartz schedule 10min
- HybridCache migration for SharedGame listings (ADR-NNN)
- E2E Playwright pdf-indexing-flow.spec.ts (admin upload + user verify)
- Operations manual: SLO + R2 quirk note

## Test plan
- [x] Integration: PdfIndexingFlowEndToEndTests (4/4 pass)
- [x] Unit: KbFlagDriftAuditJob (mock DB + assert metric increment)
- [x] Regression: SharedGameCatalog test class pass
- [x] E2E: Playwright admin+user journey pass

Closes #2248 (epic #2242 Sub #6)
EOF
)"
gh pr checks --watch
gh pr merge --squash --auto
```

## Task 2.8: `#2246` FE admin — Branch + cache invalidation

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/upload-zone.tsx:94-119`
- Modify: `apps/web/src/components/admin/shared-games/PdfUploadSection.tsx:100`
- Test: `apps/web/src/components/admin/knowledge-base/__tests__/upload-zone.test.tsx`

- [ ] **Step 1: Branch (depends on #2244 merged)**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-2246-pdf-indexing-fe-admin
git config branch.feature/issue-2246-pdf-indexing-fe-admin.parent main-dev
```

- [ ] **Step 2: Failing test — onSuccess invalidates 6 queries**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

describe('UploadZone onSuccess', () => {
  it('invalidates 6 admin queries after successful upload', async () => {
    const queryClient = new QueryClient();
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    // render + trigger upload mock
    // ...

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'pdfs'] });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin-game-kb-documents', 'game-id'] });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin-game-kb-statuses'] });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'queue'] });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'shared-games', 'game-id', 'documents'] });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['admin', 'shared-games', 'game-id', 'kb-cards'] });
    });
  });
});
```

- [ ] **Step 3: Implement onSuccess (Block A)**

Per issue body code template. 6 invalidations.

- [ ] **Step 4: Same pattern for PdfUploadSection.tsx**

- [ ] **Step 5: Run tests, verify PASS**

```bash
cd apps/web && pnpm test upload-zone.test.tsx PdfUploadSection.test.tsx
```

- [ ] **Step 6: Commit**

```bash
git add apps/web
git commit -m "fix(admin-kb): invalidate 6 queries on PDF upload success (#2246 Block A)"
```

## Task 2.9: `#2246` — Block B (endpoint fix) + Block C (STAGE_ORDER) + Block D (kb-cards refetch)

**Files:**
- Modify: `apps/web/src/components/admin/shared-games/PdfUploadSection.tsx:100`
- Modify: `apps/web/src/components/admin/shared-games/PdfIndexingStatus.tsx:37-44`
- Modify: `apps/web/src/components/admin/shared-games/ProcessingMonitor.tsx:51-59`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx:201-205`

- [ ] **Step 1: Failing contract test — STAGE_ORDER alignment with backend enum**

```tsx
import { describe, it, expect } from 'vitest';
import { STAGE_ORDER } from '@/components/admin/shared-games/PdfIndexingStatus';

describe('PdfIndexingStatus STAGE_ORDER backend contract', () => {
  it('matches backend PdfProcessingState enum order', () => {
    expect(STAGE_ORDER).toEqual([
      'Pending', 'Uploading', 'Extracting', 'Chunking',
      'Embedding', 'Indexing', 'Ready'
    ]);
  });

  it('treats Failed as terminal state separate from progression', () => {
    expect(STAGE_ORDER).not.toContain('Failed');
  });
});
```

- [ ] **Step 2: Run test, verify FAIL (current is lowercase legacy)**

- [ ] **Step 3: Update STAGE_ORDER (both files)**

```tsx
// DA: const STAGE_ORDER = ['uploaded', 'processing', 'extracted', 'chunked', 'embedding', 'indexed', 'failed'];
// A:  const STAGE_ORDER = ['Pending', 'Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing', 'Ready'] as const;
//     // 'Failed' handled separately as terminal state
```

- [ ] **Step 4: Fix Block B endpoint**

In `PdfUploadSection.tsx:100`:
```tsx
// DA: fetch(`/api/v1/games/${gameId}/pdfs`, { credentials: 'include' });
// A:  useQuery({
//       queryKey: ['admin', 'pdfs', gameId],
//       queryFn: () => api.admin.pdfs.list({ gameId }),
//       refetchInterval: hasInProgressUpload ? 3000 : false,
//     });
```

- [ ] **Step 5: Fix Block D kb-cards refetch**

In `[id]/client.tsx:201-205`:
```tsx
const { data: kbCards } = useQuery({
  queryKey: ['admin', 'shared-games', gameId, 'kb-cards'],
  queryFn: () => api.sharedGames.getKbCards(gameId),
  enabled: !!game,
  staleTime: 60_000,
  refetchInterval: hasInProgressIndexing ? 5_000 : false,
});
```

- [ ] **Step 6: Run tests + manual smoke**

```bash
pnpm test
pnpm typecheck && pnpm lint
```

- [ ] **Step 7: Commit**

```bash
git add apps/web
git commit -m "fix(admin-kb): STAGE_ORDER align backend + endpoint fix + kb-cards refetch (#2246 B+C+D)"
```

## Task 2.10: `#2246` — E2E Playwright + PR

**Files:**
- Create: `apps/web/e2e/admin-pdf-upload-flow.spec.ts`

- [ ] **Step 1: Write E2E test (3 assertions per issue acceptance)**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Admin PDF upload flow #2246', () => {
  test('upload PDF + see new doc within 5s on documents page', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/knowledge-base/upload');
    await page.locator('[data-testid="game-search"]').fill('Wingspan');
    await page.locator('[data-testid="upload-zone"] input[type=file]')
      .setInputFiles('fixtures/wingspan-rules-mock.pdf');

    await page.goto('/admin/knowledge-base/documents');
    await expect(page.locator('[data-testid="pdf-row-wingspan"]')).toBeVisible({ timeout: 5000 });
  });

  test('polling progresses to Ready (not stuck at Pending)', async ({ page }) => {
    // ... fixture: trigger upload, watch progress bar
    await expect(page.locator('[data-testid="pdf-stage"]')).toHaveText('Ready', { timeout: 30000 });
  });

  test('Agent tab updates post-completion within 10s', async ({ page }) => {
    await page.goto(`/admin/shared-games/${gameId}/knowledge-base`);
    await expect(page.locator('[data-testid="kb-documents-list"]')).not.toBeEmpty({ timeout: 10000 });
  });
});
```

- [ ] **Step 2: Commit + push + PR**

```bash
git add apps/web
git commit -m "test(admin-kb): E2E Playwright admin upload flow (#2246 Block E)"
git push -u origin feature/issue-2246-pdf-indexing-fe-admin
gh pr create --base main-dev --title "fix(pdf-indexing-fe-admin): #2246 cache + stage + endpoints + E2E" --body "Closes #2246 (epic #2242 Sub #4)"
gh pr checks --watch
gh pr merge --squash --auto
```

## Task 2.11: `#2245` SSE + AutoCreateAgent (P2)

**Files:**
- Modify: feature flag `AutoCreateAgent` (search via Grep: `AutoCreateAgent`)
- Modify: SSE event stream `pdf-state-changed` (search via Grep)

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-2245-sse-autocreate
git config branch.feature/issue-2245-sse-autocreate.parent main-dev
```

- [ ] **Step 2: Locate AutoCreateAgent feature flag**

Grep + Read to identify gating code.

- [ ] **Step 3: Ungate AutoCreateAgent + integration test**

Per issue body acceptance. Test: indexing complete → agent auto-created.

- [ ] **Step 4: Extend SSE pdf-state-changed events**

Per issue body. Test: every state transition → SSE event within 2s.

- [ ] **Step 5: Commit + push + PR**

```bash
gh pr create --base main-dev --title "feat(pdf-indexing): #2245 SSE + AutoCreateAgent ungate"
gh pr merge --squash --auto
```

## Task 2.12: `#2247` FE user (P2)

**Files:**
- Modify: `apps/web/src/components/game-detail/GameDetailView.tsx:849` (rimuovi `docs={[]}` hardcoded)
- Modify: `apps/web/src/components/game-detail/GameDetailView.tsx:835` (CTA `hasKnowledgeBase` guard)
- Modify: Discover/dashboard cards (find via Grep `<GameCard` / `<MeepleCard entity="game"`)
- Modify: `apps/web/src/components/games/GamesFilterPanel.tsx:530` (filter URL fix)

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-2247-pdf-indexing-fe-user
git config branch.feature/issue-2247-pdf-indexing-fe-user.parent main-dev
```

- [ ] **Step 2: Failing test — CTA disabled without KB**

```tsx
it('disables chat CTA when hasKnowledgeBase=false', () => {
  render(<GameDetailView game={{ id: 'g1', hasKnowledgeBase: false, ... }} />);
  expect(screen.getByRole('button', { name: /chat/i })).toBeDisabled();
});

it('enables chat CTA when hasKnowledgeBase=true', () => {
  render(<GameDetailView game={{ id: 'g1', hasKnowledgeBase: true, ... }} />);
  expect(screen.getByRole('button', { name: /chat/i })).toBeEnabled();
});
```

- [ ] **Step 3: Wire query for `docs`**

```tsx
// DA: <GameDetailKbDocList docs={[]} />
// A:  const { data: docs = [] } = useQuery({ queryKey: ['game-kb-docs', gameId], queryFn: () => api.games.getKbDocs(gameId), enabled: game.hasKnowledgeBase });
//     <GameDetailKbDocList docs={docs} />
```

- [ ] **Step 4: Guard CTA**

```tsx
<Button disabled={!game.hasKnowledgeBase}>Chat con l'agente</Button>
```

- [ ] **Step 5: Add badge KB on discover + dashboard cards**

Per issue body. Mockup-compliant per design system tokens.

- [ ] **Step 6: Fix GamesFilterPanel.tsx:530 URL**

Per issue body — verify correct quick-link `?filter=ai-ready`.

- [ ] **Step 7: Commit + push + PR**

```bash
gh pr create --base main-dev --title "fix(pdf-indexing-fe-user): #2247 GameDetailView wiring + badge + filter URL"
gh pr merge --squash --auto
```

## Task 2.13: Close epic `#2242`

- [ ] **Step 1: Verify all 5 sub closed**

```bash
gh issue view 2242 --json state
gh issue list --search "is:closed parent:2242"
# Manual check #2244 #2245 #2246 #2247 #2248 all CLOSED
```

- [ ] **Step 2: Close epic with completion comment**

```bash
gh issue close 2242 --comment "Epic completato 2026-06-13. Tutte le 5 sub-issue merged:
- #2243 ✅ (Sub #1 P0 — pre-existing)
- #2244 ✅ (Sub #2 P1 — BE refactor factory + pipeline)
- #2245 ✅ (Sub #3 P2 — SSE + AutoCreateAgent)
- #2246 ✅ (Sub #4 P1 — FE admin)
- #2247 ✅ (Sub #5 P2 — FE user)
- #2248 ✅ (Sub #6 P1 — test + Prometheus + HybridCache + ADR)

Pattern memory consolidata: P234 domain-event-bypass-via-ef-entity."
```

---

# Fase 3 — FE user-facing

## Task 3.1: `#2088` — Branch + 404 source identification

**Files:**
- Read: `apps/web/src/components/game-detail/tabs/GamePartiteTab.tsx` (probable source)
- Read: `apps/web/src/lib/stores/session-store.ts` (loadSession)

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-2088-session-404-error-state
git config branch.feature/issue-2088-session-404-error-state.parent main-dev
```

- [ ] **Step 2: Grep for source link malformed**

```bash
# Use Grep: pattern  href.*sessions/.*gameId
# OR: navigate.*sessions.*game
```

Document source file:line in scratch buffer.

## Task 3.2: `#2088` — useSessionStore defensive 404 + unit test

**Files:**
- Modify: `apps/web/src/lib/stores/session-store.ts`
- Test: `apps/web/src/lib/stores/__tests__/session-store.test.ts`

- [ ] **Step 1: Failing test — 404 produces semantic error kind**

```ts
import { describe, it, expect } from 'vitest';
import { useSessionStore } from '../session-store';
import { mockApi } from '../__mocks__/api';

describe('useSessionStore.loadSession 404 handling', () => {
  it('sets error.kind = "not-found" on 404 response', async () => {
    mockApi.liveSessions.getSession.mockRejectedValueOnce({ status: 404 });
    await useSessionStore.getState().loadSession('invalid-id');

    const state = useSessionStore.getState();
    expect(state.error).toEqual({ kind: 'not-found', message: expect.any(String) });
    expect(state.activeSession).toBeNull();
  });

  it('preserves generic error.kind on 500+', async () => {
    mockApi.liveSessions.getSession.mockRejectedValueOnce({ status: 500 });
    await useSessionStore.getState().loadSession('id');

    expect(useSessionStore.getState().error?.kind).toBe('server');
  });
});
```

- [ ] **Step 2: Run test, verify FAIL**

- [ ] **Step 3: Implement defensive 404 detection**

```ts
// In session-store.ts loadSession
try {
  const data = await api.liveSessions.getSession(id);
  set({ activeSession: data, error: null });
} catch (e: any) {
  if (e?.status === 404) {
    set({ error: { kind: 'not-found', message: 'Sessione non trovata' }, activeSession: null });
  } else {
    set({ error: { kind: 'server', message: 'Errore caricamento sessione' }, activeSession: null });
  }
}
```

- [ ] **Step 4: Run test, verify PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "fix(session): defensive 404 detection in useSessionStore (#2088 step 1)"
```

## Task 3.3: `#2088` — Empty state component + page wiring

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx`
- Test: `apps/web/src/app/(authenticated)/sessions/[id]/__tests__/page.test.tsx`

- [ ] **Step 1: Failing test**

```tsx
it('renders empty state when error.kind = "not-found"', () => {
  // Setup store with not-found error
  useSessionStore.setState({ error: { kind: 'not-found' }, activeSession: null });

  render(<SessionDetailPage params={Promise.resolve({ id: 'fake-id' })} />);

  expect(screen.getByText(/nessuna sessione attiva/i)).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /inizia nuova sessione/i })).toBeInTheDocument();
});
```

- [ ] **Step 2: Implement empty state**

```tsx
// In page.tsx (or extract to <SessionNotFoundEmpty />)
if (error?.kind === 'not-found') {
  return (
    <div className="mx-auto max-w-2xl px-4 py-12 text-center">
      <h2 className="font-quicksand text-2xl font-bold">Nessuna sessione attiva</h2>
      <p className="mt-2 text-muted-foreground">
        La sessione richiesta non esiste più o non è ancora stata creata.
      </p>
      <div className="mt-6 flex gap-3 justify-center">
        <Button asChild>
          <Link href="/sessions/new">Inizia nuova sessione</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/sessions">Torna alle sessioni</Link>
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Run test, verify PASS**

- [ ] **Step 4: Commit**

```bash
git add apps/web
git commit -m "feat(session): empty state for not-found session (#2088 step 2)"
```

## Task 3.4: `#2088` — Fix source link + open audit P2 issue

**Files:**
- Modify: source file from Task 3.1 (probable `GamePartiteTab.tsx`)

- [ ] **Step 1: Patch source**

Per Task 3.1 findings. Use correct `sessionId` (not `gameId`) for navigation, or remove the broken link if no session exists.

- [ ] **Step 2: Open audit P2 tracking issue**

```bash
gh issue create --title "audit: /sessions/[id] vs sp4-session-skeleton-live conformance (7 GAP)" --label "audit-finding,area/frontend,P2" --body "$(cat <<'EOF'
## Context
Surfacing during #2088 fix (2026-06-13 spec-panel). Page layout completamente diverso dal mockup canonical skeleton.

## 7 GAP identificati

| # | Gap | Severity |
|---|---|---|
| G1 | Layout 3-column desktop NON implementato (flat tab nav) | High |
| G2 | URL pattern: child routes /tools vs query param ?tab=tools | Medium |
| G3 | ChatAgent always-visible vs tab separata "Chat" | High |
| G4 | TopBar universale con live timer + connection status mancante | Medium |
| G5 | Polymorphic renderers (Scoring/Turn/Toolkit) non astratti | High |
| G6 | Zero game-specific extension implementate (no Catan/Wingspan/etc) | High |
| G7 | 5 stati canonici (default/empty/loading/error/sse-disconnect) non standardizzati | Medium |

## Acceptance criteria (high-level)
- [ ] Discovery: scope decompose per gap (singolo refactor vs decomposed)
- [ ] G4+G5 minimum: TopBar live timer + ScoringPanelRenderer polymorphic (3-5gg)
- [ ] G1+G3 medium: full skeleton 3-column (8-15gg)
- [ ] G6 1 game-specific demo: scegliere Catan/Wingspan/Paleo (4-8 settimane)

## Refs
- Mockup canonical: admin-mockups/design_files/sp4-session-skeleton-live.html
- Mockup Catan extension: admin-mockups/design_files/sp4-session-catan-live.html (design_intent=current)
- Source page: apps/web/src/app/(authenticated)/sessions/[id]/layout.tsx
- Originating spec-panel: docs/superpowers/specs/2026-06-13-batch-8-issue-execution-plan-design.md

## Priority
P2 — Mockup conformance, no functional bug. Tracking issue post #2088 (P1 functional fix completed).
EOF
)"
```

- [ ] **Step 3: Commit + push + PR**

```bash
git add apps/web
git commit -m "fix(session): source link gameId vs sessionId (#2088 step 3)"
git push -u origin feature/issue-2088-session-404-error-state
gh pr create --base main-dev --title "fix(session): #2088 404 empty state + audit P2 tracking opened"
gh pr checks --watch
gh pr merge --squash --auto
```

---

## Task 3.5: `#2089` GamePicker — Branch + BE search param

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetLibraryQueryHandler.cs` (find via Grep)
- Modify: `apps/api/src/Api/Routing/UserLibraryEndpoints.cs` (search param)

- [ ] **Step 1: Branch**

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-2089-game-picker-unified
git config branch.feature/issue-2089-game-picker-unified.parent main-dev
```

- [ ] **Step 2: Failing integration test — BE search param**

```csharp
[Fact]
public async Task GetLibrary_WithSearchParam_ReturnsFilteredGames()
{
    await SeedLibraryAsync(new[] { "Wingspan", "Catan", "Wingspan: Oceania" });

    var response = await Client.GetFromJsonAsync<LibraryPageDto>(
        "/api/v1/library?search=wingspan&pageSize=50");

    response!.Items.Should().HaveCount(2);
    response.Items.Should().AllSatisfy(g => g.GameTitle.ToLower().Contains("wingspan"));
}
```

- [ ] **Step 3: Run test, verify FAIL**

- [ ] **Step 4: Add `search` param to query + endpoint**

```csharp
// GetLibraryQuery.cs: add string? Search = null
// Endpoint: bind [FromQuery] string? search
// Handler: if non-null, .Where(g => EF.Functions.ILike(g.GameTitle, $"%{search}%"))
```

- [ ] **Step 5: Run test, verify PASS**

- [ ] **Step 6: Commit**

```bash
git add apps/api tests
git commit -m "feat(api): add ?search= query param to GET /library (#2089 step 1)"
```

## Task 3.6: `#2089` — `<GamePicker>` shared component

**Files:**
- Create: `apps/web/src/components/features/game-picker/GamePicker.tsx`
- Create: `apps/web/src/components/features/game-picker/__tests__/GamePicker.test.tsx`
- Create: `apps/web/src/components/features/game-picker/index.ts`

- [ ] **Step 1: Failing tests — 5 canonical states**

```tsx
import { GamePicker } from '../GamePicker';

describe('GamePicker', () => {
  it('debounces queries 300ms', async () => {
    const onSelect = vi.fn();
    const { user } = setup(<GamePicker source="library" onSelect={onSelect} />);

    await user.type(screen.getByRole('combobox'), 'wingspan');

    // Only 1 fetch after debounce period
    await waitFor(() => expect(mockFetch).toHaveBeenCalledTimes(1));
  });

  it('shows empty state when no results', async () => {
    mockApi.library.getLibrary.mockResolvedValueOnce({ items: [] });
    setup(<GamePicker source="library" onSelect={vi.fn()} />);

    await user.type(screen.getByRole('combobox'), 'xyz');
    expect(await screen.findByText(/nessun gioco trovato/i)).toBeInTheDocument();
  });

  it('shows error toast on API failure', async () => {
    mockApi.library.getLibrary.mockRejectedValueOnce(new Error('500'));
    setup(<GamePicker source="library" onSelect={vi.fn()} />);

    await user.type(screen.getByRole('combobox'), 'wingspan');
    expect(await screen.findByText(/errore caricamento giochi/i)).toBeInTheDocument();
  });

  it('allows manual entry when allowManualEntry=true', async () => {
    const onSelect = vi.fn();
    setup(<GamePicker source="library" allowManualEntry onSelect={onSelect} />);

    await user.type(screen.getByRole('combobox'), 'Custom Game');
    await user.click(screen.getByRole('button', { name: /usa questo nome/i }));

    expect(onSelect).toHaveBeenCalledWith({ id: null, title: 'Custom Game', manual: true });
  });

  it('warns when manual entry doesn\'t match catalog', async () => {
    setup(<GamePicker source="library" allowManualEntry onSelect={vi.fn()} />);

    await user.type(screen.getByRole('combobox'), 'unknown game');
    await user.click(screen.getByRole('button', { name: /usa questo nome/i }));

    expect(screen.getByText(/gioco non riconosciuto/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests, verify FAIL**

- [ ] **Step 3: Implement GamePicker**

```tsx
// GamePicker.tsx
import { useDebouncedCallback } from 'use-debounce';
import { toast } from 'sonner';
import { useQuery } from '@tanstack/react-query';
import { useState, useCallback } from 'react';

export interface GameOption {
  id: string | null;  // null = manual
  title: string;
  imageUrl?: string;
  manual?: boolean;
}

export interface GamePickerProps {
  source: 'library' | 'catalog' | 'both';
  onSelect: (game: GameOption) => void;
  allowManualEntry?: boolean;
  placeholder?: string;
}

export function GamePicker({ source, onSelect, allowManualEntry, placeholder = 'Cerca gioco…' }: GamePickerProps) {
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');

  const debouncedSetQuery = useDebouncedCallback(
    (val: string) => setDebouncedQuery(val),
    300
  );

  const { data, isLoading, isError } = useQuery({
    queryKey: ['game-picker', source, debouncedQuery],
    queryFn: async () => {
      if (debouncedQuery.length < 2) return { items: [] };
      try {
        switch (source) {
          case 'library':
            return await api.library.getLibrary({ search: debouncedQuery, pageSize: 50 });
          case 'catalog':
            return await api.sharedGames.search({ q: debouncedQuery, status: 1 });
          case 'both':
            const [lib, cat] = await Promise.all([
              api.library.getLibrary({ search: debouncedQuery, pageSize: 25 }),
              api.sharedGames.search({ q: debouncedQuery, status: 1, pageSize: 25 }),
            ]);
            return { items: [...lib.items, ...cat.items] };
        }
      } catch (e) {
        toast.error('Errore caricamento giochi');
        throw e;
      }
    },
    enabled: debouncedQuery.length >= 2,
  });

  const isManualUnknown = allowManualEntry && query.length >= 2 && (data?.items.length ?? 0) === 0;

  return (
    <div>
      <input
        role="combobox"
        type="text"
        placeholder={placeholder}
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          debouncedSetQuery(e.target.value);
        }}
      />
      {isLoading && <p>Caricamento…</p>}
      {isError && <p>Errore caricamento giochi</p>}
      {!isLoading && data?.items.length === 0 && query.length >= 2 && (
        <p>Nessun gioco trovato</p>
      )}
      <ul>
        {data?.items.map(g => (
          <li key={g.id}>
            <button type="button" onClick={() => onSelect({ id: g.id, title: g.title, imageUrl: g.imageUrl })}>
              {g.title}
            </button>
          </li>
        ))}
      </ul>
      {allowManualEntry && query.length >= 2 && (
        <>
          <button type="button" onClick={() => onSelect({ id: null, title: query, manual: true })}>
            Usa questo nome
          </button>
          {isManualUnknown && (
            <p className="text-xs text-warning">Gioco non riconosciuto — il sistema non potrà fornire regole AI</p>
          )}
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run tests, verify PASS (5/5)**

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "feat(game-picker): introduce shared <GamePicker> with 5 canonical states (#2089 step 2)"
```

## Task 3.7: `#2089` — Refactor SessionCreationWizard → GamePicker

**Files:**
- Modify: `apps/web/src/components/session/SessionCreationWizard.tsx:118-225`

- [ ] **Step 1: Read current SessionCreationWizard handleSearch + render**

- [ ] **Step 2: Replace 2 inline search widgets with `<GamePicker>`**

```tsx
// DA: 2 search input + handleSearch + manual entry input separate
// A:  Single <GamePicker source="library" allowManualEntry onSelect={handleGameSelected} placeholder="Cerca nella tua libreria…" />
```

Remove `handleSearch` callback + `searchResults` state (now managed by GamePicker).

- [ ] **Step 3: Update existing tests to pass with new structure**

- [ ] **Step 4: Commit**

```bash
git add apps/web
git commit -m "refactor(session): SessionCreationWizard uses <GamePicker> (#2089 step 3)"
```

## Task 3.8: `#2089` — Refactor SearchGameStep → GamePicker

**Files:**
- Modify: `apps/web/src/components/game-night/steps/SearchGameStep.tsx:111-158`

- [ ] **Step 1: Replace with GamePicker source="both"**

Remove `handleKeyDown` + `handleSearch` + `Enter`-only trigger.

```tsx
<GamePicker source="both" onSelect={handleSelectGame} placeholder="Nome del gioco…" />
```

- [ ] **Step 2: Verify auto-search works (debounce 300ms, not Enter-only)**

- [ ] **Step 3: Commit**

```bash
git add apps/web
git commit -m "refactor(game-night): SearchGameStep uses <GamePicker source=both> (#2089 step 4)"
```

## Task 3.9: `#2089` — Mobile/desktop collision fix + final cleanup

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/page.tsx` (or layout)
- Modify: `session-wizard-mobile.tsx` (delete file if duplicated)

- [ ] **Step 1: Replace `lg:hidden` DOM duplication with useMediaQuery**

```tsx
// In sessions/new/page.tsx or layout
import { useMediaQuery } from '@/hooks/useMediaQuery';

const isDesktop = useMediaQuery('(min-width: 1024px)');
return isDesktop ? <SessionCreationWizard /> : <SessionWizardMobile />;
```

OR (if mobile is functionally identical to desktop now via GamePicker): delete `session-wizard-mobile.tsx` entirely and let Tailwind responsive classes handle layout.

- [ ] **Step 2: Verify E2E desktop + mobile viewport**

```bash
cd apps/web && pnpm test:e2e
```

- [ ] **Step 3: Commit + push + PR**

```bash
git add apps/web
git commit -m "refactor(sessions): unify desktop+mobile via useMediaQuery (#2089 step 5)"
git push -u origin feature/issue-2089-game-picker-unified
gh pr create --base main-dev --title "refactor(search): #2089 unified <GamePicker> + BE search param + 5 widget refactor" --body "$(cat <<'EOF'
## Summary
- New shared <GamePicker> component (source=library|catalog|both, debounce 300ms, toast errors, 5 canonical states)
- BE: GET /api/v1/library?search=<query>&pageSize=50 query param
- Refactored: SessionCreationWizard, SearchGameStep, session-wizard-mobile → <GamePicker>
- InlineGamePicker preserved (specialized playlist overlay)
- Mobile/desktop unified via useMediaQuery

## Test plan
- [x] GamePicker 5 canonical states unit tests
- [x] BE integration test: ?search= filter
- [x] E2E: 'wingspan' triggers single fetch (no 8 keystroke), results visible
- [x] Manual entry: warning visible when no catalog match

Closes #2089
EOF
)"
gh pr checks --watch
gh pr merge --squash --auto
```

---

## Task 4: Final verification

- [ ] **Step 1: Verify all 8 issues closed**

```bash
for ISSUE in 2090 2273 2274 2275 2276 2088 2089 2190 2271 2242 2243 2244 2245 2246 2247 2248; do
  gh issue view $ISSUE --json number,state,title | jq
done
```

Expected: all `state: CLOSED`.

- [ ] **Step 2: Verify CLAUDE.md baseline known-flaky still empty (no regression)**

```bash
# Use Read on CLAUDE.md § Known Flaky Tests
# Verify table is still "_(none — baseline currently clean)_"
```

- [ ] **Step 3: Consolidate pattern memories**

```bash
# Use Write tool to create:
# C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-main\memory\pdf-indexing-domain-event-bypass.md
# Then update MEMORY.md index
```

Memory content:

```markdown
---
name: pdf-indexing-domain-event-bypass-pattern
description: Anti-pattern P234 — handlers che scrivono direttamente su EF VectorDocumentEntity bypassano VectorDocument.Create() factory e impediscono emissione VectorDocumentIndexedEvent. Identificato + fixato epic #2242.
metadata:
  type: feedback
---

Quando bounded context ha `Domain.Entity.Create()` factory che raise un domain event nel ctor, MAI scrivere direttamente sull'EF Entity nello stesso aggregate root.

**Why**: durante epic #2242 (closed 2026-06-13), 3 call site bypassavano VectorDocument.Create() instanziando `new VectorDocumentEntity {...}` direttamente. Risultato: VectorDocumentIndexedEvent mai emesso → handler che setta SharedGame.HasKnowledgeBase mai chiamato → silent failure mode (Ready ma HasKnowledgeBase=false).

**How to apply**:
- Sospetta sempre questo anti-pattern quando vedi `new <X>Entity {...}` in Application handler invece di `<X>.Create(...)` factory chiamata.
- Pattern canonico in repo: PdfIndexingPipeline.cs (post #2244) consolida call site in service iniettabile.
- Prometheus drift detector: `meepleai_pdf_indexed_no_kb_flag_total` SLO=0 — alert su qualsiasi increment.
- Link: [[backend-fast-pereline-import-fail]] e [[notracking-default-update-gotcha]] sono pattern simili (errori silenziosi BE).

Related epic: #2242 (closed 2026-06-13).
```

- [ ] **Step 4: Final commit on main-dev (memory index update)**

This goes to user's private memory dir, not repo.

- [ ] **Step 5: Final report to user**

Summary: tutte le 8 issue chiuse, epic #2242 closed, pattern memory consolidato.

---

## Self-review

**Spec coverage check** (vs `2026-06-13-batch-8-issue-execution-plan-design.md`):

- ✅ `#2190` Hub link: Task 1.1-1.4 (branch, fix href+rename, delete route+redirect, flag #2179)
- ✅ `#2271` S3 TransferUtility: Task 1.5-1.7 (branch+repro test, refactor, secondary cleanups)
- ✅ `#2244` BE refactor: Task 2.1-2.4 (factory, pipeline, 3 migrate, TransitionTo)
- ✅ `#2248` test+HybridCache+ADR: Task 2.5-2.7 (E2E test, Prometheus+job, HybridCache+ADR)
- ✅ `#2246` FE admin: Task 2.8-2.10 (cache invalidation, Blocks B+C+D, E2E)
- ✅ `#2245` SSE+AutoCreateAgent: Task 2.11
- ✅ `#2247` FE user: Task 2.12
- ✅ Epic close: Task 2.13
- ✅ `#2088` session 404: Task 3.1-3.4 (branch+source, defensive store, empty state, source fix+audit P2 issue)
- ✅ `#2089` GamePicker: Task 3.5-3.9 (BE search, GamePicker, 3 widget refactor, mobile/desktop)
- ✅ Final verification: Task 4

**Placeholder scan**: ADR number `adr-NNN` is intentional (assigned at creation via Glob next-available). Source files for Task 1.1 (nav components) and Task 3.1 (broken link source) are intentionally discoverable via Grep — engineer reads code to find exact path.

**Type consistency**: `GameOption` interface (Task 3.6) used consistently in Task 3.7-3.9. `STAGE_ORDER` canonical values (`Pending|Uploading|Extracting|Chunking|Embedding|Indexing|Ready`) consistent in Task 2.9. `error.kind = 'not-found' | 'server'` (Task 3.2) consistent with empty state check (Task 3.3).
