# Seeding Page Improvements — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the admin Seeding & Enrichment page from a basic enrichment trigger to a full pipeline management dashboard with real-time updates, error visibility, and actionable next-steps per game.

**Architecture:** 11 issues organized in 3 tiers (Immediate → Short-term → Medium-term). Backend changes extend the existing CQRS pattern (MediatR). Frontend changes are isolated to the seeding page client component and its supporting schemas/clients. Each issue is an independent branch off `main-dev`.

**Tech Stack:** .NET 9 (MediatR, EF Core, PostgreSQL) | Next.js 16 (React 19, Zustand, Tailwind 4, shadcn/ui) | SSE for real-time | Playwright for E2E

**Parent branch:** `main-dev`

---

## File Map

### Backend Files
| File | Responsibility | Tasks |
|------|---------------|-------|
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQuery.cs` | SeedingGameDto record | 1 |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQueryHandler.cs` | Query handler: builds DTO from SharedGames + RAG join | 1 |
| `apps/api/src/Api/Routing/BggImportQueueEndpoints.cs` | BGG queue SSE + CRUD endpoints (already exists) | 5, 6 |

### Frontend Files
| File | Responsibility | Tasks |
|------|---------------|-------|
| `apps/web/src/lib/api/schemas/seeding.schemas.ts` | Zod schemas for SeedingGameDto | 1, 3 |
| `apps/web/src/lib/api/clients/sharedGamesClient.ts` | API client methods (getSeedingStatus, enqueueBggEnrichment, etc.) | 2, 5, 6 |
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx` | Main seeding page UI | 1, 3, 4, 5, 6, 7, 8, 9, 10 |

### New Files
| File | Responsibility | Tasks |
|------|---------------|-------|
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/components/queue-status-panel.tsx` | Queue status card component | 5 |
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/components/pipeline-indicator.tsx` | Per-game pipeline progress indicator | 9 |
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/hooks/use-sse-queue.ts` | SSE hook for real-time queue updates | 6 |
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/__tests__/seeding-page.test.tsx` | Unit tests for seeding page | 11 |
| `apps/web/e2e/admin/seeding.spec.ts` | E2E Playwright tests | 11 |

---

## Tier 1: Immediate (Issues 1–4)

> **Parallelism:** Tasks 1, 2, 4 are independent and can run in parallel. **Task 3 MUST wait for Task 1** to merge (it extends the DTO shape created by Task 1).

### Task 1: Extend SeedingGameDto + Add Game Status & RAG Ready Columns

**Branch:** `feature/seeding-add-status-rag-columns`
**Parent:** `main-dev`

**Context:** The backend DTO already has `gameStatus`/`gameStatusName` but they're not displayed. RAG readiness is computed in the Excel export handler (`ExportSharedGamesTrackingQueryHandler.cs:23-30`) but not in the seeding DTO. We need to add `isRagReady` to the DTO and display both columns.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQueryHandler.cs`
- Modify: `apps/web/src/lib/api/schemas/seeding.schemas.ts`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Extend the backend SeedingGameDto record**

In `GetSeedingStatusQuery.cs`, add `IsRagReady` field:

```csharp
public sealed record SeedingGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int GameDataStatus,
    string GameDataStatusName,
    int GameStatus,
    string GameStatusName,
    bool HasUploadedPdf,
    bool IsRagReady,
    DateTime CreatedAt);
```

- [ ] **Step 2: Update the query handler to compute RAG readiness**

In `GetSeedingStatusQueryHandler.cs`, add the RAG join (same pattern as `ExportSharedGamesTrackingQueryHandler.cs:23-30`):

```csharp
public async Task<List<SeedingGameDto>> Handle(
    GetSeedingStatusQuery query, CancellationToken cancellationToken)
{
    // Compute RAG-ready set (same pattern as ExportSharedGamesTrackingQueryHandler)
    var ragReadyGameIds = await (
        from sgd in _context.Set<SharedGameDocumentEntity>()
        join vd in _context.VectorDocuments on sgd.PdfDocumentId equals vd.PdfDocumentId
        where vd.IndexingStatus == "completed"
        select sgd.SharedGameId
    ).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);

    var ragReadySet = ragReadyGameIds.ToHashSet();

    var games = await _context.SharedGames
        .AsNoTracking()
        .Where(g => !g.IsDeleted)
        .OrderBy(g => g.Title)
        .Select(g => new
        {
            g.Id,
            g.BggId,
            g.Title,
            g.GameDataStatus,
            g.Status,
            g.HasUploadedPdf,
            g.CreatedAt
        })
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    return games.Select(g => new SeedingGameDto(
        g.Id, g.BggId, g.Title, g.GameDataStatus,
        DataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown"),
        g.Status, GameStatusNames.GetValueOrDefault(g.Status, "Unknown"),
        g.HasUploadedPdf,
        ragReadySet.Contains(g.Id),
        g.CreatedAt
    )).ToList();
}
```

Add the required using at the top:
```csharp
using Api.Infrastructure.Entities.SharedGameCatalog;
```

- [ ] **Step 3: Update frontend Zod schema**

In `seeding.schemas.ts`:

```typescript
export const SeedingGameDtoSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(),
  title: z.string(),
  gameDataStatus: z.number().int(),
  gameDataStatusName: z.string(),
  gameStatus: z.number().int(),
  gameStatusName: z.string(),
  hasUploadedPdf: z.boolean(),
  isRagReady: z.boolean(),
  createdAt: z.string(),
});
```

- [ ] **Step 4: Add Game Status and RAG Ready columns in the table**

In `client.tsx`, add two new `<TableHead>` entries after "Has PDF":

```tsx
<TableHead className="w-32">Game Status</TableHead>
<TableHead className="w-24 text-center">RAG Ready</TableHead>
```

And corresponding `<TableCell>` entries in the row map:

```tsx
<TableCell>
  <Badge variant="outline" className="text-xs">
    {game.gameStatusName}
  </Badge>
</TableCell>
<TableCell className="text-center">
  {game.isRagReady ? (
    <span className="text-emerald-600 font-semibold text-sm">Yes</span>
  ) : (
    <span className="text-muted-foreground text-sm">No</span>
  )}
</TableCell>
```

- [ ] **Step 5: Build and verify**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../web && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(seeding): add Game Status and RAG Ready columns to seeding page

Extends SeedingGameDto with IsRagReady (computed from VectorDocument join).
Displays gameStatusName and RAG readiness in the admin seeding table."
```

---

### Task 2: Fix Double API Prefix in enqueueBggEnrichment

**Branch:** `fix/seeding-double-api-prefix`
**Parent:** `main-dev`

**Context:** `sharedGamesClient.ts:956` calls `POST /api/v1/api/v1/admin/bgg-queue/batch`. The `httpClient` does NOT auto-prepend any path — `getApiBase()` returns `''` in the browser, so the path in the client IS the full URL path. All other client methods use `/api/v1/...` as the full path. The doubled `/api/v1/api/v1/` is a typo. The backend endpoint is at `/api/v1/admin/bgg-queue/batch` (`BggImportQueueEndpoints.cs:21` maps group to `/api/v1/admin/bgg-queue` + route `/batch`).

**Files:**
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts:955-957`

- [ ] **Step 1: Fix the URL path**

In `sharedGamesClient.ts`, change:
```typescript
// BEFORE (doubled prefix — bug)
await httpClient.post('/api/v1/api/v1/admin/bgg-queue/batch', { bggIds });

// AFTER (correct — httpClient prepends getApiBase() which is '' in browser)
await httpClient.post('/api/v1/admin/bgg-queue/batch', { bggIds });
```

**Verified:** `httpClient` uses `getApiBase()` which returns `''` in the browser. The full path `/api/v1/admin/bgg-queue/batch` is the correct URL matching the backend route group.

- [ ] **Step 2: Test the enrichment flow manually**

Navigate to `/admin/shared-games/seeding`, select a Skeleton game, click Enrich Selected. Check browser DevTools Network tab to confirm the POST URL is correct (single `/api/v1/` prefix) and returns 201.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/sharedGamesClient.ts
git commit -m "fix(seeding): correct double API prefix in enqueueBggEnrichment URL"
```

---

### Task 3: Error Visibility for Failed Games + Retry Action

**Branch:** `feature/seeding-error-visibility`
**Parent:** `main-dev`

**Context:** Failed games show orange "Failed" badge but no error message. The `BggImportQueueEntity` has `ErrorMessage` field. The backend has a retry endpoint: `POST /api/v1/admin/bgg-queue/{id}/retry`. We need to: (a) show error messages on hover/expand, (b) add a retry button for failed games.

**Files:**
- Modify: `apps/web/src/lib/api/schemas/seeding.schemas.ts` — add `errorMessage` field
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQuery.cs` — add `ErrorMessage` to DTO
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQueryHandler.cs` — join BggImportQueue to get error
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts` — add `retryBggEnrichment()` method
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx` — show error tooltip + retry button

- [ ] **Step 1: Extend backend DTO with ErrorMessage**

In `GetSeedingStatusQuery.cs`:
```csharp
public sealed record SeedingGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int GameDataStatus,
    string GameDataStatusName,
    int GameStatus,
    string GameStatusName,
    bool HasUploadedPdf,
    bool IsRagReady,
    string? ErrorMessage,
    DateTime CreatedAt);
```

- [ ] **Step 2: Update query handler to join BggImportQueue for error messages**

In `GetSeedingStatusQueryHandler.cs`, after fetching games, left-join on BggImportQueue to get error messages for failed games:

```csharp
// Get error messages for failed games from BggImportQueue
var failedErrors = await _context.Set<BggImportQueueEntity>()
    .AsNoTracking()
    .Where(q => q.Status == BggImportStatus.Failed && q.ErrorMessage != null)
    .GroupBy(q => q.BggId)
    .Select(g => new { BggId = g.Key, Error = g.OrderByDescending(x => x.CreatedAt).First().ErrorMessage })
    .ToListAsync(cancellationToken)
    .ConfigureAwait(false);

var errorByBggId = failedErrors.ToDictionary(e => e.BggId, e => e.Error);
```

Add the required usings at the top:
```csharp
using Api.Infrastructure.Entities;
```

Then in the Select, map the error:
```csharp
return games.Select(g => new SeedingGameDto(
    g.Id, g.BggId, g.Title, g.GameDataStatus,
    DataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown"),
    g.Status, GameStatusNames.GetValueOrDefault(g.Status, "Unknown"),
    g.HasUploadedPdf,
    ragReadySet.Contains(g.Id),
    g.BggId.HasValue && errorByBggId.TryGetValue(g.BggId.Value, out var err) ? err : null,
    g.CreatedAt
)).ToList();
```

- [ ] **Step 3: Update frontend Zod schema**

```typescript
export const SeedingGameDtoSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(),
  title: z.string(),
  gameDataStatus: z.number().int(),
  gameDataStatusName: z.string(),
  gameStatus: z.number().int(),
  gameStatusName: z.string(),
  hasUploadedPdf: z.boolean(),
  isRagReady: z.boolean(),
  errorMessage: z.string().nullable(),
  createdAt: z.string(),
});
```

- [ ] **Step 4: Add retry API client method**

In `sharedGamesClient.ts`, inside the seeding section:

```typescript
/**
 * Re-enqueue failed BGG games for enrichment (ADMIN ONLY)
 * POST /api/v1/admin/bgg-queue/batch
 * Reuses batch endpoint — failed games are accepted for re-queue.
 */
async retryBggEnrichment(bggIds: number[]): Promise<void> {
  await httpClient.post('/api/v1/admin/bgg-queue/batch', { bggIds });
},
```

- [ ] **Step 5: Add error tooltip and retry button in the table**

In `client.tsx`, modify the Data Status cell for failed games:

```tsx
<TableCell>
  <div className="flex items-center gap-2">
    <Badge className={getStatusBadgeClass(game.gameDataStatus)}>
      {game.gameDataStatusName}
    </Badge>
    {game.gameDataStatus === GAME_DATA_STATUS.Failed && game.errorMessage && (
      <span
        className="text-xs text-orange-600 truncate max-w-[200px] cursor-help"
        title={game.errorMessage}
      >
        {game.errorMessage}
      </span>
    )}
  </div>
</TableCell>
```

Add a "Retry Failed" button in the toolbar (next to "Enrich Selected"):

```tsx
{failedSelectedCount > 0 && (
  <Button
    variant="outline"
    size="sm"
    onClick={() => void handleRetryFailed()}
    disabled={enriching}
  >
    <RefreshCwIcon className="h-4 w-4 mr-1.5" />
    Retry Failed ({failedSelectedCount})
  </Button>
)}
```

Add the computed value and handler:
```typescript
const failedSelectedGames = useMemo(
  () => games.filter(g =>
    selectedIds.has(g.id) &&
    g.bggId !== null &&
    g.gameDataStatus === GAME_DATA_STATUS.Failed
  ),
  [games, selectedIds]
);
const failedSelectedCount = failedSelectedGames.length;

const handleRetryFailed = useCallback(async () => {
  if (failedSelectedCount === 0) return;
  setEnriching(true);
  setEnrichMessage(null);
  try {
    const bggIds = failedSelectedGames.map(g => g.bggId as number);
    await api.sharedGames.retryBggEnrichment(bggIds);
    setEnrichMessage(`Re-queued ${bggIds.length} failed game(s) for enrichment.`);
    setSelectedIds(new Set());
    await fetchGames();
  } catch (err) {
    setEnrichMessage(`Retry failed: ${err instanceof Error ? err.message : 'Unknown error'}`);
  } finally {
    setEnriching(false);
  }
}, [failedSelectedCount, failedSelectedGames, fetchGames]);
```

- [ ] **Step 6: Build and verify**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../web && pnpm build
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(seeding): show error messages for failed games and add retry action

- Extends SeedingGameDto with ErrorMessage from BggImportQueue
- Displays error message inline for Failed status games
- Adds Retry Failed button for batch retry of selected failed games"
```

---

### Task 4: Disable Enrich Button When No Enrichable Games Exist

**Branch:** `fix/seeding-disable-enrich-button`
**Parent:** `main-dev`

**Context:** The "Enrich Selected" button is always visible even when all games are Complete. It's already `disabled` when `enrichableCount === 0`, but it would be clearer to also show a visual indicator.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Update button to show enrichable count context**

The button already disables when `enrichableCount === 0`. Improve the label to show context:

```tsx
<Button
  variant="default"
  size="sm"
  onClick={() => void handleEnrich()}
  disabled={enrichableCount === 0 || enriching}
  title={enrichableCount === 0 ? 'Select Skeleton or Failed games with BGG IDs to enrich' : undefined}
>
  <SproutIcon className="h-4 w-4 mr-1.5" />
  {enriching
    ? 'Queuing…'
    : enrichableCount > 0
      ? `Enrich Selected (${enrichableCount})`
      : 'Enrich Selected'}
</Button>
```

- [ ] **Step 2: Add empty-state guidance when all games are Complete**

After the table, if all games are Complete and none have PDFs, show a next-steps hint:

```tsx
{games.length > 0 && games.every(g => g.gameDataStatus === GAME_DATA_STATUS.Complete) && (
  <div className="mt-4 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800 dark:border-emerald-800/40 dark:bg-emerald-900/20 dark:text-emerald-300">
    All games are enriched. Next step: upload PDFs via{' '}
    <a href="/admin/knowledge-base/upload" className="underline font-medium">
      Upload &amp; Process
    </a>{' '}
    to enable RAG.
  </div>
)}
```

- [ ] **Step 3: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx
git commit -m "fix(seeding): improve Enrich button UX and add next-steps guidance

- Add tooltip explaining why Enrich is disabled
- Show next-steps banner when all games are Complete"
```

---

## Tier 2: Short-term (Issues 5–8)

### Task 5: Queue Status Panel

**Branch:** `feature/seeding-queue-status-panel`
**Parent:** `main-dev`

**Context:** Backend exposes `GET /api/v1/admin/bgg-queue/status` returning `{ TotalQueued, TotalProcessing, Items }`. Currently not used by the frontend. Add a collapsible queue status panel above the table.

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/components/queue-status-panel.tsx`
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts` — add `getBggQueueStatus()` method
- Modify: `apps/web/src/lib/api/schemas/seeding.schemas.ts` — add queue status schema
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx` — integrate panel

- [ ] **Step 1: Add queue status Zod schema**

In `seeding.schemas.ts`, add:

```typescript
export const BggQueueItemSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int(),
  gameName: z.string().nullable(),
  status: z.number().int(),
  position: z.number().int(),
  retryCount: z.number().int(),
  createdAt: z.string(),
  errorMessage: z.string().nullable(),
});

export const BggQueueStatusSchema = z.object({
  totalQueued: z.number().int(),
  totalProcessing: z.number().int(),
  items: z.array(BggQueueItemSchema),
});

export type BggQueueStatus = z.infer<typeof BggQueueStatusSchema>;
```

- [ ] **Step 2: Add API client method**

In `sharedGamesClient.ts`, seeding section:

```typescript
/**
 * Get BGG import queue status (ADMIN ONLY)
 * GET /api/v1/admin/bgg-queue/status
 */
async getBggQueueStatus(): Promise<BggQueueStatus> {
  const result = await httpClient.get(
    '/api/v1/admin/bgg-queue/status',
    BggQueueStatusSchema
  );
  return result ?? { totalQueued: 0, totalProcessing: 0, items: [] };
},
```

- [ ] **Step 3: Create QueueStatusPanel component**

Create `apps/web/src/app/admin/(dashboard)/shared-games/seeding/components/queue-status-panel.tsx`:

```tsx
'use client';

import { useCallback, useEffect, useState } from 'react';
import { Loader2Icon, ClockIcon } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';
import type { BggQueueStatus } from '@/lib/api/schemas/seeding.schemas';

const QUEUE_POLL_MS = 3000;

export function QueueStatusPanel() {
  const [status, setStatus] = useState<BggQueueStatus | null>(null);

  const fetchStatus = useCallback(async () => {
    try {
      const data = await api.sharedGames.getBggQueueStatus();
      setStatus(data);
    } catch {
      // Silently fail — panel is supplementary
    }
  }, []);

  useEffect(() => {
    void fetchStatus();
    const interval = setInterval(() => void fetchStatus(), QUEUE_POLL_MS);
    return () => clearInterval(interval);
  }, [fetchStatus]);

  if (!status || (status.totalQueued === 0 && status.totalProcessing === 0)) {
    return null; // Hide when queue is empty
  }

  const eta = status.totalQueued + status.totalProcessing;

  return (
    <Card className="border-blue-200 bg-blue-50/50 dark:border-blue-800/40 dark:bg-blue-900/10">
      <CardHeader className="pb-2">
        <CardTitle className="font-quicksand text-sm font-semibold flex items-center gap-2">
          <Loader2Icon className="h-4 w-4 animate-spin text-blue-500" />
          Enrichment Queue Active
        </CardTitle>
      </CardHeader>
      <CardContent className="flex items-center gap-4 text-sm">
        <div className="flex items-center gap-1.5">
          <Badge variant="outline" className="bg-slate-100">{status.totalQueued}</Badge>
          <span className="text-muted-foreground">queued</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Badge variant="outline" className="bg-blue-100">{status.totalProcessing}</Badge>
          <span className="text-muted-foreground">processing</span>
        </div>
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <ClockIcon className="h-3.5 w-3.5" />
          <span>~{eta}s remaining</span>
        </div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Integrate panel into seeding page**

In `client.tsx`, import and add `<QueueStatusPanel />` between the rate-limit banner and the Card:

```tsx
import { QueueStatusPanel } from './components/queue-status-panel';

// ... inside render, after enrichMessage and before <Card>:
<QueueStatusPanel />
```

- [ ] **Step 5: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(seeding): add queue status panel showing active enrichment progress

- New QueueStatusPanel component polls /bgg-queue/status every 3s
- Shows queued/processing counts and ETA
- Auto-hides when queue is empty"
```

---

### Task 6: SSE Integration for Real-Time Updates

**Branch:** `feature/seeding-sse-realtime`
**Parent:** `main-dev`

**Context:** Backend exposes SSE at `GET /api/v1/admin/bgg-queue/stream` (updates every 2s) and `GET /api/v1/admin/bgg-queue/bulk-import-progress` (updates every 1s, auto-closes). Currently the page uses 5s polling. Replace polling with SSE during active enrichment, keep polling as fallback when idle.

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/hooks/use-sse-queue.ts`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Create SSE hook**

Create `apps/web/src/app/admin/(dashboard)/shared-games/seeding/hooks/use-sse-queue.ts`:

```typescript
'use client';

import { useEffect, useRef, useState } from 'react';

export interface QueueStreamEvent {
  timestamp: string;
  queued: number;
  processing: number;
  completed: number;
  failed: number;
  eta: number;
  items: Array<{ bggId: number; gameName?: string; status: number }>;
}

interface UseSseQueueOptions {
  enabled: boolean;
  onUpdate?: (event: QueueStreamEvent) => void;
}

/**
 * SSE hook for real-time BGG queue updates.
 * Connects to /api/v1/admin/bgg-queue/stream when enabled.
 * Uses refs for callback to avoid reconnection loops.
 */
export function useSseQueue({ enabled, onUpdate }: UseSseQueueOptions) {
  const [isConnected, setIsConnected] = useState(false);
  const [lastEvent, setLastEvent] = useState<QueueStreamEvent | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);
  const onUpdateRef = useRef(onUpdate);
  const enabledRef = useRef(enabled);

  // Keep refs in sync without triggering reconnection
  onUpdateRef.current = onUpdate;
  enabledRef.current = enabled;

  useEffect(() => {
    if (!enabled) {
      eventSourceRef.current?.close();
      eventSourceRef.current = null;
      setIsConnected(false);
      return;
    }

    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

    function connect() {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }

      const baseUrl = process.env.NEXT_PUBLIC_API_URL || '';
      // Note: EventSource sends cookies automatically for same-origin requests.
      // For cross-origin, a polyfill like 'eventsource' npm package is needed.
      const es = new EventSource(`${baseUrl}/api/v1/admin/bgg-queue/stream`);

      es.onopen = () => setIsConnected(true);

      es.onmessage = (event) => {
        try {
          const data: QueueStreamEvent = JSON.parse(event.data);
          setLastEvent(data);
          onUpdateRef.current?.(data);
        } catch {
          // Ignore parse errors
        }
      };

      es.onerror = () => {
        setIsConnected(false);
        es.close();
        // Reconnect after 5s if still enabled
        reconnectTimer = setTimeout(() => {
          if (enabledRef.current) connect();
        }, 5000);
      };

      eventSourceRef.current = es;
    }

    connect();

    return () => {
      if (reconnectTimer) clearTimeout(reconnectTimer);
      eventSourceRef.current?.close();
      eventSourceRef.current = null;
      setIsConnected(false);
    };
  }, [enabled]); // Only reconnect when enabled changes

  return { isConnected, lastEvent };
}
```

- [ ] **Step 2: Integrate SSE into seeding page**

In `client.tsx`:

1. Add state to track if enrichment is active:
```typescript
const [queueActive, setQueueActive] = useState(false);
```

2. Connect SSE when queue is active, trigger refetch on updates:
```typescript
import { useSseQueue } from './hooks/use-sse-queue';

// Debounce SSE-triggered fetches to avoid hammering the API
const lastSseFetchRef = useRef(0);

const { isConnected: sseConnected } = useSseQueue({
  enabled: queueActive,
  onUpdate: (event) => {
    // When SSE reports queue is empty, switch back to polling
    if (event.queued === 0 && event.processing === 0) {
      setQueueActive(false);
    }
    // Debounce: only refetch if >3s since last SSE-triggered fetch
    const now = Date.now();
    if (now - lastSseFetchRef.current > 3000) {
      lastSseFetchRef.current = now;
      void fetchGames();
    }
  },
});
```

3. When enrichment is triggered, enable SSE:
```typescript
// Inside handleEnrich, after successful enqueue:
setQueueActive(true);
```

4. Reduce polling frequency when SSE is active:
```typescript
const pollingInterval = sseConnected ? 15000 : POLLING_INTERVAL_MS; // 15s when SSE active, 5s fallback
```

Update the useEffect to use `pollingInterval`:
```typescript
intervalRef.current = setInterval(() => {
  void fetchGames();
}, pollingInterval);
```

- [ ] **Step 3: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(seeding): use SSE for real-time queue updates during enrichment

- New useSseQueue hook connects to /bgg-queue/stream
- Activates SSE when enrichment is triggered
- Reduces polling to 15s while SSE is connected
- Falls back to 5s polling when SSE disconnects or queue empties"
```

---

### Task 7: Quick-Action Links Per Game

**Branch:** `feature/seeding-quick-actions`
**Parent:** `main-dev`

**Context:** Admin needs to navigate between 3 pages to manage the full pipeline. Add contextual action links per game row: "Upload PDF" → `/admin/knowledge-base/upload?gameId={id}`, "View" → `/admin/shared-games/{id}`.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Add Actions column to the table**

Add a new `<TableHead>` at the end:
```tsx
<TableHead className="w-28 text-right pr-6">Actions</TableHead>
```

- [ ] **Step 2: Add action links in each row**

```tsx
import { ExternalLinkIcon, UploadIcon } from 'lucide-react';
import Link from 'next/link';

// In the TableRow map, add at the end:
<TableCell className="text-right pr-6">
  <div className="flex items-center justify-end gap-1">
    <Link
      href={`/admin/shared-games/${game.id}`}
      className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
      title="View game details"
    >
      <ExternalLinkIcon className="h-3.5 w-3.5" />
    </Link>
    {!game.hasUploadedPdf && game.gameDataStatus === GAME_DATA_STATUS.Complete && (
      <Link
        href={`/admin/knowledge-base/upload?gameId=${game.id}`}
        className="inline-flex items-center gap-1 text-xs text-blue-600 hover:text-blue-800 transition-colors"
        title="Upload PDF for this game"
      >
        <UploadIcon className="h-3.5 w-3.5" />
      </Link>
    )}
  </div>
</TableCell>
```

- [ ] **Step 3: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx
git commit -m "feat(seeding): add quick-action links per game row

- View game details link (external link icon)
- Upload PDF link for Complete games without PDFs"
```

---

### Task 8: Column Sorting and Search

**Branch:** `feature/seeding-sort-search`
**Parent:** `main-dev`

**Context:** Table currently has no sorting or search. Add client-side sorting (click column headers) and a text search input.

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Add sort state and search state**

```typescript
type SortField = 'title' | 'bggId' | 'gameDataStatus' | 'createdAt';
type SortDir = 'asc' | 'desc';

const [sortField, setSortField] = useState<SortField>('title');
const [sortDir, setSortDir] = useState<SortDir>('asc');
const [search, setSearch] = useState('');
```

- [ ] **Step 2: Add sort/search logic to filteredGames**

```typescript
const filteredGames = useMemo<SeedingGameDto[]>(() => {
  let result = games;

  // Status filter
  if (statusFilter !== 'all') {
    const targetStatus = parseInt(statusFilter, 10);
    result = result.filter(g => g.gameDataStatus === targetStatus);
  }

  // Text search
  if (search.trim()) {
    const q = search.toLowerCase().trim();
    result = result.filter(g =>
      g.title.toLowerCase().includes(q) ||
      (g.bggId?.toString() ?? '').includes(q)
    );
  }

  // Sort
  const sorted = [...result].sort((a, b) => {
    let cmp = 0;
    switch (sortField) {
      case 'title': cmp = a.title.localeCompare(b.title); break;
      case 'bggId': cmp = (a.bggId ?? 0) - (b.bggId ?? 0); break;
      case 'gameDataStatus': cmp = a.gameDataStatus - b.gameDataStatus; break;
      case 'createdAt': cmp = a.createdAt.localeCompare(b.createdAt); break;
    }
    return sortDir === 'asc' ? cmp : -cmp;
  });

  return sorted;
}, [games, statusFilter, search, sortField, sortDir]);
```

- [ ] **Step 3: Add search input in the toolbar**

```tsx
import { SearchIcon } from 'lucide-react';
import { Input } from '@/components/ui/primitives/input';

// In the toolbar div, before the status filter:
<div className="relative">
  <SearchIcon className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
  <Input
    placeholder="Search games…"
    value={search}
    onChange={e => setSearch(e.target.value)}
    className="pl-8 w-[200px] h-9"
  />
</div>
```

- [ ] **Step 4: Make column headers clickable for sorting**

Create a helper:
```tsx
function SortableHeader({ field, label, currentField, currentDir, onSort }: {
  field: SortField;
  label: string;
  currentField: SortField;
  currentDir: SortDir;
  onSort: (field: SortField) => void;
}) {
  const isActive = field === currentField;
  return (
    <TableHead
      className="cursor-pointer select-none hover:bg-muted/50 transition-colors"
      onClick={() => onSort(field)}
    >
      <span className="flex items-center gap-1">
        {label}
        {isActive && (
          <span className="text-xs">{currentDir === 'asc' ? '↑' : '↓'}</span>
        )}
      </span>
    </TableHead>
  );
}
```

Toggle handler:
```typescript
const handleSort = useCallback((field: SortField) => {
  if (field === sortField) {
    setSortDir(d => d === 'asc' ? 'desc' : 'asc');
  } else {
    setSortField(field);
    setSortDir('asc');
  }
}, [sortField]);
```

Replace static `<TableHead>Title</TableHead>` etc. with `<SortableHeader>`.

- [ ] **Step 5: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx
git commit -m "feat(seeding): add column sorting and text search

- Client-side sorting on Title, BGG ID, Data Status, Created
- Text search filters by title and BGG ID
- Sort direction toggle on column header click"
```

---

## Tier 3: Medium-term (Issues 9–11)

### Task 9: Pipeline Progress Indicator

**Branch:** `feature/seeding-pipeline-indicator`
**Parent:** `main-dev`

**Context:** The full game lifecycle is: Enrich → PDF Upload → RAG Index → Ready. Currently the page only shows enrichment status. Add a mini pipeline indicator per game showing all stages.

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/components/pipeline-indicator.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Create PipelineIndicator component**

```tsx
'use client';

interface PipelineIndicatorProps {
  gameDataStatus: number;
  hasUploadedPdf: boolean;
  isRagReady: boolean;
}

const stages = [
  { key: 'enrich', label: 'Enriched' },
  { key: 'pdf', label: 'PDF' },
  { key: 'rag', label: 'RAG' },
] as const;

const COMPLETE_STATUS = 5; // GameDataStatus.Complete

export function PipelineIndicator({ gameDataStatus, hasUploadedPdf, isRagReady }: PipelineIndicatorProps) {
  const enrichComplete = gameDataStatus >= COMPLETE_STATUS;
  const pdfComplete = hasUploadedPdf;
  const ragComplete = isRagReady;

  const stageStatus = [enrichComplete, pdfComplete, ragComplete];

  return (
    <div className="flex items-center gap-1" title={`Enriched: ${enrichComplete ? 'Yes' : 'No'} | PDF: ${pdfComplete ? 'Yes' : 'No'} | RAG: ${ragComplete ? 'Yes' : 'No'}`}>
      {stages.map((stage, i) => (
        <div key={stage.key} className="flex items-center gap-1">
          <div
            className={`h-2 w-2 rounded-full ${
              stageStatus[i]
                ? 'bg-emerald-500'
                : 'bg-slate-300 dark:bg-slate-600'
            }`}
          />
          {i < stages.length - 1 && (
            <div className={`h-px w-3 ${
              stageStatus[i] ? 'bg-emerald-400' : 'bg-slate-300 dark:bg-slate-600'
            }`} />
          )}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Integrate into table**

Add a "Pipeline" column after RAG Ready:

```tsx
<TableHead className="w-24">Pipeline</TableHead>

// In row:
<TableCell>
  <PipelineIndicator
    gameDataStatus={game.gameDataStatus}
    hasUploadedPdf={game.hasUploadedPdf}
    isRagReady={game.isRagReady}
  />
</TableCell>
```

- [ ] **Step 3: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(seeding): add pipeline progress indicator per game

- 3-stage visual: Enriched → PDF → RAG
- Green dots for completed stages, gray for pending
- Tooltip shows stage details"
```

---

### Task 10: Pagination

**Branch:** `feature/seeding-pagination`
**Parent:** `main-dev`

**Context:** With 4 games, pagination isn't needed. But the catalog will grow. Add simple client-side pagination (25 per page).

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`

- [ ] **Step 1: Add pagination state**

```typescript
const PAGE_SIZE = 25;
const [page, setPage] = useState(1);
```

- [ ] **Step 2: Slice filteredGames for current page**

```typescript
const totalPages = Math.max(1, Math.ceil(filteredGames.length / PAGE_SIZE));
const paginatedGames = filteredGames.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

// Reset page when filter changes
useEffect(() => { setPage(1); }, [statusFilter, search]);
```

- [ ] **Step 3: Render paginatedGames instead of filteredGames in the table**

Replace `filteredGames.map(game => ...)` with `paginatedGames.map(game => ...)`.

- [ ] **Step 4: Add pagination controls after the table**

```tsx
{totalPages > 1 && (
  <div className="flex items-center justify-between px-6 py-3 border-t">
    <span className="text-sm text-muted-foreground">
      Showing {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, filteredGames.length)} of {filteredGames.length}
    </span>
    <div className="flex items-center gap-2">
      <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
        Previous
      </Button>
      <span className="text-sm text-muted-foreground">
        Page {page} of {totalPages}
      </span>
      <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>
        Next
      </Button>
    </div>
  </div>
)}
```

- [ ] **Step 5: Build and verify**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx
git commit -m "feat(seeding): add client-side pagination (25 per page)

- Paginates filtered/sorted results
- Resets to page 1 on filter/search change
- Shows page info and navigation controls"
```

---

### Task 11: E2E Tests

**Branch:** `feature/seeding-e2e-tests`
**Parent:** `main-dev`

**Context:** No E2E tests exist for the seeding page. Add Playwright tests covering the critical flows.

**Files:**
- Create: `apps/web/e2e/admin/seeding.spec.ts`

- [ ] **Step 1: Create E2E test file**

**IMPORTANT:** Before writing tests, check for existing auth helpers in `apps/web/e2e/` (e.g., `auth.setup.ts`, `fixtures.ts`, or a `storageState` approach). The seeding page requires admin authentication. If no helper exists, create one that logs in as admin and saves the auth state for reuse.

```typescript
import { test, expect } from '@playwright/test';

test.describe('Admin Seeding Page', () => {
  test.beforeEach(async ({ page }) => {
    // IMPORTANT: Use existing auth fixture or implement admin login.
    // Check apps/web/e2e/ for auth helpers. Example:
    // test.use({ storageState: 'e2e/.auth/admin.json' });
    await page.goto('/admin/shared-games/seeding');
    await page.waitForSelector('h1:has-text("Seeding")');
  });

  test('displays game table with correct columns', async ({ page }) => {
    await expect(page.getByRole('columnheader', { name: 'Title' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'BGG ID' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Data Status' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Has PDF' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Game Status' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'RAG Ready' })).toBeVisible();
  });

  test('status filter narrows displayed games', async ({ page }) => {
    // Get initial count
    const initialCount = await page.locator('tbody tr').count();

    // Select "Complete" filter
    await page.getByRole('combobox').click();
    await page.getByRole('option', { name: 'Complete' }).click();

    // Should show only Complete games (may be same or fewer)
    const filteredCount = await page.locator('tbody tr').count();
    expect(filteredCount).toBeLessThanOrEqual(initialCount);
  });

  test('search filters games by title', async ({ page }) => {
    await page.getByPlaceholder('Search games').fill('Catan');
    await expect(page.locator('tbody tr')).toHaveCount(1);
    await expect(page.getByText('Catan')).toBeVisible();
  });

  test('select all checkbox works', async ({ page }) => {
    await page.getByRole('checkbox', { name: 'Select all' }).click();
    // All row checkboxes should be checked
    const checkboxes = page.locator('tbody input[type="checkbox"]');
    const count = await checkboxes.count();
    for (let i = 0; i < count; i++) {
      await expect(checkboxes.nth(i)).toBeChecked();
    }
  });

  test('enrich button disabled when no enrichable games selected', async ({ page }) => {
    await expect(page.getByRole('button', { name: /Enrich Selected/ })).toBeDisabled();
  });

  test('download excel button triggers download', async ({ page }) => {
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Download Excel' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(/seeding-tracking.*\.xlsx/);
  });

  test('column sorting toggles direction', async ({ page }) => {
    // Click Title header to sort
    await page.getByRole('columnheader', { name: 'Title' }).click();
    // First game should be first alphabetically
    const firstTitle = await page.locator('tbody tr:first-child td:nth-child(2)').textContent();

    // Click again to reverse
    await page.getByRole('columnheader', { name: 'Title' }).click();
    const newFirstTitle = await page.locator('tbody tr:first-child td:nth-child(2)').textContent();

    // Should be different (reversed order)
    expect(firstTitle).not.toBe(newFirstTitle);
  });
});
```

- [ ] **Step 2: Run E2E tests locally**

```bash
cd apps/web && pnpm exec playwright test e2e/admin/seeding.spec.ts --headed
```

- [ ] **Step 3: Fix any failures and re-run**

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/admin/seeding.spec.ts
git commit -m "test(seeding): add E2E Playwright tests for seeding page

- Tests table columns, filtering, search, sorting
- Tests select all, enrich disabled state, Excel download
- 7 test cases covering critical admin flows"
```

---

## Dependency Graph

```
Tier 1a (Parallel):
  Task 1: DTO + columns   (independent)
  Task 2: Fix URL prefix   (independent)
  Task 4: Button UX        (independent)

Tier 1b (After Task 1 merges):
  Task 3: Error visibility  (depends on Task 1 — extends DTO shape)

Tier 2 (After Tier 1 merges — all independent):
  Task 5: Queue panel
  Task 6: SSE integration
  Task 7: Quick actions
  Task 8: Sort + search
  Task 9: Pipeline indicator (needs isRagReady from Task 1)

Tier 3 (After Tier 2 merges):
  Task 10: Pagination       (independent, but cleaner after Task 8)
  Task 11: E2E tests        (depends on all UI changes being stable)
```

## Backend Test Note

Tasks 1 and 3 modify `GetSeedingStatusQueryHandler`. The project has 90%+ backend coverage target. Each task should include a unit test for the handler following the pattern in `Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/`. Specifically:
- **Task 1**: Test that `IsRagReady` is `true` when VectorDocument with `IndexingStatus == "completed"` exists for the game, and `false` otherwise.
- **Task 3**: Test that `ErrorMessage` is populated from BggImportQueue for Failed-status games, and `null` for others.

## PR Strategy

Each task = 1 branch = 1 PR to `main-dev`. Tasks within same tier can be parallel PRs. Merge Tier 1 before starting Tier 2. Task 3 should be merged after Task 1.

## Risk Notes

- **Task 2 (URL fix):** Verify `httpClient` base URL behavior before changing. The double prefix might be intentional due to how the client wraps `fetch`. Test in staging.
- **Task 6 (SSE):** SSE requires credentials (cookies). Verify CORS allows SSE from the Next.js domain. May need `withCredentials` in EventSource polyfill.
- **Task 1 (RAG join):** The RAG readiness query joins across bounded contexts (SharedGameCatalog → DocumentProcessing). This is already done in ExportSharedGamesTracking, so the pattern is established.
