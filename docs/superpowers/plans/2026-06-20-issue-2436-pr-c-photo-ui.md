# #2436 PR-C — Play Records Photo UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettere all'utente di caricare foto (scoresheet/tavolo) su un PlayRecord esistente — con OCR opt-in — e visualizzarle in una gallery con lightbox nella detail view; più una mini-estensione BE che espone le foto in lettura.

**Architecture:** PR-B ha già la scrittura (`POST /play-records/{id}/photos`) + xmin + dedup. Questo PR aggiunge (a) il **read-path BE**: `PlayRecordDto.Photos[]` popolato dal `GetPlayRecordQueryHandler` con URL presigned (logica estratta in un helper DRY condiviso con l'upload handler); (b) il **FE**: schema Zod, api client multipart, hook mutation, dialog di upload multi-file (HEIC→JPEG client-side via `heic2any`), gallery + lightbox montati nella `PlayRecordDetailView` (pulsante upload creator-only).

**Tech Stack:** .NET 9 (CQRS/MediatR, EF Core, Moq+xUnit) · Next.js/React 19 (TanStack Query, Zod, Vitest, Tailwind) · `heic2any` (già in deps, usato da CustomCoverDialog).

**Spec:** `docs/superpowers/specs/2026-06-20-issue-2436-prc-2437-spec-panel.md` (decisione **C1** = estendi DTO con `photos[]`).

**Confine chiave:** l'endpoint upload richiede un `recordId` esistente → le foto si caricano dalla **detail view** (record già creato), NON dal create wizard. Il pulsante "Aggiungi foto" è visibile solo al creator (`currentUserId === record.createdByUserId`, ADR-066). La gallery è visibile a chiunque possa vedere il record.

**Contratto BE verificato (PR-B, già su main-dev):**
- `POST /api/v1/play-records/{recordId}/photos` — multipart fields: `file` (required), `extractScoreFromPhoto` (bool, opt), `caption` (string, opt). Response `201`: `PlayRecordPhotoUploadResult(Guid PhotoId, string PhotoUrl, string? ThumbnailUrl, string? OcrText, bool WasDeduplicated)`. (`PlayRecordEndpoints.cs:91,224-248`)
- Validator BE: ≤5MB, MIME `image/jpeg|png|webp` (NO HEIC), caption ≤500 char.
- Entity `PlayRecordPhotoEntity` con nav `PlayRecordEntity.Photos` già mappata (`PlayRecordEntityConfiguration` / `PlayRecordPhotoEntityConfiguration`).
- `IBlobStorageService.GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)` → `Task<string?>` (null su local storage → fallback raw path). (`Services/Pdf/IBlobStorageService.cs:139`)

---

## File Structure

**BE (create):**
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/PlayRecordPhotoUrlResolver.cs` — helper static presigning (DRY).

**BE (modify):**
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/PlayRecords/PlayRecordDto.cs` — `PlayRecordPhotoDto` + `Photos` field.
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UploadPlayRecordPhotoCommandHandler.cs` — usa helper (rimpiazza `PresignAsync` privato).
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs` — inietta `IBlobStorageService`, `Include(Photos)`, map+presign.
- `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs` — ctor + nuovo test foto.

**FE (create):**
- `apps/web/src/hooks/mutations/usePlayRecordPhotoUpload.ts`
- `apps/web/src/components/play-records/photos/PlayRecordPhotoUploadDialog.tsx`
- `apps/web/src/components/play-records/photos/PlayRecordPhotoGallery.tsx`
- + relativi `__tests__/`

**FE (modify):**
- `apps/web/src/lib/api/schemas/play-records.schemas.ts` — `PlayRecordPhotoSchema` + `photos` field.
- `apps/web/src/lib/api/play-records.api.ts` — `uploadPhoto`.
- `apps/web/src/components/play-records/PlayRecordDetailView.tsx` — mount gallery + upload button.
- `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json` — `playRecords.photos`.

---

## Task 1: BE — `PlayRecordPhotoDto` + estendi `PlayRecordDto`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/PlayRecords/PlayRecordDto.cs`

- [ ] **Step 1: Aggiungi il DTO foto e il campo `Photos`**

Apri `PlayRecordDto.cs`. Sotto il `record PlayRecordDto(...)` esistente aggiungi un nuovo record nello stesso file e aggiungi `Photos` come **ultimo** parametro del `PlayRecordDto`:

```csharp
public record PlayRecordDto(
    Guid Id,
    Guid? GameId,
    string GameName,
    DateTime SessionDate,
    TimeSpan? Duration,
    PlayRecordStatus Status,
    List<SessionPlayerDto> Players,
    SessionScoringConfigDto ScoringConfig,
    Guid CreatedByUserId,
    PlayRecordVisibility Visibility,
    DateTime? StartTime,
    DateTime? EndTime,
    string? Notes,
    string? Location,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Guid> WinnerPlayerIds,
    string OutcomeType,
    IReadOnlyList<PlayRecordPhotoDto> Photos
);

/// <summary>
/// A photo attached to a play record, exposed for read (#2436 PR-C).
/// <c>Url</c>/<c>ThumbnailUrl</c> are presigned download URLs (or raw paths on local storage).
/// </summary>
public record PlayRecordPhotoDto(
    Guid Id,
    string Url,
    string? ThumbnailUrl,
    string? OcrText,
    string? Caption,
    Guid UploadedByUserId,
    DateTime UploadedAt
);
```

- [ ] **Step 2: Verifica che NON compili ancora**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | grep -E "error|PlayRecordDto"`
Expected: errore in `GetPlayRecordQueryHandler.cs` — il `new PlayRecordDto(...)` ora manca l'argomento `Photos`. È atteso (lo sistemiamo in Task 3). NON committare ancora.

> Nota: Task 1→3 sono un'unica unità di compilazione BE; il commit avviene a fine Task 3 quando il build torna verde.

---

## Task 2: BE — Helper presigning DRY (`PlayRecordPhotoUrlResolver`)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/PlayRecordPhotoUrlResolver.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UploadPlayRecordPhotoCommandHandler.cs`

- [ ] **Step 1: Crea l'helper estraendo la logica oggi privata in `UploadPlayRecordPhotoCommandHandler.PresignAsync`**

Create `PlayRecordPhotoUrlResolver.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Api.Services.Pdf;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Resolves a stored PlayRecord photo blob path to a presigned download URL.
/// Shared by the upload command handler (write path) and the get-record query
/// handler (read path) so the FilePath→fileId/folder parsing lives in one place.
/// #2436 PR-C. On local storage GetPresignedDownloadUrlAsync returns null → raw path.
/// </summary>
internal static class PlayRecordPhotoUrlResolver
{
    public static async Task<string> ResolveAsync(
        IBlobStorageService blobStorage,
        string blobPath,
        int expirySeconds)
    {
        // blobPath is the stored FilePath. Mirror SessionAttachmentService.ParseBlobPath:
        // fileId = substring before the first '_' in the file name; folder = parent dir name.
        var fileName = Path.GetFileName(blobPath);
        if (string.IsNullOrEmpty(fileName))
            return blobPath;

        var underscoreIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        if (underscoreIndex <= 0)
            return blobPath;

        var fileId = fileName[..underscoreIndex];
        var directory = Path.GetDirectoryName(blobPath);
        if (string.IsNullOrEmpty(directory))
            return blobPath;

        var folder = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(folder))
            return blobPath;

        var signed = await blobStorage
            .GetPresignedDownloadUrlAsync(fileId, BlobCategory.PlayRecordPhoto, folder, expirySeconds)
            .ConfigureAwait(false);
        return signed ?? blobPath;
    }
}
```

- [ ] **Step 2: Refactor `UploadPlayRecordPhotoCommandHandler` per usare l'helper**

In `UploadPlayRecordPhotoCommandHandler.cs`: (a) aggiungi `using Api.BoundedContexts.GameManagement.Application.Services;` se assente; (b) sostituisci la chiamata nel `Handle` (riga ~164) da:

```csharp
        var url = await PresignAsync(stored.FilePath, cancellationToken).ConfigureAwait(false);
```
a:
```csharp
        var url = await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, stored.FilePath, PresignExpirySeconds).ConfigureAwait(false);
```
(c) **elimina** l'intero metodo privato `private async Task<string> PresignAsync(string blobPath, CancellationToken ct) { ... }`.

- [ ] **Step 3: Build verde + test upload invariati**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | grep -E "error" || echo BUILD_OK`
Expected: `BUILD_OK` (a parte l'errore atteso del query handler dal Task 1 — se presente, prosegui a Task 3 e builda lì).

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~PlayRecordPhotoUploadTests" 2>&1 | tail -5`
Expected: PASS (il refactor non cambia il comportamento dell'upload).

---

## Task 3: BE — `GetPlayRecordQueryHandler` espone le foto presigned

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs`

- [ ] **Step 1: Aggiorna il test (ctor + nuovo caso foto) — deve FALLIRE**

In `GetPlayRecordQueryHandlerTests.cs`: aggiungi gli `using` e un mock blob storage; aggiorna il costruttore del SUT; aggiungi il test foto. Sostituisci i campi/ctor:

```csharp
using Api.Services.Pdf;   // IBlobStorageService, BlobCategory
using Moq;
// ...existing usings...

public class GetPlayRecordQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _context;
    private readonly Mock<IBlobStorageService> _blob;
    private readonly GetPlayRecordQueryHandler _handler;

    public GetPlayRecordQueryHandlerTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        _blob = new Mock<IBlobStorageService>();
        // Local-storage style: no presigned URL → handler falls back to the raw BlobUrl.
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);
        _handler = new GetPlayRecordQueryHandler(_context, _blob.Object);
    }
```

Aggiungi il nuovo test in fondo alla classe (prima di `Dispose`/helpers):

```csharp
    [Fact]
    public async Task Handle_RecordWithPhotos_ReturnsPhotosOrderedByUploadedAt()
    {
        // Arrange
        var recordId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var entity = MakePlayRecord(recordId);
        entity.Players = new List<RecordPlayerEntity> { MakePlayer(Guid.NewGuid(), recordId, ("wins", 1)) };
        entity.Photos = new List<PlayRecordPhotoEntity>
        {
            new()
            {
                Id = Guid.NewGuid(), PlayRecordId = recordId,
                BlobUrl = "play-record-photos/abc/photo1.webp", ThumbnailUrl = "play-record-photos/abc/thumb1.webp",
                FileSizeBytes = 1234, Sha256Hash = "h1", OcrText = "42", Caption = "scoreboard",
                UploadedByUserId = uploaderId, UploadedAt = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = Guid.NewGuid(), PlayRecordId = recordId,
                BlobUrl = "play-record-photos/abc/photo2.webp", ThumbnailUrl = null,
                FileSizeBytes = 5678, Sha256Hash = "h2", OcrText = null, Caption = null,
                UploadedByUserId = uploaderId, UploadedAt = new DateTime(2026, 6, 20, 9, 0, 0, DateTimeKind.Utc),
            },
        };
        _context.PlayRecords.Add(entity);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetPlayRecordQuery(recordId, entity.CreatedByUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — ordered ascending by UploadedAt, presign falls back to raw BlobUrl on local
        result.Photos.Should().HaveCount(2);
        result.Photos[0].Caption.Should().BeNull();                          // 09:00 first
        result.Photos[0].Url.Should().Be("play-record-photos/abc/photo2.webp");
        result.Photos[1].Caption.Should().Be("scoreboard");                  // 10:00 second
        result.Photos[1].OcrText.Should().Be("42");
        result.Photos[1].ThumbnailUrl.Should().Be("play-record-photos/abc/thumb1.webp");
    }
```

- [ ] **Step 2: Esegui il test → FAIL (compile error: ctor a 1 arg + manca `Photos`)**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~GetPlayRecordQueryHandlerTests.Handle_RecordWithPhotos_ReturnsPhotosOrderedByUploadedAt" 2>&1 | tail -15`
Expected: FAIL — build error (`GetPlayRecordQueryHandler` non ha un ctor a 2 argomenti / `PlayRecordDto` manca `Photos`).

- [ ] **Step 3: Implementa il query handler**

In `GetPlayRecordQueryHandler.cs`: (a) aggiungi `using Api.BoundedContexts.GameManagement.Application.Services;` e `using Api.Services.Pdf;`; (b) inietta `IBlobStorageService`; (c) `Include(Photos)`; (d) mappa con presigning; (e) passa `photos` al DTO.

```csharp
internal class GetPlayRecordQueryHandler : IQueryHandler<GetPlayRecordQuery, PlayRecordDto>
{
    private const int PresignExpirySeconds = 3600;

    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public GetPlayRecordQueryHandler(MeepleAiDbContext context, IBlobStorageService blobStorage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<PlayRecordDto> Handle(GetPlayRecordQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entity = await _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.Id == query.RecordId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new NotFoundException("PlayRecord", query.RecordId.ToString());

        if (entity.CreatedByUserId != query.UserId
            && !entity.Players.Any(p => p.UserId == query.UserId))
        {
            throw new ForbiddenException("You do not have permission to view this play record.");
        }

        var scoringConfig = System.Text.Json.JsonSerializer.Deserialize<SessionScoringConfigDto>(entity.ScoringConfigJson)
            ?? new SessionScoringConfigDto(new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var winnerPlayerIds = PlayRecordOutcomeCalculator.WinnerPlayerIds(entity.Players);
        var outcomeType = PlayRecordOutcomeCalculator.OutcomeType(entity.Players);

        // Map photos with presigned URLs (read path, #2436 PR-C). Ordered oldest→newest.
        var photos = new List<PlayRecordPhotoDto>(entity.Photos.Count);
        foreach (var p in entity.Photos.OrderBy(p => p.UploadedAt))
        {
            var url = await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, p.BlobUrl, PresignExpirySeconds).ConfigureAwait(false);
            var thumb = p.ThumbnailUrl is null
                ? null
                : await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, p.ThumbnailUrl, PresignExpirySeconds).ConfigureAwait(false);
            photos.Add(new PlayRecordPhotoDto(p.Id, url, thumb, p.OcrText, p.Caption, p.UploadedByUserId, p.UploadedAt));
        }

        return new PlayRecordDto(
            entity.Id,
            entity.GameId,
            entity.GameName,
            entity.SessionDate,
            entity.Duration,
            (Domain.Enums.PlayRecordStatus)entity.Status,
            entity.Players.Select(p => new SessionPlayerDto(
                p.Id,
                p.UserId,
                p.DisplayName,
                p.Scores.Select(s => new SessionScoreDto(s.Dimension, s.Value, s.Unit)).ToList(),
                PlayRecordOutcomeCalculator.TotalScore(p)
            )).ToList(),
            scoringConfig,
            entity.CreatedByUserId,
            (Domain.Enums.PlayRecordVisibility)entity.Visibility,
            entity.StartTime,
            entity.EndTime,
            entity.Notes,
            entity.Location,
            entity.CreatedAt,
            entity.UpdatedAt,
            winnerPlayerIds,
            outcomeType,
            photos
        );
    }
}
```

- [ ] **Step 4: Verifica registrazione DI del handler**

Il handler è risolto via MediatR assembly-scan; `IBlobStorageService` è già registrato (lo usa l'upload handler). Nessuna modifica DI necessaria. Conferma con build.

Run: `cd apps/api/src/Api && dotnet build 2>&1 | grep -E "error" || echo BUILD_OK`
Expected: `BUILD_OK`

- [ ] **Step 5: Test verde (nuovo + esistenti del handler)**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~GetPlayRecordQueryHandlerTests" 2>&1 | tail -6`
Expected: PASS (tutti, incluso il nuovo `Handle_RecordWithPhotos_ReturnsPhotosOrderedByUploadedAt`).

- [ ] **Step 6: Commit (BE read-path completo)**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/PlayRecords/PlayRecordDto.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/PlayRecordPhotoUrlResolver.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UploadPlayRecordPhotoCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayRecordQueryHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/PlayRecords/GetPlayRecordQueryHandlerTests.cs
git commit -m "feat(play-records): #2436 PR-C expose photos in PlayRecordDto (presigned read-path)"
```

---

## Task 4: FE — Schema Zod `PlayRecordPhotoSchema` + `photos` su DTO

**Files:**
- Modify: `apps/web/src/lib/api/schemas/play-records.schemas.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/play-records-photo.schema.test.ts` (create)

- [ ] **Step 1: Scrivi il test che fallisce**

Create `apps/web/src/lib/api/schemas/__tests__/play-records-photo.schema.test.ts`:

```ts
import { describe, it, expect } from 'vitest';

import { PlayRecordDtoSchema, PlayRecordPhotoSchema } from '../play-records.schemas';

describe('PlayRecordPhotoSchema', () => {
  it('parses a full photo DTO', () => {
    const parsed = PlayRecordPhotoSchema.parse({
      id: '11111111-1111-1111-1111-111111111111',
      url: 'https://cdn.example/p.webp',
      thumbnailUrl: 'https://cdn.example/t.webp',
      ocrText: '42',
      caption: 'scoreboard',
      uploadedByUserId: '22222222-2222-2222-2222-222222222222',
      uploadedAt: '2026-06-20T10:00:00Z',
    });
    expect(parsed.url).toBe('https://cdn.example/p.webp');
  });

  it('accepts null thumbnail/ocr/caption', () => {
    const parsed = PlayRecordPhotoSchema.parse({
      id: '11111111-1111-1111-1111-111111111111',
      url: 'https://cdn.example/p.webp',
      thumbnailUrl: null,
      ocrText: null,
      caption: null,
      uploadedByUserId: '22222222-2222-2222-2222-222222222222',
      uploadedAt: '2026-06-20T10:00:00Z',
    });
    expect(parsed.thumbnailUrl).toBeNull();
  });

  it('PlayRecordDtoSchema treats photos as optional (BE rollout)', () => {
    const base = {
      id: '33333333-3333-3333-3333-333333333333',
      gameId: null,
      gameName: 'Catan',
      sessionDate: '2026-06-20',
      duration: null,
      status: 'Completed' as const,
      players: [],
      scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
      createdByUserId: '22222222-2222-2222-2222-222222222222',
      visibility: 'Private' as const,
      startTime: null,
      endTime: null,
      notes: null,
      location: null,
      createdAt: '2026-06-20T10:00:00Z',
      updatedAt: '2026-06-20T10:00:00Z',
    };
    expect(PlayRecordDtoSchema.parse(base).photos).toBeUndefined();
    expect(PlayRecordDtoSchema.parse({ ...base, photos: [] }).photos).toEqual([]);
  });
});
```

- [ ] **Step 2: Esegui → FAIL**

Run: `cd apps/web && pnpm test src/lib/api/schemas/__tests__/play-records-photo.schema.test.ts --run 2>&1 | tail -15`
Expected: FAIL — `PlayRecordPhotoSchema` non esportato.

- [ ] **Step 3: Aggiungi gli schemi**

In `play-records.schemas.ts`, dopo `PlayRecordOutcomeTypeSchema` (riga ~49) e prima di `// ========== DTOs ==========`, aggiungi:

```ts
// #2436 PR-C: photo attached to a play record (read path). url/thumbnailUrl are
// presigned download URLs (or raw paths on local storage).
export const PlayRecordPhotoSchema = z.object({
  id: z.string().uuid(),
  url: z.string(),
  thumbnailUrl: z.string().nullable(),
  ocrText: z.string().nullable(),
  caption: z.string().nullable(),
  uploadedByUserId: z.string().uuid(),
  uploadedAt: z.string(),
});
export type PlayRecordPhoto = z.infer<typeof PlayRecordPhotoSchema>;
```

Poi in `PlayRecordDtoSchema` (riga ~73), dopo `outcomeType: PlayRecordOutcomeTypeSchema.optional(),` aggiungi:

```ts
  // #2436 PR-C: photos exposed on read. Optional during BE rollout; tighten later.
  photos: z.array(PlayRecordPhotoSchema).optional(),
```

- [ ] **Step 4: Test verde**

Run: `cd apps/web && pnpm test src/lib/api/schemas/__tests__/play-records-photo.schema.test.ts --run 2>&1 | tail -6`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/play-records.schemas.ts apps/web/src/lib/api/schemas/__tests__/play-records-photo.schema.test.ts
git commit -m "feat(play-records): #2436 PR-C add PlayRecordPhoto schema + photos field"
```

---

## Task 5: FE — `playRecordsApi.uploadPhoto` (multipart)

**Files:**
- Modify: `apps/web/src/lib/api/play-records.api.ts`
- Test: `apps/web/src/lib/api/__tests__/play-records-upload-photo.test.ts` (create)

- [ ] **Step 1: Scrivi il test che fallisce**

Create `apps/web/src/lib/api/__tests__/play-records-upload-photo.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { playRecordsApi } from '../play-records.api';

describe('playRecordsApi.uploadPhoto', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('POSTs multipart with file + flags and returns the result', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        photoId: 'p1',
        photoUrl: 'https://cdn/p.webp',
        thumbnailUrl: null,
        ocrText: '42',
        wasDeduplicated: false,
      }),
    });

    const blob = new Blob(['x'], { type: 'image/jpeg' });
    const res = await playRecordsApi.uploadPhoto('rec-1', blob, {
      caption: 'board',
      extractScoreFromPhoto: true,
    });

    expect(res.photoId).toBe('p1');
    expect(res.ocrText).toBe('42');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain('/api/v1/play-records/rec-1/photos');
    expect(init.method).toBe('POST');
    expect(init.body).toBeInstanceOf(FormData);
    expect((init.body as FormData).get('extractScoreFromPhoto')).toBe('true');
    expect((init.body as FormData).get('caption')).toBe('board');
  });

  it('throws on non-ok response', async () => {
    const fetchMock = fetch as unknown as ReturnType<typeof vi.fn>;
    fetchMock.mockResolvedValue({ ok: false, status: 413, json: async () => ({ error: 'too big' }) });
    const blob = new Blob(['x'], { type: 'image/jpeg' });
    await expect(playRecordsApi.uploadPhoto('rec-1', blob, {})).rejects.toThrow('too big');
  });
});
```

- [ ] **Step 2: Esegui → FAIL**

Run: `cd apps/web && pnpm test src/lib/api/__tests__/play-records-upload-photo.test.ts --run 2>&1 | tail -12`
Expected: FAIL — `uploadPhoto` non esiste.

- [ ] **Step 3: Implementa `uploadPhoto`**

In `play-records.api.ts`: aggiungi il tipo result in cima (sotto gli import) e il metodo nel blocco Commands (dopo `updateRecord`, prima di `// ========== Queries ==========`):

```ts
export interface UploadPlayRecordPhotoResult {
  photoId: string;
  photoUrl: string;
  thumbnailUrl: string | null;
  ocrText: string | null;
  wasDeduplicated: boolean;
}
```

```ts
  /**
   * Upload a photo to an existing play record (multipart). #2436 PR-C.
   * Raw fetch — httpClient does not support multipart FormData.
   */
  async uploadPhoto(
    recordId: string,
    file: Blob,
    opts: { caption?: string; extractScoreFromPhoto?: boolean } = {}
  ): Promise<UploadPlayRecordPhotoResult> {
    const form = new FormData();
    form.append('file', file, file instanceof File ? file.name : 'photo.jpg');
    if (opts.extractScoreFromPhoto) form.append('extractScoreFromPhoto', 'true');
    if (opts.caption) form.append('caption', opts.caption);

    const res = await fetch(`${BASE_URL}/${recordId}/photos`, {
      method: 'POST',
      body: form,
      credentials: 'include',
      // Do NOT set Content-Type — browser sets the multipart boundary.
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to upload photo' }));
      throw new Error(error.error || error.message || 'Failed to upload photo');
    }
    return res.json();
  },
```

- [ ] **Step 4: Test verde**

Run: `cd apps/web && pnpm test src/lib/api/__tests__/play-records-upload-photo.test.ts --run 2>&1 | tail -6`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/play-records.api.ts apps/web/src/lib/api/__tests__/play-records-upload-photo.test.ts
git commit -m "feat(play-records): #2436 PR-C add uploadPhoto API client (multipart)"
```

---

## Task 6: FE — Hook `usePlayRecordPhotoUpload`

**Files:**
- Create: `apps/web/src/hooks/mutations/usePlayRecordPhotoUpload.ts`
- Test: `apps/web/src/hooks/mutations/__tests__/usePlayRecordPhotoUpload.test.tsx` (create)

> Verifica prima la query key usata da `usePlayRecord` per invalidare correttamente. Leggi `apps/web/src/lib/domain-hooks/usePlayRecords.ts` e usa la **stessa** key del `useQuery` di `usePlayRecord(recordId)` (sotto chiamata `playRecordDetailKey`). Se la key è inline (es. `['play-record', recordId]`), usa quell'array verbatim in `invalidateQueries`.

- [ ] **Step 1: Scrivi il test che fallisce**

Create `apps/web/src/hooks/mutations/__tests__/usePlayRecordPhotoUpload.test.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { playRecordsApi } from '@/lib/api/play-records.api';

import { usePlayRecordPhotoUpload } from '../usePlayRecordPhotoUpload';

function wrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('usePlayRecordPhotoUpload', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('uploads and invalidates the record detail query', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    vi.spyOn(playRecordsApi, 'uploadPhoto').mockResolvedValue({
      photoId: 'p1', photoUrl: 'u', thumbnailUrl: null, ocrText: null, wasDeduplicated: false,
    });

    const { result } = renderHook(() => usePlayRecordPhotoUpload('rec-1'), {
      wrapper: wrapper(client),
    });

    result.current.mutate({ file: new Blob(['x'], { type: 'image/jpeg' }) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(playRecordsApi.uploadPhoto).toHaveBeenCalledWith('rec-1', expect.any(Blob), {
      caption: undefined,
      extractScoreFromPhoto: undefined,
    });
    expect(invalidate).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Esegui → FAIL**

Run: `cd apps/web && pnpm test src/hooks/mutations/__tests__/usePlayRecordPhotoUpload.test.tsx --run 2>&1 | tail -12`
Expected: FAIL — hook inesistente.

- [ ] **Step 3: Implementa il hook**

Create `usePlayRecordPhotoUpload.ts` (sostituisci `playRecordDetailKey(recordId)` con la key reale verificata sopra):

```ts
/**
 * usePlayRecordPhotoUpload — mutation hook for POST /play-records/{id}/photos.
 * Invalidates the record detail query so the gallery refetches with the new photo.
 * #2436 PR-C.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { playRecordsApi, type UploadPlayRecordPhotoResult } from '@/lib/api/play-records.api';

export interface UploadPhotoVars {
  file: Blob;
  caption?: string;
  extractScoreFromPhoto?: boolean;
}

export function usePlayRecordPhotoUpload(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation<UploadPlayRecordPhotoResult, Error, UploadPhotoVars>({
    mutationFn: ({ file, caption, extractScoreFromPhoto }) =>
      playRecordsApi.uploadPhoto(recordId, file, { caption, extractScoreFromPhoto }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['play-record', recordId] });
    },
  });
}
```

- [ ] **Step 4: Test verde**

Run: `cd apps/web && pnpm test src/hooks/mutations/__tests__/usePlayRecordPhotoUpload.test.tsx --run 2>&1 | tail -6`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/mutations/usePlayRecordPhotoUpload.ts apps/web/src/hooks/mutations/__tests__/usePlayRecordPhotoUpload.test.tsx
git commit -m "feat(play-records): #2436 PR-C add usePlayRecordPhotoUpload hook"
```

---

## Task 7: FE — i18n `playRecords.photos` (it + en)

**Files:**
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`

> Localizza la chiave `playRecords` (già presente con `stats`, `new`). Aggiungi un oggetto `photos` come sibling. Mantieni l'ordine alfabetico/coerente con i sibling esistenti e **identica struttura** tra it ed en (un test MESSAGES verifica la parità delle chiavi — vedi memory i18n-runtime-catalog-gap).

- [ ] **Step 1: Aggiungi `photos` in `it.json`** (dentro `"playRecords": { ... }`)

```json
"photos": {
  "sectionTitle": "Foto della partita",
  "addButton": "Aggiungi foto",
  "dialogTitle": "Carica foto",
  "dialogDescription": "JPG, PNG, WebP o HEIC · max 5MB · fino a 10 foto.",
  "selectLabel": "Seleziona foto",
  "captionLabel": "Didascalia (opzionale)",
  "captionPlaceholder": "Es. tabellone finale",
  "extractScoreLabel": "Estrai il punteggio dalla foto (OCR)",
  "ocrResultTitle": "Testo rilevato",
  "uploadCta": "Carica",
  "uploading": "Caricamento…",
  "emptyTitle": "Nessuna foto",
  "emptyDescription": "Carica una foto del tabellone o del tavolo.",
  "photoAltFallback": "Foto della partita",
  "dedupToast": "Questa foto è già presente.",
  "uploadError": "Caricamento foto non riuscito.",
  "tooLarge": "File troppo grande, massimo 5MB.",
  "tooMany": "Massimo 10 foto per partita.",
  "badFormat": "Formato non supportato. Usa JPG, PNG, WebP o HEIC.",
  "heicFailed": "Conversione HEIC fallita. Riprova con JPEG o PNG.",
  "lightboxClose": "Chiudi",
  "lightboxPrev": "Precedente",
  "lightboxNext": "Successiva"
}
```

- [ ] **Step 2: Aggiungi `photos` in `en.json`** (stessa struttura)

```json
"photos": {
  "sectionTitle": "Game photos",
  "addButton": "Add photo",
  "dialogTitle": "Upload photo",
  "dialogDescription": "JPG, PNG, WebP or HEIC · max 5MB · up to 10 photos.",
  "selectLabel": "Select photo",
  "captionLabel": "Caption (optional)",
  "captionPlaceholder": "E.g. final board",
  "extractScoreLabel": "Extract score from photo (OCR)",
  "ocrResultTitle": "Detected text",
  "uploadCta": "Upload",
  "uploading": "Uploading…",
  "emptyTitle": "No photos",
  "emptyDescription": "Upload a photo of the board or the table.",
  "photoAltFallback": "Game photo",
  "dedupToast": "This photo is already attached.",
  "uploadError": "Photo upload failed.",
  "tooLarge": "File too large, max 5MB.",
  "tooMany": "Max 10 photos per game.",
  "badFormat": "Unsupported format. Use JPG, PNG, WebP or HEIC.",
  "heicFailed": "HEIC conversion failed. Try JPEG or PNG.",
  "lightboxClose": "Close",
  "lightboxPrev": "Previous",
  "lightboxNext": "Next"
}
```

- [ ] **Step 3: Verifica parità chiavi + typecheck**

Run: `cd apps/web && pnpm test --run -t "MESSAGES" 2>&1 | tail -10`
Expected: PASS (o nessun test MESSAGES → prosegui). Poi `pnpm typecheck 2>&1 | tail -5` → nessun errore.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(play-records): #2436 PR-C add playRecords.photos i18n (it/en)"
```

---

## Task 8: FE — `PlayRecordPhotoUploadDialog` (multi-file, heic2any, OCR toggle)

**Files:**
- Create: `apps/web/src/components/play-records/photos/PlayRecordPhotoUploadDialog.tsx`
- Test: `apps/web/src/components/play-records/photos/__tests__/PlayRecordPhotoUploadDialog.test.tsx` (create)

**Design:** Dialog con input file (`multiple`), per ogni file: valida MIME (jpeg/png/webp/heic) + size ≤5MB (HEIC convertito a JPEG **prima** del check con `heic2any`), enforce ≤10 totali. Toggle OCR (applica `extractScoreFromPhoto` a tutti). Caption opzionale singola applicata a tutti i file del batch. Upload sequenziale via il hook; `WasDeduplicated` → toast info. No crop.

- [ ] **Step 1: Scrivi il test che fallisce**

Create `__tests__/PlayRecordPhotoUploadDialog.test.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { playRecordsApi } from '@/lib/api/play-records.api';

import { PlayRecordPhotoUploadDialog } from '../PlayRecordPhotoUploadDialog';

function wrap(node: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{node}</QueryClientProvider>;
}

describe('PlayRecordPhotoUploadDialog', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('rejects files over 5MB', async () => {
    render(wrap(<PlayRecordPhotoUploadDialog recordId="r1" open onClose={() => {}} />));
    const big = new File([new Uint8Array(6 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });
    const input = screen.getByLabelText(/seleziona foto|select photo/i) as HTMLInputElement;
    fireEvent.change(input, { target: { files: [big] } });
    expect(await screen.findByText(/massimo 5MB|max 5MB/i)).toBeInTheDocument();
  });

  it('uploads a valid jpeg with OCR flag', async () => {
    const spy = vi.spyOn(playRecordsApi, 'uploadPhoto').mockResolvedValue({
      photoId: 'p1', photoUrl: 'u', thumbnailUrl: null, ocrText: '42', wasDeduplicated: false,
    });
    render(wrap(<PlayRecordPhotoUploadDialog recordId="r1" open onClose={() => {}} />));
    const file = new File(['x'], 'ok.jpg', { type: 'image/jpeg' });
    fireEvent.change(screen.getByLabelText(/seleziona foto|select photo/i), { target: { files: [file] } });
    fireEvent.click(screen.getByLabelText(/estrai il punteggio|extract score/i));
    fireEvent.click(screen.getByRole('button', { name: /carica|upload/i }));
    await waitFor(() => expect(spy).toHaveBeenCalledWith('r1', expect.any(File), expect.objectContaining({ extractScoreFromPhoto: true })));
  });
});
```

- [ ] **Step 2: Esegui → FAIL**

Run: `cd apps/web && pnpm test src/components/play-records/photos/__tests__/PlayRecordPhotoUploadDialog.test.tsx --run 2>&1 | tail -12`
Expected: FAIL — componente inesistente.

- [ ] **Step 3: Implementa il componente**

Create `PlayRecordPhotoUploadDialog.tsx`:

```tsx
'use client';

import { useState, useCallback } from 'react';

import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { usePlayRecordPhotoUpload } from '@/hooks/mutations/usePlayRecordPhotoUpload';

const MAX_BYTES = 5 * 1024 * 1024; // 5MB — matches BE validator
const MAX_FILES = 10;
const ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/heic'];

export interface PlayRecordPhotoUploadDialogProps {
  recordId: string;
  open: boolean;
  onClose: () => void;
}

export function PlayRecordPhotoUploadDialog({
  recordId,
  open,
  onClose,
}: PlayRecordPhotoUploadDialogProps): React.JSX.Element {
  const { t } = useTranslation();
  const [files, setFiles] = useState<File[]>([]);
  const [caption, setCaption] = useState('');
  const [extractScore, setExtractScore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const upload = usePlayRecordPhotoUpload(recordId);

  const handleSelect = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      setError(null);
      const selected = Array.from(e.target.files ?? []);
      if (selected.length === 0) return;
      if (selected.length > MAX_FILES) {
        setError(t('playRecords.photos.tooMany'));
        return;
      }

      const out: File[] = [];
      for (const file of selected) {
        if (!ACCEPTED_MIME.includes(file.type)) {
          setError(t('playRecords.photos.badFormat'));
          return;
        }
        let candidate = file;
        if (file.type === 'image/heic') {
          try {
            const heic2any = (await import('heic2any')).default;
            const result = await heic2any({ blob: file, toType: 'image/jpeg', quality: 0.9 });
            const jpegBlob = Array.isArray(result) ? result[0] : result;
            candidate = new File([jpegBlob], file.name.replace(/\.heic$/i, '.jpg'), {
              type: 'image/jpeg',
            });
          } catch {
            setError(t('playRecords.photos.heicFailed'));
            return;
          }
        }
        if (candidate.size > MAX_BYTES) {
          setError(t('playRecords.photos.tooLarge'));
          return;
        }
        out.push(candidate);
      }
      setFiles(out);
    },
    [t]
  );

  const handleUpload = useCallback(async () => {
    setError(null);
    try {
      for (const file of files) {
        const res = await upload.mutateAsync({
          file,
          caption: caption || undefined,
          extractScoreFromPhoto: extractScore || undefined,
        });
        if (res.wasDeduplicated) {
          toast.info(t('playRecords.photos.dedupToast'));
        } else if (res.ocrText) {
          toast.success(`${t('playRecords.photos.ocrResultTitle')}: ${res.ocrText}`);
        }
      }
      setFiles([]);
      setCaption('');
      setExtractScore(false);
      onClose();
    } catch {
      setError(t('playRecords.photos.uploadError'));
    }
  }, [files, caption, extractScore, upload, t, onClose]);

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent>
        <DialogTitle>{t('playRecords.photos.dialogTitle')}</DialogTitle>
        <DialogDescription>{t('playRecords.photos.dialogDescription')}</DialogDescription>

        <label className="block">
          <span className="text-sm font-medium">{t('playRecords.photos.selectLabel')}</span>
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp,image/heic"
            multiple
            capture="environment"
            onChange={handleSelect}
            className="mt-1 block w-full"
          />
        </label>

        {files.length > 0 && (
          <p className="text-sm text-muted-foreground">{files.map(f => f.name).join(', ')}</p>
        )}

        <label className="block">
          <span className="text-sm font-medium">{t('playRecords.photos.captionLabel')}</span>
          <input
            type="text"
            value={caption}
            maxLength={500}
            onChange={e => setCaption(e.target.value)}
            placeholder={t('playRecords.photos.captionPlaceholder')}
            className="mt-1 block w-full rounded-md border border-border bg-card px-3 py-1.5 text-sm"
          />
        </label>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={extractScore}
            onChange={e => setExtractScore(e.target.checked)}
          />
          {t('playRecords.photos.extractScoreLabel')}
        </label>

        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button
            onClick={handleUpload}
            disabled={files.length === 0 || upload.isPending}
          >
            {upload.isPending
              ? t('playRecords.photos.uploading')
              : t('playRecords.photos.uploadCta')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Test verde**

Run: `cd apps/web && pnpm test src/components/play-records/photos/__tests__/PlayRecordPhotoUploadDialog.test.tsx --run 2>&1 | tail -8`
Expected: PASS

> Se `useTranslation` nei test richiede un provider/mock, segui il pattern dei test esistenti in `apps/web/src/components/play-records/__tests__/` (es. `PlayHistory.test.tsx`) — di norma `useTranslation` ritorna la chiave o un mock globale; adatta i matcher di testo di conseguenza.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/play-records/photos/PlayRecordPhotoUploadDialog.tsx apps/web/src/components/play-records/photos/__tests__/PlayRecordPhotoUploadDialog.test.tsx
git commit -m "feat(play-records): #2436 PR-C photo upload dialog (multi-file, heic2any, OCR toggle)"
```

---

## Task 9: FE — `PlayRecordPhotoGallery` (grid + lightbox)

**Files:**
- Create: `apps/web/src/components/play-records/photos/PlayRecordPhotoGallery.tsx`
- Test: `apps/web/src/components/play-records/photos/__tests__/PlayRecordPhotoGallery.test.tsx` (create)

**Design:** grid responsive (2-col mobile / 4-col desktop) seguendo il pattern di `PhotosGallery.tsx`. Click su una tile → apre un lightbox (Dialog) con la foto a piena risoluzione + prev/next + caption + (se presente) OCR text. Empty state quando `photos` è vuoto. Pure-ish: riceve `photos: PlayRecordPhoto[]` + labels via i18n risolte dall'orchestrator. A11y: tile sono `<button>`, lightbox con `aria-label` close/prev/next, navigazione con frecce.

- [ ] **Step 1: Scrivi il test che fallisce**

Create `__tests__/PlayRecordPhotoGallery.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { PlayRecordPhoto } from '@/lib/api/schemas/play-records.schemas';

import { PlayRecordPhotoGallery } from '../PlayRecordPhotoGallery';

const labels = {
  title: 'Foto',
  emptyTitle: 'Nessuna foto',
  emptyDescription: 'Carica una foto',
  photoAltFallback: 'Foto',
  ocrResultTitle: 'Testo',
  close: 'Chiudi',
  prev: 'Prec',
  next: 'Succ',
};

const photos: PlayRecordPhoto[] = [
  { id: 'a', url: 'http://x/a.webp', thumbnailUrl: null, ocrText: '42', caption: 'board', uploadedByUserId: 'u', uploadedAt: '2026-06-20T09:00:00Z' },
  { id: 'b', url: 'http://x/b.webp', thumbnailUrl: null, ocrText: null, caption: null, uploadedByUserId: 'u', uploadedAt: '2026-06-20T10:00:00Z' },
];

describe('PlayRecordPhotoGallery', () => {
  it('renders empty state', () => {
    render(<PlayRecordPhotoGallery photos={[]} labels={labels} />);
    expect(screen.getByText('Nessuna foto')).toBeInTheDocument();
  });

  it('opens lightbox on tile click and navigates', () => {
    render(<PlayRecordPhotoGallery photos={photos} labels={labels} />);
    fireEvent.click(screen.getByRole('button', { name: 'board' }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    // OCR text of first photo visible
    expect(screen.getByText(/42/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Succ' }));
    // second photo has no caption → alt fallback
    expect(screen.getByAltText('Foto')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui → FAIL**

Run: `cd apps/web && pnpm test src/components/play-records/photos/__tests__/PlayRecordPhotoGallery.test.tsx --run 2>&1 | tail -12`
Expected: FAIL — componente inesistente.

- [ ] **Step 3: Implementa il componente**

Create `PlayRecordPhotoGallery.tsx`:

```tsx
'use client';

import { useState, useCallback } from 'react';

import clsx from 'clsx';

import {
  Dialog,
  DialogContent,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import type { PlayRecordPhoto } from '@/lib/api/schemas/play-records.schemas';

export interface PlayRecordPhotoGalleryLabels {
  title: string;
  emptyTitle: string;
  emptyDescription: string;
  photoAltFallback: string;
  ocrResultTitle: string;
  close: string;
  prev: string;
  next: string;
}

export interface PlayRecordPhotoGalleryProps {
  photos: readonly PlayRecordPhoto[];
  labels: PlayRecordPhotoGalleryLabels;
  className?: string;
}

export function PlayRecordPhotoGallery({
  photos,
  labels,
  className,
}: PlayRecordPhotoGalleryProps): React.JSX.Element {
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  const close = useCallback(() => setOpenIndex(null), []);
  const prev = useCallback(
    () => setOpenIndex(i => (i === null ? null : (i - 1 + photos.length) % photos.length)),
    [photos.length]
  );
  const next = useCallback(
    () => setOpenIndex(i => (i === null ? null : (i + 1) % photos.length)),
    [photos.length]
  );

  if (photos.length === 0) {
    return (
      <section
        data-slot="play-record-photos"
        data-empty="true"
        role="status"
        className={clsx(
          'flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-card px-4 py-8 text-center',
          className
        )}
      >
        <span aria-hidden="true" className="text-3xl">📷</span>
        <h3 className="font-display text-sm font-extrabold text-foreground">{labels.emptyTitle}</h3>
        <p className="text-xs text-muted-foreground">{labels.emptyDescription}</p>
      </section>
    );
  }

  const active = openIndex !== null ? photos[openIndex] : null;

  return (
    <section data-slot="play-record-photos" className={clsx('flex flex-col gap-2', className)}>
      <h3 className="font-display text-base font-extrabold text-foreground">
        <span aria-hidden="true" className="mr-1.5">📷</span>
        {labels.title}
      </h3>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {photos.map((p, i) => {
          const alt = p.caption ?? labels.photoAltFallback;
          return (
            <button
              key={p.id}
              type="button"
              onClick={() => setOpenIndex(i)}
              aria-label={alt}
              className="group relative aspect-square overflow-hidden rounded-md border border-border bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {/* eslint-disable-next-line @next/next/no-img-element -- thumbnail tile */}
              <img
                src={p.thumbnailUrl ?? p.url}
                alt={alt}
                loading="lazy"
                className="h-full w-full object-cover transition-transform group-hover:scale-105"
              />
            </button>
          );
        })}
      </div>

      <Dialog open={active !== null} onOpenChange={o => !o && close()}>
        <DialogContent className="max-w-3xl">
          <DialogTitle className="sr-only">{active?.caption ?? labels.photoAltFallback}</DialogTitle>
          {active && (
            <div className="flex flex-col gap-3">
              {/* eslint-disable-next-line @next/next/no-img-element -- full-res lightbox */}
              <img
                src={active.url}
                alt={active.caption ?? labels.photoAltFallback}
                className="max-h-[70vh] w-full rounded-md object-contain"
              />
              {active.caption && (
                <p className="text-sm font-medium text-foreground">{active.caption}</p>
              )}
              {active.ocrText && (
                <p className="rounded-md border border-border bg-muted px-3 py-2 text-xs text-muted-foreground">
                  <span className="font-semibold">{labels.ocrResultTitle}:</span> {active.ocrText}
                </p>
              )}
              {photos.length > 1 && (
                <div className="flex justify-between">
                  <button type="button" onClick={prev} aria-label={labels.prev} className="rounded-md px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted">
                    ← {labels.prev}
                  </button>
                  <button type="button" onClick={next} aria-label={labels.next} className="rounded-md px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted">
                    {labels.next} →
                  </button>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </section>
  );
}
```

- [ ] **Step 4: Test verde**

Run: `cd apps/web && pnpm test src/components/play-records/photos/__tests__/PlayRecordPhotoGallery.test.tsx --run 2>&1 | tail -8`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/play-records/photos/PlayRecordPhotoGallery.tsx apps/web/src/components/play-records/photos/__tests__/PlayRecordPhotoGallery.test.tsx
git commit -m "feat(play-records): #2436 PR-C photo gallery + lightbox component"
```

---

## Task 10: FE — Mount gallery + upload button in `PlayRecordDetailView`

**Files:**
- Modify: `apps/web/src/components/play-records/PlayRecordDetailView.tsx`
- Modify: `apps/web/src/components/play-records/__tests__/PlayRecordDetailView.test.tsx`

**Design:** sotto la KpiGrid (riga ~343), prima della Classifica, monta una sezione foto: la gallery (sempre, mostra empty state) + il pulsante "Aggiungi foto" **solo se** `currentUserId === record.createdByUserId`. Il pulsante apre `PlayRecordPhotoUploadDialog`.

- [ ] **Step 1: Aggiungi un test che fallisce nel test esistente**

In `PlayRecordDetailView.test.tsx` aggiungi un caso: con un record che ha `createdByUserId` uguale all'utente corrente e una foto, la gallery section è presente e mostra il pulsante upload. (Riusa il mock di `usePlayRecord` già presente nel file; aggiungi `photos: [...]` al record mockato e `createdByUserId` allineato al mock di `useCurrentUser`.)

```tsx
  it('shows the photo gallery and the creator-only add button', () => {
    // arrange: mock usePlayRecord to return a record with one photo, createdByUserId === current user
    // (segui il pattern di mocking già usato in questo file per usePlayRecord/useCurrentUser)
    renderDetail({
      createdByUserId: 'user-1',
      photos: [
        { id: 'ph1', url: 'http://x/a.webp', thumbnailUrl: null, ocrText: null, caption: 'board', uploadedByUserId: 'user-1', uploadedAt: '2026-06-20T10:00:00Z' },
      ],
    });
    expect(screen.getByRole('button', { name: /aggiungi foto|add photo/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'board' })).toBeInTheDocument();
  });
```

> Adatta `renderDetail(...)` / i mock al pattern reale del file (leggi l'header del test per il setup di `usePlayRecord`/`useCurrentUser`). Se il file non ha un helper `renderDetail`, replica il `render(<PlayRecordDetailView recordId=... />)` con i `vi.mock` già definiti.

- [ ] **Step 2: Esegui → FAIL**

Run: `cd apps/web && pnpm test src/components/play-records/__tests__/PlayRecordDetailView.test.tsx --run 2>&1 | tail -12`
Expected: FAIL — nessun pulsante "Aggiungi foto".

- [ ] **Step 3: Implementa il mount**

In `PlayRecordDetailView.tsx`: aggiungi gli import e uno stato per il dialog; monta la sezione. Import in cima (con gli altri import locali):

```tsx
import { useState } from 'react';
// ...
import { PlayRecordPhotoGallery } from './photos/PlayRecordPhotoGallery';
import { PlayRecordPhotoUploadDialog } from './photos/PlayRecordPhotoUploadDialog';
```

Dentro il componente, dopo `const { data: record, isLoading, error } = usePlayRecord(recordId);` aggiungi:

```tsx
  const [photoDialogOpen, setPhotoDialogOpen] = useState(false);
```

Dopo aver calcolato `currentUserId` (riga ~252) aggiungi:

```tsx
  const isCreator = currentUserId !== null && currentUserId === record.createdByUserId;
```

Nel JSX, subito dopo il blocco `<KpiGrid ... />` (riga ~343, dentro il container `max-w-4xl`) inserisci:

```tsx
        {/* Photos — #2436 PR-C */}
        <section aria-label={t('playRecords.photos.sectionTitle')} className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="sr-only">{t('playRecords.photos.sectionTitle')}</span>
            {isCreator && (
              <button
                type="button"
                onClick={() => setPhotoDialogOpen(true)}
                className="ml-auto rounded-md border border-border px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
              >
                📷 {t('playRecords.photos.addButton')}
              </button>
            )}
          </div>
          <PlayRecordPhotoGallery
            photos={record.photos ?? []}
            labels={{
              title: t('playRecords.photos.sectionTitle'),
              emptyTitle: t('playRecords.photos.emptyTitle'),
              emptyDescription: t('playRecords.photos.emptyDescription'),
              photoAltFallback: t('playRecords.photos.photoAltFallback'),
              ocrResultTitle: t('playRecords.photos.ocrResultTitle'),
              close: t('playRecords.photos.lightboxClose'),
              prev: t('playRecords.photos.lightboxPrev'),
              next: t('playRecords.photos.lightboxNext'),
            }}
          />
        </section>

        {isCreator && (
          <PlayRecordPhotoUploadDialog
            recordId={recordId}
            open={photoDialogOpen}
            onClose={() => setPhotoDialogOpen(false)}
          />
        )}
```

- [ ] **Step 4: Test verde + suite detail**

Run: `cd apps/web && pnpm test src/components/play-records/__tests__/PlayRecordDetailView.test.tsx --run 2>&1 | tail -8`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/play-records/PlayRecordDetailView.tsx apps/web/src/components/play-records/__tests__/PlayRecordDetailView.test.tsx
git commit -m "feat(play-records): #2436 PR-C mount photo gallery + creator-only upload in detail view"
```

---

## Task 11: FE — A11y + full-suite verification

**Files:**
- Modify (se serve): `apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx`

- [ ] **Step 1: Aggiungi uno smoke axe per la gallery (stato con foto + empty)**

Nel file axe esistente aggiungi un render della `PlayRecordPhotoGallery` (con 1 foto e con 0 foto) dentro un `axe(container)` assert `toHaveNoViolations()`. Segui il pattern degli altri blocchi del file.

- [ ] **Step 2: Esegui axe**

Run: `cd apps/web && pnpm test src/components/play-records/__tests__/play-records-axe.test.tsx --run 2>&1 | tail -8`
Expected: PASS — 0 violazioni (in particolare 0 color-contrast, gate AA blocking).

- [ ] **Step 3: Typecheck + lint (incl. token + BGG gates)**

Run: `cd apps/web && pnpm typecheck && pnpm lint 2>&1 | tail -20`
Expected: nessun errore. In particolare `local/no-hardcoded-color-utility` (i nuovi componenti usano solo token semantici `bg-card`/`text-foreground`/`border-border`/`bg-muted`/`text-destructive`).

- [ ] **Step 4: Suite play-records completa**

Run: `cd apps/web && pnpm test src/components/play-records src/hooks/mutations src/lib/api --run 2>&1 | tail -15`
Expected: PASS (0 regressioni rispetto alla baseline).

- [ ] **Step 5: Build BE finale (sanity)**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | grep -E "error" || echo BUILD_OK`
Expected: `BUILD_OK`

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/play-records/__tests__/play-records-axe.test.tsx
git commit -m "test(play-records): #2436 PR-C axe smoke for photo gallery"
```

---

## Self-review notes (per l'esecutore)

- **Verifica query key** (Task 6): `['play-record', recordId]` è un'ipotesi — confermala leggendo `usePlayRecord` in `apps/web/src/lib/domain-hooks/usePlayRecords.ts` e allinea l'`invalidateQueries`. Se diversa, aggiorna anche il test del Task 6.
- **Pattern `useTranslation` nei test**: alcuni test usano un mock che ritorna la chiave; i matcher regex (`/aggiungi foto|add photo/i`) coprono entrambi i casi (label tradotta o chiave). Se il file detail usa già un mock specifico, riusalo.
- **Local storage**: in dev/test le foto non si presignano (URL = raw path), identico al comportamento dell'upload handler — non è una regressione. In staging/prod (S3/R2) il presigning è attivo.
- **Confine**: nessun upload nel create wizard (record inesistente); upload solo dalla detail view creator-only. Conflict-UI/share/audit/MVP sono fuori da questo PR (→ #2437).
- **DEC C1 coperta** da Task 1+3. **OCR (DEC-3)** coperto da toggle Task 8 + display read-only (m3, no auto-apply). **Dedup (m2)** Task 8. **Limiti 5MB/MIME (m1)** Task 8. **i18n (m4)** Task 7. **Gallery una sola volta (m5)** Task 9/10.

## Closing (a fine implementazione)
- PR verso **main-dev** (parent). Titolo: `feat(play-records): #2436 PR-C photo upload UI + gallery + OCR toggle (FE) + DTO read-path (BE)`.
- Body: linka #2436, riassumi C1, elenca i task, nota che #2436 è completo dopo questo merge (PR-A+PR-B+PR-C) → chiudi #2436.
- Code-review prima del merge (superpowers:requesting-code-review o feature-dev:code-reviewer).
