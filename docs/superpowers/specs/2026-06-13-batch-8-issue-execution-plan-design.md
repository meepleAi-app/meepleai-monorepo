# Batch Execution Plan — 8 Issue Aperte (2026-06-13)

**Stato**: Approved — pronto per writing-plans
**Autore**: Aaron + Claude (sessione spec-panel)
**Tipo**: Multi-issue execution plan (non single feature)

## Contesto

Durante sessione `/sc:spec-panel 2026-06-13`, audit di un batch di 12 issue ha rilevato:

- **5 già chiuse**: `#2090`, `#2273`-`#2276` (no action)
- **8 aperte da lavorare**: epic `#2242` (umbrella con 5 sub aperte) + `#2271`, `#2088`, `#2089`, `#2190`

Obiettivo: chiudere tutte le 8 issue aperte in sessione continuativa senza interruzioni, con decisioni architetturali pre-risolte.

## Decisioni globali

| Decisione | Scelta | Razionale |
|-----------|--------|-----------|
| Epic `#2242` scope | Chiudere tutte le 5 sub aperte (3 P1 + 2 P2) | Elimina round-trip futuro. +~4gg effort accettato |
| Ordine esecuzione | Hotfix → Epic → FE user-facing | Minimizza context-switch, warmup veloce con #2190 |
| Branch strategy | 1 PR per issue, target `main-dev` | CLAUDE.md branch hygiene + #806 pre-creation safety |
| Verifica baseline | 0 regressione known-flaky tests | CLAUDE.md § Known Flaky Tests |

## Per-issue decisions

### `#2190` — Top-nav Hub link cleanup (P1, FE, ~2-4h)

**Decisione**: Full cleanup.

Acceptance criteria:
- Fix top-nav desktop href `/hub/games` → `/games`
- Fix bottom-tab mobile href `/hub/games` → `/games`
- Rinomina voce nav "Hub" → "**Games**"
- Elimina route `/hub/games` con 410 Gone (o redirect 308 a `/games?tab=discover`)
- Verifica sister `#2179` MainSidebar 8-voce assente desktop: aggiungi commento status, NO fix
- E2E Playwright: click "Games" voce → atterra su `/games?tab=discover`

Refs: audit `us-verification-log.md` § US-8 finding #1, Asse D P2 `#1899`, Stage 3 `#1026`.

### `#2271` — S3BlobStorageService PUT fails (P1, BE, ~4-8h)

**Decisione**: TransferUtility multipart upload + cleanup secondari.

Acceptance criteria:
- Refactor `S3BlobStorageService.StoreAsync()` per usare `TransferUtility.UploadAsync()` invece di `PutObject`
- Unit test su stream non-seekable (simula `Stream` senza `Length` noto)
- Repro test integrazione su MinIO locale: fallisce su path attuale, passa post-fix
- Update `secrets/storage.secret.example` default a `STORAGE_PROVIDER=local`
- Aggiorna health-check `s3storage`: HEAD bucket + PUT/DELETE di test object 1-byte (no più HEAD-only)
- Documentazione: nota in operations manual su quirk R2 streaming checksum

Refs: workaround dev `STORAGE_PROVIDER=local` già funzionante, sister `#1357` (R2 header quirk).

### `#2242` — Epic pdf-indexing flow repair (umbrella, P0)

Sub-issue da lavorare (tutte e 5):

#### `#2244` — BE refactor pdf-indexing (P1, ~2-3gg)

**Decisione**: Full refactor.

Acceptance criteria:
- `VectorDocument.Create()` static factory method (costruttore privato)
- `IPdfIndexingPipeline` interface + implementation in `BoundedContexts/DocumentProcessing/Application/Services/`
- DI registration: sia `IPdfIndexingPipeline` che implementation (CLAUDE.md `#2565`)
- Migrate 3 call site: `UploadPdfCommandHandler.Processing.cs:583`, `PdfProcessingPipelineService.cs:752`, `IndexPdfCommandHandler.cs:258`
- Rimuovere compensating manual `_mediator.Publish(VectorDocumentIndexedEvent)` ereditato da Sub `#2243`
- `FinalizeProcessingAsync` usa `pdfDomain.TransitionTo(PdfProcessingState.Ready)` (no EF entity bypass)
- Grep verifica: max 1 occorrenza `new VectorDocumentEntity {...}` constructor in codebase
- Unit + integration test: factory raise `VectorDocumentIndexedEvent`, pipeline invoke factory in tutti i call site

Bounded context discipline: setter `SharedGame.HasKnowledgeBase` resta in `SharedGameCatalog` context via handler.

#### `#2245` — SSE pdf-state-changed + AutoCreateAgent ungate (P2, ~2gg, post-#2244)

Acceptance criteria:
- Ungate `AutoCreateAgent` feature flag (vedi original spec Sub #3 in issue #2242)
- Extend SSE event stream `pdf-state-changed` per ogni transizione di stato
- E2E: cambio stato BE → evento SSE ricevuto FE entro 2s

#### `#2246` — FE admin pdf-indexing (P1, ~2-3gg, post-#2244)

Acceptance criteria:
- Block A — `upload-zone.tsx:94-119` `onSuccess`: aggiungere 6 `queryClient.invalidateQueries` come da issue
- Block A — `PdfUploadSection.tsx` stesso pattern
- Block B — Fix polling endpoint `PdfUploadSection.tsx:100`: `/api/v1/games/{gameId}/pdfs` → `/api/v1/admin/pdfs?gameId=...`
- Block C — Fix `STAGE_ORDER` in `PdfIndexingStatus.tsx:37-44` e `ProcessingMonitor.tsx:51-59`: allineare a backend enum (`Pending/Uploading/Extracting/Chunking/Embedding/Indexing/Ready/Failed`)
- Block D — `kb-cards` query in `[id]/client.tsx:201-205`: aggiungere `staleTime: 60_000` + `refetchInterval` conditional
- Block E — Se `#2245` shipped: ProcessingMonitor consuma SSE `pdf-state-changed` su Documents tab
- E2E Playwright: upload PDF → entro 5s `/admin/knowledge-base/documents` mostra nuovo PDF (no refresh manuale)
- Unit test: `STAGE_ORDER` contract con backend enum

#### `#2247` — FE user pdf-indexing wiring (P2, ~2gg, post-#2244)

Acceptance criteria:
- `GameDetailView.tsx:849`: rimuovi `<GameDetailKbDocList docs={[]} />` hardcoded → wire query reale
- `GameDetailView.tsx:835`: aggiungi `hasKnowledgeBase` guard sulla CTA chat
- Discover/dashboard: aggiungi badge KB su game cards (richiede `HasKnowledgeBase` su `GameDto` da Sub `#2243`)
- `GamesFilterPanel.tsx:530`: fix quick-link "AI Ready" URL
- E2E: utente con KB indicizzata vede CTA chat enabled + badge KB su discover

#### `#2248` — Test E2E + Prometheus + HybridCache + ADR (P1, ~2gg, parallelizable)

**Decisione**: Full + HybridCache (CLAUDE.md `#2620`).

Acceptance criteria:
- Integration test `tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowEndToEndTests.cs` con 4 asserzioni (DB + `/shared-games/{id}` + `/games/{id}/details` + `/knowledge-base/{id}/status`)
- Mock embedding service via `IEmbeddingService` test double (no chiamata reale Python)
- Prometheus metric `meepleai_pdf_indexed_no_kb_flag_total` con label `sharedGameId` (SLO=0)
- Background `KbFlagDriftAuditJob` Quartz ogni 10min: query `SharedGames LEFT JOIN PdfDocuments WHERE pdf.ProcessingState='Ready' AND game.HasKnowledgeBase=false`
- HybridCache pattern via `IHybridCacheService` (sostituisce L1 in-memory 15min su `SearchSharedGamesQueryHandler:104`, `GetSharedGameByIdQueryHandler:119`)
- ADR `docs/for-claude/architecture/adr/adr-XXX-kb-flag-cache-strategy.md` documenta scelta HybridCache
- E2E Playwright `apps/web/e2e/pdf-indexing-flow.spec.ts`: admin upload + user verify in journey end-to-end
- Operations manual: SLO `meepleai_pdf_indexed_no_kb_flag_total = 0`, alert su qualsiasi increment nonzero

### `#2088` — `/sessions/[id]` 404 (P1, FE, ~4-8h)

**Decisione**: Solo fix funzionale + creo issue audit P2 separata (NO impl).

Acceptance criteria:
- Empty state semantico in `/sessions/[id]/page.tsx` su `loadSession` 404: "Nessuna sessione attiva" + CTA "Inizia nuova sessione" + back link `/library/[gameId]`
- Defensive check in `useSessionStore.loadSession`: response 404 → set `error.kind = 'not-found'` (no generic "Riprova")
- Identificare e patchare source link `gameId → sessionId` (probabile `GamePartiteTab` o card click)
- Unit test: `useSessionStore` 404 ⇒ `error.kind === 'not-found'`
- E2E: `/sessions/<invalid-id>` → empty state visibile, CTA funzionante
- **Apre issue audit P2** (no impl in questa sessione): `/sessions/[id]` vs `sp4-session-skeleton-live.html`, documenta i 7 GAP rilevati in `/sc:spec-panel 2026-06-13`:
  - G1: layout 3-column desktop non implementato
  - G2: URL pattern child routes vs query param `?tab=`
  - G3: ChatAgent always-visible vs tab separata
  - G4: TopBar universale con live timer + connection status mancante
  - G5: Polymorphic renderers (Scoring/Turn/Toolkit) non astratti
  - G6: Zero game-specific extension implementate
  - G7: 5 stati canonici non standardizzati

### `#2089` — 5 search game widget unification (P1, FE, ~3-5gg)

**Decisione**: Full unification con `<GamePicker>` shared component.

Acceptance criteria:
- Nuovo `<GamePicker>` componente shared in `apps/web/src/components/features/game-picker/`
- Props: `source: 'library' | 'catalog' | 'both'`, `onSelect(game)`, `allowManualEntry?: boolean`, `placeholder?: string`
- Internal: debounce 300ms, error feedback via toast, mobile/desktop responsive (no DOM duplication)
- Refactor 5 widget:
  - `SessionCreationWizard.tsx:179` → usa `<GamePicker source="library" allowManualEntry />`
  - `SearchGameStep.tsx:158` (game-night) → usa `<GamePicker source="both" />` (library + catalog)
  - `session-wizard-mobile.tsx:327` → stesso component
  - `InlineGamePicker.tsx` → resta come overlay specializzato per game-night playlist (no refactor verso GamePicker, mantiene API attuale)
- BE: aggiungere `?search={query}` param a `/api/v1/library` (sostituisce client-side filter wasteful)
- Error feedback: catch{} → `toast.error("Errore caricamento giochi")`
- Manual entry validation: warning soft "Gioco non riconosciuto — il sistema non potrà fornire regole AI"
- Mobile/desktop collision fix: `useMediaQuery` o `<MediaQuery>` wrapper (no DOM duplication su `lg:hidden`)
- Unit test `<GamePicker>` con 5 stati (default/empty/loading/error/sse-disconnect skeleton)
- E2E: "wingspan" su SessionCreationWizard → 1 sola fetch API (no 8 keystroke), risultati visibili

## Effort estimate

| Fase | Issue | Effort |
|------|-------|--------|
| 1 | `#2190` | 2-4h |
| 1 | `#2271` | 4-8h |
| 2 | `#2244` BE refactor | 2-3gg |
| 2 | `#2248` test+HybridCache+ADR | 2gg (parallel #2244) |
| 2 | `#2246` FE admin | 2-3gg (post-#2244) |
| 2 | `#2245` SSE+AutoCreateAgent | 2gg (post-#2244) |
| 2 | `#2247` FE user | 2gg (post-#2244, parallel #2246) |
| 3 | `#2088` session 404 | 4-8h |
| 3 | `#2089` GamePicker | 3-5gg |
| **Totale** | | **~15-22gg single FTE** |

## Definition of Done globale

- [ ] Tutte le 8 issue chiuse via PR mergiata in `main-dev`
- [ ] Epic `#2242` chiuso (tutte le 5 sub closed)
- [ ] CI verde su ogni PR: Backend Fast + Frontend Tests + Frontend A11y E2E (blocking) + altri gate
- [ ] 0 regressione known-flaky baseline (CLAUDE.md § Known Flaky Tests)
- [ ] Issue audit P2 mockup `/sessions/[id]` aperta come tracking (documentati 7 GAP)
- [ ] ADR cache strategy committed in `docs/for-claude/architecture/adr/`
- [ ] Memory note aggiornato con pattern emersi (es. `P234 domain-event-bypass-via-ef-entity` da consolidare)
- [ ] Operations manual aggiornato con SLO Prometheus + S3 health-check + R2 quirk note

## Out of scope (chiarificazioni)

- Audit `/sessions/[id]` vs `sp4-session-skeleton-live.html` G1-G7: tracciato in issue P2 nuova, NO impl
- BGG search (admin-only per ADR-059, già locked)
- Discover search (sister `#2085`, separata)
- AddGameDrawer catalog search (già production via `#1974` T1+T2)
- `#2179` MainSidebar 8-voce: solo verifica/flag, NO fix in questa sessione

## Cross-references

- CLAUDE.md `#2242`, `#2565`, `#2568`, `#2620` (gotchas)
- ADR-054 (DevOps multi-branch), ADR-059 (BGG ToS), ADR-062 (config Environment)
- Memory `P181 spec-panel-on-fresh-umbrella`, `P234 domain-event-bypass-via-ef-entity` (nuovo)
- Sister issues: `#2179`, `#2085`, `#1974`, `#1357`, `#1529`, `#2177`, `#1816`
