# PDF Processing Queue Dashboard - Design Brief

**Date**: 2026-02-19
**Status**: Approved (Brainstorm session)
**Scope**: Backend queue infrastructure (Quartz) + Admin dashboard UI

## Overview

Dashboard amministrativa per monitorare e gestire la pipeline di elaborazione PDF → embedding. Sostituisce la pagina mock esistente a `/admin/knowledge-base` con una dashboard funzionale che mostra lo stato della coda in tempo reale.

## Decisioni Architetturali

| Aspetto | Decisione | Rationale |
|---------|-----------|-----------|
| **Target utenti** | Admin/Editor (utente finale riceve notifica) | Separazione ruoli |
| **Queue engine** | Quartz v1 (già presente), Temporal v2 futuro | Pragmatismo, zero nuove dipendenze |
| **UI pattern** | Lista compatta + detail panel on click | Efficienza informativa |
| **Route** | `/admin/knowledge-base` (sostituisce mock esistente) | Evoluzione naturale |
| **Azioni coda** | Add, Remove, Retry, Priorita (drag&drop), Cancel | Controllo completo |
| **Real-time** | SSE (endpoint gia esistenti) | Infrastruttura pronta |
| **Notifiche** | In-app + Email + Push a fine processing | Multi-canale |
| **Persistenza** | PostgreSQL (Quartz AdoJobStore) - sopravvive al restart | Affidabilita |
| **Limiti** | Max 100 PDF in coda, 50MB per file, 3 job paralleli | Capacita controllata |
| **Log retention** | Sempre, job completati visibili ma filtrabili | Storico completo |
| **Re-processing** | Solo full-retry, no step parziale (v1) | Semplicita |

## Architettura

```
+-------------------------------------------------------------+
|                    Frontend (Next.js)                         |
|  /admin/knowledge-base                                       |
|  +--------------------+  +-------------------------------+   |
|  |   Lista Compatta   |  |      Detail Panel             |   |
|  |                    |  |                                |   |
|  | Catan       done  |->|  Step logs, progress,          |   |
|  | Wingspan    run   |  |  azioni, metriche              |   |
|  | Gloom       queue |  |                                |   |
|  |                    |  |  [Cancel] [Retry] [Remove]     |   |
|  |  drag & drop      |  |                                |   |
|  +--------------------+  +-------------------------------+   |
|              | SSE (real-time)                                |
+-------------------------------------------------------------+
                          |
                    REST API + SSE
                          |
+-------------------------------------------------------------+
|                   Backend (.NET 9)                            |
|                                                              |
|  +--------------+    +---------------+    +---------------+  |
|  |  Endpoints   |    |  Quartz Jobs  |    |  Domain       |  |
|  |  (CQRS)      |--->|               |--->|  Events       |  |
|  |              |    |  PDF Pipeline |    |               |  |
|  | Queue CRUD   |    |  3 parallel   |    | Completed     |  |
|  | Priority     |    |  PostgreSQL   |    | Failed        |  |
|  | SSE stream   |    |  persisted    |    | Cancelled     |  |
|  +--------------+    +---------------+    +---------------+  |
|                                                 |            |
|                                          +------v--------+   |
|                                          | Notification  |   |
|                                          | In-app        |   |
|                                          | Email         |   |
|                                          | Push          |   |
|                                          +---------------+   |
+-------------------------------------------------------------+
                          |
              +-----------+-----------+
              v           v           v
        Unstructured  Embedding   Reranker
         (8001)       (8000)      (8003)
```

## Backend Design

### Domain: ProcessingJob Aggregate

```csharp
// DocumentProcessing/Domain/Entities/ProcessingJob.cs
ProcessingJob (Aggregate Root)
  - Id: Guid
  - PdfDocumentId: Guid (FK)
  - UserId: Guid (chi ha caricato)
  - Status: Queued | Processing | Completed | Failed | Cancelled
  - Priority: int (ordine drag & drop)
  - CurrentStep: Upload | Extract | Chunk | Embed | Index
  - Steps: ICollection<ProcessingStep>
  - CreatedAt / StartedAt / CompletedAt: DateTimeOffset
  - ErrorMessage: string?
  - RetryCount: int
  - MaxRetries: int (default: 3)

// Value Object per ogni step
ProcessingStep
  - StepName: ProcessingStepType (enum)
  - Status: Pending | Running | Completed | Failed | Skipped
  - StartedAt / CompletedAt: DateTimeOffset?
  - Duration: TimeSpan?
  - LogMessages: ICollection<StepLogEntry>
  - Metadata: JsonDocument? (chunks count, quality score, etc.)

StepLogEntry
  - Timestamp: DateTimeOffset
  - Level: Info | Warning | Error
  - Message: string
```

### Domain Events

```
JobQueuedEvent          -> Trigger Quartz scheduling
JobStartedEvent         -> Update SSE stream
JobStepCompletedEvent   -> Update SSE stream, log entry
JobCompletedEvent       -> Trigger notifications (in-app, email, push)
JobFailedEvent          -> Trigger notifications, increment retry
JobCancelledEvent       -> Cleanup, update SSE
JobPriorityChangedEvent -> Reorder Quartz triggers
```

### Application Layer

```
Commands:
  EnqueuePdfCommand          -> Validates, creates ProcessingJob, schedules Quartz
  CancelJobCommand           -> Interrupts Quartz job, updates status
  RetryJobCommand            -> Resets failed job, re-schedules
  RemoveFromQueueCommand     -> Removes queued (not processing) job
  ReorderQueueCommand        -> Updates priority for drag & drop

Queries:
  GetProcessingQueueQuery    -> Paginated list with filters
  GetJobDetailQuery          -> Single job + steps + logs
  GetJobLogsQuery            -> Streamed log entries for a job
```

### Quartz Configuration

```yaml
JobStore: AdoJobStore (PostgreSQL)
Max concurrent jobs: 3 (configurable)
Misfire policy: RescheduleNowWithRemainingRepeatCount
Retry: 3 attempts with exponential backoff (30s, 2min, 10min)
Priority: Lower number = higher priority (drag & drop mapped)
```

### API Endpoints

```
POST   /api/v1/admin/queue/enqueue          Add PDF to queue
DELETE /api/v1/admin/queue/{jobId}           Remove from queue (queued only)
POST   /api/v1/admin/queue/{jobId}/cancel    Cancel running job
POST   /api/v1/admin/queue/{jobId}/retry     Retry failed job
PUT    /api/v1/admin/queue/reorder           Reorder priorities (drag & drop)
GET    /api/v1/admin/queue                   List queue (paginated, filtered)
GET    /api/v1/admin/queue/{jobId}           Job detail + steps + logs
GET    /api/v1/admin/queue/{jobId}/stream    SSE for single job
GET    /api/v1/admin/queue/stream            SSE for entire queue
```

### File Structure (Backend)

```
DocumentProcessing/
  Domain/
    Entities/
      ProcessingJob.cs              (NEW - aggregate root)
      ProcessingStep.cs             (NEW - owned entity)
      StepLogEntry.cs               (NEW - owned entity)
    Enums/
      JobStatus.cs                  (NEW)
      ProcessingStepType.cs         (NEW)
      StepStatus.cs                 (NEW)
      LogLevel.cs                   (NEW)
    ValueObjects/
      JobPriority.cs                (NEW)
    Events/
      JobQueuedEvent.cs             (NEW)
      JobStartedEvent.cs            (NEW)
      JobStepCompletedEvent.cs      (NEW)
      JobCompletedEvent.cs          (NEW)
      JobFailedEvent.cs             (NEW)
      JobCancelledEvent.cs          (NEW)
      JobPriorityChangedEvent.cs    (NEW)
  Application/
    Commands/
      EnqueuePdfCommand.cs          (NEW)
      CancelJobCommand.cs           (NEW)
      RetryJobCommand.cs            (NEW)
      RemoveFromQueueCommand.cs     (NEW)
      ReorderQueueCommand.cs        (NEW)
    Queries/
      GetProcessingQueueQuery.cs    (NEW)
      GetJobDetailQuery.cs          (NEW)
    Jobs/
      PdfProcessingQuartzJob.cs     (NEW - IJob implementation)
  Infrastructure/
    Quartz/
      QuartzPdfQueueConfiguration.cs (NEW)
    Persistence/
      ProcessingJobConfiguration.cs  (NEW - EF config)
```

## Frontend Design

### Layout

```
+--------------------------------------------------------------+
| Processing Queue                    [+ Add PDF] [Filters]     |
| ------------------------------------------------------------- |
|                                                                |
|  +- Lista (40%) -------+  +- Detail Panel (60%) -----------+ |
|  |                      |  |                                 | |
|  | = Catan Rules   done |  | Wingspan Manual                | |
|  |   2min ago  47 ch    |  | Status: Embedding              | |
|  |                      |  | Progress: ========-- 78%       | |
|  | = Wingspan     run   |  |                                 | |
|  |   Processing 32/47   |  | Steps:                         | |
|  |                      |  | [x] Upload     0.3s            | |
|  | = Gloomhaven  queue  |  | [x] Extract    2.1s  S1        | |
|  |   In coda  Pri: 3    |  | [x] Chunk      0.8s  47ch      | |
|  |                      |  | [>] Embed      1.2s  32/47     | |
|  | = Terraforming fail  |  | [ ] Index      -                | |
|  |   Failed  Retry 1/3  |  |                                 | |
|  |                      |  | Logs:                           | |
|  |  drag to reorder     |  | > Extracting (Unstructured)     | |
|  |                      |  | > Quality: 0.85                 | |
|  |                      |  | > Chunking: 47 sentences        | |
|  |                      |  | > Embedding batch 4/5...        | |
|  |                      |  |                                 | |
|  |                      |  | [Cancel] [Retry] [Remove]       | |
|  +----------------------+  +---------------------------------+ |
|                                                                |
|  4 of 127 | < 1 2 3 ... 13 > | Filter: All                   |
+--------------------------------------------------------------+
```

### Filters

```
Status:    All | Queued | Processing | Completed | Failed | Cancelled
Date:      Today | Last 7d | Last 30d | Custom range
Game:      Dropdown with search
Sort:      Priority | Date | Status | Name
Search:    Free text (filename, game name)
```

### Component Structure

```
apps/web/src/app/admin/(dashboard)/knowledge-base/
  page.tsx                          (REPLACE existing)
  components/
    queue-list.tsx                  (NEW - drag & drop sortable list)
    queue-item.tsx                  (NEW - compact row)
    job-detail-panel.tsx            (NEW - right panel)
    job-step-timeline.tsx           (NEW - vertical step timeline)
    job-log-viewer.tsx              (NEW - scrollable log output)
    queue-filters.tsx               (NEW - filter bar)
    add-pdf-dialog.tsx              (NEW - upload dialog)
    queue-stats-bar.tsx             (NEW - stats summary)
    queue-actions.tsx               (NEW - action buttons)
  hooks/
    use-queue-sse.ts                (NEW - SSE real-time)
    use-queue-filters.ts            (NEW - filter state)
    use-queue-mutations.ts          (NEW - cancel, retry, remove, reorder)
  lib/
    queue-api.ts                    (NEW - API client)
```

### Dependencies

```
@dnd-kit/core + @dnd-kit/sortable   (drag & drop - lightweight, accessible)
```

## Notifications

### Trigger Flow

```
JobCompletedEvent / JobFailedEvent
  -> NotificationHandler (UserNotifications BC)
    -> In-app: Toast + badge on nav item
    -> Email: Template "Your PDF is ready" with document link
    -> Push: Browser notification "Processing completed for {name}"
```

### Recipients

- **Completed**: User who uploaded the PDF
- **Failed**: User who uploaded + all Admins (for visibility)

## Implementation Plan

| Phase | Scope | Labels |
|-------|-------|--------|
| **1** | ProcessingJob aggregate + EF migration + Quartz config | backend, area/pdf |
| **2** | Commands/Queries/Handlers + API endpoints | backend, area/pdf, area/api |
| **3** | SSE streaming for queue + single job | backend, area/pdf |
| **4** | Frontend: queue list + detail panel + filters | frontend, area/admin, area/ui |
| **5** | Frontend: drag & drop + actions (cancel/retry/remove) | frontend, area/admin, area/ui |
| **6** | Frontend: SSE integration + real-time updates | frontend, area/admin, area/ui |
| **7** | Notifications: in-app + email + push | backend, frontend |
| **8** | Tests + code review | tests, area/pdf |

## Constraints

- Max 100 PDF in queue simultaneously
- Max file size: 50MB per PDF
- Max 3 parallel processing jobs
- Jobs persist across server restarts (Quartz PostgreSQL store)
- Log retention: forever, filterable by status/date
- v1: Full retry only (no partial step re-processing)
- v2 future: Migrate to Temporal for durable workflows

## Existing Infrastructure to Leverage

- **Quartz 3.13.1**: Already in Api.csproj
- **SSE endpoints**: `/pdfs/{id}/progress/stream`, `/pdfs/{id}/status/stream`
- **Admin UI shell**: AdminShell, AdminSidebar, AdminTopNav
- **Backend API**: PDF upload (chunked), progress tracking, admin CRUD
- **Domain events**: Event-driven architecture already in place
- **UserNotifications BC**: Exists (needs verification of implementation level)

## References

- ADR-024: RAG Pipeline Enhancement Roadmap
- Existing: `EnhancedPdfProcessingOrchestrator.cs`
- Existing: `ExtractPdfTextCommandHandler.cs`
- Existing: `/admin/knowledge-base/upload` (mock UI to replace)
