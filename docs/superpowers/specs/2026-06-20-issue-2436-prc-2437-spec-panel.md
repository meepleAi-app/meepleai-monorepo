# #2436 PR-C + #2437 — Play Records photo UI + detail/edit deferred — Spec-Panel Consolidated Spec

> **Source**: `/sc:spec-panel --mode critique` 2026-06-20 (Wiegers, Fowler, Nygard, Newman, Adzic, Crispin) + 3-agent codebase discovery + fattuale BE/FE verification. Segue [[2026-06-20-issue-2436-create-deferred-spec-panel]] (PR-A/PR-B già MERGED). Companion plans: da scrivere per ciascun PR.

**Issues**: [#2436](https://github.com/meepleAi-app/meepleai-monorepo/issues/2436) (create deferred — resta PR-C) · [#2437](https://github.com/meepleAi-app/meepleai-monorepo/issues/2437) (detail/edit deferred). Parent: #2346. Mockups: `admin-mockups/design_files/sp4-play-records-{new,detail,edit}.{html,jsx}`.

## Stato pregresso (MERGED su main-dev)

- **#2436 PR-A** (autosave + draft localStorage) → `eaf1e7093` (PR #2448).
- **#2436 PR-B** (photo upload pipeline + OCR + **xmin** + **middleware globale `DbUpdateConcurrencyException → 409` + header `X-Warning-Code: concurrent-edit`**) → `dd5f14529` (PR #2450).
- BE foundation shipped copre: scrittura foto (`POST /photos`), xmin optimistic-concurrency token (server-owned), mapping 409 globale.

## Locked decisions (user, 2026-06-20 — questa sessione)

| ID | Decisione | Rationale |
|----|-----------|-----------|
| **C1** | **Estendi `PlayRecordDto` con `photos[]`** (mappato nel `GET /{id}` esistente) | Gallery persistente richiede read-path. 1 round-trip vs endpoint dedicato. PR-C diventa FE + mini-BE. |
| **C2** | **Conflict-UI stale-form completo (BE+FE)** | Espone xmin end-to-end (DTO + UpdateRequest + handler check) per coprire il caso "qualcuno ha salvato mentre editavi" — non solo race concorrenti. |
| **M1** | **#2437 completo (BE+FE)** | Include share-token (BE greenfield) + audit/restore (BE greenfield) nello stesso ciclo, oltre conflict-UI + gallery-mount + MVP. |
| **M3** | **MVP = winner derivato** | Riusa `WinnerPlayerIds` (già nel DTO, da dimension `wins>0`). Zero BE. Chip non appare nei cooperativi/senza-winner. |

### Decisioni ereditate (ancora valide)
- **DEC-1** Sequenziale: **#2436 completo (PR-C), poi #2437**.
- **DEC-3** OCR = wire ora via smoldocling (già shipped in PR-B).
- **#2437-5 edit-window 7d immutability + 410 → DROPPATO** (mantieni invariante "edit allowed after completion for corrections").

## Panel findings

### ❌ CRITICAL (risolti dalle decisioni)
- **C1 — Gallery senza read-path**: `PlayRecordDto.cs:10-29` non ha `photos`; endpoint solo `POST /photos`, nessun `GET`. → **risolto da C1** (estendi DTO).
- **C2 — Conflict-UI vs xmin server-owned**: `PlayRecord.cs:51` xmin `internal`/repository-only, non nel DTO né nel `UpdatePlayRecordCommand` (solo recordId/userId/sessionDate/notes/location). 409 scatta solo su race DB concorrenti, non su form-stale → last-write-wins silenzioso. → **risolto da C2** (xmin end-to-end).

### ⚠️ MAJOR
- **M1 — #2437 non è FE-only**: share-token greenfield (entity field + 2 cmd handler + 1 query + 3 endpoint + migration + route pubblica anonima); audit opt-in `[Auditable]` (facile) ma **restore-version greenfield totale** (tabella + endpoint list/restore + UI). → **accettato da M1** (#2437 completo).
- **M2 — `CustomCoverDialog` non è riuso diretto**: è single-file + crop obbligatorio (cover 200×300 WebP). Si riusa il *pattern* `heic2any` (`CustomCoverDialog.tsx:55-67`) + `useCustomCoverUpload` (multipart raw-fetch, `useCustomCoverUpload.ts:38-44`). **BE accetta solo JPEG/PNG/WebP, NON HEIC** → conversione client-side **obbligatoria**.
- **M3 — "MVP" ambiguo**: domain ha solo winner derivato. → **risolto da M3** (= winner).

### 🔸 MINOR (registrati per i plan)
- **m1** FE enforce **5MB** (non 10MB) + MIME JPEG/PNG/WebP; convertire HEIC→JPEG **prima** del check size.
- **m2** Dedup UX: response ha `WasDeduplicated` → non duplicare la card, segnalare "foto già presente".
- **m3** OCR UX: response ha `OcrText` → mostra read-only (es. pre-compila Note), **NON** auto-applicare agli score (rischio dati errati). Da confermare nel plan PR-C.
- **m4** i18n: manca `playRecords.photos` in `it/en.json`.
- **m5** Overlap gallery: "gallery+lightbox" (PR-C) e "photo gallery" (#2437) = **stessa** gallery. Costruita una volta in PR-C; #2437 ci monta sopra MVP chip.

## Struttura PR proposta (DEC-1 sequenziale)

| Ordine | PR | Stack | Contenuto |
|--------|----|----|-----------|
| 1 | **#2436 PR-C** | mini-BE + FE | `PlayRecordDto.photos[]` (C1) + upload multi-file (heic2any client-side) + OCR toggle + gallery + lightbox + dedup UX + i18n |
| 2 | **#2437-1** | BE + FE | Conflict-UI stale-form (C2: xmin end-to-end) + MVP chip (M3, gallery-mount nel DetailView) |
| 3 | **#2437-2** | BE + FE | Share-token (pattern `GameNightPlaylist`) + route pubblica read-only |
| 4 | **#2437-3** | BE + FE | Audit (`[Auditable]` opt-in) + restore-version (greenfield: tabella + list last-5 + restore) |

> **Rischio #2437-3**: restore-version è greenfield totale (zero prior-art). Merita un mini-brainstorm sulla snapshot strategy (quando si crea una versione: ad ogni update? solo su K5 fields? last-5 retention) prima del plan.

## PR-C — spec (prossimo deliverable)

### Goal
UI per caricare foto di una partita (scoresheet/board/tavolo), con OCR opt-in, e visualizzarle in una gallery con lightbox nella detail view. Mini-estensione BE per esporre le foto in lettura.

### BE (mini)
- **AC-BE1** `PlayRecordDto` espone `Photos` (`IReadOnlyList<PlayRecordPhotoDto>`): `Id, BlobUrl, ThumbnailUrl?, OcrText?, Caption?, UploadedByUserId, UploadedAt`. (Non esporre `Sha256Hash`/`FileSizeBytes`/`OcrConfidence` al client salvo necessità.)
- **AC-BE2** `GetPlayRecordQueryHandler` carica le foto del record (Include/projection) e le mappa nel DTO ordinate per `UploadedAt`.
- **AC-BE3** Authz invariata: il GET record già applica visibilità/ownership; le foto seguono il record.

### FE
- **AC-C1** Schema Zod `PlayRecordPhotoSchema` + `photos: z.array(...).optional()` su `PlayRecordDtoSchema` (optional durante rollout, tighten dopo).
- **AC-C2** `playRecordsApi.uploadPhoto(recordId, blob, { caption?, extractScoreFromPhoto? })` — multipart raw-fetch (pattern `useCustomCoverUpload`), header auth.
- **AC-C3** Hook `usePlayRecordPhotoUpload` (TanStack mutation) → su success invalida `usePlayRecord(recordId)`.
- **AC-C4** Componente upload **multi-file** (≤10 file, ≤5MB ciascuno post-conversione, JPEG/PNG/WebP): HEIC→JPEG client-side via `heic2any` dynamic import **prima** del size check. **No crop.**
- **AC-C5** OCR toggle ("📷 Estrai punteggio dalla foto") → passa `extractScoreFromPhoto=true`; alla risposta mostra `OcrText` read-only (m3), no auto-apply agli score.
- **AC-C6** Dedup: se `WasDeduplicated=true` → toast "foto già presente", non aggiungere card duplicata.
- **AC-C7** Gallery + lightbox nella `PlayRecordDetailView` (riusa/adatta `SessionPhotoGallery` che ha lightbox built-in, o `PhotosGallery` + dialog lightbox). A11y: keyboard nav, alt text.
- **AC-C8** i18n `playRecords.photos.*` (it/en): title, uploadButton, dragHint, extractScoreLabel, captionLabel, emptyState, dedupToast, errors.
- **AC-C9** Limiti FE allineati al BE (5MB, MIME) con errore pre-upload chiaro.

### Non-goals PR-C
- Conflict-UI 409 (→ #2437-1). Share/audit/restore (→ #2437-2/3). MVP chip (→ #2437-1). Auto-apply OCR agli score.

## Reuse map (verificata, file:line)

**Photo upload/gallery (PR-C)**
- heic2any pattern: `apps/web/src/components/features/library/custom-cover/CustomCoverDialog.tsx:55-67`
- multipart raw-fetch: `apps/web/src/hooks/mutations/useCustomCoverUpload.ts:38-44`
- gallery+lightbox: `apps/web/src/components/session/SessionPhotoGallery.tsx` (built-in) · pure: `apps/web/src/components/features/session-summary/PhotosGallery.tsx`
- BE upload endpoint: `apps/api/src/Api/Routing/PlayRecordEndpoints.cs:91,224-248` — fields `file`/`extractScoreFromPhoto`/`caption`; response `PlayRecordPhotoUploadResult(PhotoId, PhotoUrl, ThumbnailUrl?, OcrText?, WasDeduplicated)`
- entity: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecordPhoto.cs:11-61`
- DTO da estendere: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/PlayRecords/PlayRecordDto.cs:10-29`
- query handler: `GetPlayRecordQueryHandler` (GameManagement/Application/Queries/PlayRecords)
- FE schema: `apps/web/src/lib/api/schemas/play-records.schemas.ts:53-75`
- FE api client: `apps/web/src/lib/api/play-records.api.ts`
- detail mount: `apps/web/src/components/play-records/PlayRecordDetailView.tsx`

**Conflict-UI (#2437-1)**
- xmin: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs:49-54`
- update cmd: `UpdatePlayRecordCommand.cs` + `UpdatePlayRecordCommandHandler.cs`
- update endpoint: `PlayRecordEndpoints.cs:71,200-207`
- FE edit page: `apps/web/src/app/(authenticated)/play-records/[id]/edit/page.tsx:59-86`
- pattern errori tipizzati (live-session): `apps/web/src/hooks/use-update-session-scores.ts:44-54`, `ScoreTabContent.tsx:40-70`
- MVP source: `PlayRecordOutcomeCalculator.cs:25-68` (WinnerPlayerIds nel DTO)

**Share-token (#2437-2)**
- domain: `GameNightPlaylist.cs:194-213` (Generate/Revoke)
- handlers: `GenerateShareLinkCommandHandler.cs:34`, `RevokeShareLinkCommandHandler.cs`, `GetPlaylistByShareTokenQueryHandler.cs`
- endpoints: `PlaylistEndpoints.cs:176,193,210-222` (`POST /share`, `DELETE /share`, `GET /shared/{token}` AllowAnonymous)
- FE toast: `apps/web/src/components/ui/share-success-toast/share-success-toast.tsx`

**Audit/restore (#2437-3)**
- audit infra: `AuditingSaveChangesInterceptor.cs`, `AuditableAttribute.cs`, `AuditLoggingBehavior.cs`
- restore-version: **greenfield** (nessun prior-art)

## References
- ADR-067 (photo pipeline): `docs/for-claude/architecture/adr/adr-067-playrecord-photo-upload-pipeline.md`
- ADR-066 (ownership/creator-only): `docs/for-claude/architecture/adr/adr-066-playrecord-ownership-model.md`
- ADR-060 (xmin/live-session persistence): `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md`
- Spec PR-A/PR-B: `docs/superpowers/specs/2026-06-20-issue-2436-create-deferred-spec-panel.md`
