# SP3 (#3269) — RAG heading-aware: big-bang re-index/re-embed

Sub-progetto 3/4 dell'epic #3266. Branch: `feature/issue-3269-sp3-bulk-reindex` (parent `main-dev`).

## Obiettivo
Il pipeline heading-aware (SP1 #3264, SP2 Slice C #3275 + Slice D #3282, parità fresh-ingest #3281/#3287) è **già merged su main-dev** ma **latente**: agisce solo sugli ingest freschi. Il corpus già indicizzato ha `PdfDocumentEntity.StructuredElementsJson = NULL`, `text_chunks` flat (`Level=1`, `Heading=NULL`, `role_tags=0`) su entrambi i sink (`text_chunks` + `pgvector_embeddings`). SP3 è il **gate** che attiva l'epic sui dati reali via re-index big-bang.

Deliverable (reconciliation #3269, 2026-07-22): **D1** job bulk su tutti i Ready · **D2** bump IndexerVersion · **D3** suite non-regressione retrieval EN+IT · **D4** orchestrazione per-env + runbook.

## Non-goal
Algoritmo chunking (SP1/SP2 fatto) · wiring fresh-ingest (#3281) · ranking/heading-boost/reranker = **SP4 #3270** · rimozione v0/v1.0 dal registry (deprecation ≥18mo) · IndexerVersion come dispatch-key (resta label di provenienza).

## Precondizione operativa
Container Docker `unstructured` DEVE essere ricostruito con codice SP1 prima di emettere `elements[]`; un container stale ritorna 0 elementi → re-index no-op silenzioso (chunk flat). Verifica nel runbook (slice 3).

## Audit (verificato): D1 = wrapper di fan-out
`ReindexDocumentCommand(Guid PdfId, string? IndexerVersion)` fa già per singolo PDF: guard in-flight → risoluzione versione `explicit ?? stored ?? Current` → delete TextChunks → reset stato→Pending + stamp versione → SaveChanges(xmin→ConflictException) → enqueue best-effort `EnqueuePdfCommand` sul rail Quartz (`PdfProcessingPipelineService` esegue extract→chunk→embed→index completo, ripopola StructuredElementsJson + role_tags su entrambi i sink). Quindi D1 non riscrive logica pipeline: fan-out su `ReindexDocumentCommand`. NON riusare `ProcessPendingPdfs` (esclude Ready), `VectorReembeddingService` (re-embed senza re-chunk), `BulkReindexFailed` (solo ProcessingJob Failed).

## Slice 1 (questa PR) — D2 + D1
- **D2** `IndexerVersionRegistry.cs`: aggiungere `V1_1_HeadingAware = new("v1.1","v1.1 — heading-aware chunking",true)`, `All = [Legacy, V1_0(kept), V1_1]`, `Current → V1_1`. Nessuna EF migration (colonna già esiste).
- **D1** nuovo `BulkReindexReadyCommand(Guid RequestedBy, string? TargetVersion=null) : ICommand<BulkReindexResult>` + handler (deps `MeepleAiDbContext`, `IMediator`, `IProcessingJobRepository`): selettore `ProcessingState==Ready && (IndexerVersion==null || IndexerVersion!=target)` (ordinato per UploadedAt), pacing su `MaxQueueSize - CountByStatus(Queued)`, fan-out `ReindexDocumentCommand(id, target)` con **versione esplicita**, `ConflictException`/capacity → skipped (mai abort). Endpoint `POST /admin/queue/reindex-ready` accanto a `/reindex-failed` (`AdminQueueEndpoints.cs:103`).

### TDD slice 1
- **Gruppo A** (unit `IndexerVersionRegistryTests`): RED `Current.Version=="v1.1"` + heading-aware; All contiene v0/v1.0/v1.1; TryGet/IsSelectable v1.1. GREEN = bump registry.
- **Gruppo B** (unit handler, EF InMemory + Moq IMediator/IProcessingJobRepository): (1) fan-out solo Ready con versione!=target, versione esplicita; (2) idempotenza (Ready+v1.1 → skip); (3) esclusione non-Ready; (4) pacing capacità; (5) ConflictException per-PDF → skipped, batch continua.
- **Gruppo C** (Testcontainers Postgres): guardia **null-comparison SQL trap** — `indexer_version <> 'v1.1'` esclude righe NULL in Npgsql; il selettore DEVE usare `== null || != target`; asserire che le righe Ready+NULL sono selezionate.

## Slice 2 — D3-lite: baseline EN+IT pre-SP3 + metriche graded (rag-smoke estesa). Da girare su staging PRIMA del big-bang.
## Slice 3 — D4: orchestrazione per-env (`make reindex-corpus ENV=`) + runbook + ADR. Dipende da slice 1.
## Slice 4 (deferred) — D3-full: suite EN+IT con ground-truth etichettato.

## Rischi/gotcha
- **Null-trap SQL** (critico): `!= target` esclude NULL in Postgres → clausola `== null || != target` + test integrazione Gruppo C.
- **Stored-wins**: `ReindexDocument` usa `explicit ?? stored ?? Current`; il bulk passa SEMPRE target esplicito (evita label-drift v1.0).
- **Enqueue swallow**: `ReindexDocument` non rilancia il fail di enqueue (coda piena) → doc bloccato Pending senza job; mitigato dal pacing (mai saturare la coda) + re-run che drena.
- **xmin** ottimistico → `ConflictException` per-PDF → skipped, mai abort.
- **role_tags 2 sink** in sync (garantito dal rail); assert post-reindex nel runbook.
- **CI**: integration test NON nel gate PR (Docker) → unit Gruppo B è il gate veloce; embedding-service pesante → suite EN+IT non è gate per-PR.

## Decisioni
- DA-1 trigger = **admin endpoint** (slice 1) + script make (slice 3). [adottato]
- DA-2 selettore = **solo Ready**. [adottato]
- DA-3 gate promozione prod = rag-smoke estesa (slice 2) vs suite etichettata full (slice 4) → **da confermare prima della slice 2**.
- DA-4 bump v1.1 sicuro ora (SP1/SP2 su main-dev). [confermato]
