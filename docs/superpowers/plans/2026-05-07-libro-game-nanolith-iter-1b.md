# Libro Game Nanolith — Iter 1.B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Prerequisite**: Iter 1.A merged on `main-dev` (PR `feat(gamebook): Iter 1.A`). This plan extends `GamebookCampaignSession` and adds the photo-translate pipeline.

**Goal:** Consegnare il photo-translate per Storybook + Encounter Book (G3/N3) e il glossario per-campagna con history paragrafi tradotti (G4 full / N4) — il valore differenziante del dogfood Aaron.

**Architecture:** Two-phase API per D9 (segment + translate, no auto-pick). Foto sale a backend, salvata su S3 (R2) con retention 24h forzata + EXIF strip. OCR server-side (Tesseract / cloud TBD by Aaron). Translation via DeepSeek (esistente) con glossario context-injected. SSE streaming per UX progressiva. Glossario auto-bootstrap dai KB Press Start + Rules indicizzati a campaign-create time.

**Tech Stack:** .NET 9 / EF Core (jsonb) / MediatR / S3 SDK / Tesseract.NET o cloud OCR · Next.js 16 / React 19 / Server Actions for upload / SSE EventSource

**Reference spec:** `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md` §3 (N3 scenarios), §4.2 (Mermaid pipeline), §4.3 (REST naming), §5.1 (TranslatedParagraph aggregate), §6 (failure modes), §11 D9-D14 decisioni (two-phase, R2 retention, 5 nuove tabelle).

**Reference mockups:** `SP6-D translation-viewer fullscreen`, `SP6-G resume-state`, `SP6-H glossary-editor` (Phase 0 prerequisite per design doc D17). H must be available before this plan starts.

---

## File Structure

### Backend (new)
- Domain
  - `GamebookPhotoArtifact.cs` (aggregate: photoId, sessionId, ocrText, segments, status `Uploaded|Segmented|Translated|Failed`, retention)
  - `TranslatedParagraph.cs` (entity owned by campaign: paragraphNumber, sourceText, translatedText, glossaryAppliedTerms[], pageType `Storybook|Encounter`)
  - `GamebookGlossaryEntry.cs` (entity: campaignId, termEn, termIt, source `auto-bootstrap|manual`)
  - `GamebookSegment.cs` (value object: paragraphNumber, sourceText, boundingBox?)
  - Repos: `IGamebookPhotoArtifactRepository`, `IGamebookGlossaryRepository`, `ITranslatedParagraphRepository`
  - Domain service `GlossaryBootstrapService` (extracts term candidates from indexed KB chunks)
- Application
  - `UploadGamebookPhotoCommand` + handler (writes to S3, creates artifact)
  - `SegmentGamebookPhotoCommand` + handler (OCR + paragraph parsing)
  - `TranslateGamebookSegmentCommand` + handler (DeepSeek call with glossary injection)
  - `BootstrapGamebookGlossaryCommand` + handler (called on campaign-create — extends Iter 1.A flow)
  - `UpsertGamebookGlossaryEntryCommand` + handler (manual edit)
  - `GetGamebookHistoryQuery` + handler (paragraph timeline)
  - DTOs: `GamebookPhotoArtifactDto`, `TranslatedParagraphDto`, `GamebookGlossaryEntryDto`
- Infrastructure
  - `GamebookPhotoStorageService` (S3 wrapper, 24h retention enforce, EXIF strip)
  - `OcrService` (interface + impl — `TesseractOcrService` or `CloudOcrService` per D config)
  - `GamebookPhotoArtifactConfiguration.cs`, `TranslatedParagraphConfiguration.cs`, `GamebookGlossaryEntryConfiguration.cs`
- Routing
  - `GamebookPhotoEndpoints.cs` — POST upload, POST segment, POST translate (SSE), GET history, GET/PUT glossary
- Migrations
  - `<ts>_AddGamebookPhotoAndGlossaryTables.cs` — 3 new tables (photo_artifacts, translated_paragraphs, glossary_entries)

### Backend (modify)
- `CreateGamebookCampaignHandler.cs` (Iter 1.A) — call `BootstrapGamebookGlossary` post-create
- `Program.cs` — register `app.MapGamebookPhotoEndpoints()`
- `SessionTrackingDbContext.cs` — 3 new DbSets
- `appsettings.json` — `Gamebook:OcrProvider`, `Gamebook:S3:RetentionHours=24`, `Gamebook:S3:BucketName`

### Frontend (new)
- `apps/web/src/app/(authenticated)/library/games/[gameId]/play/[campaignId]/translate/page.tsx` — full-screen translate viewer
- `apps/web/src/components/v2/gamebook/TranslateViewer.tsx` — composition: CameraViewfinder + segment picker + SSE stream display
- `apps/web/src/components/v2/gamebook/SegmentPicker.tsx` — list of detected paragraphs with select-to-translate
- `apps/web/src/components/v2/gamebook/TranslationPane.tsx` — IT translation w/ glossary highlights + EN collapsible
- `apps/web/src/components/v2/gamebook/GlossaryEditor.tsx` — mockup H realization
- `apps/web/src/components/v2/gamebook/HistoryDrawer.tsx` — paragraph history (timeline)
- `apps/web/src/lib/api/gamebook-photos.ts` — upload + segment + translate clients
- `apps/web/src/lib/api/gamebook-glossary.ts` — glossary CRUD clients
- `apps/web/src/lib/gamebook/hooks/usePhotoUpload.ts`
- `apps/web/src/lib/gamebook/hooks/useSegmentPhoto.ts`
- `apps/web/src/lib/gamebook/hooks/useTranslateSegmentSSE.ts` (SSE consumer)
- `apps/web/src/lib/gamebook/hooks/useGamebookGlossary.ts`
- `apps/web/src/lib/gamebook/hooks/useGamebookHistory.ts`
- `apps/web/src/lib/gamebook/utils/exifStripper.ts` — client-side EXIF removal before upload

### Tests
- Backend
  - `GamebookPhotoArtifactTests.cs` (unit: aggregate state machine)
  - `TranslatedParagraphTests.cs` (unit)
  - `GlossaryBootstrapServiceTests.cs` (unit: extracts terms from fake KB chunks)
  - `UploadGamebookPhotoHandlerTests.cs` (unit: S3 mock, EXIF assertion)
  - `SegmentGamebookPhotoHandlerTests.cs` (unit: OCR mock returns canned segments)
  - `TranslateGamebookSegmentHandlerTests.cs` (unit: DeepSeek mock, glossary context assertion)
  - `GamebookPhotoEndpointsTests.cs` (integration: full upload→segment→translate cycle with mocked LLM/OCR)
  - `GamebookGlossaryConsistencyTests.cs` (integration: §0.2 consistency_rate calculation harness)
- Frontend
  - Unit tests for all new hooks + components
  - `apps/web/e2e/gamebook-iter1b.spec.ts` — `@ci` synthetic photo upload + `@dogfood` real Storybook page

---

## Conventions

Same as Iter 1.A. Branch from `main-dev` after Iter 1.A merged. Branch name: `feature/libro-game-iter-1b`.

**Phase 0 prerequisite**: mockup H glossary-editor must be in `docs/mockups/sp6/H-libro-game-glossary-editor.html` (per design doc D17). Iter 1.B starts only when H is committed.

**Privacy constraint** (D11): photos in S3 are tagged with `expiry: createdAt + 24h`, EXIF GPS stripped client-side BEFORE upload, server re-validates strip post-upload, lifecycle policy on bucket auto-deletes.

**Cost constraint**: each translate call logs `tokens_in/tokens_out/cost_usd` to `console` for Aaron's manual review. No paywall in Iter 1.B (superadmin bypass per D17).

---

## Tasks

### Task 0: Branch + verify Iter 1.A merged + mockup H present

- [ ] **Step 1: Verify prerequisite**

```bash
git checkout main-dev && git pull --ff-only
git log --oneline -10 | grep -i "iter 1.a\|iter-1-a" || { echo "ABORT: Iter 1.A not merged"; exit 1; }
ls docs/mockups/sp6/ | grep -i "H-libro-game-glossary-editor" || { echo "ABORT: mockup H missing — run Phase 0 first"; exit 1; }
```

- [ ] **Step 2: Create branch**

```bash
git checkout -b feature/libro-game-iter-1b
git config branch.feature/libro-game-iter-1b.parent main-dev
```

---

### Task 1: Domain — `GamebookPhotoArtifact` + `GamebookSegment`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/GamebookPhotoArtifact.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/GamebookSegment.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/PhotoArtifactStatus.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/GamebookPageType.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/GamebookPhotoArtifactTests.cs`

- [ ] **Step 1: Write failing test for aggregate state machine**

```csharp
// GamebookPhotoArtifactTests.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookPhotoArtifactTests
{
    [Fact]
    public void Create_WithValidInputs_StartsAsUploaded()
    {
        var a = GamebookPhotoArtifact.Create(Guid.NewGuid(), "s3://bucket/photos/abc.jpg", GamebookPageType.Storybook);
        a.Status.Should().Be(PhotoArtifactStatus.Uploaded);
        a.S3Key.Should().Be("s3://bucket/photos/abc.jpg");
        a.PageType.Should().Be(GamebookPageType.Storybook);
        a.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RecordSegments_FromUploaded_TransitionsToSegmented()
    {
        var a = GamebookPhotoArtifact.Create(Guid.NewGuid(), "s3://x", GamebookPageType.Storybook);
        var segments = new[]
        {
            GamebookSegment.Create(47, "You enter the cave...", boundingBox: null),
            GamebookSegment.Create(48, "A goblin attacks!", boundingBox: null),
        };
        a.RecordSegments(segments, ocrFullText: "You enter... A goblin attacks!");
        a.Status.Should().Be(PhotoArtifactStatus.Segmented);
        a.Segments.Should().HaveCount(2);
    }

    [Fact]
    public void RecordSegments_FromSegmented_Throws()
    {
        var a = GamebookPhotoArtifact.Create(Guid.NewGuid(), "s3://x", GamebookPageType.Storybook);
        a.RecordSegments(new[] { GamebookSegment.Create(1, "x", null) }, "x");
        Action act = () => a.RecordSegments(new[] { GamebookSegment.Create(2, "y", null) }, "y");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_FromAnyState_RecordsReason()
    {
        var a = GamebookPhotoArtifact.Create(Guid.NewGuid(), "s3://x", GamebookPageType.Storybook);
        a.MarkFailed("OCR provider returned 502");
        a.Status.Should().Be(PhotoArtifactStatus.Failed);
        a.FailureReason.Should().Be("OCR provider returned 502");
    }
}
```

- [ ] **Step 2: Implement**

```csharp
// PhotoArtifactStatus.cs
namespace Api.BoundedContexts.SessionTracking.Domain.Enums;
public enum PhotoArtifactStatus { Uploaded = 0, Segmented = 1, Translated = 2, Failed = 99 }
```

```csharp
// GamebookPageType.cs
namespace Api.BoundedContexts.SessionTracking.Domain.Enums;
public enum GamebookPageType { Storybook = 0, Encounter = 1 }
```

```csharp
// GamebookSegment.cs
namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

public sealed record GamebookSegment
{
    public int ParagraphNumber { get; }
    public string SourceText { get; }
    public string? BoundingBox { get; } // serialized JSON of x,y,w,h or null

    private GamebookSegment(int n, string text, string? bbox)
    {
        ParagraphNumber = n;
        SourceText = text;
        BoundingBox = bbox;
    }

    public static GamebookSegment Create(int paragraphNumber, string sourceText, string? boundingBox)
    {
        if (paragraphNumber < 0) throw new ArgumentException("paragraph >= 0", nameof(paragraphNumber));
        if (string.IsNullOrWhiteSpace(sourceText)) throw new ArgumentException("source required", nameof(sourceText));
        return new GamebookSegment(paragraphNumber, sourceText.Trim(), boundingBox);
    }
}
```

```csharp
// GamebookPhotoArtifact.cs
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

public sealed class GamebookPhotoArtifact
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string S3Key { get; private set; } = default!;
    public GamebookPageType PageType { get; private set; }
    public PhotoArtifactStatus Status { get; private set; }
    public string? OcrFullText { get; private set; }
    public IReadOnlyList<GamebookSegment> Segments { get; private set; } = Array.Empty<GamebookSegment>();
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private GamebookPhotoArtifact() { }

    public static GamebookPhotoArtifact Create(Guid campaignId, string s3Key, GamebookPageType pageType)
    {
        if (campaignId == Guid.Empty) throw new ArgumentException("campaignId required", nameof(campaignId));
        if (string.IsNullOrWhiteSpace(s3Key)) throw new ArgumentException("s3Key required", nameof(s3Key));

        var now = DateTimeOffset.UtcNow;
        return new GamebookPhotoArtifact
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            S3Key = s3Key,
            PageType = pageType,
            Status = PhotoArtifactStatus.Uploaded,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
        };
    }

    public void RecordSegments(IEnumerable<GamebookSegment> segments, string ocrFullText)
    {
        if (Status != PhotoArtifactStatus.Uploaded)
            throw new InvalidOperationException($"cannot segment from status {Status}");
        Segments = segments.ToList().AsReadOnly();
        OcrFullText = ocrFullText;
        Status = PhotoArtifactStatus.Segmented;
    }

    public void MarkTranslated()
    {
        if (Status != PhotoArtifactStatus.Segmented)
            throw new InvalidOperationException($"cannot translate from status {Status}");
        Status = PhotoArtifactStatus.Translated;
    }

    public void MarkFailed(string reason)
    {
        Status = PhotoArtifactStatus.Failed;
        FailureReason = reason;
    }
}
```

- [ ] **Step 3: Run tests — expect PASS**

```bash
dotnet test --filter "FullyQualifiedName~GamebookPhotoArtifactTests"
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/GamebookPhotoArtifactTests.cs
git commit -m "feat(gamebook): GamebookPhotoArtifact aggregate with state machine"
```

---

### Task 2: Domain — `TranslatedParagraph` + `GamebookGlossaryEntry`

**Files:**
- Create: `TranslatedParagraph.cs` (entity)
- Create: `GamebookGlossaryEntry.cs` (entity)
- Test: `TranslatedParagraphTests.cs`, `GamebookGlossaryEntryTests.cs`

- [ ] **Step 1: Tests + impl** (follow Task 1 pattern; both have factory `Create()`, audit fields, soft-delete on glossary entries only — paragraphs are immutable history)

Key invariants to enforce in tests:
- `TranslatedParagraph.AppliedGlossaryTerms` is non-null read-only list (empty when no terms matched)
- `GamebookGlossaryEntry.Source` is `'auto-bootstrap' | 'manual'`
- `GamebookGlossaryEntry.UpdateTermIt(newValue, editedBy)` flips `Source` to `'manual'`
- `TranslatedParagraph` factory rejects empty `translatedText`

Implementation skeleton (follow Iter 1.A Task 2 patterns for audit + soft-delete):

```csharp
// TranslatedParagraph.cs (entity owned by campaign)
public sealed class TranslatedParagraph
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid PhotoArtifactId { get; private set; }
    public int ParagraphNumber { get; private set; }
    public GamebookPageType PageType { get; private set; }
    public string SourceTextEn { get; private set; } = default!;
    public string TranslatedTextIt { get; private set; } = default!;
    public IReadOnlyList<string> AppliedGlossaryTerms { get; private set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private TranslatedParagraph() { }

    public static TranslatedParagraph Create(
        Guid campaignId, Guid photoArtifactId, int paragraphNumber,
        GamebookPageType pageType, string sourceEn, string translatedIt,
        IEnumerable<string> appliedTerms, Guid createdBy)
    {
        // validation + assignments
        // ...
    }
}
```

```csharp
// GamebookGlossaryEntry.cs
public sealed class GamebookGlossaryEntry
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string TermEn { get; private set; } = default!;
    public string TermIt { get; private set; } = default!;
    public GlossarySource Source { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private GamebookGlossaryEntry() { }

    public static GamebookGlossaryEntry Create(Guid campaignId, string termEn, string termIt, GlossarySource source, Guid createdBy) { /* ... */ }
    public void UpdateTermIt(string newValue, Guid editedBy) { /* flips Source to Manual */ }
}

public enum GlossarySource { AutoBootstrap = 0, Manual = 1 }
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/TranslatedParagraphTests.cs apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/GamebookGlossaryEntryTests.cs
git commit -m "feat(gamebook): TranslatedParagraph + GamebookGlossaryEntry entities"
```

---

### Task 3: Infrastructure — EF configs + DbSets + migration

**Files:**
- Create: `GamebookPhotoArtifactConfiguration.cs`, `TranslatedParagraphConfiguration.cs`, `GamebookGlossaryEntryConfiguration.cs`
- Modify: `SessionTrackingDbContext.cs` — 3 new `DbSet`s
- Repo interfaces + impls (3 each)
- Migration `<ts>_AddGamebookPhotoAndGlossaryTables`

- [ ] **Step 1: EF configs** (snake_case columns, jsonb for `Segments`, `AppliedGlossaryTerms` as `text[]` per Postgres native, indices on `CampaignId` for all 3 tables, expiration index `(expires_at) WHERE status != Failed` on photo_artifacts for cleanup job)

- [ ] **Step 2: Repos** (mirror Iter 1.A Task 4 pattern — `GetById`, `ListByCampaign`, `Add`, `SaveChanges`; for glossary add `GetByTerm(campaignId, termEn)`)

- [ ] **Step 3: Migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddGamebookPhotoAndGlossaryTables --context SessionTrackingDbContext
```

Review SQL: 3 new tables, no FK to other BC tables (soft decoupling), index on `(campaign_id)` + `(expires_at)`. Apply:

```bash
dotnet ef database update --context SessionTrackingDbContext
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/ apps/api/src/Api/Migrations/
git commit -m "feat(gamebook): EF configs + migration for photo/glossary/translated tables"
```

---

### Task 4: Infrastructure — `GamebookPhotoStorageService`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/GamebookPhotoStorageService.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/IGamebookPhotoStorage.cs`
- Test: `GamebookPhotoStorageServiceTests.cs` (uses MinIO Testcontainer or AWS SDK localstack)

- [ ] **Step 1: Interface**

```csharp
public interface IGamebookPhotoStorage
{
    /// Returns S3 key. Caller has already EXIF-stripped.
    Task<string> UploadAsync(Stream photoStream, string contentType, Guid campaignId, CancellationToken ct);

    /// Validates the photo has no EXIF GPS metadata. Throws if present.
    Task EnsureNoLocationExifAsync(string s3Key, CancellationToken ct);

    /// Returns presigned read URL (valid 5 min) for SSR consumption.
    Task<string> GetPresignedReadUrlAsync(string s3Key, TimeSpan ttl, CancellationToken ct);
}
```

- [ ] **Step 2: S3 impl using `STORAGE_PROVIDER` factory pattern from `infra/secrets/storage.secret`**

Reuse the existing factory in the project (CLAUDE.md mentions `STORAGE_PROVIDER` env var). If gamebook needs a separate bucket, add `Gamebook:S3:BucketName` config; otherwise reuse the project's primary bucket with prefix `gamebook-photos/{campaignId}/`.

- [ ] **Step 3: EXIF strip validation (server-side defense in depth)** — use `MetadataExtractor` NuGet package or `ImageSharp`

- [ ] **Step 4: Test with MinIO Testcontainer**

```csharp
[Fact]
public async Task Upload_StripsExifAtRest()
{
    // arrange: load test photo with GPS EXIF baked in
    // act: upload
    // assert: download → metadata extractor confirms no GPS tags
}
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/Gamebook* apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/
git commit -m "feat(gamebook): photo storage service with EXIF strip enforcement"
```

---

### Task 5: Infrastructure — `IOcrService` interface + adapter

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/IOcrService.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/TesseractOcrService.cs` (default for dev)
- Optional: `CloudOcrService.cs` (Google Vision / AWS Textract — gate via `Gamebook:OcrProvider` config)

- [ ] **Step 1: Interface + DTO**

```csharp
public interface IOcrService
{
    Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken ct);
}

public sealed record OcrResult(string FullText, IReadOnlyList<OcrParagraph> Paragraphs, double AverageConfidence);

public sealed record OcrParagraph(int Number, string Text, BoundingBox? Bbox);

public sealed record BoundingBox(int X, int Y, int Width, int Height);
```

- [ ] **Step 2: Tesseract impl** (NuGet `Tesseract` or `OcrLite` — must support EN model bundled with image; tessdata path configurable)

- [ ] **Step 3: Paragraph segmentation logic** — naive: detect lines starting with `^\d+\.` or `^§\d+` regex as paragraph boundary. Storybook has numbered paragraphs (`§47`); Encounter Book has fewer numbered sections — fall back to single segment if no `§` pattern found.

- [ ] **Step 4: Unit test with synthetic image** (PNG with rendered text via `SkiaSharp`)

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Services/Ocr* apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Ocr*
git commit -m "feat(gamebook): OCR service interface + Tesseract adapter"
```

---

### Task 6: Application — `UploadGamebookPhotoCommand`

**Files:**
- Create: `Commands/UploadGamebookPhotoCommand.cs`, `Validator.cs`, `Handler.cs`
- Test: `UploadGamebookPhotoHandlerTests.cs`

- [ ] **Step 1: Test** — handler ownership check (campaign owner == caller), storage upload called, artifact created, status = Uploaded

```csharp
[Fact]
public async Task Handle_WithValidCampaign_UploadsAndCreatesArtifact()
{
    var storage = new FakeStorage(); // stub returning canned key
    var campRepo = new FakeCampRepo(); // contains 1 owned campaign
    var artRepo = new FakePhotoRepo();
    var handler = new UploadGamebookPhotoHandler(campRepo, artRepo, storage);

    var dto = await handler.Handle(new UploadGamebookPhotoCommand(
        campaignId: ownedCampId, callerUserId: ownerUserId,
        photoStream: new MemoryStream(new byte[] { 0xFF, 0xD8 }),
        contentType: "image/jpeg",
        pageType: GamebookPageType.Storybook), CancellationToken.None);

    dto.Status.Should().Be("Uploaded");
    artRepo.Store.Should().HaveCount(1);
    storage.UploadCallCount.Should().Be(1);
}

[Fact]
public async Task Handle_WhenCallerIsNotCampaignOwner_ThrowsConflictException() { /* ... */ }
[Fact]
public async Task Handle_WhenCampaignMissing_ThrowsNotFoundException() { /* ... */ }
```

- [ ] **Step 2: Implement** — call `storage.UploadAsync` → `storage.EnsureNoLocationExifAsync` → `GamebookPhotoArtifact.Create` → repo.AddAsync + SaveChangesAsync. Return `GamebookPhotoArtifactDto`.

- [ ] **Step 3: Commit**

---

### Task 7: Application — `SegmentGamebookPhotoCommand`

**Files:**
- Create: `Commands/SegmentGamebookPhotoCommand.cs` + Validator + Handler
- Test: `SegmentGamebookPhotoHandlerTests.cs`

- [ ] **Step 1: Test** — happy path: artifact in `Uploaded` → OCR returns 2 paragraphs → artifact transitions to `Segmented` with both segments

- [ ] **Step 2: Test** — failure: OCR throws → `MarkFailed("OCR provider returned 502")` is called and persisted, returns failure DTO

- [ ] **Step 3: Implement**

```csharp
public async Task<GamebookPhotoArtifactDto> Handle(SegmentGamebookPhotoCommand cmd, CancellationToken ct)
{
    var art = await _artRepo.GetByIdAsync(cmd.ArtifactId, ct) ?? throw new NotFoundException(...);
    // ownership check via campaign
    var stream = await _storage.GetReadStreamAsync(art.S3Key, ct);
    OcrResult ocr;
    try { ocr = await _ocr.ExtractAsync(stream, ct); }
    catch (Exception ex) { art.MarkFailed($"OCR: {ex.Message}"); await _artRepo.SaveChangesAsync(ct); return Map(art); }

    var segments = ocr.Paragraphs.Select(p => GamebookSegment.Create(p.Number, p.Text, p.Bbox?.ToString())).ToArray();
    art.RecordSegments(segments, ocr.FullText);
    await _artRepo.SaveChangesAsync(ct);
    return Map(art);
}
```

- [ ] **Step 4: Commit**

---

### Task 8: Application — `TranslateGamebookSegmentCommand` (SSE)

**Files:**
- Create: `Commands/TranslateGamebookSegmentCommand.cs` + Handler (returns `IAsyncEnumerable<string>`)
- Modify: existing DeepSeek client to expose streaming with custom system prompt
- Test: `TranslateGamebookSegmentHandlerTests.cs` — assert glossary injected into LLM prompt

- [ ] **Step 1: Test** — given glossary `[Hive→Alveare, Worker→Lavoratore]` and segment text containing "Hive", the LLM mock receives a system prompt that includes both glossary entries; the assistant response (mocked) contains "Alveare"; final `TranslatedParagraph` is created with `AppliedGlossaryTerms = ["Hive"]`

- [ ] **Step 2: Implement streaming handler**

```csharp
public sealed class TranslateGamebookSegmentHandler
{
    public async IAsyncEnumerable<TranslateChunk> Handle(
        TranslateGamebookSegmentCommand cmd, [EnumeratorCancellation] CancellationToken ct)
    {
        // 1. Load campaign + artifact + segment
        // 2. Load glossary entries for campaignId
        // 3. Build system prompt with glossary table
        // 4. Stream from DeepSeek client, accumulating into final text
        // 5. yield each token as TranslateChunk { delta, isComplete: false }
        // 6. On stream end: detect applied glossary terms (case-insensitive match in source against glossary[i].TermEn AND translation contains glossary[i].TermIt)
        // 7. Persist TranslatedParagraph aggregate
        // 8. Update GamebookCampaignSession.UpdateProgress(paragraphNumber)
        // 9. yield final TranslateChunk { delta: "", isComplete: true, paragraphId, appliedTerms }
    }
}

public sealed record TranslateChunk(string Delta, bool IsComplete, Guid? ParagraphId, IReadOnlyList<string>? AppliedTerms);
```

- [ ] **Step 3: Cost telemetry**

```csharp
_logger.LogInformation("gamebook.translate.cost campaign={CampaignId} tokens_in={In} tokens_out={Out} cost_usd={Usd}",
    cmd.CampaignId, usage.PromptTokens, usage.CompletionTokens, EstimateCost(usage));
```

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(gamebook): TranslateGamebookSegmentCommand with SSE streaming + glossary injection"
```

---

### Task 9: Application — `GlossaryBootstrapService` + integration into `CreateGamebookCampaignHandler`

**Files:**
- Create: `Domain/Services/GlossaryBootstrapService.cs` (or Application service if it depends on KnowledgeBase repo)
- Modify: `CreateGamebookCampaignHandler.cs` (Iter 1.A) — append bootstrap call after session.AddAsync
- Test: `GlossaryBootstrapServiceTests.cs`

- [ ] **Step 1: Test** — given a fake `IKnowledgeBaseRepository` returning chunks containing terms `["Hive", "Worker", "Drone"]`, bootstrap service produces 3 `GamebookGlossaryEntry` with `Source = AutoBootstrap` and reasonable IT translations (use simple LLM call OR hardcoded-mapping fallback for Iter 1.B — Aaron will refine manually)

- [ ] **Step 2: Implement strategy**:
1. Query KB for top-N most-frequent proper-noun-like terms (capitalized, length > 3, not common English words)
2. Send batch to LLM with prompt: "Translate to Italian preserving game-specific feel, return JSON `[{en, it}]`"
3. Persist as `Source = AutoBootstrap`

> Quality bar: Aaron will manually edit via mockup H glossary-editor post-bootstrap. Bootstrap is "best effort" — accept low quality, optimize for "non-empty" not "perfect".

- [ ] **Step 3: Wire into CreateGamebookCampaignHandler**

After `await _repo.AddAsync(session, ct)` and `await _repo.SaveChangesAsync(ct)`:

```csharp
await _glossaryBootstrap.BootstrapForCampaignAsync(session.Id, session.GameId, ct);
```

Bootstrap runs **fire-and-forget for response time** (5-10 sec LLM call would block create) — wrap in `Task.Run` with logging on failure. Alternative: dedicated `BootstrapGamebookGlossaryCommand` triggered by frontend after create, with progress UI.

**Decision note**: opt for the explicit command pattern (Frontend triggers `POST /campaigns/{id}/glossary/bootstrap` after create succeeds) — simpler error UX, keeps create endpoint fast. Update `CreateGamebookCampaignHandler` does NOT auto-trigger.

- [ ] **Step 4: New command `BootstrapGamebookGlossaryCommand`** + handler + endpoint

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(gamebook): glossary bootstrap service + explicit command"
```

---

### Task 10: Routing — `GamebookPhotoEndpoints`

**Files:**
- Create: `apps/api/src/Api/Routing/GamebookPhotoEndpoints.cs`
- Modify: `Program.cs` — `app.MapGamebookPhotoEndpoints()`

- [ ] **Step 1: Endpoints**

```csharp
public static IEndpointRouteBuilder MapGamebookPhotoEndpoints(this IEndpointRouteBuilder app)
{
    var grp = app.MapGroup("/api/v1/gamebook/campaigns/{campaignId:guid}").RequireAuthorization().WithTags("Gamebook");

    // 1. Upload photo (multipart)
    grp.MapPost("/photos", async (Guid campaignId, IFormFile file, [FromForm] string pageType, IMediator m, HttpContext ctx) =>
    {
        var (_, userId) = ctx.RequireUserSession();
        await using var stream = file.OpenReadStream();
        var dto = await m.Send(new UploadGamebookPhotoCommand(
            campaignId, userId, stream, file.ContentType,
            Enum.Parse<GamebookPageType>(pageType, ignoreCase: true)));
        return Results.Created($"/api/v1/gamebook/campaigns/{campaignId}/photos/{dto.Id}", dto);
    }).DisableAntiforgery();

    // 2. Trigger segmentation
    grp.MapPost("/photos/{photoId:guid}/segment", async (Guid campaignId, Guid photoId, IMediator m, HttpContext ctx) =>
    {
        var (_, userId) = ctx.RequireUserSession();
        var dto = await m.Send(new SegmentGamebookPhotoCommand(campaignId, photoId, userId));
        return Results.Ok(dto);
    });

    // 3. Translate selected segment (SSE)
    grp.MapPost("/photos/{photoId:guid}/segments/{paragraphNumber:int}/translate", async (
        Guid campaignId, Guid photoId, int paragraphNumber, IMediator m, HttpContext ctx, CancellationToken ct) =>
    {
        var (_, userId) = ctx.RequireUserSession();
        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";

        await foreach (var chunk in ((TranslateGamebookSegmentHandler)m).Stream(
            new TranslateGamebookSegmentCommand(campaignId, photoId, paragraphNumber, userId), ct))
        {
            await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk)}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    });

    // 4. History
    grp.MapGet("/history", async (Guid campaignId, IMediator m, HttpContext ctx) =>
    {
        var (_, userId) = ctx.RequireUserSession();
        var list = await m.Send(new GetGamebookHistoryQuery(campaignId, userId));
        return Results.Ok(list);
    });

    // 5. Glossary CRUD
    grp.MapGet("/glossary", /* list entries */);
    grp.MapPost("/glossary/bootstrap", /* trigger BootstrapGamebookGlossaryCommand */);
    grp.MapPut("/glossary/{entryId:guid}", /* upsert manual */);

    return app;
}
```

> **Note**: the SSE pattern bypasses MediatR.Send for streaming. Either: (a) inject the handler directly in the endpoint (MediatR doesn't natively support `IAsyncEnumerable`); (b) use a streaming wrapper. For Iter 1.B, direct handler injection in the SSE endpoint is acceptable — document the carve-out in the route comment + note this is the only endpoint NOT going through pure `IMediator.Send` (CQRS rule exception).

- [ ] **Step 2: Wire `Program.cs`**

- [ ] **Step 3: Smoke test**

```bash
dotnet run --no-build &
curl -i http://localhost:8080/api/v1/gamebook/campaigns/00000000-0000-0000-0000-000000000000/photos
# expect: 401
```

- [ ] **Step 4: Commit**

---

### Task 11: Integration test — full upload→segment→translate pipeline

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/GamebookPhotoEndpointsTests.cs`
- Test fixture: a 1-page test PNG with rendered EN paragraph + known canonical translation

- [ ] **Step 1: Test scenario**

```csharp
[Fact(Skip = "Requires Tesseract bundle in CI — Aaron runs locally")]
public async Task FullPipeline_PhotoToTranslation_PersistsAllArtifacts()
{
    // 1. Auth client + create campaign + bootstrap glossary
    // 2. POST /photos with test PNG (multipart)
    // 3. POST /photos/{id}/segment → assert 1+ segments
    // 4. POST /photos/{id}/segments/47/translate (SSE) → consume stream → assert non-empty translation
    // 5. GET /history → assert TranslatedParagraph present with paragraph_number=47
    // 6. GET /glossary → assert ≥ 5 entries (bootstrap result)
}
```

- [ ] **Step 2: Commit**

---

### Task 12: Integration test — glossary consistency rate (§0.2 DoD)

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/GamebookGlossaryConsistencyTests.cs`

- [ ] **Step 1: Test harness**

```csharp
[Fact(Skip = "Requires real LLM + Aaron-validated fixture")]
public async Task ConsistencyRate_OverGoldenSet_MeetsThreshold()
{
    // Given: 20 source paragraphs with embedded glossary terms
    // When: each is translated through the pipeline
    // Then: count(translated containing TermIt for each TermEn present) / count(paragraphs with terms) >= 0.95
}
```

This is gated on Aaron's local validation — same pattern as Iter 1.A golden eval test.

- [ ] **Step 2: Commit**

---

### Task 13: Frontend — typed API clients

**Files:**
- Create: `apps/web/src/lib/api/gamebook-photos.ts`
- Create: `apps/web/src/lib/api/gamebook-glossary.ts`
- Create: `apps/web/src/lib/api/gamebook-history.ts`
- Tests for Zod schemas

- [ ] **Step 1: Schemas**

```typescript
// gamebook-photos.ts
export const PhotoArtifactSchema = z.object({
  id: z.string().uuid(),
  campaignId: z.string().uuid(),
  pageType: z.enum(['Storybook', 'Encounter']),
  status: z.enum(['Uploaded', 'Segmented', 'Translated', 'Failed']),
  segments: z.array(z.object({
    paragraphNumber: z.number().int(),
    sourceText: z.string(),
    boundingBox: z.string().nullable(),
  })).default([]),
  ocrFullText: z.string().nullable(),
  failureReason: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  expiresAt: z.string().datetime({ offset: true }),
});

export async function uploadPhoto(campaignId: string, file: File, pageType: 'Storybook' | 'Encounter') {
  const fd = new FormData();
  fd.append('file', file);
  fd.append('pageType', pageType);
  const raw = await httpClient.postForm(`/api/v1/gamebook/campaigns/${campaignId}/photos`, fd);
  return PhotoArtifactSchema.parse(raw);
}

export async function segmentPhoto(campaignId: string, photoId: string) { /* POST */ }

// SSE consumer NOT in this file (uses EventSource API — see hook in Task 14)
```

- [ ] **Step 2: Glossary client**

```typescript
// gamebook-glossary.ts
export const GlossaryEntrySchema = z.object({
  id: z.string().uuid(),
  termEn: z.string(),
  termIt: z.string(),
  source: z.enum(['AutoBootstrap', 'Manual']),
  updatedAt: z.string().datetime({ offset: true }),
});

export async function listGlossary(campaignId: string) { /* GET */ }
export async function upsertGlossary(campaignId: string, entry: { termEn: string; termIt: string }) { /* PUT */ }
export async function bootstrapGlossary(campaignId: string) { /* POST */ }
```

- [ ] **Step 3: History client**

```typescript
export const TranslatedParagraphSchema = z.object({
  id: z.string().uuid(),
  paragraphNumber: z.number().int(),
  pageType: z.enum(['Storybook', 'Encounter']),
  sourceTextEn: z.string(),
  translatedTextIt: z.string(),
  appliedGlossaryTerms: z.array(z.string()),
  createdAt: z.string().datetime({ offset: true }),
});

export async function getHistory(campaignId: string) { /* GET */ }
```

- [ ] **Step 4: Commit**

---

### Task 14: Frontend — hooks (`usePhotoUpload`, `useTranslateSegmentSSE`, `useGamebookGlossary`, `useGamebookHistory`)

**Files:**
- Create: 4 hooks in `apps/web/src/lib/gamebook/hooks/`
- Tests for each

- [ ] **Step 1: `usePhotoUpload`** — TanStack mutation that wraps `exifStripper.ts` + `uploadPhoto` client

```typescript
export function usePhotoUpload(campaignId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ file, pageType }: { file: File; pageType: 'Storybook' | 'Encounter' }) => {
      const stripped = await stripExifGps(file);  // utils/exifStripper.ts
      return uploadPhoto(campaignId, stripped, pageType);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['gamebook', 'photos', campaignId] }),
  });
}
```

- [ ] **Step 2: `useTranslateSegmentSSE`** — opens EventSource, accumulates `delta`, exposes `{ partialText, isComplete, paragraphId, appliedTerms, error }`

```typescript
export function useTranslateSegmentSSE() {
  const [state, setState] = useState<{ partialText: string; isComplete: boolean; error?: string }>({ partialText: '', isComplete: false });

  const start = useCallback((campaignId: string, photoId: string, paragraphNumber: number) => {
    setState({ partialText: '', isComplete: false });
    const url = `/api/v1/gamebook/campaigns/${campaignId}/photos/${photoId}/segments/${paragraphNumber}/translate`;
    const es = new EventSource(url, { withCredentials: true });
    es.onmessage = (ev) => {
      const chunk = JSON.parse(ev.data) as { delta: string; isComplete: boolean; paragraphId?: string };
      setState(prev => ({
        partialText: prev.partialText + chunk.delta,
        isComplete: chunk.isComplete,
        paragraphId: chunk.paragraphId,
      }));
      if (chunk.isComplete) es.close();
    };
    es.onerror = () => { setState(s => ({ ...s, error: 'stream_error' })); es.close(); };
    return () => es.close();
  }, []);

  return { ...state, start };
}
```

> **Note**: EventSource doesn't support POST. Either: (a) switch endpoint to GET with query params (acceptable since translate is idempotent at handler level — re-call returns same paragraph), (b) use `fetch` + ReadableStream parsing (manual SSE parser). Pick (a) for simplicity — change endpoint method to GET.

- [ ] **Step 3: `useGamebookGlossary` + `useGamebookHistory`** — standard TanStack Query reads + mutations

- [ ] **Step 4: Commit**

---

### Task 15: Frontend — `TranslateViewer` + `SegmentPicker` + `TranslationPane`

**Files:**
- Create: 3 components in `apps/web/src/components/v2/gamebook/`
- Tests for each (RTL with mocked hooks)

- [ ] **Step 1: `TranslateViewer.tsx`** — composition wiring `CameraViewfinder` (existing) + upload mutation + segment list + translation pane. State machine: `idle → uploading → segmenting → segments_ready → translating → translated`. Reference SP6-D mockup for layout.

- [ ] **Step 2: `SegmentPicker.tsx`** — renders `artifact.segments` as a list, each item shows `§{paragraphNumber}: {sourceText preview}` and a "Traduci" button. Disabled when `state !== segments_ready`.

- [ ] **Step 3: `TranslationPane.tsx`** — shows streaming `partialText`, EN original collapsible, glossary terms highlighted with hover-tooltip showing term mapping. Append-only render with React 19 `useDeferredValue` for smooth streaming UX.

- [ ] **Step 4: FREEZE compliance check before commit**

```bash
grep -rn "hsl(.*89%.*48%" apps/web/src/components/v2/gamebook/Translate*.tsx apps/web/src/components/v2/gamebook/SegmentPicker.tsx apps/web/src/components/v2/gamebook/TranslationPane.tsx || echo "FREEZE OK"
```

Expected: "FREEZE OK".

- [ ] **Step 5: Commit**

---

### Task 16: Frontend — `GlossaryEditor` (mockup H realization)

**Files:**
- Create: `apps/web/src/components/v2/gamebook/GlossaryEditor.tsx`
- Test: `GlossaryEditor.test.tsx`

- [ ] **Step 1: Implementation** — table-style editor with rows from `useGamebookGlossary`, inline-edit on `termIt`, badge for `Source`, "Aggiungi termine" CTA, "Bootstrap automatico" button (calls `bootstrapGlossary`). Match mockup H spec.

- [ ] **Step 2: Tests** — RTL: edit row → save → assert mutation called; bootstrap button → assert bootstrap mutation called

- [ ] **Step 3: Commit**

---

### Task 17: Frontend — `HistoryDrawer` (timeline of translated paragraphs)

**Files:**
- Create: `apps/web/src/components/v2/gamebook/HistoryDrawer.tsx`
- Test

- [ ] **Step 1: Implementation** — drawer triggered from `GamebookPlayShell` header (extend Iter 1.A shell), lists `TranslatedParagraph[]` from `useGamebookHistory`, each item expandable with EN source + IT translation + applied terms

- [ ] **Step 2: Wire into `GamebookPlayShell`** — add "Storia" button in header that opens drawer

- [ ] **Step 3: Commit**

---

### Task 18: Frontend — `/translate` route

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/[campaignId]/translate/page.tsx`
- Update Iter 1.A `GamebookPlayShell` to add CTA "📸 Traduci pagina" routing to this page

- [ ] **Step 1: Server component**

```typescript
export default async function TranslatePage({ params }: { params: Promise<{ gameId: string; campaignId: string }> }) {
  const { gameId, campaignId } = await params;
  const campaign = await fetchCampaignSSR(campaignId).catch(() => null);
  if (!campaign) notFound();
  return <TranslateViewer campaignId={campaignId} gameId={gameId} />;
}
```

- [ ] **Step 2: Update `GamebookPlayShell`** to route to `/play/{cid}/translate` on CTA click

- [ ] **Step 3: Commit**

---

### Task 19: E2E `@ci` synthetic + `@dogfood` real

**Files:**
- Create: `apps/web/e2e/gamebook-iter1b.spec.ts`

- [ ] **Step 1: `@ci` synthetic test** — uses pre-baked test PNG fixture, mocks the LLM/OCR endpoints with deterministic responses, asserts UI state transitions

- [ ] **Step 2: `@dogfood` placeholder** — manual run by Aaron with real Storybook page and real Nanolith glossary; not run in CI

```typescript
test.skip('@dogfood Aaron translates real Storybook §47', async () => {
  // Aaron runs locally with real Tesseract + real DeepSeek + real R2
  // Validates §0.3 "letto senza esitazione" criterion manually
});
```

- [ ] **Step 3: Commit**

---

### Task 20: Cleanup job — delete expired photo artifacts

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/GamebookPhotoExpirationJob.cs`
- Modify: `Program.cs` — register hosted service

- [ ] **Step 1: Implement** background service that runs every 1h, queries `WHERE expires_at < now() AND status != 'Failed'`, calls `storage.DeleteAsync(s3Key)` and removes the row. Log every deletion for audit.

- [ ] **Step 2: Test** — Testcontainer with row past expiry → run job once → assert row deleted from DB and storage `Delete` called

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(gamebook): photo expiration cleanup job (24h retention)"
```

---

### Task 21: Documentation + memory

**Files:**
- Modify: `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md` — append D20 delivery log
- Modify: `CLAUDE.md` — add `/play/[campaignId]/translate` to relevant section if needed
- Update: `MEMORY.md` — mark Iter 1 complete

- [ ] **Step 1: Decisions log entry**

```markdown
| D20 | **Iter 1.B delivered** (`<date>`, PR #<n>): G3+G4-full. Photo upload + OCR + segmentation + translate-with-glossary SSE pipeline, glossary editor, history drawer, 24h cleanup job. SSE endpoint uses GET (EventSource limit) — only CQRS carve-out documented. Golden eval + consistency tests skipped pending Aaron local validation. | Plan completed |
```

- [ ] **Step 2: Commit**

---

### Task 22: PR creation

- [ ] **Step 1: Push + PR**

```bash
git push -u origin feature/libro-game-iter-1b
gh pr create --base main-dev --title "feat(gamebook): Iter 1.B — photo translate + glossary + history for Nanolith dogfood" --body "$(cat <<'EOF'
## Summary

Iter 1.B of Libro Game Nanolith dogfood demo (spec `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md`). Builds on Iter 1.A.

- **G3 (N3)** Photo-translate Storybook + Encounter Book pages: upload → OCR → segment → SSE-streamed translation with glossary
- **G4 full (N4)** Glossary auto-bootstrap from indexed KB + manual editor; paragraph history timeline

## Backend
- 3 new domain entities: `GamebookPhotoArtifact`, `TranslatedParagraph`, `GamebookGlossaryEntry`
- 1 EF migration adding 3 tables
- Storage service with EXIF strip + 24h retention
- OCR service interface + Tesseract impl
- 6 application commands/queries + SSE streaming translate handler
- Cleanup hosted service for expired photos

## Frontend
- New route `/play/[campaignId]/translate`
- 4 new components (TranslateViewer, SegmentPicker, TranslationPane, GlossaryEditor) — FREEZE-compliant
- HistoryDrawer wired into `GamebookPlayShell` from Iter 1.A
- 4 new hooks; SSE consumer via EventSource
- Client-side EXIF stripper before upload

## Out of CQRS rule
- SSE translate endpoint bypasses `IMediator.Send` (returns `IAsyncEnumerable<TranslateChunk>`); single documented carve-out

## Test plan
- [ ] Backend unit + integration (excl. golden eval + consistency — Aaron-validated)
- [ ] FREEZE compliance grep gate green
- [ ] Frontend unit + typecheck + lint
- [ ] E2E `@ci` synthetic green
- [ ] Manual dogfood (Aaron): photo Storybook §47 → segment → translate → verify glossary applied → check history drawer

## Out of scope (Iter 2+)
- Encounter Book combat-aware UX (cheatsheet card popup, HP/AC sidebar)
- TTS read-aloud
- Multi-device collaborative play
- Cost paywall
- Real-time hallucination detection

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review

**Spec coverage** (vs design doc 2026-05-07):
- §3 N3.1-N3.10 photo-translate scenarios → Tasks 6, 7, 8, 14, 15
- §3 N4 glossary + history → Tasks 9, 16, 17
- §4.2 Mermaid pipeline → matches Tasks 6→7→8 sequence
- §4.3 REST naming `/api/v1/gamebook/campaigns/{id}/photos` → Task 10
- §5.1 TranslatedParagraph aggregate → Task 2
- §6 failure modes (18) → Tasks 1 (state machine `Failed`), 4 (EXIF), 7 (OCR catch), 8 (LLM streaming retry implicit via OpenRouter chain), 20 (cleanup), 14 (SSE error)
- §0.2 consistency_rate DoD → Task 12
- §0.3 "letto senza esitazione" → Task 19 dogfood
- D9 two-phase → Tasks 7 + 8 separated
- D10 SSE + cache → Task 8 + Task 14
- D11 R2 retention 24h + EXIF strip → Tasks 4 + 14 + 20
- D12 EF 5 tables → 3 new tables in this iter (Iter 1.A added gamebook_campaign_sessions, this adds 3 more = 4; if exactly 5 was intended, the 5th may be a separate audit/log table — flag for D17 review)

Gaps:
- Encounter Book layout-aware OCR (stat block tables) — explicitly best-effort + fallback, no special handling
- D14 `@dogfood` E2E real Nanolith — placeholder only, manual

**Placeholder scan**: no TBD/TODO. Several tasks reference "follow Iter 1.A pattern" which is OK (cross-plan reference, not within-plan); tasks 2 + 3 contain implementation skeletons that the engineer fills following the explicit pattern from Iter 1.A Task 2 — acceptable since both plans ship together.

**Type consistency**:
- `GamebookPageType` enum: same casing in C# and TS Zod schemas (`'Storybook' | 'Encounter'`) — verified
- `PhotoArtifactStatus` enum: TS uses string union, C# uses int-backed enum — JSON serializer must use string converter, add `[JsonConverter(typeof(JsonStringEnumConverter))]` to enum or configure globally
- SSE chunk shape `{ delta, isComplete, paragraphId, appliedTerms }` — same names backend + frontend

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Tesseract bundle size + Docker image bloat | High | Medium | Use Alpine variant + EN-only language pack; consider cloud OCR for prod |
| OCR accuracy < 95% on real Storybook | High | High | Aaron validates §0.3 manually; fallback "scrivi paragrafo manualmente" CTA in UI per Q9 |
| LLM glossary injection cost runaway | Medium | Medium | Cost telemetry per Task 8 step 3; cap glossary size to top 100 terms |
| EventSource POST limitation | Confirmed | — | Switched to GET with idempotent semantics (Task 14 Step 2) |
| EXIF strip miss → privacy violation | Low | High | Defense-in-depth: client strip + server validation (Tasks 4 + 14) |
| 24h cleanup misses → R2 cost runaway | Low | Low | Cleanup job + R2 bucket lifecycle policy (belt + suspenders) |
| FREEZE violation slipping in | Medium | High | Grep gate enforced in Task 15 step 4 + PR review |
| MediatR doesn't support `IAsyncEnumerable` | Confirmed | Low | Documented carve-out: SSE endpoint injects handler directly (Task 10 note) |

---

## Estimated Effort

| Phase | Tasks | Estimate |
|---|---|---|
| Domain (3 entities + value object + enums) | 1-2 | 1.0 day |
| Infra (configs + repos + migration + storage + OCR) | 3-5 | 1.5 days |
| Application (5 commands + bootstrap service) | 6-9 | 1.5 days |
| Routing + SSE + integration tests | 10-12 | 1.0 day |
| Frontend (clients + hooks + 4 components + route) | 13-18 | 1.5 days |
| E2E + cleanup + docs + PR | 19-22 | 0.5 day |
| **Total** | **22** | **~7.0 days** |

Buffer for unknowns (especially OCR quality + LLM streaming): +1 day → **8 days** total. **Slightly over the 5-7gg budget**; if pinch hits, defer **HistoryDrawer (Task 17)** and **GlossaryEditor (Task 16)** to Iter 1.C — both are non-critical-path for the dogfood happy path and Aaron can edit glossary via direct DB write for the first session.

---

## Carry-forward to Iter 2

Per design doc §11 Open Questions, after Aaron's first dogfood session:
1. Encounter Book UX (cheatsheet card vs viewer vs sidebar) — pick from real usage
2. Glossary cross-game vs per-game — Aaron's preference after multi-game test
3. Auto-edit detection (LLM ignored glossary → flag) — instrument first
4. Storybook "next page" auto-suggest — measure post-translation behavior
5. Multi-photo batch (snap 5 → background process) — usability win after first session
6. TTS read-aloud — only if Aaron asks for it
