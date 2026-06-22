# L3 Custom Cover Upload Design — Issue #1824

**Status**: Spec — pending user review
**Issue**: [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) (Umbrella: [#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821))
**Effort estimate**: ~5h (BE 2h + FE 2h + tests 1h)
**Date**: 2026-06-04
**Branch**: `feature/issue-1824-custom-cover-upload` (parent: `main-dev`)

## Summary

User-uploaded custom cover image per game-in-library (utente carica foto della propria copia fisica). FE-heavy con manual crop + client-side webp encode. Estensione del 4-layer cover priority chain già esistente (post-#1831 close): `CustomCoverR2Key` (L3, NEW) → `PdfDocumentEntity.CoverR2Key` (L4, shipped) → `<GameCoverPlaceholder>` (L1, shipped). L2 Wikidata (#1823) sarà reinserita tra L4 e L1 quando shippata.

## Goals & Non-goals

### Goals
- User può caricare custom cover per qualsiasi gioco nella sua library
- UI accessibile su mobile (camera capture iOS Safari + Android Chrome) e desktop (file picker)
- Crop manuale draggable + zoom (aspect 200×300 lockato)
- Privacy: EXIF GPS strip automatico (canvas re-encode)
- HEIC iOS supportato via `heic2any` lazy-loaded
- Cleanup R2 automatico on game removal from library (event-driven)

### Non-goals
- Cleanup on user account deletion (no `UserDeletedDomainEvent` esistente — deferred a Phase 2)
- AI cover generation (separate evaluation)
- BGG cover fetch (forever excluded — vedi #1821 ToS analysis)
- Cover sharing tra utenti (cover è strettamente per-user, non community catalog)
- Edit/re-crop di cover esistente (è replace via re-upload; no in-place edit modal)

## Stakeholders & Use Cases

### Primary actor
**End user** (autenticato, ha almeno 1 game in library)

### Use Case 1 — Happy path upload (DEC-1+2+3)
1. User Maria apre `/library/{catan-uuid}` (game detail)
2. Hover sul hero cover → vede icon edit overlay top-right
3. Click icon → modale crop si apre con file picker
4. Seleziona foto Catan dalla galleria (5MB JPG)
5. FE auto-detect aspect, suggerisce crop ratio 2:3 (200×300)
6. Maria drag corner per ridurre rectangle, pinch-zoom per zoomare in
7. Click "Conferma crop" → canvas encode webp q=0.8 → 145KB
8. POST `/api/v1/library/{catan-uuid}/cover` multipart
9. BE: valida (auth + size + format + gameId ∈ library) → R2 upload → DB `CustomCoverR2Key = 'user-covers/maria-uuid/catan-uuid/cover.webp'` → 201 response
10. FE: invalida `useLibraryGameDetail` query → library card mostra nuova cover entro 2s
11. **SLO p95 ≤ 5s** dal click "Conferma" al render della nuova cover

### Use Case 2 — Replace existing cover
1. Maria ha già custom cover per Catan
2. Click icon edit → modale crop con file picker
3. Seleziona nuova foto → crop → conferma
4. POST `/api/v1/library/{catan-uuid}/cover` → BE deletes vecchio R2 object → uploads new → updates DB (idempotent replace via row version)
5. FE refresh mostra nuova cover

### Use Case 3 — Remove custom cover (fallback chain)
1. Maria ha custom cover per Catan
2. Click icon edit → modale crop mostra anche bottone "Rimuovi cover personalizzata"
3. Click "Rimuovi" → conferma dialog "Sei sicura? Tornerà la copertina automatica."
4. DELETE `/api/v1/library/{catan-uuid}/cover` → BE: 204 + R2 delete + DB `CustomCoverR2Key = null`
5. FE refresh → fallback a L4 PDF cover (se PDF esiste) → o L1 placeholder

### Use Case 4 — Error: HEIC iOS
1. Maria su iPhone (Safari) → click icon edit → seleziona foto Catan da Camera Roll
2. File è HEIC by default iOS
3. FE detect MIME `image/heic` → lazy-import `heic2any` (~80KB)
4. `heic2any` convert HEIC → JPEG blob (decode + re-encode native)
5. Canvas processa JPEG → webp → upload normale
6. Recovery transparent: utente non vede HEIC complexity

### Use Case 5 — Error: image too large output (Crispin SLO)
1. User carica foto 12MB high-res
2. FE pre-validation: input ≤10MB? NO → errore "File troppo grande, massimo 10MB"
3. User downscale o sceglie diversa foto

### Use Case 6 — Error: non-comprimibile a ≤200KB
1. User carica foto con dettaglio extreme (texture nessun compress)
2. Canvas encode q=0.8 → 350KB (>200KB)
3. FE iterate q-=0.1: q=0.7 → 280KB; q=0.6 → 230KB; q=0.5 → 190KB ✓
4. Upload success a q=0.5

Se q=0.4 produce ancora >200KB:
- Errore client-side: "Immagine troppo complessa per comprimere, riprova con foto più semplice (es. solo la scatola)"
- Nessun upload tentato

## Requirements (AC) — Wiegers

### AC-R1 — Database (DB column already exists)
✅ Migration `AddCoverColumnsL2AndL3` (shipped #1839) added `UserLibraryEntryEntity.CustomCoverR2Key VARCHAR(512) NULL`

### AC-R2 — Upload endpoint
- POST `/api/v1/library/{gameId}/cover` multipart/form-data
- Auth required (session cookie or JWT — pattern esistente)
- Returns `201 Created` con body `{ coverR2Key: string, presignedUrl: string }`
- Validates: format (jpg/png/webp/heic), size (max 10MB raw), MIME type
- Replaces existing custom cover (best-effort R2 delete before new upload)

### AC-R3 — Delete endpoint
- DELETE `/api/v1/library/{gameId}/cover`
- Auth required
- Returns `204 No Content`
- R2 cleanup (best-effort, log warning on fail)
- DB `CustomCoverR2Key = null`

### AC-R4 — FE crop UI (`react-easy-crop`)
- Aspect lockato 2:3 (200×300)
- Pinch-zoom mobile + scroll-wheel desktop
- Drag-to-pan + corner resize
- A11y keyboard support (arrow keys per movimento, +/- per zoom, Enter conferma, Esc annulla)
- Mobile: modale full-screen; Desktop: centered dialog ≥600px

### AC-R5 — Size compression behavior (NEW Wiegers)
- **GIVEN** un'immagine input ≤10MB
- **WHEN** encode webp client-side
- **THEN** output ≤200KB con quality ≥0.4
- **SE** non comprimibile a q=0.4, errore client-side con messaggio user-friendly

### AC-R6 — Cleanup on game removal
- `GameRemovedFromLibraryEvent` handler triggera R2 delete dei custom cover associati
- Idempotent: event può essere replayed senza side effects
- Best-effort: log warning su R2 fail, no propagation (pattern #1873)

### AC-R7 — Render priority chain extension
- `CoverUrlResolver` (BE) estende priority: `CustomCoverR2Key` first, then existing L4 + L1 logic
- Games query handler returns `coverUrl` presigned con CustomCoverR2Key se esistente
- FE `MeepleCard.Cover` consume `coverUrl` server-resolved (no FE priority logic)

### AC-R8 — No game restriction (NEW Wiegers)
- 100% library entries possono avere custom cover (shared games + private games)
- Nessuna restrizione su BGG-noti vs custom (private) games

### AC-R9 — gameId ∈ library validation (NEW Wiegers)
- POST/DELETE su gameId NON in user library → `403 Forbidden`
- Validator BE: check `UserLibraryEntryRepository.GetByUserAndGameAsync(userId, gameId)` returns non-null

### AC-R10 — Privacy/GDPR
- EXIF GPS strip automatico via canvas re-encode (no metadata leak)
- Unit test asserta ASSENZA EXIF in output blob
- Documentazione manual deletion procedure (account deletion → ops manual)

### AC-R11 — Mobile UX (Crispin)
- iOS Safari camera capture: `<input accept="image/*" capture="environment">` works
- Android Chrome gallery + camera: works
- HEIC iOS via `heic2any` lazy-loaded
- Manual QA matrix nel PR test plan (iOS Safari real + Android Chrome real)

### AC-R12 — Performance SLO (NEW Crispin)
- p95 end-to-end < 5s dal click "Conferma crop" al render della nuova cover in library card
- Measure: client telemetry o E2E test con timing

## Architecture

### Cross-BC integration

```
UserLibrary BC (primary)
├── Domain
│   └── Entities/UserLibraryEntry.cs (CustomCoverR2Key field, shipped #1839)
├── Application
│   ├── Commands/UploadCustomCoverCommand.cs (NEW)
│   ├── Commands/UploadCustomCoverCommandHandler.cs (NEW)
│   ├── Commands/RemoveCustomCoverCommand.cs (NEW)
│   ├── Commands/RemoveCustomCoverCommandHandler.cs (NEW)
│   ├── Validators/UploadCustomCoverCommandValidator.cs (NEW)
│   ├── EventHandlers/GameRemovedFromLibraryCustomCoverHandler.cs (NEW — best-effort R2 cleanup)
│   └── Queries (existing — extend CoverUrlResolver consumption)
└── Routing/UserLibraryCoverEndpoints.cs (NEW)

KnowledgeBase BC (existing CoverUrlResolver from #1852)
└── Application/CoverUrlResolver.cs (EXTEND — add L3 priority)

Storage Service (existing)
└── IBlobStorageService.cs (reuse UploadAsync, RetrieveAsync, DeleteAsync, GeneratePresignedUrlAsync)
```

### CoverUrlResolver priority chain (post-L3 ship)

```csharp
public async Task<string?> ResolveCoverUrlAsync(Guid gameId, Guid userId, CoverSize size, CancellationToken ct)
{
    // L3 CustomCover (NEW priority)
    var customKey = await _userLibraryQuery.GetCustomCoverR2KeyAsync(userId, gameId, ct);
    if (!string.IsNullOrEmpty(customKey))
        return await _blob.GeneratePresignedUrlAsync(customKey, ct);

    // L4 PDF cover (existing #1852)
    var pdfKey = await _pdfQuery.GetCoverR2KeyAsync(gameId, size, ct);
    if (!string.IsNullOrEmpty(pdfKey))
        return await _blob.GeneratePresignedUrlAsync(pdfKey, ct);

    // L1 placeholder is rendered client-side (no URL)
    return null;
}
```

### FE component split (Fowler)

```
apps/web/src/components/features/library/
├── CustomCoverDialog.tsx (NEW orchestrator)
│   ├── CropDialog (pure UI, reusable for future L4 photo edit, ecc.)
│   ├── EditCoverOverlay (pure UI overlay icon)
│   └── useCustomCoverUpload (react-query mutation hook)
└── (existing files)

apps/web/src/components/game-detail/
└── GameDetailHero.tsx (EXTEND — add EditCoverOverlay rendering)
```

### Data flow (upload)

```
[User selects file]
    ↓
[FE: validate input size ≤10MB + MIME accept]
    ↓
[FE: if HEIC → lazy import heic2any → HEIC→JPEG blob]
    ↓
[FE: load JPEG/PNG/WebP into <Image> → canvas]
    ↓
[FE: react-easy-crop UI → user adjusts]
    ↓
[FE: canvas.toBlob(webp, q=0.8)]
    ↓
[FE: iterate q-=0.1 if blob > 200KB, max iter q=0.4]
    ↓
[FE: if still > 200KB → reject client-side]
    ↓
[FE: POST multipart {gameId, file: blob}]
    ↓
[BE: validate auth + gameId∈library (R9) + size + MIME]
    ↓
[BE: best-effort delete old R2 if CustomCoverR2Key exists]
    ↓
[BE: upload new blob to R2 path 'user-covers/{userId}/{gameId}/cover.webp']
    ↓
[BE: update DB CustomCoverR2Key + RowVersion (last-write-wins)]
    ↓
[BE: 201 + { coverR2Key, presignedUrl }]
    ↓
[FE: invalidate useLibraryGameDetail react-query]
    ↓
[FE: library card re-renders with new presigned URL]
```

### Data flow (game removal cleanup — DEC-6)

```
[User: Remove game from library]
    ↓
[RemoveGameFromLibraryCommandHandler: emit GameRemovedFromLibraryEvent]
    ↓
[GameRemovedFromLibraryCustomCoverHandler (NEW best-effort):
    if CustomCoverR2Key not null:
        try: blob.DeleteAsync(R2Key)
        catch: log warning, don't propagate
]
    ↓
[DB CustomCoverR2Key already null (entity removed)]
```

## API contract

### POST /api/v1/library/{gameId}/cover

**Request**:
```http
POST /api/v1/library/{gameId}/cover HTTP/1.1
Content-Type: multipart/form-data; boundary=----X
Cookie: session=...

------X
Content-Disposition: form-data; name="file"; filename="cover.webp"
Content-Type: image/webp

<binary webp blob, ≤200KB>
------X--
```

**Response 201 Created**:
```json
{
  "coverR2Key": "user-covers/{userId}/{gameId}/cover.webp",
  "presignedUrl": "https://r2.../user-covers/...?expires=..."
}
```

**Response 400 Bad Request**:
- File missing in multipart
- File size > 10MB
- MIME type non in [image/jpeg, image/png, image/webp, image/heic]
- File size > 200KB (after FE encode, BE re-checks)

**Response 403 Forbidden**:
- gameId not in user library (AC-R9)

**Response 401 Unauthorized**:
- No auth session

### DELETE /api/v1/library/{gameId}/cover

**Response 204 No Content**:
- Success (R2 cleanup best-effort, DB null)

**Response 403 Forbidden**:
- gameId not in user library

**Response 404 Not Found**:
- No custom cover exists for this gameId (nothing to delete) — idempotent path

## Failure modes & recovery (Nygard)

| Failure | Detection | Recovery |
|---|---|---|
| R2 upload fails after DB update | Exception on `UploadAsync` | DB rollback via TX, FE shows error |
| DB update fails after R2 upload | `DbUpdateException` | Orphan R2 object — log warning with key. Background sweep job future (deferred, same pattern as #1873 H2) |
| R2 delete fails on game removal | Exception in handler | Log warning + persist key in `GameRemovedFromLibraryEvent.CleanupError`. Orphan R2 — manual cleanup ops |
| Concurrent upload (2 devices) | Race condition | Last-write-wins via `RowVersion` on `UserLibraryEntry` entity (standard). R2: 2 PUT same key, last wins. ~200ms race window accettabile |
| HEIC decode fails (heic2any throws) | `heic2any` rejection | FE shows error "Formato non supportato, riprova con JPEG/PNG" |
| Canvas re-encode fails (browser limitation) | `canvas.toBlob` callback null | FE shows error "Errore di compressione, riprova" |
| Network timeout during upload | `fetch` timeout (30s) | FE retry button (no auto-retry); manual user action |
| User uploads 0-byte file | FE pre-validation size = 0 | Reject "File vuoto" |

## Test strategy (Crispin)

### Backend Unit Tests (~12 tests)
- `UploadCustomCoverCommandHandlerTests`:
  - happy path (valid file, gameId in library) → 201
  - gameId NOT in library → 403
  - replace existing cover → old R2 deleted, new uploaded
  - R2 upload throws → exception propagates (DB not updated)
  - DB throws → exception propagates (R2 cleanup attempted)
- `RemoveCustomCoverCommandHandlerTests`:
  - happy path (cover exists) → 204 + R2 delete + DB null
  - no custom cover exists → 404
  - gameId NOT in library → 403
  - R2 delete throws → log warning, return 204 (DB null anyway)
- `UploadCustomCoverCommandValidatorTests`:
  - file > 10MB → fail
  - MIME not in whitelist → fail
- `GameRemovedFromLibraryCustomCoverHandlerTests`:
  - cover exists → R2 delete called
  - no cover → no-op
  - R2 throws → log warning, no propagation
- `CoverUrlResolverTests` (extend existing):
  - L3 custom cover present → return L3 URL
  - L3 null, L4 present → return L4 URL
  - L3 + L4 null → return null (FE shows L1 placeholder)

### Frontend Unit Tests (~8 tests)
- `CustomCoverDialog.test.tsx`:
  - opens with file picker on edit click
  - HEIC detected → lazy-loads heic2any
  - rejects file > 10MB
  - shows crop UI for valid file
  - shows error for non-comprimibile output
- `useCustomCoverUpload.test.tsx`:
  - mutation calls POST endpoint
  - invalidates queries on success
  - error handling
- `EditCoverOverlay.test.tsx`:
  - shows icon on hover (desktop)
  - shows icon always (mobile)
  - calls onEditClick

### EXIF strip validation (Nygard CRITICAL)
- Unit test FE: input JPEG con EXIF GPS → canvas encode → output blob → parse EXIF → assert ASSENT

### E2E Test (1 happy path)
- Playwright: login → /library/{gameId} → click edit icon → upload fixture JPEG → crop → confirm → assert new cover URL in library card within 5s (SLO measurement)

### Manual QA Matrix (in PR test plan)
- iOS Safari real device: camera capture + gallery selection + HEIC handling
- Android Chrome real device: camera capture + gallery selection
- Desktop Chrome: file picker + crop drag + zoom

## Privacy/GDPR

### Data classification
- **Personal data**: user-uploaded photo (their physical game copy)
- **Storage location**: R2 bucket private (signed URLs only)
- **Retention**: indefinita finché game in library; auto-deleted on game removal

### GDPR rights
- **Right to access**: presigned URL via existing GetUserLibraryDetailQuery
- **Right to erasure**: 
  - Per-game: DELETE `/api/v1/library/{gameId}/cover` (instant)
  - Per-account: deferred (no `UserDeletedDomainEvent` esistente). **Procedura manuale ops**: SQL query `SELECT CustomCoverR2Key FROM user_library_entries WHERE UserId = @userId AND CustomCoverR2Key IS NOT NULL` + R2 batch delete + DB null. Documentato in `docs/for-developers/operations/operations-manual.md` (future PR L3 closure).
- **Data minimization**:
  - EXIF GPS strip automatico via canvas re-encode
  - Output webp 200×300 200KB max (downsize from original)
  - No filename retention (R2 key è UUID-based)

### Audit
- `CreatedAt` + `UpdatedAt` su `UserLibraryEntry` entity (existing audit pattern)
- No separate audit log per cover changes (covered by entity-level audit)

## SLO & Performance

- **Upload p95**: ≤ 5s end-to-end (click confirm → render new cover)
- **Delete p95**: ≤ 1s (DELETE → DB null + R2 cleanup)
- **Crop UI responsiveness**: 60fps on mid-tier mobile (test on iPhone 11 / Pixel 5)
- **Bundle size impact**:
  - `react-easy-crop` ~12KB gz
  - `heic2any` ~80KB gz LAZY-loaded (only when HEIC selected)
  - Total impact on initial bundle: ~12KB gz (heic2any not counted in initial)

## Out of scope / Deferred

- **Cleanup on account deletion**: deferred to Phase 2 (requires `UserDeletedDomainEvent` in Authentication BC). GDPR mitigation: manual ops procedure documented.
- **Cover edit in-place**: replace via re-upload only (no "edit existing crop" affordance — keep MVP small)
- **AI cover generation**: separate evaluation (issue not yet open)
- **L2 Wikidata enrichment**: separate issue #1823, will fit into priority chain between L4 and L1 when shipped
- **Quota per user**: implicit cap = library size (1 cover per game). No additional quota enforcement (R2 storage cost mitigated by webp ≤200KB + per-game cap)
- **Cover sharing tra utenti**: NO — covers are strettamente per-user, never aggregated to community catalog
- **Resumable upload**: file ≤200KB, single-shot POST is fine; no chunking
- **Auto-retry**: FE shows manual retry button on network error; no auto-retry to avoid duplicate uploads
- **Cover history**: NO — replace overwrites previous; no history retention

## DEC summary (locked)

| DEC | Decision | Rationale |
|---|---|---|
| DEC-1 | Manual rectangular crop via `react-easy-crop` (~12KB gz) | UX-friendly, a11y keyboard support, mobile pinch-zoom native |
| DEC-2 | Hover/tap overlay icon top-right hero MeepleCard | Best discovery + minimum chrome; edit affordance discreta |
| DEC-3 | POST + DELETE separati `/api/v1/library/{gameId}/cover` | RESTful, parity con `UploadPrivatePdfCommand` pattern |
| DEC-4 | Canvas API native via react-easy-crop blob output | Zero extra lib (react-easy-crop uses canvas internally) |
| DEC-5 | Size discipline: input ≤10MB, output ≤200KB via quality dial-down loop q=0.8→0.4 | Bandwidth efficiency + R2 storage cost control |
| DEC-6 | Cleanup on game removal via `GameRemovedFromLibraryEvent` handler; account deletion deferred | Existing event leveraged; account deletion = no event yet, GDPR mitigated via manual ops |
| DEC-7 | Extend `CoverUrlResolver` (existing #1852) with L3 priority first | Stable interface, server-side resolution, FE consumes `coverUrl` directly |
| DEC-8 | Include `heic2any` lib (~80KB) lazy-loaded for HEIC iOS | 50%+ iOS Safari traffic, UX-better than manual conversion |
| DEC-9 | FE 3-component split: `useCustomCoverUpload` hook + `CropDialog` UI + `EditCoverOverlay` UI | Single responsibility, reusable, testable |

## References

- Umbrella: [#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821) (CLOSED, L1 MVP shipped)
- L1 placeholder: [#1822](https://github.com/meepleAi-app/meepleai-monorepo/issues/1822) (shipped #1825/#1830)
- L4 PDF cover: [#1831](https://github.com/meepleAi-app/meepleai-monorepo/issues/1831) (CLOSED 2026-06-04, this session phase 3)
- L2 Wikidata: [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) (OPEN — next after L3 ship)
- `CoverUrlResolver` (existing): from #1852
- `IBlobStorageService` pattern: from `UploadPrivatePdfCommandHandler.cs`
- Cleanup event handler pattern: from #1873 `PdfDeletedEventHandler.cs`
- Spec-panel review: Wiegers + Cockburn + Adzic + Crispin + Fowler + Nygard (2026-06-04 sessione 31 fase 4)
