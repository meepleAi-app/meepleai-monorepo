# Admin Embedding Flow — Gap Filling Design

**Date**: 2026-03-20
**Status**: Reviewed
**Approach**: B — Flow Enhancement (gap filling on existing pages)
**Review**: Spec-review + spec-panel passed (2026-03-20)

## User Story

> Come admin, voglio provare un gioco che ho acquistato (ho il PDF), testare il processo di embedding e come funziona l'agente usando il RAG con la KB creata. Dopo l'upload, voglio andare nella pagina admin per visualizzare le code di embedding.

**Scenario**: Game gia nel catalogo. Admin seleziona game esistente, uploada PDF, monitora embedding nella queue, testa l'agent RAG.

**Validazione**: Solo un browser test E2E valida l'implementazione della US.

## Stato Attuale

| Flusso | Backend | Frontend | Status |
|--------|---------|----------|--------|
| PDF Upload | Completo (standard + chunked + private) | Upload wizard 4-step | Funzionante |
| Embedding Queue | 15+ endpoints admin + SSE | Queue Dashboard completo | Funzionante |
| RAG Agent | Multi-agent (Tutor/Arbitro/Decisore) + SSE | Chat UI + Debug Console | Funzionante |
| KB Management | Auto-creazione su primo PDF | KB Hub 7 sezioni | Funzionante |
| Agent Testing | Auto Test Suite + Interactive Chat | `/admin/games/{id}/agent/test` | Funzionante |
| E2E Tests | — | 289 Playwright specs, POM completo | Infra pronta |

## Gap Identificati

1. **Nessun link diretto** tra upload result → queue → agent test
2. **Progress UI disabilitata** (`NEXT_PUBLIC_ENABLE_PROGRESS_UI=false`)
3. **Nessun chunk preview** nella queue dashboard
4. **Nessun percorso coerente** — admin deve navigare manualmente tra le pagine

## Design

### 1. Link Diretti nel Flusso

#### Upload → Queue

**File**: `apps/web/src/app/(authenticated)/upload/upload-client.tsx` — Step 4 (Complete)

Dopo upload con successo, pulsante contestuale visibile **solo per admin**:

```
+--------------------------------------+
|  PDF caricato con successo!          |
|                                      |
|  Il documento e stato accodato per   |
|  l'elaborazione.                     |
|                                      |
|  [Vai alla Queue ->]  [Carica altro] |
+--------------------------------------+
```

- Link: `/admin/knowledge-base/queue?jobId={jobId}&flow=embedding&gameId={gameId}&gameName={gameName}`
- Admin check via `useAdminRole()` hook esistente
- La queue dashboard evidenzia il job se riceve `jobId` in URL search params

#### Queue → Agent Test

**File**: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx`

Pulsante nel pannello dettaglio job, visibile quando `status === 'Completed'` e `gameId` presente:

```
+-------------------------------------+
|  Status: Completed                  |
|  Game: Catan                        |
|  Chunks: 142 | Duration: 45s        |
|                                     |
|  [Testa Agent ->]  [Reindicizza]    |
+-------------------------------------+
```

- Link: `/admin/games/{gameId}/agent/test?flow=embedding&gameName={gameName}`

### 2. Progress UI Real-Time

#### Feature Flag

Abilitare in dev/integration:

```
# .env.development / .env.local
NEXT_PUBLIC_ENABLE_PROGRESS_UI=true
```

| Env | ENABLE_PROGRESS_UI | Note |
|-----|-------------------|------|
| Dev locale | `true` | Default attivo |
| Integration | `true` | Via tunnel SSH |
| Staging | `true` | Diretto |
| Prod | `false` | Per ora disabilitato |

#### Post-Upload Progress Tracker

**File**: `apps/web/src/app/(authenticated)/upload/upload-client.tsx` — dopo step 4

Nuovo componente `UploadProgressTracker` che si connette a SSE `GET /api/v1/pdfs/{pdfId}/progress/stream` (endpoint verificato in `PdfProcessingEndpoints.cs`, handler: `StreamPdfProgressQuery`):

```
+------------------------------------------+
|  Catan-Rules-v5.pdf                      |
|                                          |
|  * Upload      [done]                    |
|  * Extraction  [done]  (12s)             |
|  * Chunking    [progress]  142 chunks... |
|  o Embedding   [pending]                 |
|  o Indexing    [pending]                  |
|                                          |
|  ==============================-----  60%|
|                                          |
|  [Vai alla Queue ->]  [Carica altro]     |
+------------------------------------------+
```

**Implementazione**:
- Riusa hook esistente `usePdfProgress(documentId)` che wrappa SSE (nota: il parametro e `documentId`, non `pdfId`)
- Mappa step states: `Pending -> Processing -> Completed -> Failed`
- Fallback a polling se SSE fallisce (pattern gia usato nella queue dashboard)
- Auto-hide dopo 5s dal completamento, mostra solo risultato finale con pulsanti

### 3. Chunk Preview nella Queue Dashboard

#### Backend — Riuso Endpoint Esistente

Esiste gia `GET /api/v1/admin/sandbox/pdfs/{id}/chunks/preview?limit=20` in `AdminSandboxEndpoints.cs`.

**Query**: `GetPdfChunksPreviewQuery` (in `KnowledgeBase/Application/Queries/`)
**Handler**: `GetPdfChunksPreviewQueryHandler`
**DTO esistente**: `ChunkPreviewDto` con campi:
```csharp
Guid EmbeddingId
string TextContent
int ChunkIndex
int PageNumber
string Model
DateTime CreatedAt
```

**Estensione necessaria**: Aggiungere supporto paginazione e search server-side al query/handler esistente:
- Nuovo parametro `page` (default 1) e `pageSize` (default 20, max 100)
- Nuovo parametro `search` (filtra su `TextContent`)
- Aggiungere `FluentValidation`: `page >= 1`, `pageSize` tra 1 e 100
- Response wrappata in `PaginatedResult<ChunkPreviewDto>` con `total`, `page`, `pageSize`

**Nota**: Il `pdfDocumentId` si ottiene dal `ProcessingJob.PdfDocumentId` gia presente nel job detail. Non serve un nuovo endpoint su `/admin/queue/`.

#### Frontend — Chunk Preview Tab

**File**: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx`

Nuovo tab "Chunks" accanto a Steps e Logs:

```
+---------------------------------------------------+
|  [Steps]  [Logs]  [Chunks]                        |
|                                                    |
|  Chunks (142 totali)                    Pagina 1/8 |
|  [Search: cerca nei chunk...]                      |
|                                                    |
|  #1  p.3-4  "Setup: Place the board..."           |
|      tokens: 312  |  embedding: [done]             |
|                                                    |
|  #2  p.5    "On your turn, you may..."            |
|      tokens: 287  |  embedding: [done]             |
|                                                    |
|  #3  p.5-6  "Trading: You can trade..."           |
|      tokens: 345  |  embedding: [progress]         |
|                                                    |
|  > Mostra chunk completo                           |
+---------------------------------------------------+
```

**Componente**: `ChunkPreviewTab`
- Lazy-loaded (carica solo quando tab selezionato)
- Paginazione server-side (20 chunk/pagina)
- Search server-side sul `TextContent` (parametro `search` nell'endpoint)
- Expand/collapse per chunk completo
- Badge stato: done (indexed), progress (processing), pending, failed

**Visibilita**:
- Tab attivo solo quando job ha superato step Chunking (status >= Chunking)
- Tab disabilitato con tooltip "In attesa dell'estrazione" se ancora in Extraction

### 4. Breadcrumb Contestuale — Embedding Flow Banner

#### Meccanismo

Query param `flow=embedding&gameId={id}&gameName={name}` propagato tra le pagine:

```
Upload -> /admin/knowledge-base/queue?flow=embedding&gameId=xxx&gameName=Catan
Queue  -> /admin/games/xxx/agent/test?flow=embedding&gameName=Catan
```

#### Layout

```
+------------------------------------------------------+
|  Catan - Flusso Embedding                         [x] |
|  Upload [done] -> Queue [progress] -> Agent Test o    |
+------------------------------------------------------+
```

**Regole stato**:
- Upload: sempre done (ci siamo arrivati da li)
- Queue: done se job completed, progress se in progress, failed se errore
- Agent Test: pending (non ancora), done se utente ha inviato almeno una query

#### Componente: `EmbeddingFlowBanner`

**Implementazione**:
- Legge `flow`, `gameId`, `gameName` da `useSearchParams()`
- Per stato queue: usa `jobId` dal flow params per chiamare `GET /api/v1/admin/queue/{jobId}` (restituisce job detail con status). In alternativa, `GET /api/v1/admin/queue/?gameId={gameId}` supporta filtro gameId
- Dismissibile con "x" (salva in sessionStorage)
- Non appare se manca `flow=embedding`

**Montaggio**:
- `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/layout.tsx`
- `apps/web/src/app/admin/(dashboard)/games/[gameId]/agent/test/layout.tsx`

### 5. E2E Browser Test Parametrizzato

#### File

- **Spec**: `apps/web/e2e/flows/admin-embedding-flow.spec.ts`
- **POM**: Riusa esistenti in `e2e/pages/admin/`, `e2e/pages/upload/`

#### Scenario

```
Test: "Admin embedding flow - upload PDF -> queue monitoring -> agent test"

  1.  Login come admin
  2.  Vai a /upload
  3.  Seleziona game esistente dal catalogo
  4.  Upload PDF di test (fixture)
  5.  Verifica progress tracker con step corretti
  6.  Click "Vai alla Queue"
  7.  Verifica redirect a /admin/knowledge-base/queue?jobId=xxx
  8.  Verifica job evidenziato nel pannello
  9.  Verifica flow banner visibile con game name
  10. Attendi job completion (timeout 120s dev / 180s integration)
  11. Verifica tab "Chunks" disponibile
  12. Apri tab Chunks, verifica >= 1 chunk visibile
  13. Click "Testa Agent"
  14. Verifica redirect a /admin/games/{gameId}/agent/test
  15. Verifica flow banner mostra Queue done
  16. Invia domanda di test ("Quanti giocatori?")
  17. Verifica risposta streaming (non vuota, con citations)
  18. Test passa
```

#### Parametrizzazione

```typescript
const envConfig = {
  dev: {
    baseUrl: 'http://localhost:3000',
    apiUrl: 'http://localhost:8080',
    testPdf: 'e2e/test-data/sample-rules-short.pdf',
    gameSelector: 'seed',   // game da seed data
    timeout: 120_000,
  },
  integration: {
    baseUrl: process.env.INTEGRATION_URL,
    apiUrl: process.env.INTEGRATION_API_URL,
    testPdf: 'e2e/test-data/sample-rules-short.pdf',
    gameSelector: 'search', // cerca game reale
    timeout: 180_000,
  },
};
```

Selezionato via `TEST_ENV=dev|integration` o Playwright project config.

#### Test Data

- **PDF fixture**: `e2e/test-data/sample-rules-short.pdf` — 2-3 pagine per embedding veloce
- **Dev**: Game da seed data
- **Integration**: Game esistente nel catalogo staging

#### Assertions

| Step | Assertion | Tipo |
|------|-----------|------|
| Upload | File accepted, jobId returned | API response |
| Progress | Steps visibili, progress bar avanza | DOM + SSE |
| Queue link | Redirect corretto con jobId | Navigation |
| Job highlight | Job selezionato nel pannello | DOM state |
| Flow banner | Visibile con game name | DOM |
| Job completion | Status = Completed entro timeout | Poll/SSE |
| Chunks | Tab attivo, >= 1 chunk con preview | DOM + API |
| Agent test link | Redirect a page corretta | Navigation |
| Agent response | Testo non vuoto + citations presenti | SSE stream |

#### Mocking

- **Dev**: Nessun mock — E2E reale contro stack locale
- **Integration**: Nessun mock — test contro staging via tunnel
- **Eccezione**: Se embedding service non disponibile, `test.skip` annotato

#### Cleanup / Teardown

- **Dev**: `afterAll` hook cancella il PDF di test e il job dalla queue via API admin (`DELETE /admin/queue/{jobId}`, `POST /admin/pdfs/bulk-delete`)
- **Integration**: Stesso cleanup via API. I dati di test non devono accumularsi.
- **Identificazione**: Il PDF fixture ha un nome univoco con timestamp per evitare conflitti tra run paralleli

## File Impattati

### Frontend (Modifiche)

| File | Modifica |
|------|----------|
| `apps/web/.env.development` | `NEXT_PUBLIC_ENABLE_PROGRESS_UI=true` |
| `apps/web/src/app/(authenticated)/upload/upload-client.tsx` | Pulsante "Vai alla Queue" + progress tracker |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx` | Pulsante "Testa Agent" + tab Chunks |

### Frontend (Nuovi)

| File | Descrizione |
|------|-------------|
| `apps/web/src/components/ui/admin/upload-progress-tracker.tsx` | Progress tracker con SSE |
| `apps/web/src/components/ui/admin/chunk-preview-tab.tsx` | Tab preview chunks |
| `apps/web/src/components/ui/admin/embedding-flow-banner.tsx` | Flow banner contestuale |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/layout.tsx` | **Nuovo** — layout per mount EmbeddingFlowBanner |
| `apps/web/src/app/admin/(dashboard)/games/[gameId]/agent/test/layout.tsx` | **Nuovo** — layout per mount EmbeddingFlowBanner |
| `apps/web/e2e/flows/admin-embedding-flow.spec.ts` | E2E test parametrizzato |
| `apps/web/e2e/pages/admin/queue-dashboard.page.ts` | **Nuovo POM** — Page Object per queue dashboard |
| `apps/web/e2e/test-data/sample-rules-short.pdf` | PDF fixture per test |

### Backend (Modifiche)

| File | Modifica |
|------|----------|
| `KnowledgeBase/Application/Queries/GetPdfChunksPreviewQuery.cs` | Aggiungere paginazione (`page`, `pageSize`) e search (`search`) |
| `KnowledgeBase/Application/Queries/GetPdfChunksPreviewQueryHandler.cs` | Implementare paginazione e filtro server-side |
| `KnowledgeBase/Application/Validators/GetPdfChunksPreviewQueryValidator.cs` | **Nuovo** — FluentValidation: `page >= 1`, `pageSize` 1-100 |
| `Routing/AdminSandboxEndpoints.cs` | Aggiungere query params `page`, `pageSize`, `search` |

## Rischi e Mitigazioni

| Rischio | Probabilita | Mitigazione |
|---------|-------------|-------------|
| SSE non funziona in dev (embedding service down) | Media | Fallback polling + test.skip |
| PDF fixture troppo grande per embedding veloce | Bassa | PDF 2-3 pagine, timeout generoso |
| Query param `flow` perso in navigazione | Bassa | Propagazione esplicita in ogni link |
| VectorDocument non ha tutti i campi per chunk preview | Media | Verificare schema, adattare DTO |

## Criteri di Accettazione

1. Admin uploada PDF per game esistente e vede progress real-time
2. Pulsante "Vai alla Queue" porta alla queue con job evidenziato
3. Flow banner visibile e mostra stato corretto in ogni pagina
4. Tab "Chunks" mostra chunk estratti con metadata
5. Pulsante "Testa Agent" porta al test agent per quel game
6. Agent risponde con citations dal PDF appena embeddato
7. E2E test Playwright passa in ambiente dev e integration
