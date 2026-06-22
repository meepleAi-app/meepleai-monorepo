# SP5 F3-FU-1 — KB Ingestion log tab (Design)

- **Date**: 2026-05-29
- **Issue**: [#1650](https://github.com/meepleAi-app/meepleai-monorepo/issues/1650) (P3, `enhancement`, `area/backend`, `area/frontend`)
- **Branch**: `feature/issue-1650-ingestion-log-tab` (parent: `main-dev`)
- **Wave**: SP5 F3-FU (post-#1652 Phase F3.1/F3.2 — re-skin KB tool-pages)
- **Sibling follow-ups**: #1651 (Used-by), #1653 (Azioni avanzate), #1654 (Preview PDF), #1655 (Badge count sub-nav)
- **Status**: Draft — pending user review

## Contesto

La PR [#1649](https://github.com/meepleAi-app/meepleai-monorepo/pull/1649) ha introdotto `KbDocDetailPanel` come pannello destro dell'Explorer, oggi single-section (hero + lista chunks). Il sub-nav 8-tab `KbSubNav` opera a livello pagina KB (Explorer, Vector Collections, Processing Queue, RAG Pipeline, Embedding, Feedback, Settings, Snapshots).

Questa issue introduce un **tab interno** al `KbDocDetailPanel` per visualizzare lo stato e il log della pipeline di ingestion (`ProcessingJob` + `Steps` + `LogEntries`) del documento selezionato. È il primo dei 5 follow-up della wave F3-FU: la struttura tabbed introdotta qui sarà riusata dai sibling.

### Materiali di riferimento

- Mock pattern visivo: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-queue.html` (panel destro `.job-hero` + `.timeline` + `.ingestion-log`). NB: il mock è per la pagina top-level "Processing Queue", ma il **pattern visivo del detail panel** è riusabile 1:1 per il nostro tab interno.
- Backend esistente: `BoundedContexts/DocumentProcessing/Domain/Entities/ProcessingJob.cs` + `ProcessingStep.cs` + `StepLogEntry.cs` + enum `ProcessingStepType` (5 step: Upload/Extract/Chunk/Embed/Index) + enum `StepLogLevel` (Info/Warning/Error).
- Endpoint precedente: `GetJobDetailQueryHandler` espone già `ProcessingJob` by `JobId`. Manca solo la variante by `PdfDocumentId`.

## Decisioni prese (brainstorming 2026-05-29)

| # | Decisione | Scelta | Motivazione |
|---|---|---|---|
| Q1 | Refresh model | **Polling lieve** (refetchInterval 6s solo se status ∈ {queued, processing}) | KISS per P3, niente SSE, costo server minimo. Auto-refresh durante run, statico a job concluso |
| Q2 | Storico retries | **Solo ultimo job** + chip `retry N/M` se RetryCount>0 | Pragmatico — la maggioranza dei doc ha 1 solo job |
| Q3 | Filtri level | **Nessun filtro, solo color coding** (info/warn/err) | Tipico doc ha ~10 log entries — filtri sono over-engineering per P3 |
| Q4 | Layout step | **Timeline always-expanded + log block separato sotto** | Aderente al mock — niente accordion. Pattern `.timeline` con 5 step verticali, ognuno con icona round + label + sub + durata. Sotto, `<pre class="ingestion-log">` con tutti i log concatenati |
| Q5 | Backend query | **Nuovo endpoint dedicato** `GET /api/v1/admin/knowledge-base/docs/{docId}/ingestion-log` | Scope chiaro, niente cambio al detail envelope esistente, 1 round-trip |
| Q6a | Tab switching | **URL-driven `?tab=ingestion`** | Deep-linkable, pattern già usato in libreria, predispone per altri tab follow-up (#1651/#1653/#1654) |
| Q6b | Componenti FE | **Nuovi `IngestionTimeline` + `IngestionLogBlock`** in `explorer/ingestion/` | La pagina `/admin/knowledge-base/queue` **non esiste ancora** come React, niente da riusare. Quando arriverà potrà estrarre/condividere |
| Q6c | KPI | **4 KPI**: Progresso% · Chunks done/total · Pages done/total · **Cost stimato lato FE** | Aderente al mock. Cost = utility `estimateCost(chunkCount, model)` con caveat "stima" |
| Q6d | Actions | **Download log (.txt)** + **Copy job ID** + **Re-enqueue** (solo se `canRetry`) | Re-enqueue riusa `RetryJobCommand(jobId)` esistente. Pausa/Boost/Cancella sono queue-level, fuori scope per tab doc-level |

## Architettura

```
GET /api/v1/admin/knowledge-base/docs/{docId}/ingestion-log
       │
       ▼
GetLatestIngestionLogByDocumentIdQuery(docId) → IMediator
       │
       ▼
GetLatestIngestionLogByDocumentIdQueryHandler
   .ProcessingJobs.AsNoTracking()
   .Where(j => j.PdfDocumentId == docId)
   .OrderByDescending(j => j.CreatedAt)
   .Include(j => j.Steps).ThenInclude(s => s.LogEntries)
   .FirstOrDefaultAsync()
       │
       ▼
IngestionLogDto { JobId, PdfDocumentId, Status, RetryCount, MaxRetries, CanRetry,
                  CreatedAt, StartedAt?, CompletedAt?, ErrorMessage?, CurrentStep?,
                  Steps[5] (IngestionStepDto), AllLogEntries[N] (IngestionLogEntryDto) }
       │
       ▼ (frontend)
useKbDocIngestionLog(docId)  ◄── React Query
   - queryKey: ['kb', 'doc', docId, 'ingestion-log']
   - refetchInterval: status ∈ {queued, processing} ? 6000 : false
   - enabled: docId !== null && tab === 'ingestion'
       │
       ▼
KbDocDetailPanel  (legge ?tab via useSearchParams)
   ├─ <KbDocDetailTabs activeTab={tab}/>  (URL-driven nav: Overview ↔ Ingestion log)
   ├─ Tab "Overview" (componente attuale, hero + chunks)
   └─ Tab "Ingestion log" (NEW)
        ├─ IngestionHero (icona doc, titolo, chip status, meta: jobId/retry N/M/started/elapsed, 4 KPI)
        ├─ IngestionTimeline (5 step always-expanded vertical)
        ├─ IngestionLogBlock (<pre> con timestamp grigio + color ok/warn/err)
        └─ IngestionActions (Download .txt, Copy job ID, Re-enqueue se canRetry)
```

### Mapping 5 step backend → label UI

I 5 step del backend mantengono la loro identità (no fake split). Label UI descrittive:

| Backend enum | UI label | UI sub-text |
|---|---|---|
| `Upload` | Upload & validate | "Validazione PDF, integrità, MIME, dimensioni" |
| `Extract` | Estrazione testo | "Unstructured / SmolDocling / Docnet" |
| `Chunk` | Chunking | "Sentence-based, target 512 tok" |
| `Embed` | Embedding | "Vector embedding generation" |
| `Index` | Indexing pgvector | "HNSW index, commit tx" |

### Cost stimato (utility `estimateCost`)

Formula: `chunkCount × 512 tokens × pricePerToken(model)`

| Modello | `pricePerToken` | Note |
|---|---|---|
| `bge-base-en-v1.5` (self-hosted default) | $0 | Mostra "$0.00" + tooltip "self-hosted" |
| `text-embedding-3-small` (OpenAI fallback) | $0.00000002 (= $0.02/1M tok) | Stima FE, marcata come "≈" |
| Altri | — | Mostra "—" + tooltip "non stimabile" |

Il modello si ricava dal nome dell'endpoint embedding. **Decisione delegata all'implementazione** (entrambe accettabili):
- (a) Hardcoded `'bge-base-en-v1.5'` nel FE come default MVP — pragmatica
- (b) Esporre `embeddingModel: string` come campo addizionale di `IngestionLogDto` (letto da config server-side) — più honest

In assenza di dati specifici, (a) è il default scelto e va commentato nel codice come "swap a (b) quando l'embedding model diventa configurabile per-job".

## Componenti

### Backend (nuovi sotto `BoundedContexts/DocumentProcessing/`)

- `Application/Queries/GetLatestIngestionLogByDocumentIdQuery.cs` — record `(Guid DocumentId) : IQuery<IngestionLogDto?>`
- `Application/Queries/GetLatestIngestionLogByDocumentIdQueryHandler.cs` — internal handler, EF AsNoTracking
- `Application/DTOs/IngestionLogDto.cs` — root DTO
- `Application/DTOs/IngestionStepDto.cs` — per step (`StepType`, `Status`, `StartedAt?`, `CompletedAt?`, `DurationMs?`, `MetadataJson?`)
- `Application/DTOs/IngestionLogEntryDto.cs` — per log entry (`Timestamp`, `Level`, `Message`, `StepType` — denormalizzato per render FE)
- `Routing/AdminKbIngestionEndpoints.cs` (o estendere esistente `AdminQueueEndpoints.cs`) — Map endpoint
- Authorization: stessa policy del resto del path `/admin/knowledge-base/*` (admin/superadmin)

### Frontend (nuovi sotto `apps/web/src/`)

```
components/admin/knowledge-base/explorer/
├── KbDocDetailPanel.tsx               (MODIFY: aggiungi tab switching via ?tab=)
├── KbDocDetailTabs.tsx                (NEW: <KbDocDetailTabs activeTab/>)
└── ingestion/
    ├── IngestionPanel.tsx             (NEW: container del tab, orchestra hero+timeline+log+actions)
    ├── IngestionHero.tsx              (NEW: icona, titolo, chip, meta, 4 KPI)
    ├── IngestionTimeline.tsx          (NEW: 5 step always-expanded vertical)
    ├── IngestionTimelineStep.tsx      (NEW: singolo step — icona/label/sub/durata)
    ├── IngestionLogBlock.tsx          (NEW: <pre> con color coding)
    ├── IngestionActions.tsx           (NEW: 3 botoni + handler)
    └── __tests__/
        ├── IngestionPanel.test.tsx
        ├── IngestionTimeline.test.tsx
        ├── IngestionLogBlock.test.tsx
        └── IngestionActions.test.tsx

hooks/queries/
└── useKbDocIngestionLog.ts            (NEW: React Query w/ smart refetchInterval)

lib/api/
└── admin-kb.ts                        (MODIFY: aggiungi fetchIngestionLog(docId))

lib/admin-kb/
└── calcCost.ts                        (NEW: utility estimateCost(chunkCount, model))
```

Path discipline: tutto sotto `components/admin/knowledge-base/explorer/`, allineato a `KbDocDetailPanel`, `KbExplorer`, `KbSubNav` già esistenti.

## Data flow

### Mount tab Ingestion log (URL `?tab=ingestion`)

1. `KbDocDetailPanel` legge `searchParams.tab` via `useSearchParams()` (Next 16 client component)
2. Se `tab === 'ingestion'` e `docId !== null`, renderizza `<IngestionPanel docId={docId}/>` invece di Overview
3. `<KbDocDetailTabs>` mostra link `Overview` e `Ingestion log` con underline + amber accent (riusa pattern `KbSubNav`)
4. `useKbDocIngestionLog(docId)` fa GET `/api/v1/admin/knowledge-base/docs/{docId}/ingestion-log`
5. Se `status ∈ {queued, processing}`, React Query refetcha ogni 6s; altrimenti `refetchInterval = false`
6. Render Hero (5 KPI Stat) → Timeline (5 step iconati per status) → LogBlock (entries ordinati per Timestamp asc) → Actions

### Action: Re-enqueue (solo se `canRetry === true`)

- Riusa `POST /api/v1/admin/queue/jobs/{jobId}/retry` (esistente, mapped via `AdminQueueEndpoints`)
- Comando: `RetryJobCommand(jobId)` — gestisce internamente la coda Quartz
- FE: optimistic `queryClient.invalidateQueries({queryKey: ['kb', 'doc', docId, 'ingestion-log']})` → forza refetch → mostra nuovo stato
- Toast: "Job re-enqueued" o errore

> **Nota path asimmetrico**: `GET ingestion-log` sta sotto `/admin/knowledge-base/docs/...` (è una view by-document), mentre `POST retry` sta sotto `/admin/queue/jobs/...` (è un'azione by-job, già esistente nel BC Queue). L'asimmetria è intenzionale: niente duplicate endpoint, riusiamo `RetryJobCommand` esistente. Il FE ottiene `jobId` dal payload `ingestion-log` e lo passa al retry.

### Action: Download log

- Client-side puro: serializza `logEntries` in formato `[YYYY-MM-DD HH:mm:ss.SSS] [LEVEL] message\n`
- Trigger download via `URL.createObjectURL(new Blob([content], {type: 'text/plain'}))` + `<a download="ingestion-{jobId}.log">`

### Action: Copy job ID

- `navigator.clipboard.writeText(jobId)` + toast "Job ID copiato"

## Error handling

| Scenario | Behavior |
|---|---|
| `docId` null | Tab disabilitato + placeholder "Seleziona un documento" (riusa pattern overview) |
| Doc esiste ma 0 ProcessingJob (legacy pre-pipeline) | API 200 con `null`; FE mostra empty state "Nessun job di ingestion. Il documento potrebbe essere stato indicizzato con una pipeline precedente." |
| Job `queued` | Step `Upload` con chip queued, gli altri 4 in `pending`. LogBlock vuoto con placeholder "In coda…" |
| Job `processing` | Step corrente con icona pulsante (animate-pulse), step precedenti `done`, successivi `pending`. LogBlock mostra entries esistenti, niente live-cursor (no SSE) |
| Job `failed` | Step failed evidenziato rosso, banner alert sopra timeline "Job fallito allo step X", LogBlock con Error highlighted, button Re-enqueue se `canRetry === true` |
| Job `completed` | Tutti gli step `done`, banner "Completato in N.Ns", niente Re-enqueue button |
| Backend 404 (docId non esiste) | React Query error → tab mostra error state "Documento non trovato" |
| Backend 403 (utente non admin) | Tab mostra "Non autorizzato" (improbabile — route admin-only, controllo lato layout) |
| Backend 5xx | React Query retry 2× then error message in panel con bottone "Riprova" |
| Polling 6s su job completato | refetchInterval auto-`false` (smart) → niente carico server inutile |
| Tab Overview attiva | `useKbDocIngestionLog` non parte (`enabled: tab === 'ingestion'`) → nessuna fetch |

## Testing

### Backend (target: 90%+ unit, integration con Testcontainers)

**Unit** (`Application.Tests/BoundedContexts/DocumentProcessing/Queries/GetLatestIngestionLogByDocumentIdQueryHandlerTests.cs`):
- `Handle_NoJob_ReturnsNull` — doc senza ProcessingJob → null
- `Handle_SingleJob_ReturnsLatest` — 1 job → ritorna correttamente serializzato
- `Handle_MultipleJobs_ReturnsMostRecent` — 3 job → ritorna quello con `CreatedAt` più recente
- `Handle_JobWithSteps_SerializesAllSteps` — job con 5 step → IngestionStepDto array di 5 elementi
- `Handle_JobWithLogEntries_OrdersByTimestampAsc` — entries cross-step → ordinati timestamp asc
- `Handle_FailedJobWithRetryAvailable_SetsCanRetryTrue` — Status=Failed + RetryCount < MaxRetries → CanRetry=true
- `Handle_FailedJobAtMaxRetries_SetsCanRetryFalse` — RetryCount === MaxRetries → CanRetry=false
- `Handle_EmptyGuid_ReturnsNull` — defensive (analoga ai precedenti pattern del repo)

**Integration** (`Integration/DocumentProcessing/IngestionLogEndpointsTests.cs`):
- `GET 200` con valid docId e job esistente
- `GET 200` con `null` body se nessun job
- `GET 401` senza auth
- `GET 403` con role non-admin
- `GET 404` (o 400) con docId malformed

### Frontend (target: 85%+ Vitest)

- `useKbDocIngestionLog.test.ts`:
  - smart refetchInterval: poll su `running`, no poll su `done`/`failed`
  - enabled solo se docId !== null && tab === 'ingestion'
- `IngestionTimeline.test.tsx`:
  - render 5 step con status corretti
  - step `running` ha animate-pulse class
  - step `failed` ha colore rose-500
- `IngestionLogBlock.test.tsx`:
  - color coding: Info → text-muted-foreground, Warning → text-amber-500, Error → text-rose-500
  - empty entries → mostra placeholder
- `IngestionActions.test.tsx`:
  - Re-enqueue visibile solo se canRetry === true
  - Download genera Blob con formato corretto
  - Copy job ID chiama `navigator.clipboard.writeText`
- `KbDocDetailPanel.test.tsx`:
  - tab switching via `?tab=overview` (default) e `?tab=ingestion`
  - link tab cambia URL preservando `docId`
- `calcCost.test.ts`:
  - `bge-base-en-v1.5` → `{value: 0, model: 'bge-base-en-v1.5', formula: 'self-hosted'}`
  - `text-embedding-3-small` con 100 chunks → ≈ `$0.00102` (100 × 512 × 0.00000002)
  - modello sconosciuto → ritorna `null`

### E2E smoke (Playwright, opzionale per P3)

`apps/web/e2e/admin/kb-ingestion-log.smoke.spec.ts`:
- login admin → naviga `/admin/knowledge-base?docId={existing}&tab=ingestion`
- verifica visible: hero, timeline (5 step), log block
- click "Copy job ID" → verifica clipboard
- (skip se richiede seed PDF con job — solo manual smoke)

## Out of scope (per follow-up)

- ❌ **SSE streaming live-cursor** — differito (niente infrastruttura SSE per admin KB oggi)
- ❌ **Filtri Info/Warning/Error** — differito a #1653 (azioni avanzate)
- ❌ **Storico job (retries collapsibile)** — differito (rarely needed)
- ❌ **Pause/Boost/Cancel actions** — sono queue-level, non doc-level
- ❌ **Cost reale via metadata** — `MetadataJson` di Embed step non è popolato; cost rimane stima FE finché non si decide di popolare server-side
- ❌ **Badge count nel sub-nav** — è scope di #1655 (separate PR)

## Rischi noti

| Rischio | Probabilità | Mitigazione |
|---|---|---|
| `RetryJobCommand` esistente non gestisce edge case "job già completato" | Bassa | Verificare validator in fase implementativa; FE comunque mostra Re-enqueue solo se `canRetry` |
| `MetadataJson` per step `Embed` non popolato → Cost stimato sempre $0 self-hosted | Media | Documentato come stima FE, tooltip esplicativo. Reale lasciato a follow-up |
| 5 step backend potrebbero non essere sempre tutti presenti (es. job interrotto) | Media | FE renderizza placeholder per step missing (status=`pending`, icona muted) |
| Polling 6s su 100+ doc aperti contemporaneamente | Bassissima | Improbabile (UI admin single-user); refetchInterval=false se job done |
| Endpoint `/api/v1/admin/knowledge-base/docs/{docId}/ingestion-log` collide con path esistenti | Bassa | Verificare con `Routing/` grep prima di mappare |

## Definition of Done

- [ ] Backend: query + handler + DTO + endpoint + unit tests + integration tests
- [ ] Frontend: 7 nuovi componenti + 1 hook + 1 utility + tests (≥85% coverage sulle nuove righe)
- [ ] KbDocDetailPanel refactor per URL-driven tab switching (`?tab=overview` default, `?tab=ingestion`)
- [ ] Lint + typecheck verdi
- [ ] PR aperta verso `main-dev` con descrizione che linka questo design doc
- [ ] `/code-review:code-review` eseguito, issue P0/P1 risolti
- [ ] Issue #1650 aggiornata su GitHub con riferimento PR
- [ ] Merge in `main-dev`
- [ ] Issue #1650 chiusa con commento auto-link al commit di merge

## Riferimenti

- Issue: [#1650](https://github.com/meepleAi-app/meepleai-monorepo/issues/1650)
- Issue umbrella SP5 F3-FU: vedi anche #1651/#1653/#1654/#1655
- Mock pattern visivo: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-queue.html` (panel destro)
- Design doc precedente wave: `docs/superpowers/specs/2026-05-28-sp5-admin-f3-kb-explorer-design.md`
- Backend entities: `BoundedContexts/DocumentProcessing/Domain/Entities/{ProcessingJob,ProcessingStep,StepLogEntry}.cs`
- Backend query esistente: `BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetJobDetailQueryHandler.cs`
- Backend retry esistente: `BoundedContexts/DocumentProcessing/Application/Commands/Queue/RetryJobCommandHandler.cs`
- FE detail panel attuale: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`
- FE sub-nav attuale: `apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx`
