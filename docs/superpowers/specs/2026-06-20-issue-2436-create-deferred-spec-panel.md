# #2436 — Play Records create, deferred Path B — Spec-Panel Consolidated Spec

> **Source**: `/sc:spec-panel --mode critique` 2026-06-20 (Wiegers, Fowler, Nygard, Newman, Adzic, Crispin) + 3-agent codebase discovery. Anchors the locked decisions for the 3-PR implementation of #2436. Companion plan: `docs/superpowers/plans/2026-06-20-issue-2436-pr-a-autosave-draft.md`.

**Issue**: [#2436](https://github.com/meepleAi-app/meepleai-monorepo/issues/2436) — split from #2348 (US-INT-2b) during the Tier 2 gap-fill. Parent: #2346. Mockup: `admin-mockups/design_files/sp4-play-records-new.{html,jsx}` (shared core `pr-form-core.jsx`).

## Locked decisions (user, 2026-06-20)

| ID | Decision | Rationale |
|----|----------|-----------|
| **DEC-1** | **Sequenziale: #2436 completo, poi #2437** | Issue-by-issue per user directive. |
| **DEC-2** | **Draft persistence = localStorage-only** (no BE endpoint `GET /play-records/draft/{gameNightId}`) | YAGNI — body non giustifica cross-device (Fowler). Mirror del pattern esistente `useGameNightDraftPersist`. |
| **DEC-3** | **OCR = wire ora** via smoldocling esistente | `smoldocling /api/v1/preprocess` + `SmoldoclingPhotoPreprocessor.cs` fanno già OCR immagini → amenda ADR-067 §OCR (era O-C deferred). |
| **DEC-4** | **Autosave = debounce-on-change** (mirror `useGameNightDraftPersist`, ~800ms) **non** timer fisso 30s | Pattern provato in codebase + UX migliore (salva dopo pausa digitazione, non timer cieco). Requisito "visual indicator" soddisfatto da `isPending` + `lastSavedAt`. Deviazione deliberata dal literal "30s". |

## Meta-findings (dalla discovery — risolti)

| # | Finding | Stato |
|---|---------|-------|
| M1 | Photo-upload sembrava doppio-tracciato con #2359/#2363 | **RISOLTO**: #2359/#2363 sono CLOSED (erano issue di *risoluzione ADR*, output = ADR-067). L'**implementazione** è di US-INT-2b → **#2436 la possiede**. Zero doppio-build. |
| M2 | ADR-067 §23/§OCR "nessun endpoint immagine esiste" → **stantio** | `smoldocling /api/v1/preprocess` accetta JPEG/PNG e fa OCR; `SmoldoclingPhotoPreprocessor.cs` è il client .NET. ADR-067 OCR va amendata (DEC-3). |
| M4 | ADR-081 §49/§51 "PlayRecord no DELETE/soft-delete" → **stantio** | #2439/PR#2441 ha aggiunto soft-delete + DELETE + `PlayRecordDeletedEvent`. Riconciliare quando si tocca la cache stats (non in #2436). |
| M5 | Body cita BC errato (SessionTracking) | PlayRecord è in **GameManagement**. Template same-BC = `GameNightPlaylist`. |
| M6 | `S3BlobStorageService.StoreAsync` hardcoda `ContentType="application/pdf"` | Da correggere in PR-B per MIME immagini corrette. |

## Scope reality + 3-PR structure

| PR | Feature(s) | Stack | Classificazione post-discovery | Decisioni |
|----|-----------|-------|-------------------------------|-----------|
| **A** | Autosave 30s+indicator (item 1) + draft persistence (item 2) | FE-only | greenfield, ma **mirror** `useGameNightDraftPersist` | DEC-2, DEC-4 |
| **B** | Photo upload pipeline (item 3) + OCR (item 4) | BE | infra-exists (ADR-067 + `SessionAttachmentService` prior-art completo) + amend OCR | DEC-3; ADR-067 (P-A storage, D-A SHA256 dedup) |
| **C** | Photo upload UI + OCR toggle + preview | FE | greenfield, riuso `CustomCoverDialog` + `heic2any` | multi-file ≤10/≤5MB |

### PR-B reuse map (per future session)
- Storage: `IBlobStorageService.StoreAsync` + nuovo `BlobCategory.PlayRecordPhoto` (+ case in `BlobCategoryExtensions.ToS3Folder()` → `"play-record-photos"`). ⚠️ dimenticare il case = `ArgumentOutOfRangeException` a runtime.
- Prior-art: `SessionAttachmentService.cs` (magic-byte validation + thumbnail ImageSharp 300px/q80 + presigned 3600s).
- Entity `PlayRecordPhoto` (GameManagement): `Id, PlayRecordId, BlobUrl, ThumbnailUrl?, FileSizeBytes, Sha256Hash, OcrText?, Caption?, UploadedByUserId, UploadedAt`. UNIQUE partial index `(PlayRecordId, Sha256Hash)` per dedup (D-A).
- Authz: creator-only (`RequesterUserId == CreatedByUserId`, ADR-066).
- ⚠️ **Server-side enforcement ≤5MB/≤10 file** (oggi validato solo client) — Nygard.
- OCR (DEC-3): on upload con `ExtractScoreFromPhoto=true` → `SmoldoclingPhotoPreprocessor.PreprocessAsync(imageData)` → popola `OcrText`. Amendare ADR-067 §OCR da O-C a "O-D reuse smoldocling /preprocess".
- Upload direction: **multipart-POST-through-API** (pattern #1821 `useCustomCoverUpload`), NON browser-direct presigned PUT (solo presigned-GET esiste).

## PR-A spec (questa sessione)

### Goal
Autosave + restore della bozza del form di creazione PlayRecord su **localStorage** (per-utente), con indicatore visivo di stato salvataggio. Nessuna modifica BE.

### Behaviour (AC)
- **AC-A1** Mentre l'utente compila il form (Step 1/2/3), ogni modifica fa partire un autosave debounced (~800ms) su `localStorage` chiave `meepleai:play-record-create-draft:<userId>`.
- **AC-A2** Indicatore visivo `role="status"` `aria-live="polite"`: "Salvataggio…" durante il pending, "Bozza salvata {HH:MM}" dopo il salvataggio, nulla a riposo iniziale.
- **AC-A3** Al mount, se esiste una bozza valida (non scaduta) **e** non c'è prefill da `gameNightId` (`initialValues` assente), il form ripristina valori + players + step.
- **AC-A4** TTL 7 giorni: bozze più vecchie scartate alla lettura (rimosse da storage).
- **AC-A5** Schema-version guard: bump → bozze vecchie scartate (no crash).
- **AC-A6** Su submit con successo → `clear()` rimuove la bozza. Su cancel/discard → `clear()`.
- **AC-A7** Solo `mode='create'` (in `edit` l'autosave è disattivato).
- **AC-A8** Per-utente: `userId` null (non loggato/SSR) → no read, no write (no cross-user leak).

### Non-goals PR-A
- Nessun endpoint BE (DEC-2). Nessun draft server-side. Nessun cross-device. Nessuna foto/OCR (PR-B/C).
- Single-draft-per-user (come game-nights): una sessione di create-con-prefill può sovrascrivere la bozza free-create. Accettabile MVP.

### Dropped from #2437 (per decisione utente, fuori da #2436 ma registrato)
- **#2437-5 edit-window 7d immutability + 410 → DROPPATO** (mantieni invariante shipped "allowed even after completion for corrections").

## References
- Pattern mirror: `apps/web/src/lib/game-nights/hooks/useGameNightDraftPersist.ts`
- Debounce: `apps/web/src/lib/session-live/use-debounced-callback.ts`
- Target form: `apps/web/src/components/play-records/SessionCreateForm.tsx`
- userId: `apps/web/src/hooks/queries/useCurrentUser.ts`
- i18n: `apps/web/src/locales/{it,en}.json` → `playRecords.new`
- ADR-067 (photo, PR-B): `docs/for-claude/architecture/adr/adr-067-playrecord-photo-upload-pipeline.md`
- ADR-066 (ownership/creator-only): `docs/for-claude/architecture/adr/adr-066-playrecord-ownership-model.md`
