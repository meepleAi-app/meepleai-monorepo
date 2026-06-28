# Epic #2501 SP4 — Foto in EndgameDialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettere all'host, alla chiusura di una sessione live, di aggiungere foto-ricordo della partita dalla finestra di endgame, caricandole sul `PlayRecord` appena creato — completando il momento 9-10 della user story #2506.

**Architecture:** SP4 è **FE-only** e riusa l'infrastruttura foto `PlayRecord` già esistente (#2436/#2503). La sessione è già finalizzata (`Completed`) quando `EndgameDialog` si apre (il `/complete` è chiamato in `handleConfirmEndgame` PRIMA di montare il dialog); il `recordId` del nuovo `PlayRecord` arriva via il polling già wired `useResolvePlayRecord`. Le foto si caricano sul `PlayRecord` (endpoint `POST /play-records/{recordId}/photos`), **disaccoppiate dalla finalizzazione**: un fallimento foto non blocca il salvataggio/navigazione. Nessun nuovo BE, nessun endpoint media nativo.

**Tech Stack:** Next.js 16 / React 19, Vitest + Testing Library, Tailwind 4 + shadcn/ui, React Query.

## Global Constraints

- **Riuso, non riscrittura**: usare gli hook/endpoint esistenti — `usePlayRecordPhotoUpload(recordId)`, `useResolvePlayRecord()`, `POST /play-records/{recordId}/photos`. NON creare un endpoint media nativo `/live-sessions/{id}/media` (out-of-scope, ADR-083 Direzione A non lo richiede per SP4).
- **Foto opzionali (AC-MEDIA-2)**: il save/navigazione NON deve mai dipendere dalla presenza/successo delle foto.
- **Upload disaccoppiato (AC-MEDIA-3)**: ogni foto ha il suo stato (idle/uploading/done/error) + retry inline; un errore foto resta inline e non blocca i CTA del dialog.
- **recordId nullable durante il polling**: quando `EndgameDialog` si apre, `recordId` può essere `null` per 1-15s (polling). La sezione foto consente la **selezione + preview locale** sempre, ma l'**upload effettivo** parte solo quando `recordId != null` (durante il polling il bottone di upload è disabilitato con stato "preparazione").
- **Token/design**: solo token semantici e utility entità (no `bg-white`/`text-gray-*` ecc.; ESLint `local/no-hardcoded-color-utility` è error). Riusare i pattern di `PlayRecordPhotoUploadDialog`/`PlayRecordPhotoGallery`.
- **Vincoli foto** (allineati a `PlayRecordPhotoUploadDialog`): max 10 file, conversione HEIC→JPEG, validazione dimensione (riusare le costanti esistenti del dialog/uploader).
- **i18n**: nessuna stringa hardcoded; chiavi in `it.json` + `en.json` sotto il namespace della session-live/endgame.
- **Branch**: `feature/issue-2501-sp4-endgame-photos` (parent `main-dev`). SP4 è indipendente da SP0 (PR #2551) — NON serve quel branch.

## Riferimenti di codice (verificati nell'esplorazione — leggerli prima di implementare)
- `apps/web/src/components/features/session-live/EndgameDialog.tsx` (props `onSave?`, `saving?` ~righe 59-62; CTA ~206-243; markup punteggi ~176-204)
- `apps/web/src/components/play-records/photos/PlayRecordPhotoUploadDialog.tsx` (reference da adattare in contentless: file multiselect, HEIC→JPEG, caption, OCR checkbox, error inline, MAX_FILES)
- `apps/web/src/components/play-records/photos/PlayRecordPhotoGallery.tsx` (gallery read-only, props `{ photos, labels, className? }`)
- `apps/web/src/hooks/mutations/usePlayRecordPhotoUpload.ts` (`usePlayRecordPhotoUpload(recordId)` → mutation; vars `{ file, caption?, extractScoreFromPhoto? }`; result `{ photoId, photoUrl, thumbnailUrl, ocrText, wasDeduplicated }`)
- `apps/web/src/lib/session-live/use-resolve-play-record.ts` (`useResolvePlayRecord()` → `{ status, playRecordId, start }`)
- `apps/web/src/hooks/mutations/useCompleteLiveSession.ts` (`useCompleteLiveSession(sessionId)`)
- `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` (monta `EndgameDialog`; `resolvedPlayRecordId` ~riga 347; `handleConfirmEndgame` ~553-583; `completeLiveSession.mutate` ~571)

---

### Task 1: `EndgamePhotoUploadSection` (componente + i18n + test)

**Files:**
- Create: `apps/web/src/components/features/session-live/EndgamePhotoUploadSection.tsx`
- Modify: `apps/web/src/i18n/messages/it.json`, `apps/web/src/i18n/messages/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/EndgamePhotoUploadSection.test.tsx` (collocare seguendo il pattern dei test component esistenti — cercare con Glob i `__tests__` vicini)

**Interfaces:**
- Consumes: `usePlayRecordPhotoUpload(recordId)`; `PlayRecordPhotoGallery` (per le foto caricate, opzionale); le costanti vincoli foto di `PlayRecordPhotoUploadDialog` (MAX_FILES, validazione size, HEIC→JPEG).
- Produces: componente `EndgamePhotoUploadSection` con props:
  ```ts
  interface EndgamePhotoUploadSectionProps {
    recordId: string | null;          // null durante il polling → upload disabilitato
    onUploadingChange?: (uploading: boolean) => void; // per disabilitare i CTA del dialog durante l'upload (AC-MEDIA-1 no doppio submit)
    className?: string;
  }
  ```

**Comportamento (per AC):**
- AC-MEDIA-1: input file multiselect (max 10) → preview locale immediata (anche con `recordId === null`); un bottone "Carica foto" per file (o batch) che chiama `usePlayRecordPhotoUpload(recordId).mutateAsync({ file, caption, extractScoreFromPhoto })`. Bottone upload disabilitato quando `recordId === null` (mostra "preparazione…"). Durante l'upload chiama `onUploadingChange(true)`/`(false)`.
- AC-MEDIA-3: ogni foto ha stato `idle|uploading|done|error`; su errore → messaggio inline sotto la preview + bottone "Riprova" che ri-chiama `mutateAsync` per quella sola foto. Un errore NON solleva eccezioni non gestite e NON blocca le altre foto.
- AC-MEDIA-2: nessun comportamento che renda obbligatorie le foto (la sezione non espone alcun gate sul save — è il dialog a possedere i CTA).
- AC-MEDIA-5 (read): dopo upload riuscito, mostrare le miniature (riusare `PlayRecordPhotoGallery` con le foto caricate, oppure le preview locali con badge "caricata").

- [ ] **Step 1: Write the failing tests** (component test, Testing Library)
  Test cases (asserzioni reali, non tautologiche):
  1. `selecting_files_shows_local_previews` — selezionando 2 file appaiono 2 preview, anche con `recordId={null}`.
  2. `upload_disabled_while_recordId_null` — con `recordId={null}` il bottone "Carica foto" è disabilitato; con `recordId="..."` è abilitato.
  3. `successful_upload_marks_photo_done_and_calls_onUploadingChange` — mock `usePlayRecordPhotoUpload` che risolve; dopo l'upload la foto è marcata "caricata" e `onUploadingChange` è stato chiamato `true` poi `false`.
  4. `failed_upload_shows_inline_error_and_retry` — mock che rifiuta una volta poi risolve; appare errore inline + bottone "Riprova"; al click la foto passa a "caricata"; le altre foto non sono impattate.
  5. `respects_max_files` — selezionando >10 file ne tiene 10 e mostra un avviso.

- [ ] **Step 2: Run tests to verify they fail**
  Run: `cd apps/web && pnpm test EndgamePhotoUploadSection --run`
  Expected: FAIL — componente non esiste.

- [ ] **Step 3: Write the component + i18n keys**
  Implementare `EndgamePhotoUploadSection` adattando la logica file/HEIC/validazione di `PlayRecordPhotoUploadDialog` (smontandola dal wrapper Dialog), con la state-machine per-foto e il retry. Aggiungere le chiavi i18n in `it.json`/`en.json` (namespace coerente con le altre chiavi session-live/endgame — verificarlo nel file): label "Aggiungi foto", "Carica foto", "Riprova", template errore, "preparazione…", avviso max foto. NESSUNA stringa hardcoded.

- [ ] **Step 4: Run tests to verify they pass**
  Run: `pnpm test EndgamePhotoUploadSection --run` → PASS. Poi `pnpm typecheck && pnpm lint` su scope (no violazioni token/i18n).

- [ ] **Step 5: Commit**
  ```bash
  git add apps/web/src/components/features/session-live/EndgamePhotoUploadSection.tsx apps/web/src/components/features/session-live/__tests__/EndgamePhotoUploadSection.test.tsx apps/web/src/i18n/messages/it.json apps/web/src/i18n/messages/en.json
  git commit -m "feat(session-live): #2501 SP4 EndgamePhotoUploadSection component"
  ```

---

### Task 2: Montare la sezione foto in `EndgameDialog`

**Files:**
- Modify: `apps/web/src/components/features/session-live/EndgameDialog.tsx`
- Test: estendere il test esistente di `EndgameDialog` (cercare con Glob; se assente crearlo accanto)

**Interfaces:**
- Consumes: `EndgamePhotoUploadSection` (Task 1).
- Produces: `EndgameDialog` con nuove props:
  ```ts
  recordId?: string | null;   // passato a EndgamePhotoUploadSection
  // internamente: disabilita i CTA durante l'upload via onUploadingChange
  ```

- [ ] **Step 1: Write the failing test**
  1. `renders_photo_section_above_ctas` — quando il dialog è aperto, `EndgamePhotoUploadSection` è renderizzata sopra i CTA "Salva partita"/"Conferma".
  2. `disables_save_cta_while_photos_uploading` — quando la sezione segnala `onUploadingChange(true)`, il CTA "Salva partita" è disabilitato; a `false` torna abilitato (AC-MEDIA-1 no doppio submit).
  3. `save_cta_enabled_with_no_photos` — senza foto selezionate il CTA "Salva partita" resta abilitato (AC-MEDIA-2).

- [ ] **Step 2: Run test to verify it fails**
  Run: `pnpm test EndgameDialog --run` → FAIL.

- [ ] **Step 3: Implement**
  In `EndgameDialog.tsx`: aggiungere la prop `recordId?: string | null`; montare `<EndgamePhotoUploadSection recordId={recordId} onUploadingChange={setUploading} />` sopra i CTA; mantenere un `useState` `uploading` che disabilita il CTA "Salva partita" (`disabled={saving || uploading}`). NON alterare il comportamento esistente di `onSave`/`onAcknowledge`/focus-trap.

- [ ] **Step 4: Run test to verify it passes**
  Run: `pnpm test EndgameDialog --run` → PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add apps/web/src/components/features/session-live/EndgameDialog.tsx apps/web/src/components/features/session-live/__tests__
  git commit -m "feat(session-live): #2501 SP4 mount photo section in EndgameDialog"
  ```

---

### Task 3: Wiring `SessionLiveView` (recordId + 409 handler)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`
- Test: estendere i test esistenti di `SessionLiveView` (Glob)

**Interfaces:**
- Consumes: `resolvedPlayRecordId` (già presente ~riga 347), `EndgameDialog` con la nuova prop `recordId` (Task 2), `completeLiveSession` (già presente).

- [ ] **Step 1: Write the failing test**
  1. `passes_resolved_record_id_to_endgame_dialog` — quando `useResolvePlayRecord` risolve un id, `EndgameDialog` riceve `recordId={resolvedPlayRecordId}`.
  2. `complete_409_shows_already_completed_toast_and_no_navigation` — se `completeLiveSession.mutate` fallisce con 409, viene mostrato un toast "Partita già conclusa" (sonner) e NON si naviga (AC-MEDIA-4).

- [ ] **Step 2: Run test to verify it fails**
  Run: `pnpm test SessionLiveView --run` → FAIL.

- [ ] **Step 3: Implement**
  - Passare `recordId={resolvedPlayRecordId}` al `<EndgameDialog .../>`.
  - Aggiungere un `onError` su `completeLiveSession.mutate` (in `handleConfirmEndgame`) che, sul 409, mostra `toast` (sonner) con la chiave i18n "partita già conclusa" e non avvia il polling/navigazione. Per altri errori, comportamento esistente (o toast generico se assente). Aggiungere la chiave i18n in `it.json`/`en.json`.

- [ ] **Step 4: Run test to verify it passes**
  Run: `pnpm test SessionLiveView --run` → PASS. Poi `pnpm typecheck && pnpm lint`.

- [ ] **Step 5: Run the session-live suite (no regressions)**
  Run: `pnpm test session-live --run` (o il path della cartella) → PASS, nessuna nuova rottura.

- [ ] **Step 6: Commit**
  ```bash
  git add apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx apps/web/src/i18n/messages/it.json apps/web/src/i18n/messages/en.json
  git commit -m "feat(session-live): #2501 SP4 wire recordId + 409 handler in SessionLiveView"
  ```

---

## Out of scope (tracciato)
- Endpoint media nativo `/live-sessions/{id}/media` (ADR-083 non lo richiede per SP4; le foto vivono su PlayRecord).
- AC-MEDIA-4 lato BE (il 409 su `/complete` per sessione già `Completed`): verificare che esista già in `CompleteLiveSessionCommandHandler`; se mancante, follow-up BE separato (non bloccare SP4 FE).
- AC-MEDIA-5 BE outbox/evento: `PlayRecordDto.photos[]` è già esposto; nessun lavoro.

## Self-Review
- **Spec coverage**: AC-MEDIA-1 (sezione+CTA+upload, no doppio submit) → Task 1+2; AC-MEDIA-2 (opzionali) → Task 1+2 (nessun gate); AC-MEDIA-3 (disaccoppiato+retry) → Task 1; AC-MEDIA-4 (409) → Task 3 (FE) + verifica BE in Out-of-scope; AC-MEDIA-5 (read) → già esposto. ✓
- **Placeholder scan**: i test hanno casi e asserzioni nominate; il codice del componente fa riferimento esplicito a `PlayRecordPhotoUploadDialog` da adattare (non "TODO"). ✓
- **Type consistency**: `recordId: string | null` usato identico in Task 1/2/3; `onUploadingChange` coerente Task 1↔2. ✓
