# Admin PDF→KB Workflow Improvements

**Date**: 2026-03-15
**Status**: Draft
**Scope**: 4 UX improvements to the admin SharedGame PDF→KB pipeline

## Problem Statement

The admin PDF→KB workflow requires excessive context-switching between SharedGames and KnowledgeBase sections. Key gaps:

1. No visibility of recently processed PDFs on the SharedGames landing page
2. No way to prioritize urgent PDF processing during upload
3. No inline queue visibility in the game detail page
4. No cross-page notification when PDF processing completes

## Feature 1: Recently Processed Widget

### Overview

A collapsible widget on `/admin/shared-games/all` showing the 10 most recent PDFs with processing status, placed above the existing `GameCatalogGrid`.

### Component: `RecentlyProcessedWidget`

**Location**: `apps/web/src/components/admin/shared-games/RecentlyProcessedWidget.tsx`

**Layout**: Collapsible card with header "Ultimi PDF elaborati" + collapse toggle. Table with 5 columns:

| Column | Content | Notes |
|--------|---------|-------|
| PDF | Filename + icon | Truncated at 40 chars |
| Gioco | Game name + thumbnail (24px) | Link to game detail |
| Stato | Status badge (mapped from `processingState`) | Same color system as KbStatusBadge |
| Tempo | Relative timestamp ("2min fa") | `completedAt` or `updatedAt` |
| Azione | Context action button | "Vai al gioco" / "Vai alla coda" / "Riprova" |

**Footer**: "Vedi tutti →" link to `/admin/knowledge-base/documents`

**Collapse state**: Persisted in `localStorage` key `admin:recentPdfs:collapsed`.

**Status mapping** (`processingState` → Italian label → color):

| Backend `processingState` | Display Label | Color |
|---------------------------|---------------|-------|
| `"Ready"` | "Indicizzato" | Green |
| `"Uploading"`, `"Extracting"`, `"Chunking"`, `"Embedding"`, `"Indexing"`, `"Pending"` | "Elaborazione" | Blue (animated) |
| `"Failed"` | "Fallito" | Red |

### Backend: New Query + Endpoint

**Endpoint**: `GET /api/v1/admin/shared-games/recently-processed?limit=10`

**Query**: `GetRecentlyProcessedDocumentsQuery` (MediatR)

**Handler logic**:
```
SELECT pd.Id, pd.FileName, pd.ProcessingState, pd.UpdatedAt, pd.CompletedAt,
       pd.ErrorCategory, pd.CanRetry,
       sgd.SharedGameId, sg.Name AS GameName, sg.ThumbnailUrl
FROM pdf_documents pd
JOIN shared_game_documents sgd ON sgd.PdfDocumentId = pd.Id
JOIN shared_games sg ON sg.Id = sgd.SharedGameId
WHERE sgd.SharedGameId IS NOT NULL
ORDER BY COALESCE(pd.CompletedAt, pd.UpdatedAt) DESC
LIMIT @limit
```

**Response DTO**: `RecentlyProcessedDocumentDto`
```csharp
public sealed record RecentlyProcessedDocumentDto(
    Guid PdfDocumentId,
    string FileName,
    string ProcessingState,      // "Ready", "Extracting", "Failed", etc.
    DateTime Timestamp,          // CompletedAt ?? UpdatedAt
    string? ErrorCategory,
    bool CanRetry,
    Guid SharedGameId,
    string GameName,
    string? ThumbnailUrl);
```

**Routing**: Registered in `AdminSharedGameContentEndpoints.cs` (existing file) with admin authorization.

**Note — Cross-BC read query**: This query joins tables from DocumentProcessing BC (`pdf_documents`) with SharedGameCatalog BC (`shared_game_documents`, `shared_games`) via `MeepleAiDbContext`. This is a deliberate cross-BC read following the existing precedent set by `GetGameRagReadinessQueryHandler` in SharedGameCatalog. The query is placed in SharedGameCatalog Application because the primary consumer is the SharedGames admin UI.

### Frontend Integration

- `sharedGamesClient.ts`: Add `getRecentlyProcessed(limit?: number)` method
- Page `/admin/shared-games/all/page.tsx`: Import and render `RecentlyProcessedWidget` above `GameCatalogGrid`
- Uses `useQuery` with `refetchInterval: 15_000` (15s polling for near-real-time updates)
- Retry action calls existing `POST /admin/queue/{jobId}/retry` endpoint

---

## Feature 2: Priority Urgent Selector

### Overview

A priority selector in `PdfUploadSection` that allows admins to mark uploads as "Urgente" (top of queue).

### UI Changes to `PdfUploadSection`

**New prop**:
```typescript
interface PdfUploadSectionProps {
  // ... existing props
  showPrioritySelector?: boolean;  // default: true for admin context
}
```

**Selector**: Appears after file selection, before upload starts. Two-option `Select` (shadcn):
- **Normale** (default) — "Elaborazione standard"
- **Urgente — in testa alla coda** — "Priorita massima, elaborato per primo"

**Visual**: Urgent option has amber accent (`text-amber-600`) + `Zap` icon.

### Backend Changes

**Endpoint modification**: `POST /api/v1/ingest/pdf`

Add optional query parameter: `?priority=urgent`

**Command**: `UploadPdfCommand` — add optional `Priority` property (string, nullable). The existing `UploadPdfCommandHandler` checks: if `priority=urgent` and user is admin, set `ProcessingPriority.Urgent(30)` on the auto-enqueued `ProcessingJob`. Non-admin users cannot set urgent (silently ignored, defaults to `Normal`).

**Note — Enqueue relationship**: The upload endpoint auto-enqueues via `UploadPdfCommandHandler`. The existing `EnqueuePdfCommand` (used in `AdminQueueEndpoints`) already supports a `Priority` field for manual re-enqueue. This change adds the same capability to the upload path. Both flows ultimately set `ProcessingJob.Priority`.

**Validation**: Create `UploadPdfCommandValidator.cs` — `priority` must be `null`, `"normal"`, or `"urgent"`.

### Upload Flow

1. Admin selects file → priority selector appears
2. Admin picks "Urgente" (or keeps "Normale")
3. XHR upload sends `FormData` to `/api/v1/ingest/pdf?priority=urgent`
4. `UploadPdfCommandHandler` creates `PdfDocument` + `ProcessingJob` with `Priority.Urgent`
5. Quartz scheduler picks it up first (ORDER BY Priority DESC)

---

## Feature 3: Mini-Queue Widget + Deep-Link

### Overview

An inline processing queue widget in the Documents tab of `/admin/shared-games/[id]`, showing this game's jobs + global queue count, with a deep-link to the full queue dashboard.

### Component: `GameProcessingQueue`

**Location**: `apps/web/src/components/admin/shared-games/GameProcessingQueue.tsx`

**Position**: Below `PdfUploadSection` in the Documents tab of `GameDetailClient`.

**Layout**:
```
┌─────────────────────────────────────────────────┐
│ Coda elaborazione                               │
│ 3 di questo gioco · 12 totali                   │
├─────────────────────────────────────────────────┤
│ 🔴 rulebook.pdf    Urgente   ████░░ 67%        │
│ 🟡 errata.pdf      Normale   ░░░░░░ In coda    │
│ 🟡 homerule.pdf    Normale   ░░░░░░ In coda    │
├─────────────────────────────────────────────────┤
│ Apri coda completa →                            │
└─────────────────────────────────────────────────┘
```

**Props**:
```typescript
interface GameProcessingQueueProps {
  gameId: string;
}
```

**Rows**: Max 5 jobs for this game. Each row shows:
- Priority badge: `Urgente` (red) / `Alta` (amber) / `Normale` (blue) / `Bassa` (gray)
- Filename (truncated)
- Progress bar or status text ("In coda", percentage, "Completato", "Fallito")

**Header counts**:
- Game-specific: from `GET /api/v1/admin/queue?gameId={id}&status=Queued,Processing` → count
- Global: from `GET /api/v1/admin/queue/status` → `queueDepth` field

**Backend change required**: `GetProcessingQueueQuery` currently has no `GameId` filter. Add optional `Guid? GameId` parameter. The handler must join `ProcessingJobs` → `PdfDocument` → `SharedGameDocument` (on `PdfDocumentId`) to filter by `SharedGameDocument.SharedGameId == gameId`. This is a non-trivial handler change — see "Modified Files (Backend)" section.

**Deep-link footer**: `` <Link href={`/admin/knowledge-base/queue?gameId=${gameId}`}>Apri coda completa →</Link> ``

### Real-Time Updates

- SSE connection to `GET /api/v1/admin/queue/stream`
- Client-side filter: only process events where `event.gameId === gameId`
- On `JobCompleted`/`JobFailed`: refetch query data
- On `StepCompleted`: update progress inline

### Data Fetching

Two parallel `useQuery` calls:
1. `adminClient.getQueueJobs({ gameId, limit: 5, status: ['Queued', 'Processing'] })`
2. `adminClient.getQueueStatus()` → returns `{ queueDepth, processingCount, ... }`

Both with `refetchInterval: 10_000` as fallback (SSE is primary update mechanism).

---

## Feature 4: Toast Notification on PDF Completion

### Overview

A global SSE listener mounted in the admin dashboard layout that shows toast notifications when any PDF completes or fails processing, regardless of which admin page is active.

### Component: `PdfProcessingNotifier`

**Location**: `apps/web/src/components/admin/layout/PdfProcessingNotifier.tsx`

**Mount point**: Inside `apps/web/src/app/admin/(dashboard)/layout.tsx`, rendered as a client component alongside `UnifiedShell`. This ensures it's mounted once for all admin pages and unmounted when leaving the admin section.

### SSE Connection

- Connects to `GET /api/v1/admin/queue/stream` on mount
- Filters for event types: `JobCompleted`, `JobFailed`
- Auto-reconnect on connection loss (exponential backoff: 1s, 2s, 4s, max 30s)
- Disconnects on unmount (cleanup in `useEffect` return)

### Toast Display

**Success toast** (JobCompleted):
```
✅ rulebook.pdf per Catan
   Indicizzazione completata
   [Vai al gioco]
```

**Failure toast** (JobFailed):
```
❌ manual.pdf per Ark Nova
   Elaborazione fallita: Parsing error
   [Riprova]
```

**Behavior**:
- Auto-dismiss after 8 seconds
- Click "Vai al gioco" → `router.push(`/admin/shared-games/${gameId}`)`
- Click "Riprova" → `POST /api/v1/admin/queue/${jobId}/retry` + dismiss
- Max 3 concurrent toasts (older ones dismissed)
- Uses `toast` from `sonner` (NOT `useToast` — that's a `console.warn` stub for E2E testing)

### Backend SSE Payload Enhancement

**Important**: Domain events (`JobCompletedEvent`, `JobFailedEvent`) must NOT be modified — they are internal domain concerns and should not carry read-model data like game names. Instead, enrich the SSE DTOs at the stream handler layer.

**SSE DTOs to modify** (in `QueueStreamEventDto.cs`):

```csharp
// Current JobCompletedData
public sealed record JobCompletedData(int TotalDurationSeconds);

// Enhanced JobCompletedData
public sealed record JobCompletedData(
    int TotalDurationSeconds,
    string? FileName,          // NEW
    Guid? SharedGameId,        // NEW
    string? GameName);         // NEW

// Same pattern for JobFailedData — add FileName, SharedGameId, GameName, ErrorCategory
```

**Enrichment**: In `JobCompletedStreamHandler` and `JobFailedStreamHandler` (within `QueueStreamEventHandlers.cs`), inject `ISharedGameDocumentRepository` (or use `MeepleAiDbContext` directly) to resolve `PdfDocumentId → SharedGameDocument → SharedGame` at the SSE emission layer. Use a lightweight query:

```csharp
var gameInfo = await _dbContext.SharedGameDocuments
    .Where(sgd => sgd.PdfDocumentId == notification.PdfDocumentId)
    .Select(sgd => new { sgd.SharedGameId, GameName = sgd.SharedGame.Name })
    .FirstOrDefaultAsync(ct);
```

**Domain events remain unchanged**: `JobCompletedEvent`, `JobFailedEvent` keep their current structure.

---

## Shared Concerns

### Authentication & Authorization

All endpoints require admin role. Existing `RequireAdminSession()` pattern applies.

### Error Handling

- Widget data fetch failures: Show "Impossibile caricare" inline message with retry button
- SSE connection failures: Silent reconnect with backoff, no user-facing error
- Priority parameter validation: Invalid values default to `Normal`

### Testing Strategy

| Feature | Unit Tests | Integration Tests | E2E |
|---------|-----------|-------------------|-----|
| RecentlyProcessedWidget | Component render + states | Query handler | Navigate + verify |
| PrioritySelector | Select interaction | Ingest with priority | Upload + verify queue order |
| GameProcessingQueue | Render + counts | Queue filter by game | Deep-link navigation |
| PdfProcessingNotifier | Toast display logic | SSE event parsing | Cross-page notification |

**Target**: 15-20 unit tests, 8-10 integration tests, 4 E2E scenarios.

### Localization

All user-facing strings in Italian, consistent with existing admin UI:
- "Ultimi PDF elaborati", "Coda elaborazione", "Indicizzazione completata"
- "Elaborazione fallita", "Vai al gioco", "Apri coda completa"
- "Urgente — in testa alla coda", "Elaborazione standard"

---

## Files to Create/Modify

### New Files (Frontend)
- `apps/web/src/components/admin/shared-games/RecentlyProcessedWidget.tsx`
- `apps/web/src/components/admin/shared-games/GameProcessingQueue.tsx`
- `apps/web/src/components/admin/layout/PdfProcessingNotifier.tsx`

### Modified Files (Frontend)
- `apps/web/src/components/admin/shared-games/PdfUploadSection.tsx` — add priority selector
- `apps/web/src/app/admin/(dashboard)/shared-games/all/page.tsx` — add RecentlyProcessedWidget
- `apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx` — add GameProcessingQueue
- `apps/web/src/app/admin/(dashboard)/layout.tsx` — mount PdfProcessingNotifier
- `apps/web/src/lib/api/clients/sharedGamesClient.ts` — add `getRecentlyProcessed()`
- `apps/web/src/lib/api/clients/adminClient.ts` — add queue status method if missing

### New Files (Backend)
- `GetRecentlyProcessedDocumentsQuery.cs` + Handler (SharedGameCatalog Application/Queries) — cross-BC read query
- `RecentlyProcessedDocumentDto.cs` (SharedGameCatalog Application/DTOs)
- `UploadPdfCommandValidator.cs` (DocumentProcessing Application/Validators) — new validator

### Modified Files (Backend)
- `UploadPdfCommand.cs` — add optional `Priority` property
- `UploadPdfCommandHandler.cs` — respect priority parameter when auto-enqueuing
- `AdminSharedGameContentEndpoints.cs` — register `/recently-processed` route
- `GetProcessingQueueQuery.cs` + Handler — add optional `Guid? GameId` filter with SharedGameDocument join
- `QueueStreamEventHandlers.cs` — inject repo, enrich `JobCompletedData`/`JobFailedData` DTOs with game info
- `QueueStreamEventDto.cs` — add `FileName?`, `SharedGameId?`, `GameName?` to `JobCompletedData`/`JobFailedData`

---

## Out of Scope

- Drag-and-drop reorder in mini-queue (full queue dashboard already supports this)
- Notification preferences/settings (all admins see all notifications)
- WebSocket upgrade (SSE is sufficient for this use case)
- Mobile-specific layout (admin dashboard is desktop-first)
